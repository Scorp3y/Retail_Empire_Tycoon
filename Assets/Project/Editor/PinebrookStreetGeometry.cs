using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;

/// <summary>One junction model supplies rounded kerbs, separated crossings and sign setbacks.</summary>
internal sealed class PinebrookStreetGeometry
{
    internal sealed class Junction
    {
        internal Vector3 Center;internal Vector3[] Arms;
        internal float Mouth(Vector3 arm)
        {
            float distance=8;
            foreach(var other in Arms)
            {float angle=Vector3.Angle(arm,other);if(angle>5&&angle<175)distance=Mathf.Max(distance,8/Mathf.Tan(angle*Mathf.Deg2Rad/2));}
            return distance;
        }
        internal IEnumerable<Vector3> CrossingArms()
        {
            if(Arms.Length<3)return Array.Empty<Vector3>();
            if(Arms.Length==3)
            {
                // At a T, cross the side street; at a Y, cross the single southern stem.
                var minor=Arms.Where(a=>!Arms.Any(b=>Vector3.Dot(a,b)<-.98f)).ToArray();
                return new[]{minor.Length==1?minor[0]:Arms.OrderBy(a=>a.z).First()};
            }
            return Arms.Where(a=>Arms.Where(b=>Vector3.Angle(a,b)>1).All(b=>Vector3.Angle(a,b)>=65));
        }
    }
    internal readonly struct Crossing
    {
        internal readonly Vector3 Center,Direction;internal Crossing(Vector3 center,Vector3 direction){Center=center;Direction=direction;}
    }
    private readonly PinebrookDistrictLayout plan;
    internal readonly Junction[] Junctions;
    internal Crossing[] Crossings=>Junctions.SelectMany(j=>j.CrossingArms().Select(a=>new Crossing(j.Center+a*(j.Mouth(a)+2.5f),a))).ToArray();
    internal PinebrookStreetGeometry(PinebrookDistrictLayout plan)
    {
        this.plan=plan;var sections=PinebrookMobility.Split().Concat(new[]{new PinebrookMobility.Segment(-24,300,-24,310)}).ToArray();
        Junctions=sections.SelectMany(s=>new[]{s.A,s.B}).Distinct().Select(p=>new Junction{Center=p,Arms=sections
            .Where(s=>(s.A-p).sqrMagnitude<.01f||(s.B-p).sqrMagnitude<.01f)
            .Select(s=>(((s.A-p).sqrMagnitude<.01f?s.B:s.A)-p).normalized).OrderBy(a=>Mathf.Atan2(a.z,a.x)).ToArray()}).Where(j=>j.Arms.Length>1).ToArray();
        if(Junctions.Any(j=>j.Arms.Length>4))throw new InvalidOperationException("Residential intersections may have at most four arms.");
    }
    internal void Roads(PinebrookSurfaceUnion union,TownMeshBuilder draft)
    {
        union.Block(new Rect(-28,198,8,98));
        // A straight full-width connection replaces the former rounded dead end at the gateway.
        union.Rectangle(new Rect(-28,296,8,14),draft,.12f,TownColor.Asphalt);
        foreach(var road in plan.streets)union.Segment(road.Start,road.End,road.width,draft,.12f,TownColor.Asphalt);
        Corners(union,draft,4,4,.12f,TownColor.Asphalt);
    }
    internal void Sidewalks(PinebrookSurfaceUnion union,TownMeshBuilder draft)
    {
        union.Rectangle(new Rect(-31,296,14,14),draft,.16f,TownColor.Pavement);
        foreach(var road in plan.streets)union.Segment(road.Start,road.End,road.width+6,draft,.16f,TownColor.Pavement);
        Corners(union,draft,7,1,.16f,TownColor.Pavement);
    }
    private void Corners(PinebrookSurfaceUnion union,TownMeshBuilder draft,float halfWidth,float radius,float height,TownColor color)
    {
        foreach(var j in Junctions)for(int i=0;i<j.Arms.Length;i++)
        {
            var a=j.Arms[i];var b=j.Arms[(i+1)%j.Arms.Length];float angle=Mathf.Repeat(Mathf.Atan2(b.z,b.x)-Mathf.Atan2(a.z,a.x),Mathf.PI*2);
            if(angle<.12f||angle>=Mathf.PI-.01f)continue;
            var bisector=(a+b).normalized;var center=j.Center+bisector*((halfWidth+radius)/Mathf.Sin(angle/2));
            float start=Mathf.Atan2(a.z,a.x)-Mathf.PI/2,sweep=-(Mathf.PI-angle);
            var points=new List<Vector3>{j.Center};
            for(int sample=0;sample<=8;sample++){float t=start+sweep*sample/8;points.Add(center+new Vector3(Mathf.Cos(t),0,Mathf.Sin(t))*radius);}
            union.Polygon(points.ToArray(),draft,height,color);
        }
    }
    internal TownMeshBuilder Paint()
    {
        var paint=new TownMeshBuilder();
        foreach(var street in plan.streets)
        {
            var direction=(street.End-street.Start).normalized;float length=Vector3.Distance(street.Start,street.End);
            for(float d=9;d<length-8;d+=6)
            {var p=street.Start+direction*d;if(Junctions.Any(j=>Vector3.Distance(p,j.Center)<j.Arms.Max(a=>j.Mouth(a))+4))continue;Stroke(paint,p,direction,3,.10f);}
        }
        foreach(var crossing in Crossings)
            for(float offset=-3.2f;offset<=3.21f;offset+=.8f)Stroke(paint,crossing.Center+Vector3.Cross(Vector3.up,crossing.Direction)*offset,crossing.Direction,2.4f,.46f);
        return paint;
    }
    internal static void Stroke(TownMeshBuilder paint,Vector3 p,Vector3 forward,float length,float width)
    {p.y=.13f;var a=forward*length/2;var b=Vector3.Cross(Vector3.up,forward)*width/2;paint.Quad(p-a-b,p+a-b,p+a+b,p-a+b,TownColor.White);}
}
