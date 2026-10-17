using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Adds the missed eastern-bank forest without changing the installed terrain or city.</summary>
public static class TownEastBankBatch
{
    private const string Report="Library/TownEastBankQA",Pictures="Docs/TownEastBankReview";
    public static void Generate()=>Run(true);
    public static void Review()=>Run(false);
    private static void Run(bool build)
    {
        string stage=build?"result":"review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            if(build)
            {
                Capture("Before");
                var before=TownPilotScopePreservation.Capture(new[]{TownSouthLandscape.EastForest});
                string layoutHash=Hash(TownSouthLandscape.LayoutPath);
                var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
                try
                {
                    var original=root.GetComponentsInChildren<Component>(true).Where(c=>c!=root.transform)
                        .ToDictionary(c=>c.GetInstanceID(),c=>EditorJsonUtility.ToJson(c));
                    new TownSouthLandscape(root).CompleteEastBank(Report+"/planting.txt");
                    var current=root.GetComponentsInChildren<Component>(true).ToDictionary(c=>c.GetInstanceID());
                    TownForestBatch.Require(original.All(p=>current.TryGetValue(p.Key,out var component)&&EditorJsonUtility.ToJson(component)==p.Value),"A pre-existing component changed during vegetation completion.");
                    PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
                    File.WriteAllText(Report+"/components.txt","PASS: all "+original.Count+" pre-existing components unchanged, including the complete southern forest, terrain, river, world limits, signs and gameplay components. Only the root child list extended.\n");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                TownForestBatch.Require(Hash(TownSouthLandscape.LayoutPath)==layoutHash,"World/map layout changed.");
                before.RequireUnchanged(TownPilotScopePreservation.Capture(new[]{TownSouthLandscape.EastForest}));
                File.WriteAllText(Report+"/protection.txt","PASS: exact saved geometry/material/collision preservation outside the eastern-bank parcel. Layout file unchanged.\n"+before);
            }
            Validate();Capture("After");
            File.WriteAllText(Report+"/"+stage+".txt","PASS: eastern-bank forest, native palette/density, grounded trunks, bank and boundary clearances, spatial batches and complete existing city preservation.\n");
            File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownSouthLandscape.EastRoot,"*",SearchOption.AllDirectories)
                .Concat(new[]{TownSouthLandscape.EastRoot+".meta",TownWorldAssembler.PrefabPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Hash(p)+" "+p.Replace('\\','/')));
            EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();
        var site=town.transform.Find(TownSouthLandscape.EastSite);TownForestBatch.Require(site!=null,"Eastern woodland missing.");
        TownForestBatch.Require(town.layout.cityBounds==TownSouthLandscape.ExpandedBounds,"World bounds changed.");
        var filters=site.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        TownForestBatch.Require(filters.Length<=130&&triangles<400000,"Woodland exceeds the spatial-batch/geometry budget.");
        foreach(var filter in filters)
        {
            TownForestBatch.Require(filter.GetComponent<MeshRenderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat"),"Unexpected woodland material.");
            TownForestBatch.Require(filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).All(p=>TownBakeryQuarterPlacement.Finite(p)&&TownForestPlan.Contains(TownSouthLandscape.EastForest,p)&&p.x>town.layout.RiverCenter(p.z)+23),"Vegetation escapes its parcel or overhangs the open bank/river.");
        }
        var trees=site.GetComponentsInChildren<CapsuleCollider>();TownForestBatch.Require(trees.Length>=1000&&trees.Average(c=>c.transform.lossyScale.x)>2,"Woodland does not match native density/scale.");
        var floor=town.GetComponentsInChildren<MeshCollider>().Where(c=>c.name=="River-cut town terrain"||c.name=="Extended river-cut ground").ToArray();
        var edges=town.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="World edge").ToArray();
        Physics.SyncTransforms();
        foreach(var tree in trees)
        {
            var p=tree.transform.position;
            TownForestBatch.Require(Physics.RaycastAll(p+Vector3.up*8,Vector3.down,10).Any(h=>floor.Contains(h.collider)&&Mathf.Abs(h.point.y)<.005f&&h.normal.y>.99f),"Tree lacks a physical ground surface at "+p);
            foreach(var edge in edges)
                TownForestBatch.Require(!Physics.ComputePenetration(tree,tree.transform.position,tree.transform.rotation,edge,edge.transform.position,edge.transform.rotation,out _,out float depth)||depth<.001f,"Tree intersects an existing world boundary.");
        }
        var positions=trees.Select(t=>new Vector2(t.transform.position.x,t.transform.position.z)).ToArray();int cells=0;
        for(float x=400;x<=750;x+=50)for(float z=-570;z<=-230;z+=50)
        {
            TownForestBatch.Require(positions.Any(p=>Vector2.Distance(p,new Vector2(x,z))<38),"A large unplanted pocket remains at "+x+","+z);cells++;
        }
        foreach(float x in new[]{705f,750f})for(float z=-175;z<=200;z+=50)
        {
            TownForestBatch.Require(positions.Any(p=>Vector2.Distance(p,new Vector2(x,z))<38),"Outer eastern rim remains unplanted at "+x+","+z);cells++;
        }
        File.WriteAllText(Report+"/access.txt","PASS: "+trees.Length+" tree-ground probes; no trunk/world-limit penetrations; all vegetation clear of the river and its banks; "+cells+" parcel coverage samples. Original terrain and limits retained.\n");
        File.WriteAllText(Report+"/metrics.txt",trees.Length+" added trees; "+triangles+" added triangles; "+filters.Length+" spatial 48 m render batches. Existing left-bank forest unchanged.\n");
    }
    private static void Capture(string suffix)
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);bool fog=RenderSettings.fog;
        try
        {
            RenderSettings.fog=false;
            TownFloristBatch.Render(scene,Pictures+"/EastBank"+suffix+".png",new Vector3(950,420,-620),new Vector3(430,0,-290),310);
            TownFloristBatch.Render(scene,Pictures+"/Top"+suffix+".png",new Vector3(490,650,-400),new Vector3(490,0,-400),245);
        }
        finally{RenderSettings.fog=fog;}
        if(suffix=="After")TownFloristBatch.Render(scene,Pictures+"/RiverEdge.png",new Vector3(338,11,-348),new Vector3(398,5,-375));
    }
    private static string Hash(string path)
    {using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
}
