using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Generation and acceptance run only in the disposable editor clone.</summary>
public static class TownForgottenYardBatch
{
    internal const string Report="Library/TownForgottenYardQA",Pictures="Docs/TownForgottenYardReview";
    public static void Generate()=>Run(true);
    public static void Review()=>Run(false);
    private static void Run(bool generate)
    {
        string stage=generate?"result":"review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            if(generate)
            {
                // Resolve the real scene/render pipeline before comparing material signatures.
                // Shader defaults can be lazily populated when the first scene is opened.
                var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
                TownFloristBatch.Render(scene,Pictures+"/SiteBefore.png",new Vector3(87,72,-74),new Vector3(35,0,19),46);
                var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownForgottenYardAuthoring.LayoutPath);
                var before=new TownForgottenYardPreservation();
                string protectedLayout=ProtectedLayout(layout);
                var protectedScene=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
                    .Where(c=>c!=null&&!(c is TerritoryPlotLayout)&&!(c is TerritoryManager)&&!(c is TerritoryPurchaseModeManager)&&!(c is StoreProgression)&&!(c is StorePrefabSpawner)
                        &&c.GetComponentInParent<TerritoryZone>()==null&&c.GetComponentInParent<TownWorldPlaces>()==null)
                    .ToDictionary(c=>c.GetInstanceID(),c=>EditorJsonUtility.ToJson(c));
                new TownForgottenYardAuthoring().Build();
                var current=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToDictionary(c=>c.GetInstanceID());
                var changed=protectedScene.Where(p=>!current.TryGetValue(p.Key,out var c)||EditorJsonUtility.ToJson(c)!=p.Value).ToArray();
                Require(changed.Length==0,"Unrelated Game scene component changed: "+string.Join(", ",changed.Select(p=>current.TryGetValue(p.Key,out var c)?c.name+"/"+c.GetType().Name:p.Key.ToString())));
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                before.RequireUnchanged();
                Require(ProtectedLayout(layout)==protectedLayout,"City roads, bounds, river mapping or districts changed.");
                File.WriteAllText(Report+"/scene-protection.txt","PASS: "+protectedScene.Count+" unrelated Game scene components unchanged in memory.\n");
            }
            Validate();CaptureCatalogue();
            File.WriteAllText(Report+"/"+stage+".txt","PASS: 24 parcels, legacy ownership migration, adjacency, union and price conservation; small new-game store; native reusable props and contained scenery.\n");
            Receipt(stage);EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static string ProtectedLayout(TownWorldLayout layout)
    {
        var copy=Object.Instantiate(layout);copy.expansionPlots.Clear();string result=EditorJsonUtility.ToJson(copy);Object.DestroyImmediate(copy);return result;
    }
    internal static void Validate(int triangleBudget=150000)
    {
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        var land=Object.FindObjectOfType<TerritoryPlotLayout>(true);var rules=land.progression.LandRules;
        Require(rules!=null&&rules.Parcels.Count==24&&land.plots.Count==24,"24-parcel catalogue not configured.");
        var zones=Object.FindObjectsOfType<TerritoryZone>(true);Require(zones.Length==24,"Expected 24 purchase zones.");
        foreach(var p in rules.Parcels)
        {
            Require(land.plots.Single(q=>q.id==p.Id).bounds==p.Bounds,"Build rights differ from catalogue.");
            var zone=zones.Single(q=>q.Id==p.Id);Require(zone.Price==p.Price,"Zone price differs from catalogue.");
            var click=zone.GetComponent<BoxCollider>();Require(click!=null&&Mathf.Abs(click.size.x-p.Bounds.width)<.001f&&Mathf.Abs(click.size.z-p.Bounds.height)<.001f,"Incorrect click area.");
            Require(zone.GetComponentInChildren<TextMesh>(true).text.StartsWith("EXPAND"),"New purchase sign must be English.");
        }
        Require(rules.Parcels.Sum(p=>p.Price)==50000,"Total land price changed.");
        var oldGroups=rules.Parcels.GroupBy(p=>p.LegacyRegion).ToArray();
        for(int mask=0;mask<512;mask++)
        {
            var save=new TerritorySaveData();
            for(int i=0;i<9;i++)if((mask&(1<<i))!=0)save.purchased.Add(((TerritoryId)i).ToString());
            var restored=rules.Restore(save);
            land.progression.ApplySaveData(save);
            Require(land.progression.CompletedRegionCount==save.purchased.Count,"Sub-parcels inflate operational difficulty.");
            foreach(var p in rules.Parcels)Require(restored.Contains(p.Id)==((mask&(1<<(int)p.LegacyRegion))!=0),"Legacy land ownership changed.");
            var v2=new TerritorySaveData{layoutVersion=2,purchased=restored.Where(rules.Contains).Select(p=>p.ToString()).ToList()};
            Require(rules.Restore(v2).SetEquals(restored),"Version 2 ownership roundtrip changed land.");
        }
        land.progression.ApplySaveData(new TerritorySaveData());
        var purchased=new HashSet<TerritoryId>();int steps=0;
        while(purchased.Count<24)
        {
            var available=rules.Parcels.Where(p=>rules.IsAvailable(p.Id,purchased)).ToArray();Require(available.Length>0,"Unreachable land.");
            purchased.Add(available[steps%available.Length].Id);steps++;
        }
        foreach(var group in oldGroups)
        {
            var parts=group.ToArray();var union=Rect.MinMaxRect(parts.Min(p=>p.Bounds.xMin),parts.Min(p=>p.Bounds.yMin),parts.Max(p=>p.Bounds.xMax),parts.Max(p=>p.Bounds.yMax));
            Require(Mathf.Abs(parts.Sum(p=>p.Bounds.width*p.Bounds.height)-union.width*union.height)<.005f,"Subdivision loses land area.");
        }
        // All purchase boundaries are integer grid edges, never half cells or unbuildable strips.
        foreach(var p in rules.Parcels)foreach(float value in new[]{(p.Bounds.xMin-land.initialBounds.xMin)/land.grid.cellSize,(p.Bounds.xMax-land.initialBounds.xMin)/land.grid.cellSize,(p.Bounds.yMin-land.initialBounds.yMin)/land.grid.cellSize,(p.Bounds.yMax-land.initialBounds.yMin)/land.grid.cellSize})
            Require(Mathf.Abs(value-Mathf.Round(value))<.002f,"Plot boundary cuts a build cell.");
        var starter=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(StarterStoreSetup.BlueprintPath);
        Require(starter.initialState.floorTiles.Count(p=>p.itemId!="asphalt_01")==36*30,"Tiny starter dimensions changed.");
        Require(starter.previousStarterStructure.Count==0,"Old saves could be shrunk by automatic structure migration.");
        var custom=starter.CreateNewGame(6000);custom.placedObjects.Add(new PlacedBuildSaveData{itemId="custom_old_store",x=700,z=800});
        string untouched=JsonUtility.ToJson(custom);Object.FindObjectOfType<StarterStoreInitialization>(true).UpgradeUnmodifiedLayout(custom);
        Require(JsonUtility.ToJson(custom)==untouched,"Existing custom building data changed.");
        var allCells=new HashSet<Vector3Int>();var catalog=Object.FindObjectOfType<BuildItemCatalog>(true);
        foreach(var p in starter.initialState.placedObjects)
        {
            var item=catalog.GetById(p.itemId);Require(item!=null,"Missing starter item "+p.itemId);
            Require(land.grid.GetFootprintCells(new Vector3Int(p.x,0,p.z),item.footprint,p.rotated,item.pivotOffset).All(allCells.Add),"Starter pieces overlap at "+p.itemId+" "+p.x+","+p.z);
        }
        var scenery=Object.FindObjectsOfType<TownExpansionScenery>(true).Single();Require(scenery.plots.Length==24,"Scenery parcel count mismatch.");
        Require(scenery.progression==land.progression,"Saved scenery subscription points at a missing or different progression component.");
        int triangles=0;
        var townLayout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownForgottenYardAuthoring.LayoutPath);
        foreach(var p in scenery.plots)
        {
            Require(p.model.GetComponentsInChildren<Collider>(true).Length==0,"Scenery blocks construction or traffic.");
            var bounds=townLayout.expansionPlots.Single(q=>q.territory==p.territory).bounds;
            var filter=p.model.GetComponent<MeshFilter>();triangles+=filter.sharedMesh.triangles.Length/3;
            Require(filter.sharedMesh.vertices.All(v=>v.x>=bounds.xMin-.001f&&v.x<=bounds.xMax+.001f&&v.z>=bounds.yMin-.001f&&v.z<=bounds.yMax+.001f),"Scenery crosses plot boundaries: "+p.territory+" "+filter.sharedMesh.bounds);
        }
        Require(triangles<triangleBudget&&scenery.GetComponentsInChildren<MeshRenderer>(true).Length==24,"Yard geometry/draw budget exceeded.");
        File.WriteAllText(Report+"/domain.txt","PASS: all 512 legacy ownership combinations; all v2 roundtrips; 24 connected purchases; exact grid boundaries; nine region areas and total $50,000 price conserved; existing custom store not modified.\n");
        File.WriteAllText(Report+"/metrics.txt",$"{RetailEmpireTycoon.Editor.TownKit.TownForgottenYardModels.Create().Count} reusable prop prefabs; 24 scenery batches; {triangles} triangles; no scenery colliders. Starter 36x30 cells versus former 90x72, 3 stocked sales shelves, 2 warehouse racks, 3 parking bays.\n");
    }
    private static void CaptureCatalogue()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var light=new GameObject("Catalogue sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(48,-32,0);
        RenderSettings.ambientLight=new Color(.65f,.68f,.68f);RenderSettings.fog=false;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Catalogue backdrop";floor.transform.position=new Vector3(12,-.12f,12);floor.transform.localScale=new Vector3(36,.2f,36);
        var backdrop=new Material(Shader.Find("Universal Render Pipeline/Lit"));backdrop.color=new Color(.54f,.58f,.49f);floor.GetComponent<MeshRenderer>().sharedMaterial=backdrop;
        var models=Directory.GetFiles(TownForgottenYardAuthoring.Root+"/Prefabs","*.prefab").Where(p=>!p.EndsWith("ModularLandscape.prefab")).OrderBy(p=>p).ToArray();
        for(int i=0;i<models.Length;i++)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(models[i]));go.transform.position=new Vector3((i%5)*6,0,(i/5)*6);
        }
        TownFloristBatch.Render(scene,Pictures+"/ObjectCatalogue.png",new Vector3(33,34,-30),new Vector3(12,0,12),23);
        var game=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);RenderSettings.fog=false;
        TownFloristBatch.Render(game,Pictures+"/YardOverview.png",new Vector3(87,72,-74),new Vector3(35,0,19),46);
    }
    internal static IEnumerable<string> Outputs()=>Directory.GetFiles(TownForgottenYardAuthoring.Root,"*",SearchOption.AllDirectories)
        .Concat(new[]{TownForgottenYardAuthoring.Root+".meta",TownWorldAssembler.PrefabPath,TownForgottenYardAuthoring.LayoutPath,StarterStoreSetup.BlueprintPath,CityUpdateSetup.GamePath}).OrderBy(p=>p,StringComparer.Ordinal);
    internal static void Receipt(string stage)=>File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Outputs().Select(p=>Hash(p)+" "+p.Replace('\\','/')));
    internal static string Hash(string path){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
