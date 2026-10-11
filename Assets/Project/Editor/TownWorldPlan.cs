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
        layout.cityShopOrigin=new Vector3(-62,0,-108);layout.shopToCityScale=2;
        layout.cityBounds=new Rect(-288,-216,960,720);
        Rect reserved=land.initialBounds;
        foreach(var plot in land.plots)reserved=Union(reserved,plot.bounds);
        reserved=Union(reserved,starter.serviceYard);
        var min=layout.MapShopPoint(new Vector3(reserved.xMin,0,reserved.yMin));
        var max=layout.MapShopPoint(new Vector3(reserved.xMax,0,reserved.yMax));
        layout.reservedShopPlot=new Rect(min.x,min.z,max.x-min.x,max.z-min.z);
        layout.expansionPlots=land.plots.Select(plot=>
        {
            var a=layout.MapShopPoint(new Vector3(plot.bounds.xMin,0,plot.bounds.yMin));
            return new TownExpansionPlot{territory=plot.id,bounds=new Rect(a.x,a.z,plot.bounds.width*layout.shopToCityScale,plot.bounds.height*layout.shopToCityScale)};
        }).ToList();
        layout.districts=new List<TownDistrict>
        {
            District("Поставщики и выдача",new Rect(-282,-184,66,364),new Color(.64f,.64f,.56f)),
            District("Вокзал и грузовой двор",new Rect(-282,180,166,48),new Color(.64f,.64f,.56f)),
            District("Магазин и торговые улицы",new Rect(-212,-204,430,80),new Color(.74f,.71f,.59f)),
            District("Жилые кварталы",new Rect(-212,-72,90,120),new Color(.7f,.76f,.6f)),
            District("Северные жилые кварталы",new Rect(-108,54,270,128),new Color(.7f,.76f,.6f)),
            District("Офисы и жилые корпуса",new Rect(90,-138,174,326),new Color(.71f,.77f,.55f)),
            District("Лесополоса и река",new Rect(278,-204,162,678),new Color(.41f,.6f,.35f)),
            District("Удалённая ферма",new Rect(454,236,190,238),new Color(.68f,.74f,.43f))
        };
        // The large south-east block is deliberately uncut: all future store plots remain usable.
        var cells=new HashSet<Vector2Int>();
        foreach(int z in new[]{-16,-12,4,12,16})for(int x=-18;x<=22;x++)cells.Add(new Vector2Int(x,z));
        foreach(int x in new[]{-18,-10,14,22})for(int z=-16;z<=16;z++)cells.Add(new Vector2Int(x,z));
        // A western local street puts normal homes near the shop without cutting its expansion reserve.
        for(int z=-12;z<=12;z++)cells.Add(new Vector2Int(-18,z));
        foreach(int x in new[]{-2,6})
        {
            for(int z=-16;z<=-12;z++)cells.Add(new Vector2Int(x,z));
            for(int z=4;z<=16;z++)cells.Add(new Vector2Int(x,z));
        }
        // New local streets occupy the space released by the compact store representation.
        for(int z=-12;z<=4;z++)cells.Add(new Vector2Int(10,z));
        for(int x=-10;x<=14;x++)cells.Add(new Vector2Int(x,1));
        foreach(int z in new[]{-6,-2})for(int x=-18;x<=-10;x++)cells.Add(new Vector2Int(x,z));
        foreach(int x in new[]{-2,6})for(int z=1;z<=4;z++)cells.Add(new Vector2Int(x,z));
        layout.roads=cells.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(cell=>new TownRoadTile
        {
            cell=cell,connections=Enumerable.Range(0,4).Where(i=>cells.Contains(cell+Steps[i])).Sum(i=>1<<i)
        }).ToList();
        layout.countryRoad=new TownTransportPath{name="Дорога к ферме",width=8,points=SmoothCountryRoad()};
        layout.railway=new TownTransportPath{name="Северная железнодорожная линия",width=4,points=RailwayRoute()};
        layout.freightSiding=new TownTransportPath{name="Грузовая ветка",width=4,points=new[]{
            new Vector3(-260,2.2f,228),new Vector3(-267,2.2f,224),new Vector3(-270,2.2f,212),new Vector3(-270,2.2f,185)}};
        layout.stationDistance=170;
        Validate(layout);
    }
    private static Vector3[] RailwayRoute()
    {
        // Beyond the bridge the line bends through woodland, then behind the farm, not across its fields.
        var result=new List<Vector3>{new Vector3(-360,2.2f,228),new Vector3(-190,2.2f,228),new Vector3(265,2.2f,228),
            new Vector3(299,4.4f,228),new Vector3(345,4.4f,228),new Vector3(390,2.2f,275)};
        var bend=new[]{new Vector3(345,2.2f,228),new Vector3(390,2.2f,275),new Vector3(430,2.2f,430),new Vector3(475,2.2f,488),new Vector3(740,2.2f,488),new Vector3(850,2.2f,488)};
        for(int i=1;i<bend.Length-2;i++)for(int step=1;step<=24;step++)
        {
            float t=step/24f,t2=t*t,t3=t2*t;var a=bend[i-1];var b=bend[i];var c=bend[i+1];var d=bend[i+2];
            result.Add(.5f*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
        }
        return result.ToArray();
    }
    private static Vector3[] SmoothCountryRoad()
    {
        // A distinct eastbound T-junction replaces a second road laid over the northbound city street.
        var result=new List<Vector3>{new Vector3(264,0,-36),new Vector3(270,0,-36)};
        Bezier(new Vector3(270,0,-36),new Vector3(280,0,-36),new Vector3(289,0,-40),new Vector3(299,0,-40),24);
        Bezier(new Vector3(299,0,-40),new Vector3(303,.1f,-40),new Vector3(307,1.9f,-40),new Vector3(311,2,-40),8);
        result.Add(new Vector3(345,2,-40));
        Bezier(new Vector3(345,2,-40),new Vector3(350,1.9f,-40),new Vector3(355,.1f,-40),new Vector3(359,0,-40),8);
        Bezier(new Vector3(359,0,-40),new Vector3(390,0,-40),new Vector3(399,0,-8),new Vector3(415,0,30),30);
        Bezier(new Vector3(415,0,30),new Vector3(440,0,95),new Vector3(480,0,165),new Vector3(515,0,213),55);
        Bezier(new Vector3(515,0,213),new Vector3(547,0,254),new Vector3(550,0,262),new Vector3(550,0,285),35);
        return result.ToArray();
        void Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int samples)
        {
            for(int i=1;i<=samples;i++)
            {
                float t=(float)i/samples,u=1-t;
                result.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);
            }
        }
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
        if(layout.countryRoad.points.Length<2||layout.railway.points.Length<2)throw new InvalidOperationException("Transport corridors are missing.");
        if((layout.countryRoad.points[0]-new Vector3(264,0,-36)).sqrMagnitude>.001f)throw new InvalidOperationException("Country road is disconnected from its T-junction.");
        foreach(var point in layout.countryRoad.points)
            if(layout.reservedShopPlot.Overlaps(new Rect(point.x-5,point.z-5,10,10)))throw new InvalidOperationException("Country road crosses shop reserve.");
        // At grade the road and railway never cross: their river crossings have independent bridges.
        for(int i=1;i<layout.countryRoad.points.Length;i++)
            for(int j=1;j<layout.railway.points.Length;j++)
                if(SegmentsIntersect(layout.countryRoad.points[i-1],layout.countryRoad.points[i],layout.railway.points[j-1],layout.railway.points[j]))
                    throw new InvalidOperationException("Unprotected road/rail crossing.");
    }
    private static bool SegmentsIntersect(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        float Cross(Vector3 u,Vector3 v)=>u.x*v.z-u.z*v.x;
        float denominator=Cross(b-a,d-c);if(Mathf.Abs(denominator)<.0001f)return false;
        float t=Cross(c-a,d-c)/denominator,u=Cross(c-a,b-a)/denominator;
        return t>=0&&t<=1&&u>=0&&u<=1;
    }
    public static Vector3 NearestRoad(TownWorldLayout layout,Vector3 position)=>layout.roads
        .Select(r=>r.Position(layout.roadTileSize)).OrderBy(p=>(p-position).sqrMagnitude).First();
}
