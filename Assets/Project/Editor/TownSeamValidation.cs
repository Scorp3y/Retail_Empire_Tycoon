using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Saved rendered native geometry and real pedestrian surface acceptance, independent of marker bounds.</summary>
internal static class TownSeamValidation
{
    public static TownSeamReplacement[] ReadPlan()
    {
        var report=JsonUtility.FromJson<TownSeamReport>(File.ReadAllText("Library/TownSeamQA/plan.json"));
        Require(report!=null&&report.replacements!=null&&report.replacements.Length>0,"Repair plan is missing.");return report.replacements;
    }
    public static void ValidateSaved(IReadOnlyList<TownSeamReplacement> repairs)
    {
        TownWorldSafety.RequireIsolatedEditor();var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try{Validate(town,repairs);}
        finally{PrefabUtility.UnloadPrefabContents(town);}
    }
    private static void Validate(GameObject town,IReadOnlyList<TownSeamReplacement> repairs)
    {
        var root=town.transform.Find(TownSeamPlacement.Hierarchy);Require(root!=null,"Saved repair root is missing.");
        Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Scenery introduced gameplay behaviour.");
        var filters=root.GetComponentsInChildren<MeshFilter>(true);Require(filters.Length==1,"Repairs must have one batched visual mesh.");
        var filter=filters[0];var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
        Require(mesh!=null&&renderer!=null&&renderer.enabled&&filter.gameObject.activeInHierarchy,"Repair mesh is not actually rendered.");
        Require(renderer.sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat"),"Native shared material changed.");
        var actual=mesh.vertices;var uv=mesh.uv;var uv2=mesh.uv2;var normals=mesh.normals;var indices=mesh.triangles;
        Require(uv.Length==actual.Length&&uv2.Length==actual.Length&&normals.Length==actual.Length,"Incomplete saved mesh attributes.");
        int vertexOffset=0,triangleOffset=0;
        foreach(var repair in repairs)
        {
            Require(root.Find(repair.id)!=null,"Missing repair marker: "+repair.id);
            var expected=repair.Build();
            try
            {
                var vertices=expected.vertices;var texture=expected.uv;var light=expected.uv2;var normal=expected.normals;var triangles=expected.triangles;
                Require(vertexOffset+vertices.Length<=actual.Length&&triangleOffset+triangles.Length<=indices.Length,"Repair geometry truncated.");
                for(int i=0;i<vertices.Length;i++)
                    Require(Finite(actual[i+vertexOffset])&&(actual[i+vertexOffset]-vertices[i]).sqrMagnitude<1e-8f
                        &&(uv[i+vertexOffset]-texture[i]).sqrMagnitude<1e-12f&&(uv2[i+vertexOffset]-light[i]).sqrMagnitude<1e-8f
                        &&(normals[i+vertexOffset]-normal[i]).sqrMagnitude<1e-8f,repair.id+": actual native XYZ/UV/UV2/normal differs.");
                for(int i=0;i<triangles.Length;i++)Require(indices[i+triangleOffset]==triangles[i]+vertexOffset,repair.id+": native winding/order differs.");
                if(repair.kind=="Steps")
                {
                    float rise=(repair.top-repair.ground)/3;Require(rise>=.06f&&rise<=.2f&&Mathf.Abs(repair.top-.55f)<.0001f,repair.id+": wrong stair/door contact.");
                }
                if(repair.kind=="Roof")Require(repair.roofCorners.Length==4&&repair.roofCorners.All(p=>Finite(p)&&p.y<=repair.pose.m13),repair.id+": roof support is inverted.");
                if(repair.kind=="Pipe")Require(repair.top>repair.ground&&Mathf.Abs(expected.bounds.min.y-(repair.pose.m13+repair.ground))<.001f,repair.id+": downpipe does not meet ground.");
                vertexOffset+=vertices.Length;triangleOffset+=triangles.Length;
            }
            finally{Object.DestroyImmediate(expected);}
        }
        Require(vertexOffset==actual.Length&&triangleOffset==indices.Length,"Undeclared extra repair geometry.");
        ValidateContacts(town,root,repairs);
        var platformColliders=root.GetComponentsInChildren<MeshCollider>(true);
        Require(platformColliders.Length==repairs.Count(r=>r.kind=="Platform"),"Wrong physical platform count.");
        foreach(var repair in repairs.Where(r=>r.kind=="Platform"))
        {
            var collider=root.Find(repair.id).GetComponent<MeshCollider>();
            Require(collider!=null&&collider.enabled&&!collider.isTrigger&&!collider.convex&&collider.sharedMesh!=null,"Missing real platform collision.");
            var physical=collider.sharedMesh;
            Require(physical.triangles.Length==6&&physical.vertices.All(p=>Mathf.Abs(p.y-.14f)<.0001f),"Platform collision is not a horizontal two-triangle native floor.");
            Require(physical.bounds.size.y<.0001f&&Mathf.Abs(physical.bounds.size.x-repair.area.width)<.0001f&&Mathf.Abs(physical.bounds.size.z-repair.area.height)<.0001f,"Platform collision leaves the real native pad footprint.");
            Require(Mathf.Abs(repair.area.yMax+150)<.0001f,"Stop floor overlaps or misses the native street sidewalk.");
        }
        int renderers=town.GetComponentsInChildren<Renderer>(true).Length;
        int trianglesCount=town.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(renderers<=480&&trianglesCount<=1400000,"Repair geometry budget exceeded.");
        File.WriteAllText("Library/TownSeamQA/assets.txt",$"PASS {repairs.Count} rendered exact native replacements; XYZ/UV/UV2/normals/winding and two physical flush platforms.\nTown {renderers}/480 renderers; {trianglesCount}/1400000 triangles. Not an FPS measurement.\n"+string.Join("\n",repairs.GroupBy(r=>r.kind).Select(g=>g.Key+": "+g.Count())));
    }
    public static IEnumerator Check(CityWorld city,IReadOnlyList<TownSeamReplacement> repairs)
    {
        Require(city!=null&&city.town!=null,"Live town missing.");
        // Entering Play combines static renderers into GPU-only meshes. Exact saved XYZ/UV/contact
        // acceptance runs before EnterPlaymode, with asset hashes sealed by the pipeline.
        // Here validate the actual renderer's batch segment, then probe live physics independently.
        ValidateLiveRenderer(city.town.gameObject,repairs);Physics.SyncTransforms();int probes=0;
        var root=city.town.transform.Find(TownSeamPlacement.Hierarchy);
        foreach(var repair in repairs.Where(r=>r.kind=="Platform"))
        {
            var collider=root.Find(repair.id).GetComponent<MeshCollider>();
            foreach(var point in Samples(repair.area))
            {
                var origin=city.town.transform.TransformPoint(new Vector3(point.x,2,point.y));
                Require(collider.Raycast(new Ray(origin,-city.town.transform.up),out var hit,3),"Live pedestrian floor missing.");
                Require(Mathf.Abs(city.town.transform.InverseTransformPoint(hit.point).y-.14f)<.0001f,"Live platform join is not flush.");probes++;
            }
            yield return null;
        }
        File.WriteAllText("Library/TownSeamQA/runtime.txt",$"PASS live renderer/material/static-batch index segment and {probes} actual platform surface probes. Exact saved XYZ/UV/contacts validated before Play with sealed asset hashes. Existing buyers retained; no decorative people introduced.\n");
    }
    private static void ValidateLiveRenderer(GameObject town,IReadOnlyList<TownSeamReplacement> repairs)
    {
        var root=town.transform.Find(TownSeamPlacement.Hierarchy);Require(root!=null,"Live repair root missing.");
        var filter=root.Find("Repair geometry").GetComponent<MeshFilter>();var renderer=filter.GetComponent<MeshRenderer>();
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(TownSeamPlan.AssetRoot+"/Meshes/SeamRepairs.asset");
        Require(saved!=null&&filter.sharedMesh!=null&&renderer!=null&&renderer.enabled&&filter.gameObject.activeInHierarchy,"Live repair geometry is not visible.");
        Require(renderer.sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat"),"Live repair palette changed.");
        if(renderer.isPartOfStaticBatch)
            Require(filter.sharedMesh.GetIndexCount(renderer.subMeshStartIndex)==saved.GetIndexCount(0),"Live static batch lost the repair renderer's actual index segment.");
        else Require(filter.sharedMesh==saved,"Live repair renderer does not reference the verified native mesh.");
        foreach(var repair in repairs)
        {
            var marker=root.Find(repair.id);Require(marker!=null&&(marker.localPosition-repair.pose.MultiplyPoint3x4(Vector3.zero)).sqrMagnitude<1e-8f,"Live repair placement changed: "+repair.id);
        }
    }
    private static void ValidateContacts(GameObject town,Transform root,IReadOnlyList<TownSeamReplacement> repairs)
    {
        var geometry=new TownSeamGeometry(town);
        var excluded=new HashSet<TownSeamGeometry.Triangle>(geometry.Views.Where(v=>v.Filter.transform.IsChildOf(root)).SelectMany(v=>v.Triangles));
        foreach(var repair in repairs)
        {
            if(repair.kind=="Roof")foreach(var corner in repair.roofCorners)
                Require(Mathf.Abs(geometry.Height(corner,corner.y-.1f,corner.y+.1f,excluded)-corner.y)<.0001f,repair.id+": support not seated on the actual saved roof.");
            if(repair.kind=="Steps")
            {
                for(int step=0;step<3;step++)
                {
                    var p=repair.pose.MultiplyPoint3x4(new Vector3(0,0,.73f-.34f*step));
                    float expected=repair.pose.m13+repair.ground+(repair.top-repair.ground)*(step+1)/3;
                    Require(Mathf.Abs(geometry.Height(p,expected-.03f,expected+.03f)-expected)<.0001f,repair.id+": missing actual stair tread.");
                }
                var toe=repair.pose.MultiplyPoint3x4(new Vector3(0,0,.85f));
                Require(Mathf.Abs(geometry.Height(toe,-.01f,.3f,excluded)-(repair.pose.m13+repair.ground))<.0001f,repair.id+": stair does not meet the actual pavement.");
                var sill=repair.pose.MultiplyPoint3x4(new Vector3(0,0,-.2f));
                Require(Mathf.Abs(geometry.Height(sill,repair.top-.02f,repair.top+.02f,excluded)-repair.top)<.0001f,repair.id+": final stair height disagrees with actual door threshold.");
            }
            if(repair.kind=="Pipe")
            {
                var p=repair.pose.MultiplyPoint3x4(Vector3.zero);
                Require(Mathf.Abs(geometry.Height(p,-.01f,.3f,excluded)-(repair.pose.m13+repair.ground))<.0001f,repair.id+": pipe ends above or below the actual ground.");
                Require(Mathf.Abs(geometry.Height(p,repair.top-.1f,repair.top+.1f,excluded)-(repair.pose.m13+repair.top))<.0001f,repair.id+": pipe does not reach the actual eave.");
            }
            if(repair.kind=="Canopy")
            {
                foreach(float x in new[]{repair.braceLeft,repair.braceRight})
                {
                    var start=repair.pose.MultiplyPoint3x4(new Vector3(x,repair.plate-.75f,-.82f));
                    Require(town.GetComponentsInChildren<BoxCollider>(true).Any(c=>(c.name=="Residential long block"||c.name=="Office long block")
                        &&TownResidentialPlacement.ColliderBounds(c).Contains(start)),repair.id+": brace does not enter its real building wall.");
                }
            }
        }
    }
    private static IEnumerable<Vector2> Samples(Rect area)
    {foreach(float x in new[]{.05f,.5f,.95f})foreach(float z in new[]{.05f,.5f,.95f})yield return new Vector2(Mathf.Lerp(area.xMin,area.xMax,x),Mathf.Lerp(area.yMin,area.yMax,z));}
    private static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
    private static void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
}
[Serializable] internal sealed class TownSeamReport{public TownSeamReplacement[] replacements;}
