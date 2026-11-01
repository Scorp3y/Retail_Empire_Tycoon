using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Removes only exact recognised tree/sign triangles and the sidewalk strips opened by new junctions.</summary>
internal sealed class PinebrookSitePreparation
{
    private readonly GameObject town;
    private readonly PinebrookDistrictLayout plan;
    private readonly HashSet<string> remove=new();
    private readonly List<GameObject> retired=new();
    private int meshCopies,triangles,trees;
    internal PinebrookSitePreparation(GameObject town,PinebrookDistrictLayout plan){this.town=town;this.plan=plan;}
    internal void Apply()
    {
        var original=town.transform.Find("Forest Quarter");
        foreach(var node in town.GetComponentsInChildren<Transform>(true))
        {
            if(node.IsChildOf(original)||node.name!="Pine"&&node.name!="TreeWide"&&node.name!="TreeRound")continue;
            if(node.position.x< -390||node.position.x>310||node.position.z<280||node.position.z>800)continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+node.name+".prefab");if(source==null)throw new InvalidOperationException("Missing recognised native tree "+node.name);
            var mesh=source.GetComponent<MeshFilter>().sharedMesh;var bounds=WorldBounds(mesh,node.localToWorldMatrix);
            if(!NeedsClearing(bounds))continue;
            Add(mesh,node.localToWorldMatrix);var bush=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/BushRound.prefab").GetComponent<MeshFilter>().sharedMesh;
            Add(bush,Matrix4x4.TRS(node.position+new Vector3(2,0,-2),Quaternion.identity,Vector3.one*1.2f));retired.Add(node.gameObject);trees++;
        }
        RecogniseSign("New district railway crossing/DistrictSign","Assets/Art/TownGatewayUpdate/Prefabs/DistrictSign.prefab");
        RecogniseSign("Forest Quarter/Street furnishings/DistrictMarker","Assets/Art/TownForestQuarter/Prefabs/DistrictMarker.prefab");
        var bus=town.transform.Find("Forest Quarter/Street furnishings/BusShelter");if(bus!=null){var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownForestQuarter/Prefabs/BusShelter.prefab");foreach(var f in source.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="BusLabel"))Add(f.sharedMesh,bus.localToWorldMatrix*source.transform.worldToLocalMatrix*f.transform.localToWorldMatrix);}
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.transform.IsChildOf(town.transform.Find("Pinebrook")))continue;
            var bounds=filter.GetComponent<Renderer>().bounds;if(bounds.max.z<240||bounds.min.z>800||bounds.max.x< -390||bounds.min.x>310)continue;
            var source=filter.sharedMesh;var points=source.vertices.Select(filter.transform.TransformPoint).ToArray();var uv=source.uv;var kept=new List<int>[source.subMeshCount];int removed=0;
            bool sidewalk=false; // The superseded local road group has been replaced as a whole.
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                kept[sub]=new List<int>();var indices=source.GetTriangles(sub);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=points[indices[i]];var b=points[indices[i+1]];var c=points[indices[i+2]];var center=(a+b+c)/3;
                    bool candidate=center.x>=-395&&center.x<=315&&center.z>=240&&center.z<=805;
                    bool curb=sidewalk&&center.y<.3f&&uv.Length==points.Length&&Mathf.FloorToInt(uv[indices[i]].x*32)!=7&&plan.roads.Any(r=>r.Contains(center));
                    // A previous ground repair copied the old district's paving into
                    // separate collider surfaces. Remove those superseded copies too.
                    bool obsoleteFloor=filter.name.StartsWith("Stable flat ground",StringComparison.Ordinal)&&
                        new[]{a,b,c}.All(p=>p.x>=-132&&p.x<=80&&p.z>=302&&p.z<=438&&p.y>.005f&&p.y<.3f);
                    if(candidate&&(curb||obsoleteFloor||remove.Contains(Key(a,b,c)))){removed++;continue;}
                    kept[sub].Add(indices[i]);kept[sub].Add(indices[i+1]);kept[sub].Add(indices[i+2]);
                }
            }
            if(removed==0)continue;
            var copy=Object.Instantiate(source);copy.name="Pinebrook scoped trim "+meshCopies;
            for(int sub=0;sub<kept.Length;sub++)copy.SetTriangles(kept[sub],sub,false);
            string sourcePath=AssetDatabase.GetAssetPath(source);string path;
            if(sourcePath.StartsWith(PinebrookDistrictArt.Root+"/Meshes/RetainedTownTrim",StringComparison.Ordinal))path=sourcePath.Substring(PinebrookDistrictArt.Root.Length+1);
            else{string hierarchy=filter.name;for(var parent=filter.transform.parent;parent!=null;parent=parent.parent)hierarchy=parent.name+"/"+hierarchy;using(var hash=System.Security.Cryptography.SHA256.Create())path="Meshes/RetainedTownTrim_"+BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hierarchy+sourcePath))).Replace("-","").Substring(0,16)+".asset";}
            meshCopies++;PinebrookDistrictArt.Save(copy,path);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(PinebrookDistrictArt.Root+"/"+path);
            foreach(var collider in filter.GetComponents<MeshCollider>())if(collider.sharedMesh==source)collider.sharedMesh=saved;filter.sharedMesh=saved;triangles+=removed;
        }
        foreach(var node in retired)if(node!=null)Object.DestroyImmediate(node);
        if(trees>0||!File.Exists(PinebrookDistrictBuild.Report+"/site-preparation.txt"))File.WriteAllText(PinebrookDistrictBuild.Report+"/site-preparation.txt",trees+" exact native trees cleared only from planned construction envelopes; "+triangles+" recognised tree/sign or junction-curb triangles removed from "+meshCopies+" owned copies. Other original mesh vertex buffers and triangle lists retained.\n");
    }
    private bool NeedsClearing(Bounds b)
    {
        var rect=new Rect(b.min.x,b.min.z,b.size.x,b.size.z);
        if(plan.clearings.Any(r=>r.Rect.Overlaps(rect)))return true;
        if(town.transform.Find("Forest Quarter/Architecture").GetComponentsInChildren<Renderer>().Any(r=>r.bounds.Intersects(b)))return true;
        if(plan.roads.Any(r=>r.Expanded(4).Overlaps(rect)))return true;
        if(new Rect(84,518,84,66).Overlaps(rect))return true;
        return plan.paths.Any(p=>p.Distance(b.center)<=p.width/2+Mathf.Max(b.extents.x,b.extents.z)+1);
    }
    private void RecogniseSign(string nodePath,string prefabPath)
    {
        var node=town.transform.Find(nodePath);if(node==null)return;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);if(source==null)throw new FileNotFoundException(prefabPath);
        foreach(var f in source.GetComponentsInChildren<MeshFilter>())Add(f.sharedMesh,node.localToWorldMatrix*source.transform.worldToLocalMatrix*f.transform.localToWorldMatrix);
        retired.Add(node.gameObject);
    }
    private void Add(Mesh mesh,Matrix4x4 pose){var p=mesh.vertices.Select(pose.MultiplyPoint3x4).ToArray();var ids=mesh.triangles;for(int i=0;i<ids.Length;i+=3)remove.Add(Key(p[ids[i]],p[ids[i+1]],p[ids[i+2]]));}
    private static string Point(Vector3 p)=>Mathf.RoundToInt(p.x*100)+","+Mathf.RoundToInt(p.y*100)+","+Mathf.RoundToInt(p.z*100);
    private static string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Point(a),Point(b),Point(c)}.OrderBy(v=>v,StringComparer.Ordinal));
    private static Bounds WorldBounds(Mesh mesh,Matrix4x4 pose){var vertices=mesh.vertices;var b=new Bounds(pose.MultiplyPoint3x4(vertices[0]),Vector3.zero);foreach(var v in vertices)b.Encapsulate(pose.MultiplyPoint3x4(v));return b;}
}
