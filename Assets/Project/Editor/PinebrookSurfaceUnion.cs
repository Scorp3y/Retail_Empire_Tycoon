using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;
using V=TownCityCorrectionsGeometry.Vertex;

/// <summary>Exact flat surface union: diagonal streets have straight edges, no one-metre stair steps or coplanar layers.</summary>
internal sealed class PinebrookSurfaceUnion
{
    private sealed class Face{internal V[] Points;internal Rect Bounds;}
    private readonly List<Face> occupied=new();
    internal void Polygon(Vector3[] polygon,TownMeshBuilder draft,float height,TownColor color)
    {
        for(int i=1;i<polygon.Length-1;i++)Triangle(polygon[0],polygon[i],polygon[i+1],draft,height,color);
    }
    internal void Rectangle(Rect rect,TownMeshBuilder draft,float height,TownColor color)
        =>Polygon(new[]{new Vector3(rect.xMin,0,rect.yMin),new Vector3(rect.xMin,0,rect.yMax),new Vector3(rect.xMax,0,rect.yMax),new Vector3(rect.xMax,0,rect.yMin)},draft,height,color);
    internal void Block(Rect rect)=>Polygon(new[]{new Vector3(rect.xMin,0,rect.yMin),new Vector3(rect.xMin,0,rect.yMax),new Vector3(rect.xMax,0,rect.yMax),new Vector3(rect.xMax,0,rect.yMin)},null,0,TownColor.Asphalt);
    internal void Segment(Vector3 a,Vector3 b,float width,TownMeshBuilder draft,float height,TownColor color)
    {
        a.y=b.y=0;var side=Vector3.Cross(Vector3.up,(b-a).normalized)*width/2;
        Polygon(new[]{a-side,a+side,b+side,b-side},draft,height,color);
        Disk(a,width/2,draft,height,color);Disk(b,width/2,draft,height,color);
    }
    private void Disk(Vector3 center,float radius,TownMeshBuilder draft,float height,TownColor color)
    {
        var points=new Vector3[16];for(int i=0;i<16;i++){float angle=-i*Mathf.PI*2/16;points[i]=center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);}
        Polygon(points,draft,height,color);
    }
    private void Triangle(Vector3 a,Vector3 b,Vector3 c,TownMeshBuilder draft,float height,TownColor color)
    {
        V Vertex(Vector3 p)=>new(p,Vector3.up,TownPalette.UV(color),new Vector2(p.x,p.z));
        var initial=new[]{Vertex(a),Vertex(b),Vertex(c)};var bounds=Bounds(initial);
        var pieces=new List<List<V>>{initial.ToList()};
        foreach(var face in occupied.Where(f=>f.Bounds.Overlaps(bounds)).ToArray())
        {
            var next=new List<List<V>>();foreach(var p in pieces)next.AddRange(TownGroundOverlapRepair.Subtract(p,face.Points,out _));pieces=next;if(pieces.Count==0)return;
        }
        foreach(var p in pieces)for(int i=1;i<p.Count-1;i++)
        {
            var triangle=new[]{p[0],p[i],p[i+1]};
            // Float clipping at town coordinates can leave long, microscopic slivers
            // along a shared diagonal. They are invisible but PhysX still hits them.
            // Reject by minimum altitude, not area: a long needle can have large area.
            var ab=triangle[1].P-triangle[0].P;var ac=triangle[2].P-triangle[0].P;
            float longestEdge=Mathf.Max(ab.magnitude,ac.magnitude,(triangle[2].P-triangle[1].P).magnitude);
            if(longestEdge<.001f || Vector3.Cross(ab,ac).magnitude/longestEdge<.001f)continue;
            occupied.Add(new Face{Points=triangle,Bounds=Bounds(triangle)});
            if(draft==null)continue;
            var x=triangle[0].P;var y=triangle[1].P;var z=triangle[2].P;x.y=y.y=z.y=height;
            if(Vector3.Cross(y-x,z-x).y<0){var swap=y;y=z;z=swap;}
            draft.Triangle(x,y,z,color);
        }
    }
    private static Rect Bounds(V[] p)=>Rect.MinMaxRect(p.Min(v=>v.P.x),p.Min(v=>v.P.z),p.Max(v=>v.P.x),p.Max(v=>v.P.z));
}
