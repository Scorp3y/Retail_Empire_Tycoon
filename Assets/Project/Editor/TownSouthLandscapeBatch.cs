using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Isolated authoring and acceptance of the southern terrain extension.</summary>
public static class TownSouthLandscapeBatch
{
    internal const string Report="Library/TownSouthLandscapeQA",Pictures="Docs/TownSouthLandscapeReview";
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
                Capture("Before");var before=TownPilotScopePreservation.Capture(TownSouthLandscape.Permission);
                var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
                try
                {
                    var protectedComponents=ExistingComponents(root);
                    new TownSouthLandscape(root).Build();
                    var after=ExistingComponents(root);
                    TownForestBatch.Require(protectedComponents.All(p=>after.TryGetValue(p.Key,out var value)&&value==p.Value),"Existing city component changed outside the three authorised world limits.");
                    PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
                    File.WriteAllText(Report+"/components.txt","PASS: "+protectedComponents.Count+" pre-existing components retained unchanged; only three world-boundary colliders extended/moved. Root child list extended. No old tree, building, street, sign or supplier removed/moved. Layout data unchanged except cityBounds.\n");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                before.RequireUnchanged(TownPilotScopePreservation.Capture(TownSouthLandscape.Permission));
                File.WriteAllText(Report+"/protection.txt","PASS: exact saved visual/material/collision preservation outside southern terrain and the two side-boundary strips.\n"+before);
            }
            Validate();TownForestBatch.Validate();
            File.Copy(TownForestBatch.Report+"/access.txt",Report+"/retained-district-access.txt",true);
            Capture("After");
            File.WriteAllText(Report+"/"+stage+".txt","PASS: southern terrain, native mixed woodland, world limits, map, physical ground/seam checks and retained district access verified.\n");
            File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Outputs().OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Hash(p)+" "+p.Replace('\\','/')));
            EditorApplication.Exit(0);
        }
        catch(Exception error){Failure(stage,error);}
    }
    private static Dictionary<int,string> ExistingComponents(GameObject root)
        =>root.GetComponentsInChildren<Component>(true).Where(c=>c!=root.transform)
            .Where(c=>!(c is BoxCollider box&&box.name=="World edge"&&(box.size.x<2||box.center.z<0)))
            .Where(c=>!UnderExtension(c.transform,root.transform)).ToDictionary(c=>c.GetInstanceID(),c=>EditorJsonUtility.ToJson(c));
    private static bool UnderExtension(Transform node,Transform root)
    {while(node!=root&&node!=null){if(node.name==TownSouthLandscape.Site)return true;node=node.parent;}return false;}
    private static IEnumerable<string> Outputs()=>Directory.GetFiles(TownSouthLandscape.Root,"*",SearchOption.AllDirectories)
        .Concat(new[]{TownSouthLandscape.Root+".meta",TownWorldAssembler.PrefabPath,TownSouthLandscape.LayoutPath});
    private static string Hash(string path)
    {using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();
        var site=town.transform.Find(TownSouthLandscape.Site);TownForestBatch.Require(site!=null,"Southern extension missing.");
        var layout=town.layout;TownForestBatch.Require(layout.cityBounds==TownSouthLandscape.ExpandedBounds,"Expanded bounds not saved.");
        TownWorldPlan.Validate(layout);
        var filters=site.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        TownForestBatch.Require(triangles<400000&&filters.Length<=100,"Landscape exceeds its geometry/spatial-batch budget.");
        foreach(var filter in filters)
        {
            TownForestBatch.Require(filter.sharedMesh.vertices.All(TownBakeryQuarterPlacement.Finite),"Non-finite landscape geometry.");
            TownForestBatch.Require(filter.GetComponent<MeshRenderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat"),"Landscape does not use the original town material.");
            bool vegetation=filter.transform.parent.name=="Mixed woodland";
            if(vegetation)TownForestBatch.Require(filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).All(p=>TownForestPlan.Contains(TownSouthLandscape.Forest,p)),"Vegetation escaped the marked southern strip.");
            else TownForestBatch.Require(filter.sharedMesh.vertices.All(p=>p.z<=-336.001f+.002f&&p.z>=-624.001f),"New terrain overlaps old terrain or exceeds its south edge.");
        }
        var trees=site.GetComponentsInChildren<CapsuleCollider>();TownForestBatch.Require(trees.Length>=1000,"Dense woodland missing.");
        var edges=town.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="World edge").ToArray();
        TownForestBatch.Require(edges.Length==4&&edges.Count(c=>c.size.x<2&&Mathf.Abs(c.center.z)<.001f&&Mathf.Abs(c.size.z-1008)<.001f)==2,"Side world limits not extended.");
        TownForestBatch.Require(edges.Any(c=>Mathf.Abs(c.center.z+504)<.001f&&Mathf.Abs(c.size.x-960)<.001f)&&edges.Any(c=>Mathf.Abs(c.center.z-504)<.001f&&Mathf.Abs(c.size.x-960)<.001f),"North/south world limits incorrect.");
        var old=town.GetComponentsInChildren<MeshCollider>().Single(c=>c.name=="River-cut town terrain");
        var added=site.Find("Extended river-cut ground").GetComponent<MeshCollider>();
        string Point(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
        var seamOld=old.sharedMesh.vertices.Select(old.transform.TransformPoint).Where(p=>Mathf.Abs(p.z+336)<.001f).Select(Point).Distinct().OrderBy(p=>p).ToArray();
        var seamNew=added.sharedMesh.vertices.Where(p=>Mathf.Abs(p.z+336)<.001f).Select(Point).Distinct().OrderBy(p=>p).ToArray();
        TownForestBatch.Require(seamOld.Length>=6&&seamOld.SequenceEqual(seamNew),"Native ground/river cross-section has a gap at the old map edge.");
        Physics.SyncTransforms();int probes=0;
        void Ground(float x,float z)
        {
            var hits=Physics.RaycastAll(new Vector3(x,8,z),Vector3.down,15,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider==old||h.collider==added).ToArray();
            TownForestBatch.Require(hits.Any(h=>Mathf.Abs(h.point.y)<.005f&&h.normal.y>.99f),"New ground unsupported or discontinuous at "+x+","+z);probes++;
        }
        for(float z=-500;z<-205;z+=12)for(float x=-276;x<670;x+=24)if(Mathf.Abs(x-layout.RiverCenter(z))>21)Ground(x,z);
        foreach(float z in new[]{-336.02f,-336f,-335.98f})for(float x=-400;x<790;x+=16)if(Mathf.Abs(x-layout.RiverCenter(z))>21)Ground(x,z);
        for(float x=-216;x<=264;x+=4)
            TownForestBatch.Require(!Physics.OverlapBox(new Vector3(x,1.3f,-192),new Vector3(1.8f,.7f,4)).Any(c=>c.transform.IsChildOf(site)),"New vegetation blocks the existing perimeter street.");
        CheckMap(scene,town);
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" physical ground/seam probes; exact river-bank/bed cross-section matches the old terrain edge; southern street free of new props; world limits at z=-504/+504 with 1008 m side spans.\n");
        File.WriteAllText(Report+"/metrics.txt",triangles+" added triangles, "+filters.Length+" spatial render batches, "+trees.Length+" physical tree trunks. Playable bounds 960 x 1008 m (previously 960 x 720 m), +288 m south / +40% area. Original ground untouched; 288 m continuation beyond the previous visible edge.\n");
    }
    private static void CheckMap(UnityEngine.SceneManagement.Scene scene,TownWorldPlaces town)
    {
        var node=new GameObject("Southern map acceptance",typeof(RectTransform),typeof(CanvasRenderer));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(node,scene);
        try
        {
            var graphic=node.AddComponent<CityMapGraphic>();graphic.town=town;((RectTransform)node.transform).sizeDelta=new Vector2(1200,960);
            var populate=typeof(CityMapGraphic).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.UI.VertexHelper)},null);
            int Count(){using var helper=new UnityEngine.UI.VertexHelper();populate.Invoke(graphic,new object[]{helper});return helper.currentVertCount;}
            int expanded=Count();town.layout.cityBounds=TownSouthLandscape.OriginalBounds;int original;
            try{original=Count();}finally{town.layout.cityBounds=TownSouthLandscape.ExpandedBounds;}
            TownForestBatch.Require(expanded-original==192,"Map river/bounds do not extend with the physical world.");
            var map=typeof(CityMapGraphic).GetMethod("Map",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var min=(Vector2)map.Invoke(graphic,new object[]{new Vector2(-288,-504)});var max=(Vector2)map.Invoke(graphic,new object[]{new Vector2(672,504)});
            TownForestBatch.Require(Vector2.Distance(min,new Vector2(-600,-480))<.001f&&Vector2.Distance(max,new Vector2(600,480))<.001f,"Map framing does not use the expanded world.");
            File.WriteAllText(Report+"/map.txt","PASS: real CityMapGraphic uses expanded 960 x 1008 m bounds, correct corner mapping, and 192 additional river UI vertices; original street graph retained.\n");
        }
        finally{UnityEngine.Object.DestroyImmediate(node);}
    }
    public static void Survey()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            Capture("Before");
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var layout=root.GetComponent<TownWorldPlaces>().layout;
                File.WriteAllLines(Report+"/survey.txt",new[]{"City bounds "+layout.cityBounds,"Reserved shop plot "+layout.reservedShopPlot}
                    .Concat(root.GetComponentsInChildren<Collider>(true).Where(c=>c.name=="World edge"||c.name=="River-cut town terrain")
                        .Select(c=>c.name+" "+TownResidentialPlacement.ColliderBounds(c)))
                    .Concat(new[]{"Pine","TreeWide","BushRound","BushWide","GrassClump"}.Select(id=>
                    {
                        var tree=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+id+".prefab");
                        return id+" "+tree.GetComponent<MeshFilter>().sharedMesh.bounds;
                    })));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/survey-result.txt","PASS: native southern boundary survey.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Failure("survey-result",error);}
    }
    private static void Capture(string suffix)
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        bool fog=RenderSettings.fog;
        try
        {
            // Planning views only: remove distance fog temporarily so the new boundary is legible. Never save the scene.
            RenderSettings.fog=false;
            TownFloristBatch.Render(scene,Pictures+"/South"+suffix+".png",new Vector3(-520,300,-490),new Vector3(-25,0,-130),300);
            TownFloristBatch.Render(scene,Pictures+"/Top"+suffix+".png",new Vector3(0,700,-200),new Vector3(0,0,-200),390);
        }
        finally{RenderSettings.fog=fog;}
        if(suffix=="After")TownFloristBatch.Render(scene,Pictures+"/ForestEdge.png",new Vector3(-15,9,-194),new Vector3(-65,5,-240));
    }
    private static void Failure(string stage,Exception error)
    {Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
}
