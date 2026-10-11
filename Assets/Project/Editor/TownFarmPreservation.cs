using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEngine;

/// <summary>Compares the actual authored farm geometry and interaction contracts across regeneration.</summary>
internal static class TownFarmPreservation
{
    private static readonly Rect FarmArea=new Rect(454,236,200,238);

    public static string Capture()
        =>Capture(FarmArea,SupplierKind.Farmers);

    /// <summary>Canonical geometry hash ignores batching/order but includes actual positions, colours and collision volumes.</summary>
    public static string Capture(Rect area,SupplierKind supplier)
    {
        var farm=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var parts=new List<string>();
            foreach(var filter in farm.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    var pa=filter.transform.TransformPoint(vertices[a]);var pb=filter.transform.TransformPoint(vertices[b]);var pc=filter.transform.TransformPoint(vertices[c]);
                    var centre=(pa+pb+pc)/3;
                    if(!area.Contains(new Vector2(centre.x,centre.z)))continue;
                    string Vertex(int index,Vector3 position)=>Point(position)+":"+Mathf.RoundToInt(uv[index].x*10000);
                    parts.Add(Hash128.Compute(string.Join("|",new[]{Vertex(a,pa),Vertex(b,pb),Vertex(c,pc)}.OrderBy(v=>v,StringComparer.Ordinal))).ToString());
                }
            }
            foreach(var collider in farm.GetComponentsInChildren<Collider>())
            {
                var centre=collider.bounds.center;
                if(!area.Contains(new Vector2(centre.x,centre.z)))continue;
                parts.Add(collider.GetType().Name+":"+Point(centre)+":"+Point(collider.bounds.size));
            }
            var checkpoint=farm.GetComponent<TownWorldPlaces>().suppliers.Single(s=>s.supplier==supplier);
            parts.Add("Farm checkpoint:"+Point(checkpoint.transform.position)+":"+Point(checkpoint.transform.forward)+":"+checkpoint.radius+":"+checkpoint.requiresRearDocking);
            var court=farm.GetComponentsInChildren<SupplierLoadingCourt>().SingleOrDefault(c=>c.checkpoint==checkpoint);
            if(court!=null)parts.Add("Farm route:"+court.bounds+":"+Point(court.streetAccess)+":"+Point(court.entrance)+":"+Point(court.staging));
            return Hash128.Compute(string.Join("\n",parts.OrderBy(p=>p,StringComparer.Ordinal))).ToString();
        }
        finally {PrefabUtility.UnloadPrefabContents(farm);}
    }

    private static string Point(Vector3 point)=>Mathf.RoundToInt(point.x*10000)+","+Mathf.RoundToInt(point.y*10000)+","+Mathf.RoundToInt(point.z*10000);
}
