using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>Reads actual saved triangles. Template recognition never relies on a chunk name alone.</summary>
internal sealed class TownSeamGeometry
{
    internal sealed class MeshView
    {
        public readonly MeshFilter Filter;
        public readonly Vector3[] Local,World;
        public readonly Vector2[] Uv;
        public readonly int[] Indices;
        public readonly List<Triangle> Triangles=new List<Triangle>();
        public MeshView(MeshFilter filter)
        {
            Filter=filter;Local=filter.sharedMesh.vertices;World=Local.Select(filter.transform.TransformPoint).ToArray();
            Uv=filter.sharedMesh.uv;Indices=filter.sharedMesh.triangles;
        }
    }
    internal sealed class Triangle
    {
        public readonly MeshView View;
        public readonly int Offset;
        public Vector3 A=>View.World[View.Indices[Offset]];
        public Vector3 B=>View.World[View.Indices[Offset+1]];
        public Vector3 C=>View.World[View.Indices[Offset+2]];
        public Vector3 Normal=>Vector3.Cross(B-A,C-A).normalized;
        public Triangle(MeshView view,int offset){View=view;Offset=offset;}
        public bool HeightAt(Vector3 point,out float height)
        {
            height=0;var n=Normal;if(n.y<.1f)return false;
            float Side(Vector3 a,Vector3 b)=>(b.x-a.x)*(point.z-a.z)-(b.z-a.z)*(point.x-a.x);
            float a=Side(A,B),b=Side(B,C),c=Side(C,A);
            if(!((a>=-.0001f&&b>=-.0001f&&c>=-.0001f)||(a<=.0001f&&b<=.0001f&&c<=.0001f)))return false;
            height=A.y-(n.x*(point.x-A.x)+n.z*(point.z-A.z))/n.y;return true;
        }
    }
    public IReadOnlyList<MeshView> Views { get; }
    private readonly Dictionary<Vector3Int,List<Triangle>> anchors=new Dictionary<Vector3Int,List<Triangle>>();
    private readonly Dictionary<Vector2Int,List<Triangle>> surfaces=new Dictionary<Vector2Int,List<Triangle>>();
    private readonly HashSet<Triangle> claimed=new HashSet<Triangle>();
    public TownSeamGeometry(GameObject town)
    {
        var views=new List<MeshView>();
        var lowDomains=town.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name=="Residential long block"||c.name=="Office long block")
            .Select(c=>TownCourtyardGeometry.Expand(TownCourtyardGeometry.Rect(TownResidentialPlacement.ColliderBounds(c)),3)).ToList();
        lowDomains.Add(new Rect(-84,-154,8,6));lowDomains.Add(new Rect(83,-155,14,8));
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null)throw new InvalidOperationException("Missing saved mesh: "+filter.name);
            var view=new MeshView(filter);views.Add(view);
            bool legacy=filter.name.StartsWith("TownChunk_",StringComparison.Ordinal);
            for(int i=0;i<view.Indices.Length;i+=3)
            {
                var tri=new Triangle(view,i);view.Triangles.Add(tri);
                var min=Vector3.Min(tri.A,Vector3.Min(tri.B,tri.C));var max=Vector3.Max(tri.A,Vector3.Max(tri.B,tri.C));
                // Roof recognition excludes countryside trees and the already validated new architecture.
                var footprint=Rect.MinMaxRect(min.x,min.z,max.x,max.z);
                if(legacy&&(max.y>2||lowDomains.Any(r=>r.Overlaps(footprint)))&&min.x>-216&&max.x<264&&min.z>-190&&max.z<204)
                    foreach(var p in new[]{tri.A,tri.B,tri.C})
                    {var key=Key(p);if(!anchors.TryGetValue(key,out var list))anchors.Add(key,list=new List<Triangle>());list.Add(tri);}
                if(tri.Normal.y<.1f||max.y<-.01f||max.x<-216||min.x>264||max.z<-198||min.z>204)continue;
                for(int z=Mathf.FloorToInt(Mathf.Max(min.z,-198)/4);z<=Mathf.FloorToInt(Mathf.Min(max.z,204)/4);z++)
                    for(int x=Mathf.FloorToInt(Mathf.Max(min.x,-216)/4);x<=Mathf.FloorToInt(Mathf.Min(max.x,264)/4);x++)
                    {var key=new Vector2Int(x,z);if(!surfaces.TryGetValue(key,out var list))surfaces.Add(key,list=new List<Triangle>());list.Add(tri);}
            }
        }
        Views=views.AsReadOnly();
    }
    private static Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000));
    public List<Triangle> Match(Mesh template,Matrix4x4 pose,bool allowMissing=false)
    {
        var v=template.vertices.Select(pose.MultiplyPoint3x4).ToArray();var uv=template.uv;var indices=template.triangles;
        var result=new List<Triangle>();var used=new HashSet<Triangle>();
        for(int i=0;i<indices.Length;i+=3)
        {
            var key=Key(v[indices[i]]);Triangle found=null;
            for(int z=-1;z<=1&&found==null;z++)for(int y=-1;y<=1&&found==null;y++)for(int x=-1;x<=1&&found==null;x++)
            {
                if(!anchors.TryGetValue(key+new Vector3Int(x,y,z),out var candidates))continue;
                foreach(var tri in candidates)
                {
                    if(claimed.Contains(tri)||used.Contains(tri))continue;
                    for(int turn=0;turn<3&&found==null;turn++)
                    {
                        bool same=true;
                        for(int corner=0;corner<3;corner++)
                        {
                            int a=indices[i+corner],b=tri.View.Indices[tri.Offset+(corner+turn)%3];
                            if((v[a]-tri.View.World[b]).sqrMagnitude>1e-8f||(uv[a]-tri.View.Uv[b]).sqrMagnitude>1e-12f){same=false;break;}
                        }
                        if(same)found=tri;
                    }
                    if(found!=null)break;
                }
            }
            if(found==null){if(allowMissing)continue;return null;}
            used.Add(found);result.Add(found);
        }
        foreach(var triangle in result)claimed.Add(triangle);
        return result;
    }
    public float Height(Vector3 point,float minimum,float maximum,ISet<Triangle> excluded=null)
    {
        if(!surfaces.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x/4),Mathf.FloorToInt(point.z/4)),out var list))return float.NaN;
        float highest=float.NegativeInfinity;
        foreach(var triangle in list)
            if((excluded==null||!excluded.Contains(triangle))&&triangle.HeightAt(point,out var y)&&y>=minimum&&y<=maximum)highest=Mathf.Max(highest,y);
        return float.IsNegativeInfinity(highest)?float.NaN:highest;
    }
    public static Mesh Template(string id)
    {
        string category=new[]{"BusShelter","UtilityCabinet"}.Contains(id)?"Street":"Facades";
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TownDetailKitGeneration.Root+"/Prefabs/"+category+"/"+id+".prefab");
        if(prefab==null)throw new InvalidOperationException("Native detail template missing: "+id);
        return prefab.GetComponent<MeshFilter>().sharedMesh;
    }
}
