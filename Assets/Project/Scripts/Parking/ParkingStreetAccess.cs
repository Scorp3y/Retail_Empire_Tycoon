using System.Collections.Generic;
using RetailEmpireTycoon.City;
using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>Finds a hidden upstream spawn and a directed road journey to the private driveway.
    /// The road controller handles intervening junctions; departure never reverses a street lane.</summary>
    public sealed class ParkingStreetAccess
    {
        private readonly TownRoadTraffic traffic;
        private readonly Transform streetEntry, drivewayEntry;
        private TrafficPolyline lane;
        private float accessDistance;
        public int LaneIndex {get;private set;}
        public float ExitDistance {get;private set;}
        public int SpawnLane {get;private set;}
        public float SpawnDistance {get;private set;}
        public float AccessDistance=>accessDistance;
        public string Status {get;private set;}
        private readonly List<int> arrivalLanes=new List<int>();
        private readonly List<Vector3> arrival=new List<Vector3>(),departure=new List<Vector3>();
        public IReadOnlyList<Vector3> Arrival=>arrival;
        public IReadOnlyList<Vector3> Departure=>departure;

        public ParkingStreetAccess(TownRoadTraffic traffic,Transform streetEntry,Transform drivewayEntry)
        {this.traffic=traffic;this.streetEntry=streetEntry;this.drivewayEntry=drivewayEntry;}

        public bool TryPrepare(float width,float length,Camera camera)
        {
            arrival.Clear();departure.Clear();
            if(traffic==null || streetEntry==null || drivewayEntry==null || !FindLane())return Reject("No connected street lane.");
            float scale=traffic.transform.lossyScale.x;
            float start=FindUpstreamSpawn(32/scale);
            if(start<0)return Reject("No upstream street spawn.");
            float end=Mathf.Min(lane.Length-12,accessDistance+32/scale);
            ExitDistance=end;
            if(end-accessDistance<6/scale)return Reject("No downstream departure room.");
            var spawnPath=traffic.Network.Lanes[SpawnLane].Path;
            var first=World(spawnPath,start);
            var initialDirection=World(spawnPath,start+1)-first;
            var bounds=new Bounds(first+Vector3.up*.4f,new Vector3(length+1,1.8f,length+1));
            // A large camera view may expose the whole block. Delay arrivals instead of popping into it.
            if(camera!=null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),bounds))return Reject("Street spawn is visible.");
            var rotation=Quaternion.LookRotation(initialDirection);
            foreach(var collider in Physics.OverlapBox(first+Vector3.up*.4f,new Vector3(width*.5f+.15f,.3f,length*.5f+.4f),rotation,~0,QueryTriggerInteraction.Ignore))
                if(collider.GetComponentInParent<TrafficVehicle>()!=null)return Reject("Street spawn is occupied.");
            arrival.Add(first);arrival.Add(first+initialDirection);
            departure.Add(streetEntry.position+Vector3.up*.02f);
            AddSamples(departure,accessDistance,end);
            Status="Ready";return true;
        }
        private bool Reject(string reason){Status=reason;return false;}

        /// <summary>Keep the destination lane fixed while the shared road driver handles signals and turns.</summary>
        public void GuideArrival(TrafficVehicle car)
        {
            if(car.IsTurning)return;
            int index=arrivalLanes.IndexOf(car.LaneIndex);
            if(index>=0 && index<arrivalLanes.Count-1)car.ChooseDeparture(arrivalLanes[index+1]);
        }
        private float FindUpstreamSpawn(float distance)
        {
            arrivalLanes.Clear();arrivalLanes.Add(LaneIndex);
            int current=LaneIndex;float at=accessDistance;
            for(int attempt=0;attempt<4;attempt++)
            {
                if(at-distance>=12)
                {
                    SpawnLane=current;SpawnDistance=at-distance;return SpawnDistance;
                }
                var currentLane=traffic.Network.Lanes[current];
                currentLane.Path.Sample(0,out var forward);
                int previous=-1;
                for(int i=0;i<traffic.Network.Lanes.Count;i++)
                {
                    var candidate=traffic.Network.Lanes[i];
                    if(candidate.to!=currentLane.from)continue;
                    candidate.Path.Sample(candidate.Path.Length,out var approaching);
                    if(Vector3.Dot(approaching,forward)>.95f){previous=i;break;}
                }
                if(previous<0)return -1;
                var path=traffic.Network.Lanes[previous].Path;
                distance-=at+traffic.Network.Turn(previous,current).Length;
                current=previous;at=path.Length;arrivalLanes.Insert(0,current);
            }
            return -1;
        }

        private bool FindLane()
        {
            var inward=(drivewayEntry.position-streetEntry.position).normalized;
            float best=float.PositiveInfinity;lane=null;
            for(int index=0;index<traffic.Network.Lanes.Count;index++)
            {
                var candidate=traffic.Network.Lanes[index];
                for(float d=8;d<candidate.Path.Length-8;d+=1)
                {
                    var point=candidate.Path.Sample(d,out var tangent);
                    var direction=traffic.transform.TransformDirection(tangent);
                    if(Vector3.Dot(Vector3.Cross(Vector3.up,direction),inward)<.7f)continue;
                    float distance=(traffic.transform.TransformPoint(point)-streetEntry.position).sqrMagnitude;
                    if(distance>=best)continue;
                    best=distance;lane=candidate.Path;accessDistance=d;LaneIndex=index;
                }
            }
            return lane!=null && best<36;
        }

        private Vector3 World(float distance)
            => World(lane,distance);
        private Vector3 World(TrafficPolyline path,float distance)
        {
            var point=traffic.transform.TransformPoint(path.Sample(distance,out _));
            point.y=.02f;return point;
        }
        private void AddSamples(List<Vector3> points,float start,float end)
        {
            float step=1/traffic.transform.lossyScale.x;
            for(float d=start;d<end;d+=step)points.Add(World(d));
            points.Add(World(end));
        }
    }
}
