using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RetailEmpireTycoon.City;

/// <summary>Locates legacy visible surfaces that may remain after an authored plan revision.</summary>
public static class PinebrookRevisionAudit
{
    public static void FixMarkingWinding()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();const string path=PinebrookDistrictArt.Root+"/Meshes/LaneAndCrosswalkPaint.asset";
            var mesh=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(path));var vertices=mesh.vertices;var indices=mesh.triangles;
            if(vertices.Any(p=>Mathf.Abs(p.y-.13f)>.0001f))throw new InvalidOperationException("Road-paint height contract changed.");
            for(int i=0;i<indices.Length;i+=3)
                if(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).y<0){int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}
            mesh.triangles=indices;mesh.RecalculateNormals();PinebrookDistrictArt.Save(mesh,"Meshes/LaneAndCrosswalkPaint.asset");AssetDatabase.SaveAssets();
            PinebrookDistrictBuild.Validate();PinebrookDistrictBuild.Capture();PinebrookDistrictBuild.Receipt("result","PASS: revised concept layout, detailed facades, upward-facing road paint, physical clearance and both worlds.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=UnityEngine.Object.FindObjectOfType<TownWorldPlaces>();var lines=new System.Collections.Generic.List<string>();
        var targets=new[]{new Vector3(72,0,366),new Vector3(28,0,374),new Vector3(-76,0,374),new Vector3(-76,0,400)};
        foreach(var f in town.GetComponentsInChildren<MeshFilter>())
        {
            if(!f.GetComponent<Renderer>().enabled)continue;
            var mesh=f.sharedMesh;var v=mesh.vertices;var ids=mesh.triangles;var hits=new int[targets.Length];int outside=0;
            for(int i=0;i<ids.Length;i+=3)
            {
                var center=f.transform.TransformPoint((v[ids[i]]+v[ids[i+1]]+v[ids[i+2]])/3);
                if(f.transform.IsChildOf(town.transform.Find("Pinebrook"))&&new[]{ids[i],ids[i+1],ids[i+2]}.Any(k=>{var p=f.transform.TransformPoint(v[k]);return p.x< -410||p.x>350||p.z<238||p.z>840;}))outside++;
                for(int n=0;n<targets.Length;n++)if(Mathf.Abs(center.x-targets[n].x)<5&&Mathf.Abs(center.z-targets[n].z)<5)hits[n]++;
            }
            if(hits.All(n=>n==0)&&outside==0)continue;string hierarchy=f.name;for(var p=f.transform.parent;p!=null;p=p.parent)hierarchy=p.name+"/"+hierarchy;
            lines.Add(string.Join(",",hits)+" | OUTSIDE="+outside+" | "+hierarchy+" | "+AssetDatabase.GetAssetPath(mesh)+" | "+f.GetComponent<Renderer>().bounds);
        }
        Directory.CreateDirectory(PinebrookDistrictBuild.Report);File.WriteAllLines(PinebrookDistrictBuild.Report+"/legacy-surfaces.txt",lines);EditorApplication.Exit(0);
    }
}
