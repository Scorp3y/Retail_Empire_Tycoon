using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;
using V=TownCityCorrectionsGeometry.Vertex;

/// <summary>Subtracts coincident low ground faces instead of hiding z-fighting with a height offset.
/// Walkway surfaces win over asphalt; equal surfaces keep one copy. Markings at a distinct height,
/// building walls, terrain and all original resources stay untouched.</summary>
internal static class TownGroundOverlapRepair
{
    private sealed class Face
    {
        internal TownCityCorrectionsGeometry.View View;
        internal int Offset,Rank,Level;
        internal V[] Vertices;
        internal Rect Bounds;
        internal float Area;
    }
    internal static int Repair(TownCityCorrectionsGeometry geometry)
    {
        var faces=new List<Face>();
        foreach(var v in geometry.Views.Where(v=>v.HasPalette))for(int i=0;i<v.Indices.Length;i+=3)
        {
            if(v.Removed.Contains(i))continue;int rank=Rank(v,i);if(rank==0)continue;
            var points=TownCityCorrectionsGeometry.Points(v,i);
            if(points.Min(p=>p.y)<.005f||points.Max(p=>p.y)>.2f||points.Max(p=>p.y)-points.Min(p=>p.y)>.00001f
                ||Vector3.Cross(points[1]-points[0],points[2]-points[0]).normalized.y<.99f)continue;
            var vertices=points.Select((p,k)=>{var a=v.Vertices[v.Indices[i+k]];return new V(p,v.Filter.transform.TransformDirection(a.N),a.U,a.V);}).ToArray();
            faces.Add(new Face{View=v,Offset=i,Rank=rank,Level=Mathf.RoundToInt(points[0].y*10000),Vertices=vertices,Bounds=Bounds(points),Area=Area(vertices)});
        }
        var buckets=new Dictionary<(int X,int Z,int Y),List<Face>>();int repaired=0;
        foreach(var face in faces.OrderByDescending(f=>f.Rank).ThenBy(f=>f.Area))
        {
            var previous=new HashSet<Face>();foreach(var key in Keys(face))if(buckets.TryGetValue(key,out var group))previous.UnionWith(group);
            var pieces=new List<List<V>>{face.Vertices.ToList()};bool changed=false;
            foreach(var other in previous.Where(f=>f.Bounds.Overlaps(face.Bounds)))
            {
                var next=new List<List<V>>();foreach(var piece in pieces)
                {
                    var outside=Subtract(piece,other.Vertices,out bool cut);changed|=cut;next.AddRange(outside);
                }
                pieces=next;if(pieces.Count==0)break;
            }
            if(changed)
            {
                face.View.Removed.Add(face.Offset);repaired++;
                foreach(var p in pieces)for(int i=1;i<p.Count-1;i++)face.View.AddWorld(p[0],p[i],p[i+1]);
            }
            foreach(var key in Keys(face)){if(!buckets.TryGetValue(key,out var group))buckets.Add(key,group=new());group.Add(face);}
        }
        return repaired;
    }
    private static int Rank(TownCityCorrectionsGeometry.View v,int i)
    {
        if(TownCityCorrectionsGeometry.Color(v,i,TownColor.Pavement))return 30;
        if(TownCityCorrectionsGeometry.Color(v,i,TownColor.Sand))return 20;
        if(TownCityCorrectionsGeometry.Color(v,i,TownColor.Asphalt))return 10;
        return 0;
    }
    private static IEnumerable<(int,int,int)> Keys(Face f)
    {
        for(int z=Mathf.FloorToInt(f.Bounds.yMin/12);z<=Mathf.FloorToInt(f.Bounds.yMax/12);z++)
        for(int x=Mathf.FloorToInt(f.Bounds.xMin/12);x<=Mathf.FloorToInt(f.Bounds.xMax/12);x++)yield return(x,z,f.Level);
    }
    internal static List<List<V>> Subtract(List<V> subject,V[] triangle,out bool changed)
    {
        float sign=Mathf.Sign(Cross(triangle[1].P-triangle[0].P,triangle[2].P-triangle[0].P));
        var inside=subject;var outside=new List<List<V>>();
        for(int e=0;e<3;e++)
        {
            var a=triangle[e].P;var delta=triangle[(e+1)%3].P-a;
            float D(V v)=>sign*Cross(delta,v.P-a);
            var piece=Clip(inside,v=>-D(v));if(Area(piece)>.00001f)outside.Add(piece);
            inside=Clip(inside,D);if(inside.Count<3)break;
        }
        changed=Area(inside)>.00001f;
        return changed?outside:new List<List<V>>{subject};
    }
    private static List<V> Clip(List<V> polygon,Func<V,float> distance)
    {
        var output=new List<V>();if(polygon.Count==0)return output;var a=polygon[polygon.Count-1];float da=distance(a);
        foreach(var b in polygon)
        {
            float db=distance(b);
            if((da>=0)!=(db>=0))output.Add(V.Lerp(a,b,Mathf.Clamp01(da/(da-db))));
            if(db>=0)output.Add(b);a=b;da=db;
        }
        return output;
    }
    private static float Cross(Vector3 a,Vector3 b)=>a.x*b.z-a.z*b.x;
    private static float Area(IReadOnlyList<V> p)
    {
        if(p.Count<3)return 0;float sum=0;for(int i=1;i<p.Count-1;i++)sum+=Mathf.Abs(Cross(p[i].P-p[0].P,p[i+1].P-p[0].P))*.5f;return sum;
    }
    private static Rect Bounds(Vector3[] p)=>Rect.MinMaxRect(p.Min(a=>a.x),p.Min(a=>a.z),p.Max(a=>a.x),p.Max(a=>a.z));
}
