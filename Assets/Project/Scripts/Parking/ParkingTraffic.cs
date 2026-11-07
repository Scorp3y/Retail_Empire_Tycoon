using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>Parking arrivals reserve their bay and approach; parked visitors release it only after departure.</summary>
    public sealed class ParkingTraffic : MonoBehaviour
    {
        public GridSystem grid;
        public FloorPainter asphalt;
        public GameObject carPrefab;
        private ParkingVisitorFleet fleet;
        private int designCursor;
        [SerializeField] private RetailEmpireTycoon.StoreOperations.StoreOperations store;
        [SerializeField] private TownRoadTraffic streetTraffic;
        private ParkingStreetAccess streetAccess;
        public Transform roadEntry;
        public Transform drivewayEntry;
        public float arrivalInterval=12;
        // Kept for compatibility with older editor scripts; buyer completion now controls departure.
        [HideInInspector] public float parkingStaySeconds=25;
        private readonly ParkingReservations reservations=new ParkingReservations();
        private readonly List<Visit> visits=new List<Visit>();
        private Visit moving;
        private float nextArrival,clock;
        public bool CanEdit(ParkingBay bay) => bay==null || visits.All(v=>v.bay!=bay);
        public IReadOnlyList<GameObject> VisitorCars=>visits.Select(visit=>visit.car).ToArray();
        public bool HasVisitorBays=>FindObjectsOfType<ParkingBay>().Any(IsVisitorBay);
        private static bool IsVisitorBay(ParkingBay bay)=>bay.GetComponentInParent<BuildPreview>()==null && !(bay.GetComponent<PlacedObject>()?.playerParking ?? false);
        public bool BlocksStreetEntrance(Vector3 point,float cellHalfWidth)
        {
            if(roadEntry==null || drivewayEntry==null)return false;
            var start=roadEntry.position;var delta=drivewayEntry.position-start;delta.y=0;
            var offset=point-start;offset.y=0;
            float along=delta.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(offset,delta)/delta.sqrMagnitude):0;
            return (offset-delta*along).sqrMagnitude<Mathf.Pow(.7f+cellHalfWidth,2);
        }
        public string MovementState=>moving==null?"No active manoeuvre":$"{moving.car?.name} at {moving.car?.transform.position} step {moving.next}/{moving.route.Count}, target {(moving.next<moving.route.Count?moving.route[moving.next]:Vector3.zero)}, blockedSince={moving.blockedSince}, obstacle={LastObstacle}";
        public string LastObstacle {get;private set;}
        public string ArrivalState {get;private set;}
        private void Awake()
        {
            fleet=Resources.Load<ParkingVisitorFleet>("Parking/Visitors");
            if(store==null)store=FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();
            streetAccess=new ParkingStreetAccess(streetTraffic,roadEntry,drivewayEntry);
            if(fleet==null){Debug.LogError("Parking visitor fleet is missing; old miniature cars are not used.",this);enabled=false;}
        }
        private sealed class Visit
        {
            public ParkingBay bay; public GameObject car; public List<Vector3> route;
            public int id,next; public float leaveAt,blockedSince,speed,width,length,modelScale; public bool leaving;
            public TrafficVehicle wheels;
            public ShopperVisit shopper;
            public bool parked;
            public float retryAt,parkedAt;
            public List<Vector3> exitRoute;
            public int streetDepartureStart;
            public int departureLane;
            public float departureDistance, mergeDistance;
            public bool arrivingOnStreet;
        }
        private void Update()
        {
            float remaining=Time.deltaTime;
            while(remaining>0){float step=Mathf.Min(.1f,remaining);Advance(step);remaining-=step;}
        }
        /// <summary>One simulation clock for arrivals, motion and departure; supports pause/speed changes and deterministic checks.</summary>
        public void Advance(float seconds)
        {
            if(seconds<=0||seconds>.25f||float.IsNaN(seconds))throw new System.ArgumentOutOfRangeException(nameof(seconds));
            clock+=seconds;
            foreach(var visit in visits.ToArray())
                if(visit.bay==null || visit.car==null) Release(visit);
            foreach(var visit in visits.Where(visit=>visit.parked && visit.leaveAt==0))
            {
                if(visit.shopper!=null)
                {
                    if(visit.shopper.IsFinished)visit.leaveAt=clock+.8f;
                    continue;
                }
                if(store==null || !store.IsOpen || clock-visit.parkedAt>60){visit.leaveAt=clock+.8f;continue;}
                if(clock<visit.retryAt)continue;
                visit.retryAt=clock+1;
                // Try both front doors. Never create the actor inside the vehicle collider.
                foreach(int side in new[]{-1,1})
                {
                    var door=visit.car.transform.position+visit.car.transform.right*side*(visit.width*.5f+.22f);
                    door.y=.025f;
                    if(store.TrySpawnParkingCustomer(door,out visit.shopper))break;
                }
            }
            if(moving!=null) { Move(seconds); return; }
            var departure=visits.FirstOrDefault(v=>v.leaveAt>0 && clock>=v.leaveAt);
            if(departure!=null)
            {
                if(!reservations.BeginManeuver(departure.bay.GetInstanceID())) return;
                departure.leaving=true;
                departure.route=departure.exitRoute;
                departure.next=0; departure.blockedSince=0;
                moving=departure; return;
            }
            if(clock<nextArrival || store==null || !store.CanAcceptCustomer) return;
            nextArrival=clock+.6f;
            if(!streetAccess.PrepareLane()){ArrivalState=streetAccess.Status;return;}
            var passing=streetTraffic.ActiveCars.Where(car=>ParkingVisitorFleet.IsAllowed(car.DesignId) && streetAccess.CanDivert(car)).ToArray();
            foreach(var bay in FindObjectsOfType<ParkingBay>().Where(IsVisitorBay)
                .OrderBy(bay=>(bay.ApproachPosition-drivewayEntry.position).sqrMagnitude))
            {
                int id=bay.GetInstanceID(); if(reservations.IsReserved(id)) continue;
                var existing=passing.FirstOrDefault(car=>bay.Fits(car.Width*car.transform.lossyScale.x,car.Length*car.transform.lossyScale.x));
                var design=existing!=null?existing:fleet.NextFitting(bay,ref designCursor);
                if(design==null)continue;
                float scale=existing!=null?existing.transform.lossyScale.x:fleet.ModelScale;
                float width=design.Width*scale;
                var route=Route(bay,width); if(route.Count==0){ArrivalState="No asphalt route to "+bay.name;continue;}
                float length=design.Length*scale;
                if(existing==null && !streetAccess.TryPrepare(width,length,Camera.main)){ArrivalState=streetAccess.Status;continue;}
                var exit=new List<Vector3>(route);exit.Reverse();
                int streetDepartureStart=exit.Count;
                exit.AddRange(streetAccess.Departure);
                if(!reservations.Reserve(id)||!reservations.BeginManeuver(id)) continue;
                var driver=existing;
                if(existing!=null)
                {
                    if(!streetTraffic.TryReserveParkingArrival(driver)){reservations.Release(id);continue;}
                }
                else
                {
                    var arrival=streetAccess.Arrival;
                    driver=Instantiate(design,arrival[0],Quaternion.LookRotation(arrival[1]-arrival[0]));
                    driver.name="Parking visitor";
                    driver.transform.localScale=Vector3.one*scale;
                    if(!streetTraffic.TryJoinTraffic(driver,streetAccess.SpawnLane,streetAccess.SpawnDistance,true))
                    {Destroy(driver.gameObject);reservations.Release(id);ArrivalState="Waiting for a road traffic slot.";continue;}
                }
                streetAccess.GuideArrival(driver);
                moving=new Visit{id=id,bay=bay,car=driver.gameObject,route=route,exitRoute=exit,width=width,length=length,modelScale=scale,wheels=driver,
                    arrivingOnStreet=true,streetDepartureStart=streetDepartureStart,
                    departureLane=streetAccess.LaneIndex,departureDistance=streetAccess.ExitDistance,mergeDistance=streetAccess.MergeDistance};
                visits.Add(moving);ArrivalState=existing!=null?"Diverted from city traffic":"Arriving from street";nextArrival=clock+arrivalInterval;break;
            }
        }
        private List<Vector3> Route(ParkingBay bay,float vehicleWidth)
        {
            var cells=asphalt.BuildSaveData().Where(s=>s.floor==0).Select(s=>new Vector3Int(s.x,0,s.z)).Where(asphalt.IsAsphalt).ToHashSet();
            if(cells.Count==0) return new List<Vector3>();
            var entry=drivewayEntry!=null?drivewayEntry.position:roadEntry.position;
            var start=cells.OrderBy(c=>(grid.CellToWorld(c)-entry).sqrMagnitude).First();
            var goal=grid.WorldToCell(bay.ApproachPosition);
            if(!cells.Contains(goal)) return new List<Vector3>();
            var frontier=new Queue<Vector3Int>(); var previous=new Dictionary<Vector3Int,Vector3Int>(); frontier.Enqueue(start); previous[start]=start;
            while(frontier.Count>0)
            {
                var current=frontier.Dequeue(); if(current==goal) break;
                foreach(var direction in new[]{Vector3Int.left,Vector3Int.right,Vector3Int.forward,Vector3Int.back})
                {
                    var next=current+direction;
                    // Reserve space for the entire car, not just its center point.
                    if(previous.ContainsKey(next)||!cells.Contains(next)||grid.IsOccupied(next))continue;
                    int radius=Mathf.CeilToInt((vehicleWidth*.5f+.025f)/grid.cellSize);
                    bool clear=true;
                    for(int offset=-radius;offset<=radius;offset++)
                    {
                        var side=next+new Vector3Int(direction.z,0,direction.x)*offset;
                        if(!cells.Contains(side)||grid.IsOccupied(side)){clear=false;break;}
                    }
                    if(!clear)continue;
                    previous[next]=current; frontier.Enqueue(next);
                }
            }
            if(!previous.ContainsKey(goal)) return new List<Vector3>();
            var path=new List<Vector3>(); var cursor=goal;
            while(cursor!=start) { path.Add(grid.CellToWorld(cursor)+Vector3.up*.02f); cursor=previous[cursor]; }
            path.Add(grid.CellToWorld(start)+Vector3.up*.02f); path.Reverse();
            if(drivewayEntry!=null)path.Insert(0,drivewayEntry.position+Vector3.up*.02f);
            path.Insert(0,roadEntry.position+Vector3.up*.02f);
            path.Add(bay.ParkPosition+Vector3.up*.02f);
            return RoundTurns(path);
        }

        private static List<Vector3> RoundTurns(List<Vector3> path)
        {
            var corners=new List<Vector3>{path[0]};
            for(int i=1;i<path.Count-1;i++)
            {
                var from=(path[i]-path[i-1]).normalized;
                var to=(path[i+1]-path[i]).normalized;
                if(Vector3.Dot(from,to)<.999f || i==1 || i==path.Count-2)corners.Add(path[i]);
            }
            corners.Add(path[path.Count-1]);
            var result=new List<Vector3>{corners[0],corners[1]};
            for(int i=2;i<corners.Count-2;i++)
            {
                var center=corners[i];
                var before=corners[i-1]-center;
                var after=corners[i+1]-center;
                float cut=Mathf.Min(.48f,Mathf.Min(before.magnitude,after.magnitude)*.45f);
                var a=center+before.normalized*cut;
                var b=center+after.normalized*cut;
                for(int j=0;j<=5;j++)
                {
                    float t=j/5f,u=1-t;
                    result.Add(a*u*u+center*(2*u*t)+b*t*t);
                }
            }
            result.Add(corners[corners.Count-2]);result.Add(corners[corners.Count-1]);
            return result;
        }

        private void Move(float seconds)
        {
            if(moving.arrivingOnStreet)
            {
                var driver=moving.wheels;streetAccess.GuideArrival(driver);
                if(driver.IsTurning || driver.LaneIndex!=streetAccess.LaneIndex || driver.Distance<streetAccess.AccessDistance)return;
                var start=driver.transform.position;
                var heading=streetTraffic.transform.TransformDirection(driver.Forward);
                var entry=moving.route[1];
                var inward=(drivewayEntry.position-roadEntry.position).normalized;
                float handle=Vector3.Distance(start,entry)*.45f;
                var b=start+heading*handle;
                var c=entry-inward*handle;
                moving.route.RemoveAt(0);
                for(int i=12;i>=1;i--)
                {
                    float t=i/13f,u=1-t;
                    moving.route.Insert(0,start*u*u*u+b*(3*u*u*t)+c*(3*u*t*t)+entry*t*t*t);
                }
                moving.speed=Mathf.Min(1.25f,driver.Speed*streetTraffic.transform.lossyScale.x);
                streetTraffic.WithdrawParkingCar(driver);moving.car.transform.SetParent(transform,true);
                moving.car.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.None;
                moving.arrivingOnStreet=false;
            }
            if(moving.next>=moving.route.Count)
            {
                if(moving.leaving)
                {
                    // Recycle only out of view. The car has already travelled down the street, not vanished at the entrance.
                    if(!Visible(moving.car,moving.length))Release(moving);
                    else if(streetTraffic.TryJoinTraffic(moving.wheels,moving.departureLane,moving.departureDistance,
                        mergeSpeed: moving.speed / streetTraffic.transform.lossyScale.x))
                    {
                        var finished=moving;finished.car=null;Release(finished);
                    }
                }
                else
                {
                    moving.car.transform.SetPositionAndRotation(moving.bay.ParkPosition,moving.bay.ParkRotation);
                    var parkedBody=moving.car.GetComponent<Rigidbody>();parkedBody.position=moving.bay.ParkPosition;parkedBody.rotation=moving.bay.ParkRotation;
                    moving.wheels.AnimateParkingWheels(0,0,1);
                    moving.parked=true;moving.parkedAt=clock;reservations.EndManeuver(moving.bay.GetInstanceID());moving=null;
                }
                return;
            }
            if (moving.leaving && moving.next == moving.streetDepartureStart)
            {
                streetTraffic.RequestPrivateMerge(moving.wheels, moving.departureLane, moving.mergeDistance);
                if (!streetTraffic.HasMergeGap(moving.wheels, moving.departureLane, moving.mergeDistance))
                {
                    moving.speed = 0;
                    moving.wheels.AnimateParkingWheels(0, 0, seconds);
                    return;
                }
            }
            var car=moving.car; var target=moving.route[moving.next]; var delta=target-car.transform.position; delta.y=0;
            if(delta.sqrMagnitude<.001f) { moving.next++; return; }
            var front=car.transform.position+delta.normalized*(moving.length*.5f+.025f)+Vector3.up*.4f;
            var blocking=Physics.BoxCastAll(front,new Vector3(moving.width*.45f,.25f,.03f),delta.normalized,Quaternion.LookRotation(delta.sqrMagnitude>.00001f?delta:Vector3.forward),
                Mathf.Min(delta.magnitude,.35f),~0,QueryTriggerInteraction.Ignore)
                .FirstOrDefault(h=>!h.transform.IsChildOf(car.transform) && h.transform!=car.transform && h.normal.y<.65f);
            bool obstacle=blocking.collider!=null;LastObstacle=obstacle?blocking.collider.name:string.Empty;
            if(obstacle)
            {
                moving.speed=0;
                moving.wheels.AnimateParkingWheels(0,0,seconds);
                if(moving.blockedSince==0) moving.blockedSince=clock;
                // A removed bay or blocked driveway must not retain a lease forever.
                // Do not make an occupied, visible car disappear just because a queue is slow.
                if(clock-moving.blockedSince>30 && !Visible(car,moving.length))Release(moving);
                return;
            }
            moving.blockedSince=0;
            if(delta.sqrMagnitude<.001f) { moving.next++; return; }
            bool onStreet=moving.leaving && moving.next>=moving.streetDepartureStart;
            bool reversing=moving.leaving&&moving.next<=1;
            var look=delta.normalized;
            if(!reversing && moving.next+1<moving.route.Count)
            {
                var ahead=moving.route[moving.next+1]-target;ahead.y=0;
                if(ahead.sqrMagnitude>.0001f)look=Vector3.Slerp(look,ahead.normalized,.5f*Mathf.Clamp01(1-delta.magnitude/.65f));
            }
            float steer=Vector3.SignedAngle(car.transform.forward,reversing?-look:look,Vector3.up);
            float limit=onStreet?streetTraffic.Network.Lanes[moving.departureLane].speedLimit*streetTraffic.transform.lossyScale.x*.75f
                :reversing?.95f:Mathf.Lerp(1.7f,.78f,Mathf.Clamp01(Mathf.Abs(steer)/75));
            if(moving.next==moving.route.Count-1 && !moving.leaving)limit=Mathf.Min(limit,Mathf.Sqrt(2*1.7f*delta.magnitude));
            moving.speed=Mathf.MoveTowards(moving.speed,limit,(limit<moving.speed?2.6f:1.5f)*seconds);
            var position=Vector3.MoveTowards(car.transform.position,target,moving.speed*seconds);
            var facing=Quaternion.LookRotation(reversing?-look:look);
            moving.wheels.AnimateParkingWheels(Vector3.Distance(car.transform.position,position)*(reversing?-1:1)/moving.modelScale,steer*1.6f,seconds);
            car.transform.SetPositionAndRotation(position,Quaternion.RotateTowards(car.transform.rotation,facing,125*seconds));
            var body=car.GetComponent<Rigidbody>();body.position=position;body.rotation=car.transform.rotation;
            if((position-target).sqrMagnitude<.000001f)moving.next++;
        }
        private void Release(Visit visit)
        {
            if(visit.arrivingOnStreet)streetTraffic.WithdrawParkingCar(visit.wheels);
            if (visit.wheels != null) streetTraffic.CancelPrivateMerge(visit.wheels);
            if(visit.shopper!=null && !visit.shopper.IsFinished)store?.CancelParkingVisit(visit.shopper);
            reservations.Release(visit.id);
            if(visit.car!=null) Destroy(visit.car); visits.Remove(visit); if(moving==visit) moving=null;
        }
        private static bool Visible(GameObject car,float length)
        {
            var camera=Camera.main;
            return camera!=null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),
                new Bounds(car.transform.position+Vector3.up*.4f,Vector3.one*(length+1)));
        }
        private void OnDisable() { foreach(var visit in visits.ToArray()) Release(visit); }
    }
}
