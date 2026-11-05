using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Bounded traffic pool around the observer. Owns spawning, safety rules and junction reservations.
    /// No per-car Update, global vehicle registry, player-save access or map-wide simulation.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class TownRoadTraffic : MonoBehaviour
    {
        [SerializeField] private TrafficRoadNetwork network;
        [SerializeField] private TrafficVehicle[] fleet = Array.Empty<TrafficVehicle>();
        [SerializeField] private TownJunctionSignals[] signals = Array.Empty<TownJunctionSignals>();
        [SerializeField] private TownLevelCrossing crossing;
        public const int DefaultCapacity = 23;
        [SerializeField] private int maximumCars = DefaultCapacity;
        [SerializeField] private float chunkSize = 96;
        private readonly List<TrafficVehicle> pool = new();
        private readonly List<TrafficVehicle> active = new();
        private readonly HashSet<TrafficVehicle> parkingArrivals = new();
        private readonly Dictionary<TrafficVehicle,(int Lane,float Distance)> privateMerges=new();
        private readonly Dictionary<Vector2Int,List<Spawn>> spawnChunks = new();
        private readonly List<Spawn> candidates = new();
        private readonly List<Spawn> sparseCandidates=new();
        private readonly List<TrafficVehicle> approaches = new();
        private readonly List<float> clearances = new();
        private bool[] priorityLanes;
        private readonly RaycastHit[] hits = new RaycastHit[48];
        private readonly Collider[] spawnHits = new Collider[32];
        private System.Random random;
        private Transform observer;
        private Vector2Int observerChunk;
        private bool initialized, hasObserverChunk;
        private float nextSpawn, nextObserverSearch;
        private float nextSparseSpawn;
        private int spawnCursor, designCursor,sparseCursor;
        private Vector3 spawnHalfExtents;
        public IReadOnlyList<TrafficVehicle> ActiveCars => active;
        public TrafficRoadNetwork Network => network;
        public int PoolCount => pool.Count;
        public int MaximumCars => maximumCars;
        public long SimulatedCarSteps { get; private set; }
        public int SuccessfulSpawns { get; private set; }
        public Vector2Int ObserverChunk => observerChunk;
        public int SparseTrafficCount=>SparseCount(null);

        private int SparseCount(TrafficVehicle except)
        {
            if(network==null||network.SparseTrafficMaximum==0)return 0;
            int count=0;
            foreach(var car in active)
                if(car!=except&&!parkingArrivals.Contains(car)&&(network.IsSparseLane(car.LaneIndex)
                    ||car.ReservedJunction>=0&&car.NextLane>=0&&network.IsSparseLane(car.NextLane)))count++;
            return count;
        }
        private bool DistrictEntryClear(TrafficVehicle car,int departure)
            =>network.SparseTrafficMaximum==0||parkingArrivals.Contains(car)||network.IsSparseLane(car.LaneIndex)
                ||!network.IsSparseLane(departure)||SparseCount(car)<network.SparseTrafficMaximum;

        private readonly struct Spawn
        {
            public readonly int Lane;
            public readonly float Distance;
            public readonly Vector3 Position;
            public Spawn(int lane,float distance,Vector3 position) { Lane=lane;Distance=distance;Position=position; }
        }

        public void Configure(TrafficRoadNetwork roads, TrafficVehicle[] designs, TownJunctionSignals[] junctionSignals, TownLevelCrossing railCrossing, int capacity=DefaultCapacity)
        {
            if (roads == null || designs == null || designs.Length != 20) throw new ArgumentException("The approved twenty-car fleet and road network are required.");
            if(capacity<1 || capacity>96)throw new ArgumentOutOfRangeException(nameof(capacity));
            foreach (var design in designs) if (design == null) throw new ArgumentException("Missing traffic prefab.");
            network=roads;fleet=(TrafficVehicle[])designs.Clone();signals=junctionSignals;crossing=railCrossing;maximumCars=capacity;
        }
        public void SetObserver(Transform target) { observer=target;hasObserverChunk=false; }
        /// <summary>Creates a gap before a private car reaches the public lane. Existing occupants clear it first.</summary>
        public void RequestPrivateMerge(TrafficVehicle car,int lane,float metres)
        {
            if(car==null || lane<0 || lane>=network.Lanes.Count)return;
            privateMerges[car]=(lane,metres);
        }
        public void CancelPrivateMerge(TrafficVehicle car) => privateMerges.Remove(car);

        /// <summary>Check the entry gap before a visible car leaves its driveway.</summary>
        public bool HasMergeGap(TrafficVehicle car, int lane, float metres)
            => HasLaneGap(car, lane, metres, .85f);

        private bool HasLaneGap(TrafficVehicle car, int lane, float metres, float approachSeconds)
        {
            var position = network.Lanes[lane].Path.Sample(metres, out var direction);
            var lateral = Vector3.Cross(Vector3.up, direction);
            foreach (var other in active)
            {
                var offset = other.LocalPosition - position;
                float along = Vector3.Dot(offset, direction);
                float closingSpace = along < 0 ? other.Speed * approachSeconds : 0;
                if (Mathf.Abs(along) < ProjectedHalfSize(other, direction) + car.Length * .5f + 1 + closingSpace &&
                    Mathf.Abs(Vector3.Dot(offset, lateral)) < ProjectedHalfSize(other, lateral) + car.Width * .5f + .25f)
                    return false;
            }
            return true;
        }
        /// <summary>Transfers a parking car to the road simulation without a visual replacement.
        /// Capacity is preserved; only an off-screen pool slot may be displaced.</summary>
        public bool TryJoinTraffic(TrafficVehicle car,int lane,float metres,bool reservedForParking=false,float mergeSpeed=0)
        {
            Initialize();
            if(car==null || lane<0 || lane>=network.Lanes.Count || metres<0 || metres>=network.Lanes[lane].Path.Length)
                throw new ArgumentException("A departing car must join an existing road lane.");
            if (!HasLaneGap(car, lane, metres, 0)) return false;
            if(pool.Count>=maximumCars)
            {
                TrafficVehicle reusable=null;
                foreach(var candidate in pool)
                {
                    if(parkingArrivals.Contains(candidate))continue;
                    if(!candidate.gameObject.activeSelf){reusable=candidate;break;}
                    if(observer!=null && Vector3.Distance(candidate.transform.position,observer.position)<30)continue;
                    var camera=Camera.main;
                    var bounds=new Bounds(candidate.transform.position+Vector3.up,Vector3.one*(candidate.Length+2)*transform.lossyScale.x);
                    if(camera!=null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),bounds))continue;
                    reusable=candidate;break;
                }
                if(reusable==null)return false;
                Recycle(reusable);pool.Remove(reusable);Destroy(reusable.gameObject);
            }
            car.transform.SetParent(transform,true);car.enabled=true;
            car.Place(network,lane,metres,ChooseDeparture(lane),mergeSpeed);
            pool.Add(car);active.Add(car);
            if(reservedForParking)parkingArrivals.Add(car);
            privateMerges.Remove(car);
            return true;
        }
        /// <summary>Transfers ownership back to parking (or cancels its road journey). Does not destroy the car.</summary>
        public void WithdrawParkingCar(TrafficVehicle car)
        {
            if(car==null)return;
            car.ReservedJunction=-1;active.Remove(car);pool.Remove(car);parkingArrivals.Remove(car);privateMerges.Remove(car);car.enabled=false;
        }
        private void Start() { Initialize(); }
        private void OnDisable()
        {
            foreach (var car in active) car.gameObject.SetActive(false);
            active.Clear();privateMerges.Clear();hasObserverChunk=false;
        }
        private void FixedUpdate()
        {
            if (observer == null && Time.unscaledTime >= nextObserverSearch)
            {
                nextObserverSearch=Time.unscaledTime+1;
                var camera=Camera.main;
                if (camera != null)
                {
                    var chase=camera.GetComponent<PickupCamera>();
                    SetObserver(chase != null && chase.target != null ? chase.target : camera.transform);
                }
            }
            if (observer != null) Simulate(Time.fixedDeltaTime,Time.timeAsDouble,observer.position,true);
        }

        private void Initialize()
        {
            if (initialized) return;
            if (network == null || fleet.Length != 20) throw new InvalidOperationException("Town traffic has not been authored.");
            random=new System.Random(17491);
            ReadApproachPriority();
            float widest=0,longest=0;
            foreach(var design in fleet)
            {
                widest=Mathf.Max(widest,design.Width);
                longest=Mathf.Max(longest,design.Length);
            }
            // A recycled slot may contain any design, including the larger new cargo bodies.
            spawnHalfExtents=new Vector3(widest*.5f+.05f,.55f,longest*.5f+.30f);
            for (int i=0;i<network.Lanes.Count;i++)
            {
                var path=network.Lanes[i].Path;
                for (float d=12;d<path.Length-12;d+=28)
                {
                    var p=path.Sample(d,out _);var cell=Cell(p);
                    if (!spawnChunks.TryGetValue(cell,out var list)) spawnChunks.Add(cell,list=new List<Spawn>());
                    var spawn=new Spawn(i,d,p);list.Add(spawn);
                    if(network.IsSparseResidentLane(i))sparseCandidates.Add(spawn);
                }
            }
            initialized=true;
        }

        /// <summary>worldObserver supports both full-size City and the uniformly scaled Game town.</summary>
        public void Simulate(float seconds,double clock,Vector3 worldObserver,bool physicsQueries)
        {
            if (seconds <= 0 || seconds > .25f || float.IsNaN(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            Initialize();
            var localObserver=transform.InverseTransformPoint(worldObserver);
            var cell=Cell(localObserver);
            bool initial=!hasObserverChunk;
            if (initial || cell != observerChunk)
            {
                observerChunk=cell;hasObserverChunk=true;candidates.Clear();spawnCursor=0;
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                    if(spawnChunks.TryGetValue(cell+new Vector2Int(x,z),out var group))candidates.AddRange(group);
                for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var value=candidates[i];candidates[i]=candidates[j];candidates[j]=value;}
            }
            for (int i=active.Count-1;i>=0;i--)
            {
                var car=active[i];var at=Cell(car.LocalPosition);
                bool resident=network.IsSparseLane(car.LaneIndex);
                if(!parkingArrivals.Contains(car) && (!resident&&(Mathf.Abs(at.x-cell.x)>2 || Mathf.Abs(at.y-cell.y)>2) || (car.HoldSeconds>90 && Vector3.Distance(car.LocalPosition,localObserver)>95))) Recycle(car);
            }
            nextSpawn-=seconds;nextSparseSpawn-=seconds;
            if (initial || nextSpawn<=0)
            {
                nextSpawn=.5f;
                // Two residents remain visible in this small distant neighbourhood while
                // sharing the existing global pool. Nearby traffic may fill it to four.
                if(network.SparseTrafficMaximum>0)
                    for(int i=0;i<(initial?2:1)&&SparseCount(null)<2&&active.Count<maximumCars;i++)TrySpawn(localObserver,initial,physicsQueries,true);
                int attempts=initial ? maximumCars : 3;
                int reserve=network.SparseTrafficMaximum>0?Mathf.Max(0,2-SparseCount(null)):0;
                for(int i=0;i<attempts && active.Count<maximumCars-reserve;i++) TrySpawn(localObserver,initial,physicsQueries);
            }
            AdvanceTraffic(seconds, clock, physicsQueries);
        }

        private void AdvanceTraffic(float seconds, double clock, bool physicsQueries)
        {
            approaches.Clear();
            clearances.Clear();
            foreach (var car in active)
            {
                if (car.NextLane<0) car.ChooseDeparture(ChooseDeparture(car.LaneIndex));
                ReleaseClearedReservation(car);
                float clearance=FrontClearance(car);
                if (crossing != null && crossing.IsWarning) clearance=Mathf.Min(clearance,CrossingClearance(car));
                if (physicsQueries) clearance=Mathf.Min(clearance,ObstacleClearance(car,clearance));
                clearances.Add(clearance);
                if (car.IsTurning || car.ReservedJunction >= 0 &&
                    (network.Lanes[car.LaneIndex].to != car.ReservedJunction || StopDistance(car) < -.001f)) continue;

                var lane = network.Lanes[car.LaneIndex];
                float stop = StopDistance(car);
                if (!parkingArrivals.Contains(car) && stop < .15f && car.HoldSeconds > 2 && !ExitClear(car))
                    ChooseClearDeparture(car);
                // Re-evaluate permission before the line, including when a signal changed
                // or an approaching priority car appeared since the previous physics step.
                car.ReservedJunction = -1;
                if (stop <= Mathf.Max(8, car.Speed * 2f) && clearance + .01f >= Mathf.Max(0, stop)
                    && SignalAllows(lane, clock) && ExitClear(car))
                    approaches.Add(car);
            }

            foreach (var car in approaches)
            {
                // Grant a clear approach before the braking envelope reaches the stop line.
                // Until the car crosses that line, the grant is rechecked every physics step.
                float rollingApproach = Mathf.Clamp(car.Speed * 1.3f, 4f, 14f);
                if (StopDistance(car) <= rollingApproach && MayEnterJunction(car))
                    car.ReservedJunction = network.Lanes[car.LaneIndex].to;
            }

            for (int i = 0; i < active.Count; i++)
            {
                var car = active[i];
                float clearance = clearances[i];
                if (!car.IsTurning && car.ReservedJunction < 0)
                    clearance = Mathf.Min(clearance, Mathf.Max(0, StopDistance(car)));
                car.Advance(seconds, clearance);
                SimulatedCarSteps++;
            }
        }

        private static float StopDistance(TrafficVehicle car) => car.Remaining - 7 - car.Length * .5f;

        private bool MayEnterJunction(TrafficVehicle car)
        {
            int junction = network.Lanes[car.LaneIndex].to;
            foreach (var other in active)
                if (other != car && other.ReservedJunction == junction &&
                    (network.Lanes[other.LaneIndex].to != junction || ManeuversConflict(car, other))) return false;

            foreach (var other in approaches)
            {
                if (other == car || network.Lanes[other.LaneIndex].to != junction || !ManeuversConflict(car, other)) continue;
                if (HasPriority(other, car)) return false;
            }
            return true;
        }

        private bool HasPriority(TrafficVehicle first, TrafficVehicle second)
        {
            bool firstMain = priorityLanes[first.LaneIndex], secondMain = priorityLanes[second.LaneIndex];
            if (firstMain != secondMain && network.Junctions[network.Lanes[first.LaneIndex].to].signalIndex < 0)
                return firstMain;
            if (Vector3.Dot(ArrivalDirection(first), ArrivalDirection(second)) < -.92f)
            {
                bool firstLeft = TurnAngle(first) < -25, secondLeft = TurnAngle(second) < -25;
                if (firstLeft != secondLeft) return !firstLeft;
            }
            // Once precedence is equal, let the car already at the line go first.
            int firstStop = Mathf.RoundToInt(StopDistance(first) * 4), secondStop = Mathf.RoundToInt(StopDistance(second) * 4);
            if (firstStop != secondStop) return firstStop < secondStop;
            if (first.HoldSeconds != second.HoldSeconds) return first.HoldSeconds > second.HoldSeconds;
            return first.LaneIndex < second.LaneIndex;
        }

        private bool ManeuversConflict(TrafficVehicle first, TrafficVehicle second)
        {
            return first.NextLane == second.NextLane ||
                Vector3.Dot(ArrivalDirection(first), ArrivalDirection(second)) > -.98f ||
                Mathf.Abs(TurnAngle(first)) > 6 || Mathf.Abs(TurnAngle(second)) > 6;
        }

        private Vector3 ArrivalDirection(TrafficVehicle car)
        {
            var path = network.Lanes[car.LaneIndex].Path;
            path.Sample(path.Length, out var direction);
            return direction;
        }

        private float TurnAngle(TrafficVehicle car)
        {
            if (car.NextLane < 0) return 180;
            network.Lanes[car.NextLane].Path.Sample(0, out var direction);
            return Vector3.SignedAngle(ArrivalDirection(car), direction, Vector3.up);
        }

        private void ReadApproachPriority()
        {
            priorityLanes = new bool[network.Lanes.Count];
            var signs = transform.parent != null ? transform.parent.Find("City junction equipment") : null;
            var districtSigns = transform.parent != null ? transform.parent.Find("Pinebrook/Signs") : null;
            for (int i = 0; i < network.Lanes.Count; i++)
            {
                var lane = network.Lanes[i];
                lane.Path.Sample(lane.Path.Length, out var arrival);
                var junction = network.Junctions[lane.to].position;
                if (signs != null)
                {
                    bool signed = false;
                    foreach (Transform node in signs)
                    {
                        if ((transform.InverseTransformPoint(node.position) - junction).sqrMagnitude > 1) continue;
                        foreach (Transform marker in node)
                        {
                            if (!marker.name.StartsWith("Approach ", StringComparison.Ordinal) ||
                                Vector3.Dot(transform.InverseTransformDirection(marker.forward), arrival) > -.9f) continue;
                            priorityLanes[i] = marker.Find("PriorityRoadSign") != null;
                            signed = marker.Find("YieldSign") != null || priorityLanes[i];
                            break;
                        }
                        break;
                    }
                    if (signed) continue;
                }
                if (districtSigns != null && TryReadDistrictSign(districtSigns, junction, arrival, out bool districtPriority))
                {
                    priorityLanes[i] = districtPriority;
                    continue;
                }
                // An unsigned T-junction gives the continuing road precedence over its stem.
                var exits = network.Departures(lane.to);
                if (exits.Count != 3) continue;
                foreach (int exit in exits)
                {
                    network.Lanes[exit].Path.Sample(0, out var departure);
                    if (Vector3.Dot(arrival, departure) > .94f) { priorityLanes[i] = true; break; }
                }
            }
        }

        private bool TryReadDistrictSign(Transform signs, Vector3 junction, Vector3 arrival, out bool priority)
        {
            priority = false;
            float nearest = 24 * 24;
            bool found = false;
            var right = Vector3.Cross(Vector3.up, arrival).normalized;
            foreach (Transform marker in signs)
            {
                if (Vector3.Dot(transform.InverseTransformDirection(marker.forward), arrival) > -.92f) continue;
                var offset = transform.InverseTransformPoint(marker.position) - junction;
                float lateral = Vector3.Dot(offset, right);
                if (Vector3.Dot(offset, arrival) >= 0 || lateral < 3 || lateral > 8 || offset.sqrMagnitude >= nearest) continue;
                bool main = marker.Find("PriorityRoadSign") != null;
                if (!main && marker.Find("YieldSign") == null) continue;
                nearest = offset.sqrMagnitude;
                priority = main;
                found = true;
            }
            return found;
        }

        private int ChooseDeparture(int incoming)
        {
            var lane=network.Lanes[incoming];var exits=network.Departures(lane.to);
            bool keepOutside=network.SparseTrafficMaximum>0&&!network.IsSparseLane(incoming)&&SparseCount(null)>=network.SparseTrafficMaximum;
            int alternatives=0;foreach(int index in exits)if(network.Lanes[index].to!=lane.from&&!network.IsSparseLane(index))alternatives++;
            bool Eligible(int index)=>network.Lanes[index].to!=lane.from&&(!keepOutside||alternatives==0||!network.IsSparseLane(index));
            lane.Path.Sample(lane.Path.Length, out var incomingDirection);
            int Weight(int index)
            {
                network.Lanes[index].Path.Sample(0, out var outgoingDirection);
                return Vector3.Dot(incomingDirection, outgoingDirection) > .93f ? 3 : 1;
            }
            int count=0;
            foreach(int index in exits) if(Eligible(index))count+=Weight(index);
            if(count==0) return exits[0]; // The only turnaround is the authored farm apron.
            int selected=random.Next(count);
            foreach(int index in exits)
            {
                if (!Eligible(index)) continue;
                selected -= Weight(index);
                if (selected < 0) return index;
            }
            throw new InvalidOperationException("No road departure.");
        }
        private bool SignalAllows(TrafficRoadNetwork.Lane lane,double clock)
        {
            int binding=network.Junctions[lane.to].signalIndex;
            if(binding<0)return true;
            if(signals==null || binding>=signals.Length || signals[binding]==null)return false;
            var signal=signals[binding];
            // Read the existing deterministic clock, never tick or mutate distant lamp objects.
            lane.Path.Sample(lane.Path.Length,out var direction);
            return signal.AllowsVehicle(direction,clock);
        }
        private bool ExitClear(TrafficVehicle car)
            => ExitClear(car,car.NextLane);
        private bool ExitClear(TrafficVehicle car,int departure)
        {
            if(!DistrictEntryClear(car,departure))return false;
            foreach(var merge in privateMerges)
                if(merge.Key!=null && merge.Value.Lane==departure && merge.Value.Distance<car.Length+merge.Key.Length+10)return false;
            var exit=network.Lanes[departure].Path.Sample(0,out var direction);
            var side=Vector3.Cross(Vector3.up,direction);
            foreach(var other in active)
            {
                if(other==car)continue;
                var offset=other.LocalPosition-exit;
                float along=Vector3.Dot(offset,direction);
                float across=Mathf.Abs(Vector3.Dot(offset,side));
                float halfLength=ProjectedHalfSize(other,direction),halfWidth=ProjectedHalfSize(other,side);
                if(across<car.Width*.5f+halfWidth+.2f && along+halfLength>-.2f && along-halfLength<car.Length+2)return false;
            }
            return true;
        }
        private void ChooseClearDeparture(TrafficVehicle car)
        {
            var incoming=network.Lanes[car.LaneIndex];
            foreach(int index in network.Departures(incoming.to))
                if(network.Lanes[index].to!=incoming.from && ExitClear(car,index)){car.ChooseDeparture(index);return;}
        }
        private static float ProjectedHalfSize(TrafficVehicle car,Vector3 axis)
            => Mathf.Abs(Vector3.Dot(car.Forward,axis))*car.Length*.5f
                +Mathf.Abs(Vector3.Dot(Vector3.Cross(Vector3.up,car.Forward),axis))*car.Width*.5f;
        private void ReleaseClearedReservation(TrafficVehicle car)
        {
            if(car.ReservedJunction<0 || car.IsTurning)return;
            if(network.Lanes[car.LaneIndex].from==car.ReservedJunction && car.Distance>car.Length*.5f+1)
            { car.ReservedJunction=-1; }
        }
        private float FrontClearance(TrafficVehicle car)
        {
            float result=35;
            foreach(var merge in privateMerges)
            {
                if(merge.Key==null || car.IsTurning || car.LaneIndex!=merge.Value.Lane)continue;
                var mergePoint=network.Lanes[merge.Value.Lane].Path.Sample(merge.Value.Distance,out var direction);
                var waiting=transform.InverseTransformPoint(merge.Key.transform.position);
                float waitingAlong=Vector3.Dot(waiting-mergePoint,direction);
                float occupantAlong=Vector3.Dot(car.LocalPosition-mergePoint,direction);
                // On the farm's shared access, a car may already be ahead of the delivery vehicle.
                // Holding that leader for a gap farther ahead would strand both cars in the same queue.
                if(waitingAlong<0 && occupantAlong>waitingAlong-car.Length*.5f)continue;
                float gap=merge.Value.Distance-car.Distance;
                // A vehicle already passing the access must continue clearing it.
                float buffer=(car.Length+merge.Key.Length)*.5f+2;
                if(gap>buffer-.1f)result=Mathf.Min(result,Mathf.Max(0,gap-buffer));
            }
            foreach(var other in active)
            {
                if(other==car)continue;
                var offset=other.LocalPosition-car.LocalPosition;
                float ahead=Vector3.Dot(offset,car.Forward);
                if(ahead<=0 || ahead>40)continue;
                float lateral=Mathf.Abs(Vector3.Dot(offset,Vector3.Cross(Vector3.up,car.Forward).normalized));
                var side=Vector3.Cross(Vector3.up,car.Forward).normalized;
                if(lateral<car.Width*.5f+ProjectedHalfSize(other,side)+.22f && Mathf.Abs(offset.y)<2)
                    result=Mathf.Min(result,Mathf.Max(0,ahead-car.Length*.5f-ProjectedHalfSize(other,car.Forward)-2));
            }
            return result;
        }
        private float CrossingClearance(TrafficVehicle car)
        {
            var p=car.LocalPosition;
            // These native coordinates are the existing single level crossing, not a second road.
            if(Mathf.Abs(p.x+24)>3.5f || Mathf.Abs(car.Forward.z)<.7f)return 35;
            float edge=car.Forward.z>0 ? 217 : 239;
            float gap=(edge-p.z)*Mathf.Sign(car.Forward.z)-car.Length*.5f;
            // An occupant inside the deck must be able to leave while warnings are active.
            return p.z>217 && p.z<239 ? 35 : gap>=0 ? gap : 35;
        }
        private float ObstacleClearance(TrafficVehicle car,float maximum)
        {
            float scale=transform.lossyScale.x;
            var front=car.LocalPosition+car.Forward*(car.Length*.5f+.25f)+Vector3.up*.9f;
            int count=Physics.BoxCastNonAlloc(transform.TransformPoint(front),new Vector3(car.Width*.44f,.50f,.10f)*scale,
                transform.TransformDirection(car.Forward),hits,car.transform.rotation,Mathf.Min(25,maximum+1)*scale,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return 0;
            float result=maximum;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                var roadVehicle=hit.collider.GetComponentInParent<TrafficVehicle>();
                // Parking visitors reuse the same meshes/wheel component, but their road driver
                // is disabled. They must remain physical obstacles to public traffic.
                if(hit.collider.transform.IsChildOf(car.transform) || roadVehicle!=null&&roadVehicle.enabled || hit.normal.y>.65f)continue;
                result=Mathf.Min(result,Mathf.Max(0,hit.distance/scale-1));
            }
            return result;
        }
        private void TrySpawn(Vector3 observerPosition,bool initial,bool physicsQueries,bool districtOnly=false)
        {
            var source=districtOnly?sparseCandidates:candidates;
            for(int attempt=0;attempt<source.Count;attempt++)
            {
                var spawn=source[(districtOnly?sparseCursor++:spawnCursor++)%source.Count];
                bool sparse=network.IsSparseLane(spawn.Lane);
                if(sparse&&(SparseCount(null)>=network.SparseTrafficMaximum||!initial&&nextSparseSpawn>0))continue;
                bool merging=false;
                foreach(var merge in privateMerges)if(merge.Value.Lane==spawn.Lane && Mathf.Abs(merge.Value.Distance-spawn.Distance)<16){merging=true;break;}
                if(merging)continue;
                if(!initial && Vector3.Distance(spawn.Position,observerPosition)<48)continue;
                bool occupied=false;
                foreach(var other in active)if(Vector3.Distance(other.LocalPosition,spawn.Position)<18){occupied=true;break;}
                if(occupied)continue;
                var design=fleet[designCursor%fleet.Length];
                if(physicsQueries && !SpawnClear(spawn,design))continue;
                TrafficVehicle car=null;
                foreach(var item in pool)if(!item.gameObject.activeSelf && item.DesignId==design.DesignId){car=item;break;}
                if(car==null)
                {
                    if(pool.Count>=maximumCars)
                    {
                        foreach(var item in pool)if(!item.gameObject.activeSelf){car=item;break;}
                        if(car==null)return;
                    }
                    else { car=Instantiate(design,transform);car.gameObject.SetActive(false);pool.Add(car); }
                }
                car.Place(network,spawn.Lane,spawn.Distance,ChooseDeparture(spawn.Lane));
                car.gameObject.SetActive(true);active.Add(car);designCursor++;SuccessfulSpawns++;if(sparse)nextSparseSpawn=network.SparseTrafficSpawnInterval;return;
            }
        }
        private bool SpawnClear(Spawn spawn,TrafficVehicle design)
        {
            network.Lanes[spawn.Lane].Path.Sample(spawn.Distance,out var direction);
            float scale=transform.lossyScale.x;
            var center=transform.TransformPoint(spawn.Position+Vector3.up*.85f);
            // A recycled pool slot can have a different body; reserve the fleet's largest envelope.
            int count=Physics.OverlapBoxNonAlloc(center,spawnHalfExtents*scale,
                spawnHits,transform.rotation*Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore);
            if(count==spawnHits.Length)return false;
            for(int i=0;i<count;i++)if(spawnHits[i].bounds.max.y>center.y-.50f*scale)return false;
            return true;
        }
        private void Recycle(TrafficVehicle car)
        {
            car.ReservedJunction=-1;car.gameObject.SetActive(false);active.Remove(car);
        }
        private Vector2Int Cell(Vector3 p)=>new(Mathf.FloorToInt(p.x/chunkSize),Mathf.FloorToInt(p.z/chunkSize));
    }
}
