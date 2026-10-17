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
        [SerializeField] private int maximumCars = 48;
        [SerializeField] private float chunkSize = 96;
        private readonly List<TrafficVehicle> pool = new();
        private readonly List<TrafficVehicle> active = new();
        private readonly Dictionary<Vector2Int,List<Spawn>> spawnChunks = new();
        private readonly List<Spawn> candidates = new();
        private readonly Dictionary<int,TrafficVehicle> reservations = new();
        private readonly RaycastHit[] hits = new RaycastHit[48];
        private readonly Collider[] spawnHits = new Collider[32];
        private System.Random random;
        private Transform observer;
        private Vector2Int observerChunk;
        private bool initialized, hasObserverChunk;
        private float nextSpawn, nextObserverSearch;
        private int spawnCursor, designCursor;
        private Vector3 spawnHalfExtents;
        public IReadOnlyList<TrafficVehicle> ActiveCars => active;
        public TrafficRoadNetwork Network => network;
        public int PoolCount => pool.Count;
        public int MaximumCars => maximumCars;
        public long SimulatedCarSteps { get; private set; }
        public int SuccessfulSpawns { get; private set; }
        public Vector2Int ObserverChunk => observerChunk;

        private readonly struct Spawn
        {
            public readonly int Lane;
            public readonly float Distance;
            public readonly Vector3 Position;
            public Spawn(int lane,float distance,Vector3 position) { Lane=lane;Distance=distance;Position=position; }
        }

        public void Configure(TrafficRoadNetwork roads, TrafficVehicle[] designs, TownJunctionSignals[] junctionSignals, TownLevelCrossing railCrossing, int capacity=48)
        {
            if (roads == null || designs == null || designs.Length != 20) throw new ArgumentException("The approved twenty-car fleet and road network are required.");
            if(capacity<1 || capacity>96)throw new ArgumentOutOfRangeException(nameof(capacity));
            foreach (var design in designs) if (design == null) throw new ArgumentException("Missing traffic prefab.");
            network=roads;fleet=(TrafficVehicle[])designs.Clone();signals=junctionSignals;crossing=railCrossing;maximumCars=capacity;
        }
        public void SetObserver(Transform target) { observer=target;hasObserverChunk=false; }
        private void Start() { Initialize(); }
        private void OnDisable()
        {
            foreach (var car in active) car.gameObject.SetActive(false);
            active.Clear();reservations.Clear();hasObserverChunk=false;
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
                    list.Add(new Spawn(i,d,p));
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
                if (Mathf.Abs(at.x-cell.x)>2 || Mathf.Abs(at.y-cell.y)>2 || (car.HoldSeconds>90 && Vector3.Distance(car.LocalPosition,localObserver)>95)) Recycle(car);
            }
            nextSpawn-=seconds;
            if (initial || nextSpawn<=0)
            {
                nextSpawn=.5f;
                int attempts=initial ? maximumCars : 3;
                for(int i=0;i<attempts && active.Count<maximumCars;i++) TrySpawn(localObserver,initial,physicsQueries);
            }
            foreach (var car in active)
            {
                if (car.NextLane<0) car.ChooseDeparture(ChooseDeparture(car.LaneIndex));
                ReleaseClearedReservation(car);
                float clearance=FrontClearance(car);
                if (!car.IsTurning && car.ReservedJunction>=0 && network.Lanes[car.LaneIndex].to==car.ReservedJunction)
                {
                    float beforeLine=car.Remaining-7-car.Length*.5f;
                    // A reservation is not permission to pass a newly red signal. Cars that
                    // already crossed the line must clear the junction rather than stop in it.
                    if(beforeLine>.05f && !SignalAllows(network.Lanes[car.LaneIndex],clock))
                    {reservations.Remove(car.ReservedJunction);car.ReservedJunction=-1;}
                }
                if (!car.IsTurning && car.ReservedJunction<0)
                {
                    var lane=network.Lanes[car.LaneIndex];
                    float toStop=car.Remaining-7-car.Length*.5f;
                    bool canEnter=SignalAllows(lane,clock) && ExitClear(car);
                    if (canEnter && toStop<Mathf.Max(2,car.Speed*2) && !reservations.ContainsKey(lane.to))
                    { reservations.Add(lane.to,car);car.ReservedJunction=lane.to; }
                    if (car.ReservedJunction<0) clearance=Mathf.Min(clearance,Mathf.Max(0,toStop));
                }
                if (crossing != null && crossing.IsWarning) clearance=Mathf.Min(clearance,CrossingClearance(car));
                if (physicsQueries) clearance=Mathf.Min(clearance,ObstacleClearance(car,clearance));
                car.Advance(seconds,clearance);SimulatedCarSteps++;
            }
        }

        private int ChooseDeparture(int incoming)
        {
            var lane=network.Lanes[incoming];var exits=network.Departures(lane.to);
            int count=0;
            foreach(int index in exits) if(network.Lanes[index].to!=lane.from)count++;
            if(count==0) return exits[0]; // The only turnaround is the authored farm apron.
            int selected=random.Next(count);
            foreach(int index in exits)
                if(network.Lanes[index].to!=lane.from && selected--==0)return index;
            throw new InvalidOperationException("No road departure.");
        }
        private bool SignalAllows(TrafficRoadNetwork.Lane lane,double clock)
        {
            int binding=network.Junctions[lane.to].signalIndex;
            if(binding<0)return true;
            var signal=signals[binding];
            // Read the existing deterministic clock, never tick or mutate distant lamp objects.
            var state=TownJunctionSignals.Evaluate(clock+signal.PhaseOffset);
            lane.Path.Sample(lane.Path.Length,out var direction);
            return (Mathf.Abs(direction.z)>Mathf.Abs(direction.x) ? state.NorthSouth : state.EastWest)==JunctionSignalAspect.Green;
        }
        private bool ExitClear(TrafficVehicle car)
        {
            var exit=network.Lanes[car.NextLane].Path.Sample(0,out _);
            foreach(var other in active)
                if(other!=car && Vector3.Distance(other.LocalPosition,exit)<(car.Length+other.Length)*.5f+3)return false;
            return true;
        }
        private void ReleaseClearedReservation(TrafficVehicle car)
        {
            if(car.ReservedJunction<0 || car.IsTurning)return;
            if(network.Lanes[car.LaneIndex].from==car.ReservedJunction && car.Distance>car.Length*.5f+1)
            { reservations.Remove(car.ReservedJunction);car.ReservedJunction=-1; }
        }
        private float FrontClearance(TrafficVehicle car)
        {
            float result=35;
            foreach(var other in active)
            {
                if(other==car)continue;
                var offset=other.LocalPosition-car.LocalPosition;
                float ahead=Vector3.Dot(offset,car.Forward);
                if(ahead<=0 || ahead>40)continue;
                float lateral=Mathf.Abs(Vector3.Dot(offset,Vector3.Cross(Vector3.up,car.Forward).normalized));
                if(lateral<(car.Width+other.Width)*.5f+.22f && Mathf.Abs(offset.y)<2)
                    result=Mathf.Min(result,Mathf.Max(0,ahead-(car.Length+other.Length)*.5f-2));
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
                if(hit.collider.transform.IsChildOf(car.transform) || hit.collider.GetComponentInParent<TrafficVehicle>()!=null || hit.normal.y>.65f)continue;
                result=Mathf.Min(result,Mathf.Max(0,hit.distance/scale-1));
            }
            return result;
        }
        private void TrySpawn(Vector3 observerPosition,bool initial,bool physicsQueries)
        {
            for(int attempt=0;attempt<candidates.Count;attempt++)
            {
                var spawn=candidates[spawnCursor++%candidates.Count];
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
                car.gameObject.SetActive(true);active.Add(car);designCursor++;SuccessfulSpawns++;return;
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
            if(car.ReservedJunction>=0)reservations.Remove(car.ReservedJunction);
            car.ReservedJunction=-1;car.gameObject.SetActive(false);active.Remove(car);
        }
        private Vector2Int Cell(Vector3 p)=>new(Mathf.FloorToInt(p.x/chunkSize),Mathf.FloorToInt(p.z/chunkSize));
    }
}
