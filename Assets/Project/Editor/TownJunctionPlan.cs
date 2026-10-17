using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEngine;

/// <summary>Explicit road hierarchy and sparse signal sites, derived from the existing authored street graph.</summary>
internal static class TownJunctionPlan
{
    internal const string Site="City junction equipment";
    internal static readonly Vector2Int[] SignalCells={new(-10,-12),new(14,-12),new(-10,4),new(14,4),new(6,12),new(-10,16)};

    internal sealed class Junction
    {
        internal Vector2Int Cell { get; }
        internal Vector3 Center { get; }
        internal int Mask { get; }
        internal int PublicMask { get; }
        internal bool HasSignals=>SignalCells.Contains(Cell);
        internal bool MainNorthSouth { get; }
        internal IEnumerable<int> Approaches=>Enumerable.Range(0,4).Where(d=>(Mask&(1<<d))!=0);
        internal Rect Area=>new(Center.x-14.5f,Center.z-14.5f,29,29);

        internal Junction(TownRoadTile road,int mask,float tileSize)
        {
            Cell=road.cell;Center=road.Position(tileSize);Mask=mask;PublicMask=road.connections;
            bool ns=(mask&5)==5,ew=(mask&10)==10;
            if(!ns&&!ew)throw new InvalidOperationException("A junction needs a straight through road: "+Cell);
            MainNorthSouth=ns&&(!ew||VerticalRank(Cell.x)>HorizontalRank(Cell.y));
        }

        internal bool IsMain(int direction)=>(direction%2==0)==MainNorthSouth;
        internal bool IsPrivate(int direction)=>(PublicMask&(1<<direction))==0&&Cell!=new Vector2Int(-2,16)&&Cell!=new Vector2Int(22,-3);
    }

    internal static Junction[] Create(TownWorldLayout layout)
    {
        var result=layout.roads.Select(r=>new {road=r,mask=VisibleMask(r)})
            .Where(p=>Count(p.mask)>=3).Select(p=>new Junction(p.road,p.mask,layout.roadTileSize)).ToArray();
        foreach(var cell in SignalCells)
            if(result.Count(j=>j.Cell==cell)!=1)throw new InvalidOperationException("Signal site missing from native network: "+cell);
        return result;
    }

    internal static Rect[] Areas(TownWorldLayout layout)=>Create(layout).Select(j=>j.Area).ToArray();
    internal static Vector3 Outward(int approach)=>new(TownWorldPlan.Steps[approach].x,0,TownWorldPlan.Steps[approach].y);
    internal static Vector3 ArrivalRight(int approach)=>Vector3.Cross(Vector3.up,-Outward(approach));
    internal static Vector3 At(Junction j,int approach,float distance,float right,float y=0)=>j.Center+Outward(approach)*distance+ArrivalRight(approach)*right+Vector3.up*y;
    private static int Count(int mask)=>Enumerable.Range(0,4).Count(d=>(mask&(1<<d))!=0);

    private static int VisibleMask(TownRoadTile road)
    {
        // These are existing curb openings from TownWorldAssembler, not new public roads.
        var c=road.cell;int mask=road.connections;
        if(c==new Vector2Int(-2,-12))mask=15;
        if(c.y==-12&&(c.x==17||c.x==19))mask=11;
        if(c==new Vector2Int(22,-3))mask=7;
        if(c.x==-18&&c.y>=-12&&c.y<=13&&(c.y+12)%5==0)mask|=8;
        if(c==new Vector2Int(-4,-16))mask|=1;
        foreach(var opening in new[]{new Vector2Int(-10,8),new(-10,-9),new(-2,8),new(22,8),new(6,8),new(14,8),new(22,-2)})
            if(c==opening)mask|=8;
        // The already-built district gateway added this fourth arm after the original road tiles were baked.
        if(c==new Vector2Int(-2,16))mask|=1;
        return mask;
    }

    private static int HorizontalRank(int row)=>row==-12?100:row==4?90:row==16?80:row==-16?50:row==12?40:row==1?30:20;
    private static int VerticalRank(int column)=>column==14?95:column==-10?85:column==22?75:column==-18?65:column==10?45:column==6?35:25;
}
