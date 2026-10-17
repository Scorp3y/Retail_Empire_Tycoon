using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEngine;

/// <summary>Compresses the installed public tile graph; adds only the existing forest and farm corridors.</summary>
internal static class TrafficNetworkAuthoring
{
    private static readonly Vector2Int[] Steps={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    internal static TrafficRoadNetwork Build(GameObject town,TownJunctionSignals[] signals)
    {
        var layout=town.GetComponent<TownWorldPlaces>().layout;
        var signalCenters=town.GetComponent<TownSignalChunks>().Junctions;
        var tiles=layout.roads.ToDictionary(r=>r.cell);
        var forced=new HashSet<Vector2Int>{new(-2,16),new(22,-3)};
        bool Node(TownRoadTile tile)=>forced.Contains(tile.cell) || (tile.connections!=5 && tile.connections!=10);
        var nodes=new List<TrafficRoadNetwork.Junction>();
        var nodeCells=new Dictionary<Vector2Int,int>();
        int Add(Vector3 p)
        {
            int existing=nodes.FindIndex(n=>Vector3.Distance(n.position,p)<.01f);
            if(existing>=0)return existing;
            int signal=Array.FindIndex(signals,s=>signalCenters.Any(binding=>binding.Signal==s&&Vector3.Distance(binding.Center,p)<.5f));
            nodes.Add(new TrafficRoadNetwork.Junction{position=p,signalIndex=signal});return nodes.Count-1;
        }
        foreach(var tile in layout.roads.Where(Node))nodeCells.Add(tile.cell,Add(tile.Position(layout.roadTileSize)));
        var lanes=new List<TrafficRoadNetwork.Lane>();
        foreach(var tile in layout.roads.Where(Node))
        {
            for(int dir=0;dir<4;dir++)
            {
                if((tile.connections&(1<<dir))==0)continue;
                var cursor=tile.cell+Steps[dir];
                int guard=0;
                while(!nodeCells.ContainsKey(cursor))
                {
                    if(++guard>200 || !tiles.ContainsKey(cursor))throw new InvalidOperationException("Broken native road graph.");
                    cursor+=Steps[dir];
                }
                AddLane(nodeCells[tile.cell],nodeCells[cursor],new[]{tile.Position(layout.roadTileSize),tiles[cursor].Position(layout.roadTileSize)},false);
            }
        }
        // Exact centre lines of the already-built north avenue and five forest district roads.
        int gateway=nodeCells[new Vector2Int(-2,16)];
        int middleSouth=Add(new Vector3(-24,0,310)),middleNorth=Add(new Vector3(-24,0,430));
        int westSouth=Add(new Vector3(-120,0,310)),westNorth=Add(new Vector3(-120,0,430));
        int eastSouth=Add(new Vector3(72,0,310)),eastNorth=Add(new Vector3(72,0,430));
        var avenue=Enumerable.Range(0,119).Select(i=>new Vector3(-24,0,192+i)).ToArray();
        Pair(gateway,middleSouth,avenue,false);
        foreach(var pair in new[]{(middleSouth,middleNorth),(westSouth,westNorth),(eastSouth,eastNorth),
                     (westSouth,middleSouth),(middleSouth,eastSouth),(westNorth,middleNorth),(middleNorth,eastNorth)})
            Pair(pair.Item1,pair.Item2,new[]{nodes[pair.Item1].position,nodes[pair.Item2].position},false);
        int farm=Add(layout.countryRoad.points.Last());
        Pair(nodeCells[new Vector2Int(22,-3)],farm,layout.countryRoad.points,true);
        var result=ScriptableObject.CreateInstance<TrafficRoadNetwork>();
        result.name="Public road traffic network";result.Configure(nodes.ToArray(),lanes.ToArray());
        // Ensure every district can be reached and every lane has a legal departure.
        var visited=new HashSet<int>();var queue=new Queue<int>();queue.Enqueue(0);
        while(queue.Count>0){int at=queue.Dequeue();if(!visited.Add(at))continue;foreach(int edge in result.Departures(at))queue.Enqueue(lanes[edge].to);}
        if(visited.Count!=nodes.Count)throw new InvalidOperationException("Disconnected traffic district.");
        foreach(var node in nodes)
            if(layout.reservedShopPlot.Contains(new Vector2(node.position.x,node.position.z)))throw new InvalidOperationException("Traffic entered shop reserve.");
        return result;

        void Pair(int a,int b,Vector3[] path,bool rural){AddLane(a,b,path,rural);AddLane(b,a,path.Reverse().ToArray(),rural);}
        void AddLane(int a,int b,Vector3[] center,bool rural)
        {
            var source=new TrafficPolyline(center);
            var points=new List<Vector3>();
            for(float d=5.5f;d<source.Length-5.5f;d+=1)points.Add(LanePoint(d));
            points.Add(LanePoint(source.Length-5.5f));
            if(points.Count<2)throw new InvalidOperationException("Junctions too close for lane trimming.");
            lanes.Add(new TrafficRoadNetwork.Lane{from=a,to=b,points=points.ToArray(),rural=rural,speedLimit=rural ? 10 : 8.5f});
            Vector3 LanePoint(float d)
            {
                var p=source.Sample(d,out var tangent);
                var lateral=Vector3.Cross(Vector3.up,tangent).normalized*1.80f;
                p+=lateral;
                // Contact height is the authored asphalt, including both existing bridges.
                if(Mathf.Abs(p.x+24)<4 && p.z>=198 && p.z<=310)
                    p.y=CrossingHeight(p.z);
                else if(!rural && (p.z>=310 || Mathf.Abs(p.x+24)<4 && p.z>=198))p.y+=.123f;
                else p.y+=rural ? .123f : .043f;
                return p;
            }
        }
    }
    private static float CrossingHeight(float z)=>z<224
        ? Mathf.Lerp(.12f,2.30f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(198,224,z)))
        : z>232 ? Mathf.Lerp(2.30f,.12f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(232,264,z))) : 2.30f;
}
