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
        private readonly List<int> lanes=new();
        private Vector3[] mapPoints;
        public IReadOnlyList<int> Lanes=>lanes;
        public Stop Start {get;private set;}
        public Stop End {get;private set;}
        public DriverRoadRoute(TrafficRoadNetwork network){roads=network??throw new ArgumentNullException(nameof(network));}
        public Stop Nearest(Vector3 position,Vector3? preferredHeading=null)
        {
            float best=float.PositiveInfinity;Stop result=default;
            for(int lane=0;lane<roads.Lanes.Count;lane++)
            {
                var path=roads.Lanes[lane].Path;
                for(float d=10;d<path.Length-10;d+=1)
                {
                    var point=path.Sample(d,out var tangent);float cost=(point-position).sqrMagnitude;
                    if(preferredHeading.HasValue)cost+=8*(1-Vector3.Dot(tangent,preferredHeading.Value.normalized));
                    if(cost>=best)continue;
                    best=cost;result=new Stop(lane,d,point);
                }
            }
            if(float.IsPositiveInfinity(best))throw new InvalidOperationException("No public lane has room for a delivery stop.");
            return result;
        }
        public bool Plan(Vector3 from,Vector3 to,Vector3? startHeading=null)
        {
            Start=Nearest(from,startHeading);End=Nearest(to);lanes.Clear();mapPoints=null;
            if(Start.Lane==End.Lane && End.Distance>=Start.Distance){lanes.Add(Start.Lane);return true;}
            int count=roads.Lanes.Count;
            var cost=Enumerable.Repeat(float.PositiveInfinity,count).ToArray();
            var previous=Enumerable.Repeat(-1,count).ToArray();var visited=new bool[count];
            // Seed exits rather than the starting lane so a stop behind the vehicle requires a legal loop.
            foreach(int next in roads.Departures(roads.Lanes[Start.Lane].to))
            {cost[next]=roads.Lanes[Start.Lane].Path.Length-Start.Distance+roads.Turn(Start.Lane,next).Length;previous[next]=-2;}
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
                    float candidate=cost[current]+roads.Lanes[current].Path.Length+roads.Turn(current,next).Length;
                    if(candidate>=cost[next])continue;
                    cost[next]=candidate;previous[next]=current;
                }
            }
            return false;
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
