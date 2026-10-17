using System;
using System.Linq;
using UnityEngine;

/// <summary>Metre-based layout beyond the existing northern crossing; never relocates that crossing.</summary>
internal static class TownForestPlan
{
    internal const string Site = "Forest Quarter";
    internal const float Floor = .12f;
    internal static readonly Rect Permission = new(-153, 280, 326, 214);
    internal static readonly Rect[] Roads = {
        new(-28,310,8,120), new(-124,306,200,8), new(-124,426,200,8),
        new(-124,310,8,120), new(68,310,8,120)
    };
    internal static readonly Rect[] Clearings = {
        new(-132,298,216,24), new(-132,418,216,24), new(-132,310,24,120),
        new(60,310,24,120), new(-36,310,24,120),
        new(-112,322,76,94), new(-12,322,76,94),
        new(-106,440,42,26), new(0,440,42,26), new(86,326,58,88)
    };
    internal static readonly Vector3[] Trail = {
        new(81,0,420), new(100,0,438), new(130,0,456), new(153,0,435),
        new(156,0,397), new(151,0,354), new(151,0,320), new(131,0,301), new(82,0,301)
    };
    internal static readonly Home[] Homes = {
        new("CourtyardHouse",-76,400,180), new("CourtyardHouse",-104,366,90),
        new("PassageHouse",-48,366,270), new("CourtyardHouse",28,400,180),
        new("PassageHouse",0,366,90), new("CourtyardHouse",56,366,270),
        new("CafeCorner",-76,332,180), new("MarketCorner",28,332,180),
        new("TownhouseA",-96,452,180), new("TownhouseB",-87.6f,452,180),
        new("TownhouseB",10,452,180), new("TownhouseA",18.4f,452,180),
        new("CommunityPavilion",120,402,180)
    };
    internal readonly struct Home
    {
        internal readonly string Id;
        internal readonly Vector3 Position;
        internal readonly float Yaw;
        internal Home(string id,float x,float z,float yaw){Id=id;Position=new Vector3(x,Floor,z);Yaw=yaw;}
    }
    internal static bool OnRoad(Vector3 p,float margin=0)=>Roads.Any(r=>Contains(r,p,margin));
    internal static bool NeedsClearing(Bounds bounds)
        => Clearings.Any(r=>r.Overlaps(new Rect(bounds.min.x,bounds.min.z,bounds.size.x,bounds.size.z)))
        || Trail.Skip(1).Select((b,i)=>SegmentDistance(bounds.center,Trail[i],b)).Any(d=>d<4+Mathf.Max(bounds.extents.x,bounds.extents.z));
    internal static bool Contains(Rect r,Vector3 p,float margin=0)
        =>p.x>=r.xMin-margin&&p.x<=r.xMax+margin&&p.z>=r.yMin-margin&&p.z<=r.yMax+margin;
    internal static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
    {
        var delta=new Vector2(b.x-a.x,b.z-a.z);
        float t=Mathf.Clamp01(Vector2.Dot(new Vector2(p.x-a.x,p.z-a.z),delta)/delta.sqrMagnitude);
        return Vector2.Distance(new Vector2(p.x,p.z),new Vector2(a.x,a.z)+delta*t);
    }
}
