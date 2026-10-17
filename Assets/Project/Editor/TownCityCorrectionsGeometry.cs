using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Scoped copy-on-write editing. Original native meshes stay read-only; unchanged vertices
/// retain their exact local values, normals and UVs. Clipped fragments interpolate all channels.</summary>
internal sealed class TownCityCorrectionsGeometry
{
    internal readonly struct Vertex
    {
        internal readonly Vector3 P,N;internal readonly Vector2 U,V;
        internal Vertex(Vector3 p,Vector3 n,Vector2 u,Vector2 v){P=p;N=n;U=u;V=v;}
        internal static Vertex Lerp(Vertex a,Vertex b,float t)=>new(Vector3.Lerp(a.P,b.P,t),Vector3.Lerp(a.N,b.N,t).normalized,Vector2.Lerp(a.U,b.U,t),Vector2.Lerp(a.V,b.V,t));
    }
    internal sealed class View
    {
        internal readonly MeshFilter Filter;internal readonly Mesh Source;internal readonly Vertex[] Vertices;internal readonly Vector3[] World;internal readonly int[] Indices;internal readonly Bounds Bounds;
        internal readonly bool HasPalette;internal readonly int FixedPalette;
        internal readonly HashSet<int> Removed=new();internal readonly List<Vertex> Added=new();
        internal View(MeshFilter f)
        {
            Filter=f;Source=f.sharedMesh;var p=Source.vertices;var n=Source.normals;var u=Source.uv;var v=Source.uv2;
            Vertices=p.Select((x,i)=>new Vertex(x,n[i],u[i],v.Length==p.Length?v[i]:new Vector2(x.x,x.z))).ToArray();World=p.Select(f.transform.TransformPoint).ToArray();Indices=Source.triangles;
            Bounds=new Bounds(f.transform.position,Vector3.zero);if(World.Length>0){Bounds=new Bounds(World[0],Vector3.zero);foreach(var point in World)Bounds.Encapsulate(point);}
            var renderer=f.GetComponent<MeshRenderer>();var material=renderer!=null?renderer.sharedMaterial:null;
            HasPalette=material!=null&&material.HasProperty("_BaseMap")&&material.GetTexture("_BaseMap")!=null&&material.GetTexture("_BaseMap").width==32;
            FixedPalette=material!=null&&material.HasProperty("_PaletteIndex")&&material.GetFloat("_PaletteIndex")>=0?Mathf.RoundToInt(material.GetFloat("_PaletteIndex")):-1;
        }
        internal void AddWorld(Vertex a,Vertex b,Vertex c)
        {
            if(Vector3.Cross(b.P-a.P,c.P-a.P).sqrMagnitude<1e-10f)return;
            Vertex Local(Vertex v)=>new(Filter.transform.InverseTransformPoint(v.P),Filter.transform.InverseTransformDirection(v.N),v.U,v.V);
            Added.Add(Local(a));Added.Add(Local(b));Added.Add(Local(c));
        }
        internal void AppendWorld(Mesh mesh,Matrix4x4 pose)
        {
            var p=mesh.vertices;var n=mesh.normals;var u=mesh.uv;var t=mesh.triangles;
            Vertex Read(int i){var w=pose.MultiplyPoint3x4(p[i]);return new(w,pose.MultiplyVector(n[i]).normalized,u[i],new Vector2(w.x,w.z));}
            for(int i=0;i<t.Length;i+=3)AddWorld(Read(t[i]),Read(t[i+1]),Read(t[i+2]));
        }
        internal void Save(string id,string assetRoot=TownCityCorrectionsPlacement.Root)
        {
            if(Removed.Count==0&&Added.Count==0)return;
            if(Source.subMeshCount!=1)throw new InvalidOperationException("Scoped repair requires one material per source mesh: "+Source.name);
            var kept=new List<int>();for(int i=0;i<Indices.Length;i+=3)if(!Removed.Contains(i)){kept.Add(Indices[i]);kept.Add(Indices[i+1]);kept.Add(Indices[i+2]);}
            var used=kept.Distinct().OrderBy(i=>i).ToArray();var map=new int[Vertices.Length];for(int i=0;i<used.Length;i++)map[used[i]]=i;
            var result=used.Select(i=>Vertices[i]).Concat(Added).ToArray();int offset=used.Length;
            var mesh=new Mesh{name=id,indexFormat=IndexFormat.UInt32};mesh.vertices=result.Select(v=>v.P).ToArray();mesh.normals=result.Select(v=>v.N).ToArray();mesh.uv=result.Select(v=>v.U).ToArray();mesh.uv2=result.Select(v=>v.V).ToArray();
            if(Source.colors32.Length==Vertices.Length){var colors=Source.colors32;mesh.colors32=used.Select(i=>colors[i]).Concat(Enumerable.Repeat(new Color32(255,255,255,255),Added.Count)).ToArray();}
            mesh.triangles=kept.Select(i=>map[i]).Concat(Enumerable.Range(offset,Added.Count)).ToArray();mesh.RecalculateBounds();
            string path=assetRoot+"/Meshes/"+id+".asset";TownBakeryPlacement.SaveAsset(mesh,path);Filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
    }
    internal readonly View[] Views;
    internal TownCityCorrectionsGeometry(Transform root)=>Views=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null).Select(f=>new View(f)).ToArray();
    internal void Save(string assetRoot=TownCityCorrectionsPlacement.Root){int n=0;foreach(var v in Views)v.Save("ScopedMesh"+n++,assetRoot);}
    internal int Remove(Func<View,int,bool> predicate)
    {
        int count=0;foreach(var v in Views)for(int i=0;i<v.Indices.Length;i+=3)if(!v.Removed.Contains(i)&&predicate(v,i)){v.Removed.Add(i);count++;}return count;
    }
    internal int MatchAndRemove(Mesh template,Matrix4x4 pose,Transform scope)
    {
        var world=template.vertices.Select(pose.MultiplyPoint3x4).ToArray();var uv=template.uv;var idx=template.triangles;
        var views=Views.Where(v=>v.Filter.transform.IsChildOf(scope)).ToArray();
        var index=new Dictionary<Vector3Int,List<(View V,int T)>>();
        Vector3Int Key(Vector3 p)=>new(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000));
        foreach(var v in views)for(int t=0;t<v.Indices.Length;t+=3)
        {
            if(v.Removed.Contains(t))continue;
            for(int corner=0;corner<3;corner++){var k=Key(v.World[v.Indices[t+corner]]);if(!index.TryGetValue(k,out var list))index.Add(k,list=new());list.Add((v,t));}
        }
        var found=new List<(View V,int T)>();var claimed=new HashSet<(View V,int T)>();
        for(int i=0;i<idx.Length;i+=3)
        {
            bool match=false;var key=Key(world[idx[i]]);
            for(int dz=-1;dz<=1&&!match;dz++)for(int dy=-1;dy<=1&&!match;dy++)for(int dx=-1;dx<=1&&!match;dx++)
            {
                if(!index.TryGetValue(key+new Vector3Int(dx,dy,dz),out var candidates))continue;
                foreach(var p in candidates)
                {
                    if(claimed.Contains(p))continue;
                    for(int turn=0;turn<3&&!match;turn++)
                    {
                        bool same=true;for(int c=0;c<3;c++){int a=idx[i+c],b=p.V.Indices[p.T+(c+turn)%3];if((world[a]-p.V.World[b]).sqrMagnitude>2e-8f||(uv[a]-p.V.Vertices[b].U).sqrMagnitude>1e-10f){same=false;break;}}
                        if(same){found.Add(p);claimed.Add(p);match=true;}
                    }
                    if(match)break;
                }
            }
            if(!match)throw new InvalidOperationException("Cannot identify complete native model triangle "+i/3+" in "+scope.name+" / "+template.name);
        }
        foreach(var p in found)p.V.Removed.Add(p.T);return found.Count;
    }
    internal int ClipGround(Rect cut,float maximum=.25f,Func<View,int,bool> allowed=null)
    {
        int count=0;
        foreach(var v in Views)
        {
          if(v.Bounds.max.x<=cut.xMin||v.Bounds.min.x>=cut.xMax||v.Bounds.max.z<=cut.yMin||v.Bounds.min.z>=cut.yMax||v.Bounds.min.y>maximum)continue;
          for(int i=0;i<v.Indices.Length;i+=3)
          {
            if(v.Removed.Contains(i)||allowed!=null&&!allowed(v,i))continue;
            var points=new[]{v.World[v.Indices[i]],v.World[v.Indices[i+1]],v.World[v.Indices[i+2]]};
            if(points.Max(p=>p.y)>maximum||points.Max(p=>p.y)<.005f||points.Min(p=>p.y)<-.01f||!Overlaps(cut,points))continue;
            var polygon=points.Select((p,j)=>{var a=v.Vertices[v.Indices[i+j]];return new Vertex(p,v.Filter.transform.TransformDirection(a.N),a.U,new Vector2(p.x,p.z));}).ToList();
            var fragments=new List<List<Vertex>>();var inside=polygon;
            for(int edge=0;edge<4;edge++)
            {
                float Distance(Vertex p)=>edge==0?p.P.x-cut.xMin:edge==1?cut.xMax-p.P.x:edge==2?p.P.z-cut.yMin:cut.yMax-p.P.z;
                var exterior=Clip(inside,p=>-Distance(p));if(exterior.Count>=3)fragments.Add(exterior);inside=Clip(inside,Distance);if(inside.Count==0)break;
            }
            if(inside.Count<3)continue;
            v.Removed.Add(i);count++;
            foreach(var fragment in fragments)for(int k=1;k<fragment.Count-1;k++)v.AddWorld(fragment[0],fragment[k],fragment[k+1]);
          }
          // A later driveway can cross fragments left by an earlier cut in the same large slab.
          // Clip those too; otherwise the second gate retains an invisible raised collision strip.
          if(v.Added.Count>0&&(allowed==null||v.Indices.Length>0&&allowed(v,0)))
          {
            var previous=v.Added.ToArray();v.Added.Clear();
            for(int t=0;t<previous.Length;t+=3)
            {
                var poly=previous.Skip(t).Take(3).Select(a=>new Vertex(v.Filter.transform.TransformPoint(a.P),v.Filter.transform.TransformDirection(a.N),a.U,a.V)).ToList();var points=poly.Select(a=>a.P).ToArray();
                if(points.Max(p=>p.y)>maximum||points.Max(p=>p.y)<.005f||points.Min(p=>p.y)<-.01f||!Overlaps(cut,points)){v.Added.AddRange(previous.Skip(t).Take(3));continue;}
                var outside=new List<List<Vertex>>();var inside=poly;
                for(int edge=0;edge<4;edge++)
                {
                    float D(Vertex p)=>edge==0?p.P.x-cut.xMin:edge==1?cut.xMax-p.P.x:edge==2?p.P.z-cut.yMin:cut.yMax-p.P.z;
                    var part=Clip(inside,p=>-D(p));if(part.Count>=3)outside.Add(part);inside=Clip(inside,D);if(inside.Count==0)break;
                }
                if(inside.Count<3){v.Added.AddRange(previous.Skip(t).Take(3));continue;}
                foreach(var part in outside)for(int i=1;i<part.Count-1;i++)v.AddWorld(part[0],part[i],part[i+1]);
            }
          }
        }
        return count;
    }
    private static List<Vertex> Clip(List<Vertex> source,Func<Vertex,float> distance)
    {
        var result=new List<Vertex>();if(source.Count==0)return result;var a=source[source.Count-1];float da=distance(a);
        foreach(var b in source){float db=distance(b);if((da>=0)!=(db>=0))result.Add(Vertex.Lerp(a,b,da/(da-db)));if(db>=0)result.Add(b);a=b;da=db;}return result;
    }
    private static bool Overlaps(Rect r,Vector3[] p)=>p.Max(v=>v.x)>r.xMin+.0001f&&p.Min(v=>v.x)<r.xMax-.0001f&&p.Max(v=>v.z)>r.yMin+.0001f&&p.Min(v=>v.z)<r.yMax-.0001f;
    internal static bool Color(View v,int triangle,TownColor color)
    {
        if(v.FixedPalette>=0)return v.FixedPalette==(int)color;
        if(!v.HasPalette)return false;
        for(int k=0;k<3;k++)if(Mathf.FloorToInt(v.Vertices[v.Indices[triangle+k]].U.x*32)!=(int)color)return false;
        return true;
    }
    internal static Vector3[] Points(View v,int i)=>new[]{v.World[v.Indices[i]],v.World[v.Indices[i+1]],v.World[v.Indices[i+2]]};
}
