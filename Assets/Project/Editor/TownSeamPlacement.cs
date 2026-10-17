using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Replaces exact recognised triangles, never shared source assets or the surrounding street.</summary>
internal sealed class TownSeamPlacement
{
    public const string Hierarchy="Seam repairs stage 6";
    private readonly TownSeamPlan plan;
    private readonly GameObject town;
    public TownSeamPlacement(TownSeamPlan plan,GameObject town){this.plan=plan;this.town=town;}
    public void Build()
    {
        if(town.transform.Find(Hierarchy)!=null)throw new InvalidOperationException("Generate from the immutable stage-five town.");
        Directory.CreateDirectory(TownSeamPlan.AssetRoot+"/Meshes");AssetDatabase.Refresh();
        var material=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        if(material==null)throw new InvalidOperationException("Native shared town material is missing.");
        var replacements=plan.Replacements;
        if(replacements.Count==0)throw new InvalidOperationException("No evidenced repairs were recognised.");
        var removed=new HashSet<TownSeamGeometry.Triangle>();
        foreach(var item in replacements)foreach(var tri in item.originalTriangles)
            if(!removed.Add(tri))throw new InvalidOperationException("Two repairs claim the same original triangle.");
        int trimmed=0;
        foreach(var view in plan.Geometry.Views)
        {
            var offsets=new HashSet<int>(view.Triangles.Where(removed.Contains).Select(t=>t.Offset));
            if(offsets.Count==0)continue;
            var source=view.Filter.sharedMesh;
            if(source.subMeshCount!=1||view.Filter.GetComponent<MeshCollider>()!=null)
                throw new InvalidOperationException("A recognised visual detail belongs to an unsupported physical/multimaterial mesh.");
            var retained=view.Triangles.Where(t=>!offsets.Contains(t.Offset)).SelectMany(t=>new[]{view.Indices[t.Offset],view.Indices[t.Offset+1],view.Indices[t.Offset+2]}).ToArray();
            if(retained.Length==0)throw new InvalidOperationException("Repair must not erase a whole legacy chunk.");
            var used=retained.Distinct().OrderBy(i=>i).ToArray();var mapping=new int[source.vertexCount];
            for(int i=0;i<used.Length;i++)mapping[used[i]]=i;
            var mesh=new Mesh{name="SeamTrim"+trimmed,indexFormat=IndexFormat.UInt32};
            mesh.vertices=used.Select(i=>view.Local[i]).ToArray();
            var normals=source.normals;var uv=source.uv;var uv2=source.uv2;var colors=source.colors32;var tangents=source.tangents;
            if(normals.Length==source.vertexCount)mesh.normals=used.Select(i=>normals[i]).ToArray();
            if(uv.Length==source.vertexCount)mesh.uv=used.Select(i=>uv[i]).ToArray();
            if(uv2.Length==source.vertexCount)mesh.uv2=used.Select(i=>uv2[i]).ToArray();
            if(colors.Length==source.vertexCount)mesh.colors32=used.Select(i=>colors[i]).ToArray();
            if(tangents.Length==source.vertexCount)mesh.tangents=used.Select(i=>tangents[i]).ToArray();
            mesh.triangles=retained.Select(i=>mapping[i]).ToArray();mesh.RecalculateBounds();
            view.Filter.sharedMesh=Save(mesh,"SeamTrim"+trimmed++);
        }
        var root=new GameObject(Hierarchy).transform;root.SetParent(town.transform,false);
        var parts=new List<CombineInstance>();var temporary=new List<Mesh>();
        try
        {
            foreach(var item in replacements)
            {
                var mesh=item.Build();temporary.Add(mesh);parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});
                var marker=new GameObject(item.id).transform;marker.SetParent(root,false);
                marker.localPosition=item.pose.MultiplyPoint3x4(Vector3.zero);
                if(item.kind=="Platform")
                {
                    marker.gameObject.isStatic=true;
                    // A real horizontal plane, not a raised box edge across the public pedestrian connection.
                    marker.gameObject.AddComponent<MeshCollider>().sharedMesh=Save(Object.Instantiate(mesh),item.id+".Collision");
                }
            }
            var combined=new Mesh{name="SeamRepairs",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(parts.ToArray(),true,true);
            var geometry=new GameObject("Repair geometry",typeof(MeshFilter),typeof(MeshRenderer));geometry.transform.SetParent(root,false);geometry.isStatic=true;
            geometry.GetComponent<MeshFilter>().sharedMesh=Save(combined,"SeamRepairs");geometry.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        finally{foreach(var mesh in temporary)Object.DestroyImmediate(mesh);}
        File.WriteAllText("Library/TownSeamQA/placement.txt",$"PASS {removed.Count} exact recognised triangles replaced in {trimmed} copied chunks. All existing shared mesh assets and original colliders remain unchanged.\n");
        File.WriteAllText("Library/TownSeamQA/templates.txt","PASS protected native templates reused; contact geometry generated before placement. No missing legacy kit was regenerated.\n"+string.Join("\n",replacements.Select(r=>r.id+": "+r.kind)));
    }
    private static Mesh Save(Mesh mesh,string id)
    {
        string path=TownSeamPlan.AssetRoot+"/Meshes/"+id+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(previous==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,previous);EditorUtility.SetDirty(previous);Object.DestroyImmediate(mesh);return previous;
    }
}
