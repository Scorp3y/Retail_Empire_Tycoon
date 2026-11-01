using System;
using System.Linq;
using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;

/// <summary>Builds a connected apron, a six-metre manoeuvring aisle and full-sized marked visitor bays.</summary>
internal static class PinebrookParkingGeometry
{
    internal static void Surfaces(PinebrookDistrictLayout.Parking[] lots,PinebrookSurfaceUnion union,TownMeshBuilder roads)
    {
        foreach(var lot in lots)
        {
            if(lot.stalls<4||lot.stalls>10||lot.occupied.Distinct().Count()!=lot.occupied.Length||lot.occupied.Any(i=>i<0||i>=lot.stalls))throw new InvalidOperationException("Invalid parking layout: "+lot.name);
            float half=lot.Width/2,r=.55f;
            var corners=new[]{new Vector2(-half+r,0),new Vector2(half-r,0),new Vector2(half,r),new Vector2(half,12.2f-r),new Vector2(half-r,12.2f),new Vector2(-half+r,12.2f),new Vector2(-half,12.2f-r),new Vector2(-half,r)};
            union.Polygon(corners.Select(p=>lot.Local(p.x,p.y)).ToArray(),roads,.12f,TownColor.Asphalt);
            union.Segment(new Vector3(lot.roadX,0,lot.roadZ),lot.Local(0,3.1f),5.5f,roads,.12f,TownColor.Asphalt);
        }
    }
    internal static TownMeshBuilder Paint(PinebrookDistrictLayout.Parking[] lots)
    {
        var paint=new TownMeshBuilder();
        foreach(var lot in lots)
        {
            var direction=Quaternion.Euler(0,lot.yaw,0)*Vector3.forward;
            for(int i=0;i<=lot.stalls;i++)PinebrookStreetGeometry.Stroke(paint,lot.Local((i-lot.stalls/2f)*2.8f,9.1f),direction,5.6f,.09f);
            PinebrookStreetGeometry.Stroke(paint,lot.Local(0,11.9f),Quaternion.Euler(0,lot.yaw,0)*Vector3.right,lot.stalls*2.8f,.09f);
        }
        return paint;
    }
}
