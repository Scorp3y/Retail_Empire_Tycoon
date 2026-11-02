using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEngine;

namespace RetailEmpireTycoon.Logistics
{
    /// <summary>Directed shortest route over the existing public lanes. Private access is a separate slow leg.</summary>
    public sealed class DriverRoadRoute
    {
        public readonly struct Stop
        {
            public readonly int Lane;
            public readonly float Distance;
            public readonly Vector3 Position;
            public Stop(int lane,float distance,Vector3 position){Lane=lane;Distance=distance;Position=position;}
        }
        private readonly TrafficRoadNetwork roads;
        private readonly bool avoidStreetUTurns;
        private readonly float stopMargin;
        private readonly List<int> lanes=new();
        private Vector3[] mapPoints;
        public IReadOnlyList<int> Lanes=>lanes;
        public Stop Start {get;private set;}
        public Stop End {get;private set;}
        public DriverRoadRoute(TrafficRoadNetwork network,bool avoidStreetUTurns=false,float stopMargin=10)
        {
            roads=network??throw new ArgumentNullException(nameof(network));this.avoidStreetUTurns=avoidStreetUTurns;
            if(!float.IsFinite(stopMargin)||stopMargin<0)throw new ArgumentOutOfRangeException(nameof(stopMargin));this.stopMargin=stopMargin;
        }
        public Stop Nearest(Vector3 position,Vector3? preferredHeading=null)
        {
            float best=float.PositiveInfinity;Stop result=default;
            for(int lane=0;lane<roads.Lanes.Count;lane++)
            {
                var candidate=NearestOnLane(lane,position);float cost=(candidate.Position-position).sqrMagnitude;
                if(preferredHeading.HasValue)
                {roads.Lanes[lane].Path.Sample(candidate.Distance,out var tangent);cost+=8*(1-Vector3.Dot(tangent,preferredHeading.Value.normalized));}
                if(cost<best){best=cost;result=candidate;}
            }
            if(float.IsPositiveInfinity(best))throw new InvalidOperationException("No public lane has room for a delivery stop.");
            return result;
        }
        public bool Plan(Vector3 from,Vector3 to,Vector3? startHeading=null)
        {
            return Plan(Nearest(from,startHeading),Nearest(to));
        }
        /// <summary>Plans between explicit projected lanes, allowing a private entrance to evaluate either legal direction.</summary>
        public bool Plan(Stop from,Stop to)
        {
            Stop Validate(Stop stop)
            {
                if(stop.Lane<0||stop.Lane>=roads.Lanes.Count||!float.IsFinite(stop.Distance)||stop.Distance<0||stop.Distance>roads.Lanes[stop.Lane].Path.Length)
                    throw new ArgumentException("A route stop must lie on an authored lane.");
                return new Stop(stop.Lane,stop.Distance,roads.Lanes[stop.Lane].Path.Sample(stop.Distance,out _));
            }
            Start=Validate(from);End=Validate(to);lanes.Clear();mapPoints=null;
            if(Start.Lane==End.Lane && End.Distance>=Start.Distance){lanes.Add(Start.Lane);return true;}
            int count=roads.Lanes.Count;
            var cost=Enumerable.Repeat(float.PositiveInfinity,count).ToArray();
            var previous=Enumerable.Repeat(-1,count).ToArray();var visited=new bool[count];
            // Seed exits rather than the starting lane so a stop behind the vehicle requires a legal loop.
            foreach(int next in roads.Departures(roads.Lanes[Start.Lane].to))
            {if(!Allowed(Start.Lane,next))continue;cost[next]=roads.Lanes[Start.Lane].Path.Length-Start.Distance+roads.Turn(Start.Lane,next).Length;previous[next]=-2;}
            for(int step=0;step<count;step++)
            {
                int current=-1;
                for(int i=0;i<count;i++)if(!visited[i] && (current<0 || cost[i]<cost[current]))current=i;
                if(current<0 || float.IsPositiveInfinity(cost[current]))return false;
                if(current==End.Lane)
                {
                    while(current>=0){lanes.Insert(0,current);current=previous[current];}
                    lanes.Insert(0,Start.Lane);return true;
                }
                visited[current]=true;
                foreach(int next in roads.Departures(roads.Lanes[current].to))
                {
                    if(!Allowed(current,next))continue;
                    float candidate=cost[current]+roads.Lanes[current].Path.Length+roads.Turn(current,next).Length;
                    if(candidate>=cost[next])continue;
                    cost[next]=candidate;previous[next]=current;
                }
            }
            return false;
        }
        private bool Allowed(int incoming,int outgoing) => !avoidStreetUTurns
            || roads.Lanes[incoming].from!=roads.Lanes[outgoing].to
            || roads.Departures(roads.Lanes[incoming].to).Count==1;
        public Stop NearestOnLane(int lane,Vector3 position)
        {
            if(lane<0||lane>=roads.Lanes.Count)throw new ArgumentOutOfRangeException(nameof(lane));
            var samples=roads.Lanes[lane].points;float best=float.PositiveInfinity,accumulated=0;Stop result=default;
            for(int i=1;i<samples.Length;i++)
            {
                var delta=samples[i]-samples[i-1];float length=delta.magnitude;if(length<.0001f)continue;
                float t=Mathf.Clamp01(Vector3.Dot(position-samples[i-1],delta)/delta.sqrMagnitude);var p=samples[i-1]+delta*t;
                if((p-position).sqrMagnitude<best){best=(p-position).sqrMagnitude;result=new Stop(lane,accumulated+length*t,p);}
                accumulated+=length;
            }
            // Delivery AI must withdraw before automatic junction turning starts.
            // Player GPS requests zero margin, as it displays rather than drives the car.
            var path=roads.Lanes[lane].Path;float margin=Mathf.Min(stopMargin,path.Length*.25f);
            float distance=Mathf.Clamp(result.Distance,margin,path.Length-margin);
            return new Stop(lane,distance,path.Sample(distance,out _));
        }
        public Vector3[] MapPoints()
        {
            if(mapPoints!=null)return mapPoints;
            var points=new List<Vector3>();
            for(int i=0;i<lanes.Count;i++)
            {
                var path=roads.Lanes[lanes[i]].Path;float start=i==0?Start.Distance:0,end=i==lanes.Count-1?End.Distance:path.Length;
                for(float d=start;d<end;d+=4)points.Add(path.Sample(d,out _));
                points.Add(path.Sample(end,out _));
                if(i<lanes.Count-1){var turn=roads.Turn(lanes[i],lanes[i+1]);for(float d=0;d<turn.Length;d+=2)points.Add(turn.Sample(d,out _));}
            }
            return mapPoints=points.ToArray();
        }
    }
}
