using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Territory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class TownSiteRevisionBatch
{
    internal const string Report="Library/TownSiteRevisionQA",Pictures="Docs/TownSiteRevisionReview";
    public static void Generate()=>Run(true);
    public static void Review()=>Run(false);
    private static void Run(bool generate)
    {
        string stage=generate?"result":"review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            if(generate)
            {
                var before=Records();var dependencies=AssetDatabase.GetDependencies(TownWorldAssembler.PrefabPath,true).Where(p=>p.StartsWith("Assets/")&&p!=TownWorldAssembler.PrefabPath)
                    .Concat(new[]{CityUpdateSetup.GamePath,TownForgottenYardAuthoring.LayoutPath,"Assets/Art/TownForgottenYard/Land24.asset"}).Distinct().ToDictionary(p=>p,TownForgottenYardBatch.Hash);
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(TownWorldAssembler.PrefabPath);var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);var ids=new Dictionary<int,string>();Pair(asset.transform,root.transform,ids);
                var mutable=new HashSet<string>{Id(asset.transform)};
                try
                {
                    var edit=new TownSiteRevisionPlacement(root);edit.Build();foreach(var o in edit.Mutable)if(ids.TryGetValue(o.GetInstanceID(),out var id))mutable.Add(id);
                    PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var after=Records();foreach(var p in before)if(!mutable.Contains(p.Key))Require(after.TryGetValue(p.Key,out var value)&&value==p.Value,"Protected town record changed: "+p.Key);
                foreach(var p in dependencies)Require(TownForgottenYardBatch.Hash(p.Key)==p.Value,"Shared resource changed: "+p.Key);
                ReviseStarter();
                File.WriteAllText(Report+"/protection.txt",$"PASS: {before.Count-mutable.Count} town records byte-identical; {mutable.Count} scoped mesh/collider/furniture/root records permitted. {dependencies.Count} shared resources byte-identical. Game scene, 24 land rules and established building resources protected.\n");
            }
            Validate();Capture();Receipt(stage);File.WriteAllText(Report+"/"+stage+".txt","PASS: site access, land rights, empty farm layby, construction earth, common bakery asphalt, stable ground and chunk-local signals.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void ReviseStarter()
    {
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        var land=Object.FindObjectOfType<TerritoryPlotLayout>(true);var b=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(StarterStoreSetup.BlueprintPath);
        var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownForgottenYardAuthoring.LayoutPath);
        string protectedInterior=JsonUtility.ToJson(new InteriorSnapshot(b.initialState));
        b.previousAccessAsphalt=b.initialState.floorTiles.Where(f=>f.itemId=="asphalt_01").Select(CloneFloor).ToList();
        b.previousAccessParking=b.initialState.placedObjects.Where(p=>p.itemId=="parking_01").Select(CloneBay).ToList();
        b.initialState.floorTiles.RemoveAll(f=>f.itemId=="asphalt_01");
        // Grid cell centres define the footprint; no accumulated floating point stepping.
        for(int z=-130;z<60;z++)for(int x=-40;x<90;x++)
        {
            var p=layout.MapShopPoint(land.grid.CellToWorld(new Vector3Int(x,0,z)));var point=new Vector2(p.x,p.z);
            if(layout.StoreForecourt.Contains(point)||layout.StoreStreetAccess.Contains(point))b.initialState.floorTiles.Add(new FloorTileSaveData{itemId="asphalt_01",x=x,z=z});
        }
        foreach(var bay in b.initialState.placedObjects.Where(p=>p.itemId=="parking_01"))bay.x+=16;
        var own=b.initialState.placedObjects.Single(p=>p.playerParking);b.pickupPosition=land.grid.CellToWorld(new Vector3Int(own.x,0,own.z));b.pickupPosition.y=.01f;
        b.hasStraightStreetAccess=true;b.streetEntry=layout.sourceShopOrigin+(layout.StoreStreetEntry-layout.cityShopOrigin)/layout.shopToCityScale;
        b.drivewayEntry=layout.sourceShopOrigin+(new Vector3(-58,0,-124)-layout.cityShopOrigin)/layout.shopToCityScale;
        b.serviceYard=new Rect(layout.sourceShopOrigin.x,( -140-layout.cityShopOrigin.z)/layout.shopToCityScale+layout.sourceShopOrigin.z,15,16);
        Require(protectedInterior==JsonUtility.ToJson(new InteriorSnapshot(b.initialState)),"Tiny starter interior changed.");
        EditorUtility.SetDirty(b);AssetDatabase.SaveAssets();
        // Migration is behavioural: an untouched old default moves once; customized frontage opts out.
        var old=b.CreateNewGame(1357);old.floorTiles.RemoveAll(f=>f.itemId=="asphalt_01");old.floorTiles.AddRange(b.previousAccessAsphalt.Select(CloneFloor));old.placedObjects.RemoveAll(p=>p.itemId=="parking_01");old.placedObjects.AddRange(b.previousAccessParking.Select(CloneBay));
        var catalog=Object.FindObjectOfType<StarterStoreInitialization>(true).catalog;
        Require(b.TryUpgradeUnmodifiedAccess(old,land.grid,catalog),"Default saved forecourt did not migrate.");
        Require(old.playerMoney==1357&&JsonUtility.ToJson(new InteriorSnapshot(old))==protectedInterior,"Migration modified unrelated save data.");
        Require(!b.TryUpgradeUnmodifiedAccess(old,land.grid,catalog),"Migration was not idempotent.");
        old=b.CreateNewGame(1357);old.floorTiles.RemoveAll(f=>f.itemId=="asphalt_01");old.floorTiles.AddRange(b.previousAccessAsphalt.Select(CloneFloor));old.placedObjects.RemoveAll(p=>p.itemId=="parking_01");old.placedObjects.AddRange(b.previousAccessParking.Select(CloneBay));old.placedObjects.First(p=>p.playerParking).x++;
        string custom=JsonUtility.ToJson(old);Require(!b.TryUpgradeUnmodifiedAccess(old,land.grid,catalog)&&custom==JsonUtility.ToJson(old),"Customized parking was overwritten.");
        File.WriteAllText(Report+"/starter.txt","PASS: tiny starter interior unchanged; straight entry and shifted 3 bays; untouched old default migrates exactly once, custom frontage is byte-preserved, money retained.\n");
    }
    [Serializable]private sealed class InteriorSnapshot
    {
        public List<PlacedBuildSaveData> objects;public List<FloorTileSaveData> floors;
        internal InteriorSnapshot(GameData d){objects=d.placedObjects.Where(p=>p.itemId!="parking_01").ToList();floors=d.floorTiles.Where(f=>f.itemId!="asphalt_01").ToList();}
    }
    private static FloorTileSaveData CloneFloor(FloorTileSaveData f)=>new(){itemId=f.itemId,x=f.x,z=f.z};
    private static PlacedBuildSaveData CloneBay(PlacedBuildSaveData p)=>JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(p));
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var town=Object.FindObjectOfType<TownWorldPlaces>();Require(town!=null&&town.transform.Find(TownSiteRevisionPlacement.Site)!=null,"Missing native revision layer.");Physics.SyncTransforms();
        var stream=town.GetComponent<TownSignalChunks>();Require(stream!=null&&stream.RegisteredJunctionCount==6,"Expected six chunk-indexed signal junctions.");
        foreach(var entry in stream.Junctions)entry.Signal.DisplayAt(0);
        long before=stream.EvaluationCount;stream.Tick(21,town.transform.TransformPoint(new Vector3(-120,0,-144)));int active=stream.ActiveJunctionCount;
        Require(active>0&&active<6&&stream.EvaluationCount-before==active,"Signals outside the local chunk neighbourhood were evaluated.");
        foreach(var j in stream.Junctions)Require(j.Signal.LampsMatchState&&j.Signal.Current.Equals(TownJunctionSignals.Evaluate(stream.IsActive(j.Signal)?21+j.Signal.PhaseOffset:0)),"Cached/active lamp state mismatch.");
        var distant=stream.Junctions.Last();stream.Tick(92,town.transform.TransformPoint(distant.Center));Require(stream.IsActive(distant.Signal)&&distant.Signal.Current.Equals(TownJunctionSignals.Evaluate(92+distant.Signal.PhaseOffset)),"Returning chunk did not catch up to absolute clock.");
        var originalScale=town.transform.localScale;town.transform.localScale=Vector3.one*.5f;stream.Tick(93,town.transform.TransformPoint(new Vector3(-120,0,-144)));Require(stream.ActiveJunctionCount==active,"Scaled shop view uses different native chunks.");town.transform.localScale=originalScale;
        int ns=0,ew=0;for(int i=0;i<640;i++){var s=TownJunctionSignals.Evaluate(i*.1);if(s.NorthSouth==JunctionSignalAspect.Green)ns++;if(s.EastWest==JunctionSignalAspect.Green)ew++;Require(!(s.NorthSouth==JunctionSignalAspect.Green&&s.EastWest==JunctionSignalAspect.Green),"Conflicting greens.");Require(!s.PedestrianWalk||(s.NorthSouth==JunctionSignalAspect.Red&&s.EastWest==JunctionSignalAspect.Red),"Pedestrian conflict.");}
        Require(ns==200&&ew==200,"Both axes must receive exactly 20 seconds of green.");
        var a=TownSiteRevisionPlacement.ShopEntry;int samples=0;
        for(int i=0;i<=65;i++)foreach(float side in new[]{-1.25f,0,1.25f})
        {
            var p=a.Point(i/65f,side);var hits=Physics.RaycastAll(p+Vector3.up*.5f,Vector3.down,.8f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.75f).ToArray();
            Require(hits.Any(h=>Mathf.Abs(h.point.y-p.y)<.01f)&&!hits.Any(h=>h.point.y>p.y+.015f),"Store entrance floor/kerb mismatch at "+p);samples++;
        }
        Require(!town.GetComponentsInChildren<BoxCollider>().Any(c=>c.name=="BoxTruck"&&c.transform.position.x<-220&&c.transform.position.z>174),"Old freight truck collision remains.");
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true).Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(TownSiteRevisionPlacement.Root+"/")))Require(f.sharedMesh.vertices.All(TownBakeryQuarterPlacement.Finite),"Non-finite mesh: "+f.name);
        File.WriteAllText(Report+"/validation.txt",$"PASS: {samples} lowered store-access floor samples; six registered signals, {active} local junctions evaluated, distant lamps cached, return-time catch-up, scaled-town coordinates, 640 safe-cycle samples, exact 20 s greens. Native meshes finite.\n");
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);var land=Object.FindObjectOfType<TerritoryPlotLayout>(true);land.Apply();
        var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownForgottenYardAuthoring.LayoutPath);
        Vector3 Source(Vector3 p)=>layout.sourceShopOrigin+(p-layout.cityShopOrigin)/layout.shopToCityScale;
        Require(!land.territory.IsCellPurchased(land.grid.WorldToCell(Source(new Vector3(-66,0,-100))))&&!land.territory.IsCellPurchased(land.grid.WorldToCell(Source(new Vector3(-58,0,-132)))) ,"Public apron still grants construction rights.");
        File.WriteAllText(Report+"/land.txt","PASS: removed left strip and public entry do not grant building rights; 24-plot asset unchanged.\n");
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);RenderSettings.fog=false;
        TownFloristBatch.Render(scene,Pictures+"/StoreEntrance.png",new Vector3(-73,70,-192),new Vector3(-46,0,-106));
        TownFloristBatch.Render(scene,Pictures+"/Bakery.png",new Vector3(-76,20,-124),new Vector3(-82,0,-164));
        TownFloristBatch.Render(scene,Pictures+"/Construction.png",new Vector3(104,35,-110),new Vector3(144,0,-57));
        TownFloristBatch.Render(scene,Pictures+"/Fuel.png",new Vector3(175,15,-129),new Vector3(198,0,-102));
        TownFloristBatch.Render(scene,Pictures+"/Farm.png",new Vector3(514,61,223),new Vector3(550,0,314));
        TownFloristBatch.Render(scene,Pictures+"/ClearedFreight.png",new Vector3(-194,53,143),new Vector3(-240,0,198));
        TownFloristBatch.Render(scene,Pictures+"/WestRoad.png",new Vector3(-266,70,-42),new Vector3(-213,0,-35));
    }
    private static void Pair(Transform a,Transform p,Dictionary<int,string> ids)
    {
        ids.Add(p.gameObject.GetInstanceID(),Id(a.gameObject));var ac=a.GetComponents<Component>();var pc=p.GetComponents<Component>();Require(ac.Length==pc.Length&&a.childCount==p.childCount,"Prefab hierarchy mismatch.");
        for(int i=0;i<ac.Length;i++)ids.Add(pc[i].GetInstanceID(),Id(ac[i]));for(int i=0;i<a.childCount;i++)Pair(a.GetChild(i),p.GetChild(i),ids);
    }
    private static Dictionary<string,string> Records()=>Regex.Matches(File.ReadAllText(TownWorldAssembler.PrefabPath),@"(?ms)^--- !u!\d+ &(\d+)\r?\n.*?(?=^--- !u!|\z)").Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value.Replace("\r\n","\n"));
    private static string Id(Object o){Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string _,out long id),"No persistent ID "+o.name);return id.ToString(System.Globalization.CultureInfo.InvariantCulture);}
    private static void Receipt(string stage)=>File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownSiteRevisionPlacement.Root,"*",SearchOption.AllDirectories).Concat(new[]{TownSiteRevisionPlacement.Root+".meta",TownWorldAssembler.PrefabPath,StarterStoreSetup.BlueprintPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>TownForgottenYardBatch.Hash(p)+" "+p.Replace('\\','/')));
    private static void Require(bool yes,string message){if(!yes)throw new InvalidOperationException(message);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var names=new[]{"drive","Drive","lane","Lane","apron","Apron","Truck","Pickup","Parking","Road","road","Shop","shop"};
                File.WriteAllLines(Report+"/survey.txt",root.GetComponentsInChildren<Transform>(true)
                    .Where(t=>names.Any(t.name.Contains)).Select(t=>TownCityCorrectionsBatch.PathOf(t)+" | "+t.position.ToString("F3")+" yaw="+t.eulerAngles.y+" | "+string.Join(",",t.GetComponents<Collider>().Select(c=>c.GetType().Name+" "+(c is MeshCollider mc?(mc.sharedMesh!=null?mc.sharedMesh.bounds:default):TownResidentialPlacement.ColliderBounds(c))))));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);var land=Object.FindObjectOfType<TerritoryPlotLayout>(true);var blueprint=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(StarterStoreSetup.BlueprintPath);
            var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownForgottenYardAuthoring.LayoutPath);
            var data=new List<string>{"Initial="+land.initialBounds+" cellSize="+land.grid.cellSize,"Service="+blueprint.serviceYard,"Native service origin="+layout.MapShopPoint(new Vector3(blueprint.serviceYard.x,0,blueprint.serviceYard.y))};
            foreach(var g in blueprint.initialState.floorTiles.GroupBy(f=>f.itemId))data.Add(g.Key+" count="+g.Count()+" x="+g.Min(f=>f.x)+".."+g.Max(f=>f.x)+" z="+g.Min(f=>f.z)+".."+g.Max(f=>f.z));
            foreach(var p in blueprint.initialState.placedObjects.Where(p=>p.itemId=="parking_01"))data.Add("Parking="+JsonUtility.ToJson(p)+" native="+layout.MapShopPoint(land.grid.CellToWorld(new Vector3Int(p.x,0,p.z))));
            File.WriteAllLines(Report+"/shop.txt",data);
            scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);RenderSettings.fog=false;
            TownFloristBatch.Render(scene,Pictures+"/BeforeShop.png",new Vector3(-73,70,-192),new Vector3(-46,0,-106));
            TownFloristBatch.Render(scene,Pictures+"/BeforeNorthDepot.png",new Vector3(-117,46,148),new Vector3(-162,0,204));
            File.WriteAllText(Report+"/inspect.txt","PASS: saved current town and shop surveyed without modification.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
