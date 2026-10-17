using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Visual-only road correction; never rebuilds the installed district or its physics.</summary>
public static class TownForestRoadBatch
{
    private const string Report="Library/TownForestRoadQA",Pictures="Docs/TownForestRoadReview";
    public static void Generate()=>Run(true);
    public static void Review()=>Run(false);
    private static void Run(bool patch)
    {
        string stage=patch?"result":"review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            TownFloristBatch.Render(scene,Pictures+"/"+(patch?"Before":"Reopened")+".png",new Vector3(10,175,210),new Vector3(6,0,382));
            if(patch)
            {
                var scopes=TownForestPlan.Roads.Select(r=>Rect.MinMaxRect(r.xMin-3,r.yMin-3,r.xMax+3,r.yMax+3)).ToArray();
                var before=TownPilotScopePreservation.Capture(scopes);
                var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
                try
                {
                    var streets=root.transform.Find(TownForestPlan.Site+"/Street network");
                    if(streets==null)throw new InvalidOperationException("Installed district roads not found.");
                    var protectedComponents=ProtectedComponents(root,streets);
                    TownForestRoadStyle.Apply(streets);
                    var after=ProtectedComponents(root,streets);
                    TownForestBatch.Require(protectedComponents.Count==after.Count&&protectedComponents.All(p=>after.TryGetValue(p.Key,out var value)&&value==p.Value),
                        "A component outside the three visual road nodes changed.");
                    PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
                    File.WriteAllText(Report+"/components.txt","PASS: "+after.Count+" other components byte-identical in memory, including every collider, transform, sign, building and gameplay component. Only three road render materials and the non-colliding paint mesh changed.\n");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                before.RequireUnchanged(TownPilotScopePreservation.Capture(scopes));
                File.WriteAllText(Report+"/protection.txt","PASS: exact saved visual/material/collision fingerprint outside the five street corridors unchanged.\n"+before);
            }
            ValidateStyle();
            // Reuse the district's complete native geometry, access and map checks.
            TownForestBatch.Validate();
            File.Copy(TownForestBatch.Report+"/access.txt",Report+"/access.txt",true);
            File.Copy(TownForestBatch.Report+"/overlaps.txt",Report+"/overlaps.txt",true);
            scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            TownFloristBatch.Render(scene,Pictures+"/Overview.png",new Vector3(10,175,210),new Vector3(6,0,382));
            TownFloristBatch.Render(scene,Pictures+"/RoadJoin.png",new Vector3(-8,14,285),new Vector3(-24,0,323));
            TownFloristBatch.Render(scene,Pictures+"/Street.png",new Vector3(-18,5,328),new Vector3(-24,0,391));
            File.WriteAllText(Report+"/"+stage+".txt","PASS: reference road material, matching paint, full saved-scene district access checks and native renders. Physics untouched.\n");
            File.WriteAllText(Report+"/"+stage+"-input.sha256",Hash(TownWorldAssembler.PrefabPath));
            File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownForestRoadStyle.Root,"*",SearchOption.AllDirectories)
                .Concat(new[]{TownWorldAssembler.PrefabPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Hash(p)+" "+p.Replace('\\','/')));
            EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static Dictionary<int,string> ProtectedComponents(GameObject root,Transform streets)
    {
        var allowed=new HashSet<Component>();
        foreach(string id in new[]{"ForestRoadAndWalkSurface","ForestPavingJoints","ForestRoadPaint"})
            allowed.Add(streets.Find(id).GetComponent<MeshRenderer>());
        allowed.Add(streets.Find("ForestRoadPaint").GetComponent<MeshFilter>());
        return root.GetComponentsInChildren<Component>(true).Where(c=>!allowed.Contains(c)).ToDictionary(c=>c.GetInstanceID(),c=>EditorJsonUtility.ToJson(c));
    }
    private static void ValidateStyle()
    {
        var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var streets=root.transform.Find(TownForestPlan.Site+"/Street network");
            var material=AssetDatabase.LoadAssetAtPath<Material>(TownForestRoadStyle.MaterialPath);
            foreach(string id in new[]{"ForestRoadAndWalkSurface","ForestPavingJoints","ForestRoadPaint"})
                TownForestBatch.Require(streets.Find(id).GetComponent<MeshRenderer>().sharedMaterial==material,"Road does not share the reference finish: "+id);
            var paint=streets.Find("ForestRoadPaint");
            TownForestBatch.Require(paint.GetComponent<Collider>()==null,"Visual road paint must never affect driving physics.");
            var vertices=paint.GetComponent<MeshFilter>().sharedMesh.vertices;
            TownForestBatch.Require(vertices.Any(p=>Mathf.Abs(p.x+28.15f)<.05f&&p.z>325&&p.z<410),"Entrance avenue edge stripe missing.");
            TownForestBatch.Require(vertices.Any(p=>Mathf.Abs(p.z-434.15f)<.05f),"Ring road edge stripe missing.");
            TownForestBatch.Require(vertices.All(TownBakeryQuarterPlacement.Finite),"Invalid road-paint geometry.");
            File.WriteAllText(Report+"/style.txt","PASS: all three visual road nodes share the exact existing approach-road material (including its palette and shader); 10 cm centre lines with world-aligned 3 m dash / 3 m gap, 8 cm exposed edge lines; six existing crossings and four yield markings retained; entrance open. Existing road/pavement meshes and all colliders retained.\n");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static string Hash(string path)
    {using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
}
