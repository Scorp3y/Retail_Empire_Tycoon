using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Territory;
using RetailEmpireTycoon.BuildSystem;
using UnityEngine;

internal static class TownWorldPlan
{
    internal static readonly Vector2Int[] Steps={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    public static void Configure(TownWorldLayout layout,TerritoryPlotLayout land,StarterStoreBlueprint starter)
    {
        layout.sourceShopOrigin=new Vector3(land.initialBounds.xMin,0,land.initialBounds.yMin);
        // Keep two metres of clearance even with the existing 0.1667 m grid's rounding.
        layout.cityShopOrigin=new Vector3(-62,0,-108);layout.shopToCityScale=3;
        layout.cityBounds=new Rect(-288,-216,576,456);
        Rect reserved=land.initialBounds;
        foreach(var plot in land.plots)reserved=Union(reserved,plot.bounds);
        reserved=Union(reserved,starter.serviceYard);
        var min=layout.MapShopPoint(new Vector3(reserved.xMin,0,reserved.yMin));
        var max=layout.MapShopPoint(new Vector3(reserved.xMax,0,reserved.yMax));
        layout.reservedShopPlot=new Rect(min.x,min.z,max.x-min.x,max.z-min.z);
        layout.expansionPlots=land.plots.Select(plot=>
        {
            var a=layout.MapShopPoint(new Vector3(plot.bounds.xMin,0,plot.bounds.yMin));
            return new TownExpansionPlot{territory=plot.id,bounds=new Rect(a.x,a.z,plot.bounds.width*3,plot.bounds.height*3)};
        }).ToList();
        layout.districts=new List<TownDistrict>
        {
            District("Промышленный район",new Rect(-210,-136,84,344),new Color(.64f,.64f,.56f)),
            District("Торговый центр",new Rect(-114,54,276,84),new Color(.74f,.71f,.59f)),
            District("Жилой район",new Rect(-210,-186,468,36),new Color(.7f,.76f,.6f)),
            District("Фермерский квартал",new Rect(174,-136,84,344),new Color(.68f,.74f,.43f)),
            District("Парк и зелёная зона",new Rect(-114,150,276,60),new Color(.41f,.6f,.35f))
        };
        // The large south-east block is deliberately uncut: all future store plots remain usable.
        var cells=new HashSet<Vector2Int>();
        foreach(int z in new[]{-16,-12,4,12,18})for(int x=-18;x<=22;x++)cells.Add(new Vector2Int(x,z));
        foreach(int x in new[]{-18,-10,14,22})for(int z=-16;z<=18;z++)cells.Add(new Vector2Int(x,z));
        foreach(int x in new[]{-2,6})
        {
            for(int z=-16;z<=-12;z++)cells.Add(new Vector2Int(x,z));
            for(int z=4;z<=18;z++)cells.Add(new Vector2Int(x,z));
        }
        layout.roads=cells.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(cell=>new TownRoadTile
        {
            cell=cell,connections=Enumerable.Range(0,4).Where(i=>cells.Contains(cell+Steps[i])).Sum(i=>1<<i)
        }).ToList();
        Validate(layout);
    }
    private static TownDistrict District(string name,Rect bounds,Color color)=>new TownDistrict{name=name,bounds=bounds,color=color};
    private static Rect Union(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));

    public static void Validate(TownWorldLayout layout)
    {
        var cells=layout.roads.ToDictionary(r=>r.cell);var visited=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();
        queue.Enqueue(layout.roads[0].cell);
        while(queue.Count>0)
        {
            var cell=queue.Dequeue();if(!visited.Add(cell))continue;var tile=cells[cell];
            for(int i=0;i<4;i++)if((tile.connections&(1<<i))!=0)
            {
                var next=cell+Steps[i];
                if(!cells.TryGetValue(next,out var neighbour)||(neighbour.connections&(1<<((i+2)%4)))==0)
                    throw new InvalidOperationException("Road seam is not reciprocal: "+cell);
                queue.Enqueue(next);
            }
        }
        if(visited.Count!=cells.Count)throw new InvalidOperationException("Town contains isolated roads.");
        foreach(var road in layout.roads)
        {
            var point=road.Position(layout.roadTileSize);
            if(layout.reservedShopPlot.Overlaps(new Rect(point.x-6,point.z-6,12,12)))
                throw new InvalidOperationException("A public road crosses the store expansion plot: "+road.cell);
        }
    }
    public static Vector3 NearestRoad(TownWorldLayout layout,Vector3 position)=>layout.roads
        .Select(r=>r.Position(layout.roadTileSize)).OrderBy(p=>(p-position).sqrMagnitude).First();
}
