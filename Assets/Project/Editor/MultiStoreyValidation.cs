using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Real placement, routes, saves and parking tests in a disposable scene, without SaveManager.</summary>
[InitializeOnLoad]
public static class MultiStoreyValidation
{
    private const string Pending="MultiStoreyQA.Pending";
    private const string Folder="Library/MultiStoreyQA";
    private static IEnumerator checks;
    private static readonly StringBuilder report=new();
    private static double deadline;
    static MultiStoreyValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Folder);
        InspectOriginalScale();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Construction under test");
        var grid=root.AddComponent<GridSystem>();grid.cellSize=ShopProportions.CellSize;
        var territory=root.AddComponent<RetailEmpireTycoon.Territory.TerritoryManager>();
        territory.AddPurchasedRect(new Vector3Int(-65,0,-50),new Vector3Int(65,0,65));
        var floors=root.AddComponent<FloorPainter>();floors.grid=grid;floors.territory=territory;
        floors.floorRoot=new GameObject("Surfaces").transform;floors.floorRoot.SetParent(root.transform);
        var build=root.AddComponent<BuildController>();build.grid=grid;build.territory=territory;build.floorPainter=floors;
        build.inventory=root.AddComponent<BuildInventory>();
        var preview=new GameObject("Preview").AddComponent<BuildPreview>();preview.transform.SetParent(root.transform);build.preview=preview;
        CameraBuildExperienceSetup.ConfigurePreview(preview);
        var catalog=root.AddComponent<BuildItemCatalog>();
        var items=new List<BuildItemData>{Item("Assets/Prefabs/Structures/Wall.asset"),Item("Assets/Prefabs/City/parking_01.asset"),Item("Assets/Prefabs/StoreUpgrade/floor_cream.asset"),Item("Assets/Prefabs/City/asphalt_01.asset")};
        typeof(BuildItemCatalog).GetField("items",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(catalog,items);
        var editor=root.AddComponent<BuildEditingController>();editor.building=build;editor.ReadPointerInput=false;
        var camera=new GameObject("Camera",typeof(Camera),typeof(MainCamera));camera.tag="MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(5,10,-9),Quaternion.Euler(50,-15,0));build.worldCamera=camera.GetComponent<Camera>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Project/Editor/MultiStoreySandbox.unity");
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    public static void RunGame()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SessionState.SetBool("MultiStoreyQA.Game",true);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+240;checks=SessionState.GetBool("MultiStoreyQA.Game",false)?CheckGame():Check();}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Construction QA timed out.");
            if(checks.MoveNext())return;
            SessionState.SetBool(Pending,false);File.WriteAllText(Folder+(SessionState.GetBool("MultiStoreyQA.Game",false)?"/game-result.txt":"/result.txt"),report.ToString()+"PASS all construction integration checks. No player save written.\n");SessionState.SetBool("MultiStoreyQA.Game",false);EditorApplication.Exit(0);
        }
        catch(Exception error)
        {SessionState.SetBool(Pending,false);Debug.LogException(error);File.WriteAllText(Folder+(SessionState.GetBool("MultiStoreyQA.Game",false)?"/game-result.txt":"/result.txt"),report+"\nFAIL "+error);SessionState.SetBool("MultiStoreyQA.Game",false);EditorApplication.Exit(1);}
    }
    private static IEnumerator Check()
    {
        yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
        var build=Object.FindObjectOfType<BuildController>();var grid=build.grid;var floors=build.floorPainter;
        var inventory=build.inventory;var catalog=build.GetComponent<BuildItemCatalog>();
        var tile=Item("Assets/Prefabs/StoreUpgrade/floor_cream.asset");
        var asphalt=Item("Assets/Prefabs/City/asphalt_01.asset");
        var wall=Item("Assets/Prefabs/Structures/Wall.asset");
        for(int floor=0;floor<=1;floor++)
        {
            var cell=new Vector3Int(-5,floor,7);Require(grid.WorldToCell(grid.CellToWorld(cell))==cell,"Grid loses storey.");
        }
        Require(floors.GetRectCells(new Vector3Int(0,1,0),new Vector3Int(2,1,3)).All(c=>c.y==1),"Floor brush flattened.");
        var transportItems=Resources.LoadAll<BuildItemData>("MultiStorey").OrderBy(i=>i.id).ToArray();
        Require(transportItems.Length==3&&transportItems.All(i=>i.icon!=null&&i.prefab!=null),"Transport catalogue incomplete.");
        foreach(var item in transportItems)
        {
            Require(item.prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c!=null),"Missing script in "+item.id);
            if(item.storeyConnection!=StoreyConnectionKind.Stairs)Require(item.prefab.GetComponent<StoreyTransportVisual>()!=null,"Transport animation missing: "+item.id);
        }
        var anchors=new Dictionary<BuildItemData,Vector3Int>();var openings=new HashSet<Vector3Int>();
        for(int i=0;i<transportItems.Length;i++)
        {
            var anchor=new Vector3Int(3+i*12,0,8);anchors.Add(transportItems[i],anchor);
            foreach(var cell in grid.GetFootprintCells(anchor+Vector3Int.up,transportItems[i].footprint,false))openings.Add(cell);
        }
        floors.PaintCells(floors.GetRectCells(new Vector3Int(0,0,0),new Vector3Int(45,0,35)),tile);
        var upstairs=floors.GetRectCells(new Vector3Int(0,1,0),new Vector3Int(45,1,35)).Where(c=>!openings.Contains(c)).ToList();
        inventory.Add(tile,upstairs.Count+30);build.EnterBuildMode(tile);
        int stock=inventory.GetCount(tile);Require(build.TryPaintCells(upstairs),"Cannot paint second floor.");
        Require(inventory.GetCount(tile)==stock-upstairs.Count,"Brush inventory accounting.");
        var roof=floors.GetRectCells(new Vector3Int(1,2,1),new Vector3Int(3,2,3));Require(build.TryPaintCells(roof),"Cannot paint ceiling from flooring material.");
        Require(!build.TryPaintCells(new List<Vector3Int>{new(1000,1,0)}),"Brush accepts unowned land.");
        var unsafeCell=new Vector3Int(-20,1,10);inventory.Add(wall,2);build.EnterBuildMode(wall);
        Require(!build.CanPlaceAt(unsafeCell).ok,"Floating upper-storey furniture allowed.");
        foreach(var pair in anchors)
        {
            inventory.Add(pair.Key,1);build.EnterBuildMode(pair.Key);
            var validation=build.CanPlaceAt(pair.Value);Require(validation.ok,pair.Key.id+": "+validation.message);
            Require(build.TryPlaceAt(pair.Value),"Failed transport placement: "+pair.Key.id);
            Require(openings.Any(grid.IsOccupied),"Upper opening not reserved.");
        }
        yield return null;Physics.SyncTransforms();
        var transports=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item!=null&&p.item.storeyConnection!=StoreyConnectionKind.None).ToArray();
        var navigation=new StoreNavigation(grid,build.territory);navigation.RefreshDoorways(transports);
        foreach(var transport in transports)
        {
            var link=new StoreyLink(grid,transport);var lower=grid.CellToWorld(link.Lower)+Vector3.up*StoreNavigation.FloorOffset(link.Lower.y);var upper=grid.CellToWorld(link.Upper)+Vector3.up*StoreNavigation.FloorOffset(link.Upper.y);
            Require(!floors.RemoveCells(new List<Vector3Int>{link.Upper},inventory),"Upper landing can be erased.");
            Require(!floors.AreCellsValid(StoreyLink.OpeningCells(grid,transport).ToList(),tile),"Opening can be sealed.");
            Require(navigation.TryPath(lower,upper,out var path),"No upward route: "+transport.item.id);
            Require(path.Any(p=>p.y>.8f),"Route stays at ground level.");
            var actor=new GameObject("Passenger "+transport.item.id).AddComponent<ShopCharacter>();actor.transform.position=lower;actor.Initialize(navigation);
            Require(actor.MoveTo(upper),"Actor refused upstairs destination.");
            float travelDeadline=Time.realtimeSinceStartup+25;
            while(!actor.Arrived&&Time.realtimeSinceStartup<travelDeadline){actor.Advance(.05f);yield return null;}
            Require(actor.Arrived&&Vector3.Distance(actor.transform.position,upper)<.02f,"Passenger failed ascent: "+transport.item.id);
            Require(actor.MoveTo(lower),"Actor refused downward route.");
            travelDeadline=Time.realtimeSinceStartup+25;
            while(!actor.Arrived&&Time.realtimeSinceStartup<travelDeadline){actor.Advance(.05f);yield return null;}
            Require(actor.Arrived&&Vector3.Distance(actor.transform.position,lower)<.02f,"Passenger failed descent: "+transport.item.id);
            Object.Destroy(actor.gameObject);
        }
        report.AppendLine("PASS all three transports: real placement, protected openings/landings, continuous ascent and descent by ShopCharacter.");
        inventory.Add(wall,2);build.EnterBuildMode(wall);Require(build.TryPlaceAt(new Vector3Int(39,0,27)),"Ground wall placement failed.");
        build.EnterBuildMode(wall);Require(build.TryPlaceAt(new Vector3Int(39,1,27)),"Same XZ occupied on a different floor.");
        build.Storeys.SelectFloor(1);Require(build.Storeys.ActiveFloor==1&&Mathf.Abs(build.worldCamera.GetComponent<MainCamera>().FocusPoint.y-GridSystem.StoreyHeight)<.001f,"Floor selector/camera not elevated.");
        build.Storeys.SetCeiling(true);Require(build.Storeys.PaintCeiling,"Ceiling brush mode failed.");
        var data=new GameData{placedObjects=build.BuildPlacedSaveData(),floorTiles=floors.BuildSaveData()};
        string json=JsonUtility.ToJson(data);var roundtrip=JsonUtility.FromJson<GameData>(json);
        Require(roundtrip.placedObjects.Any(p=>p.floor==1)&&roundtrip.floorTiles.Any(p=>p.floor==2),"Save loses upper storeys.");
        roundtrip.usesModularStore=true;
        var starter=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>("Assets/Prefabs/StarterStore/StarterStore.asset");
        string protectedData=JsonUtility.ToJson(roundtrip);
        Require(!starter.TryUpgradeUnmodifiedStructure(roundtrip,grid,catalog)&&!starter.TryUpgradeUnmodifiedAccess(roundtrip,grid,catalog)
            &&!starter.TryUpgradeUnmodifiedFrontage(roundtrip,grid,catalog)&&JsonUtility.ToJson(roundtrip)==protectedData,"A legacy migration changed multi-storey construction.");
        floors.ApplySaveData(roundtrip.floorTiles,catalog);build.ApplyPlacedSaveData(roundtrip.placedObjects,catalog);yield return null;
        Require(Object.FindObjectsOfType<PlacedObject>().Count(p=>p.anchorCell.y==1)==1,"Saved upper furniture missing or duplicated.");
        report.AppendLine("PASS independent floor occupancy, shared ceiling material, floor/ceiling save roundtrip and camera storey selection.");
        var bay=Item("Assets/Prefabs/City/parking_01.asset");
        floors.PaintCells(floors.GetRectCells(new Vector3Int(-55,0,-35),new Vector3Int(-5,0,-5)),asphalt);
        var editor=build.GetComponent<BuildEditingController>();
        for(int facing=0;facing<4;facing++)
        {
            inventory.Add(bay,1);build.EnterBuildMode(bay);build.RotateSelected(facing);
            var anchor=new Vector3Int(-45,0,-25);
            Require(build.TryPlaceAt(anchor),"New parking placement failed in orientation "+facing);
            var placed=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==bay);
            var bounds=CityUpdateSetup.Measure(placed.gameObject);var cells=placed.occupiedCells;
            Require(bounds.min.x>=cells.Min(c=>c.x)*grid.cellSize-.001f&&bounds.max.x<=(cells.Max(c=>c.x)+1)*grid.cellSize+.001f
                &&bounds.min.z>=cells.Min(c=>c.z)*grid.cellSize-.001f&&bounds.max.z<=(cells.Max(c=>c.z)+1)*grid.cellSize+.001f,"Parking paint exceeds occupied cells.");
            editor.Begin();editor.Select(placed);Require(editor.DeleteSelected(),"Empty customer bay cannot return to inventory.");editor.Finish();yield return null;
        }
        report.AppendLine("PASS parking placement in all four orientations: paint contained by occupancy, asphalt approaches, safe return to inventory.");
        var current=build.BuildPlacedSaveData();current.Add(new PlacedBuildSaveData{itemId=bay.id,x=-50,z=-30,playerParking=true});
        build.ApplyPlacedSaveData(current,catalog);yield return null;
        var parking=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.playerParking);
        Require(parking.parkingModuleVersion==bay.parkingGeometryVersion && parking.occupiedCells.Count==bay.footprint.x*bay.footprint.y,"Free legacy bay was not safely upgraded.");
        current.Add(new PlacedBuildSaveData{itemId=wall.id,x=-43,z=-30,wallModuleVersion=BuildItemData.CurrentWallModuleVersion});
        build.ApplyPlacedSaveData(current,catalog);yield return null;
        parking=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.playerParking);
        Require(parking.occupiedCells.Count==bay.previousParkingFootprint.x*bay.previousParkingFootprint.y,"Blocked legacy bay overwrote existing construction while enlarging.");
        editor.Begin();build.Storeys.SelectFloor(0);editor.Select(Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.playerParking));
        var target=new Vector3Int(-30,0,-27);Require(editor.PreviewAt(target).ok,"Private parking candidate invalid.");
        editor.CancelSelection();Require(parking.anchorCell.x==-50,"Cancel mutated private bay.");
        editor.Select(parking);editor.PreviewAt(target);Require(editor.Confirm(),"Private parking cannot be moved.");
        Require(parking.parkingModuleVersion==bay.parkingGeometryVersion && parking.occupiedCells.Count==bay.footprint.x*bay.footprint.y,"Moved bay not upgraded.");
        Require(Vector3.Distance(CityUpdateSetup.Measure(parking.gameObject).size,bay.placementBounds.size)<.001f,"Moved bay paint does not match its new footprint.");
        Require(parking.playerParking&&build.BuildPlacedSaveData().Single(p=>p.playerParking).x==target.x,"Private bay identity lost.");
        editor.Select(parking);Require(!editor.DeleteSelected(),"Private return point can be deleted.");editor.Finish();
        report.AppendLine("PASS legacy parking preserved, explicit private-bay selection, cancel, resize on valid move, saved identity, deletion protection.");
        CheckFacade(build);
        Capture(build);
        File.WriteAllText(Folder+"/roundtrip.json",json);
    }
    private static void CheckFacade(BuildController build)
    {
        var world=new GameObject("City facade QA").AddComponent<CityWorld>();
        world.facadeRoot=new GameObject("Facade").transform;world.facadeRoot.SetParent(world.transform);
        var places=world.gameObject.AddComponent<TownWorldPlaces>();places.layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>("Assets/Prefabs/TownWorld/TownLayout.asset");world.town=places;
        var trip=new GameObject("Trip QA").AddComponent<CityTrip>();
        typeof(CityWorld).GetField("trip",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(world,trip);
        var source=Object.FindObjectsOfType<StoreWallOccluder>().First();var before=source.WorldBounds;
        source.gameObject.SetActive(false);var inactive=source.WorldBounds;
        Require(Vector3.Distance(before.size,inactive.size)<.0001f,"Suspending store changes wall dimensions.");source.gameObject.SetActive(true);
        typeof(CityWorld).GetMethod("SyncFacade",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(world,new object[]{SceneManager.GetActiveScene()});
        var filter=source.GetComponentInChildren<MeshFilter>();
        var expected=places.layout.MapShopPoint(filter.transform.position);
        var copy=world.facadeRoot.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Store facade "+filter.name)
            .OrderBy(f=>Vector3.Distance(f.transform.position,expected)).First();
        Require(Vector3.Distance(copy.transform.position,places.layout.MapShopPoint(filter.transform.position))<.001f,"City store origin differs from town mapping.");
        Require(Vector3.Distance(copy.transform.lossyScale,Vector3.Scale(filter.transform.lossyScale,places.layout.ShopVisualScale))<.001f,"City store shrinks with store bounds.");
        report.AppendLine("PASS facade uses fixed town transform and stable inactive mesh bounds, without fit-to-28m scaling.");
        Object.Destroy(world.gameObject);Object.Destroy(trip.gameObject);
    }
    private static IEnumerator CheckGame()
    {
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer in game sandbox.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var build=starter.building;var trip=starter.cityTrip;trip.orders.save=null;
        var initial=starter.CreateNewGame(12000);
        var progression=Object.FindObjectOfType<StoreProgression>();progression.ApplySaveData(initial.territory);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);
        starter.ApplyNewGameBuildings(initial);for(int i=0;i<4;i++)yield return null;
        Object.FindObjectOfType<StoreOperations>().SetOpen(false);
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopWindow>(true);shop.gameObject.SetActive(true);shop.OpenCategory_Structures();
        yield return null;
        var labels=shop.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text).ToArray();
        foreach(var item in Resources.LoadAll<BuildItemData>("MultiStorey"))Require(labels.Contains(RetailEmpireTycoon.UI.Shop.ShopText.Item(item)),"New transport absent from real shop: "+item.id);
        Canvas.ForceUpdateCanvases();shop.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=0;
        float showUntil=Time.realtimeSinceStartup+.65f;while(Time.realtimeSinceStartup<showUntil)yield return null;
        ScreenCapture.CaptureScreenshot(Folder+"/TransportCatalogue.png");for(int i=0;i<4;i++)yield return null;shop.Close();
        var editing=build.GetComponent<BuildEditingController>();
        var inventoryWindow=Object.FindObjectOfType<RetailEmpireTycoon.UI.Windows.BuildInventoryWindow>(true);inventoryWindow.gameObject.SetActive(true);yield return null;
        var parkingButton=inventoryWindow.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text==RetailEmpireTycoon.UI.Shop.ShopText.Get("Личная парковка","Private parking"));
        parkingButton.onClick.Invoke();yield return null;
        Require(editing.Selected!=null&&editing.Selected.playerParking,"Private parking shortcut not connected.");
        // The starter apron is public land by design. Prepare a real owned asphalt patch,
        // rather than granting construction rights to the old public parking area.
        var owned=build.territory.PurchasedRects[0];
        build.floorPainter.PaintCells(build.floorPainter.GetRectCells(
            new Vector3Int(owned.max.x-25,0,owned.min.z+3),new Vector3Int(owned.max.x-2,0,owned.min.z+26)),
            Item("Assets/Prefabs/City/asphalt_01.asset"));
        var parking=editing.Selected;bool moved=false;
        foreach(var rect in build.territory.PurchasedRects)
        {
            for(int x=rect.min.x;x<=rect.max.x&&!moved;x++)for(int z=rect.min.z;z<=rect.max.z&&!moved;z++)
            {
                var candidate=new Vector3Int(x,0,z);
                if(candidate==parking.anchorCell)continue;
                if(editing.PreviewAt(candidate).ok)moved=editing.Confirm();
            }
            if(moved)break;
        }
        Require(moved,"No usable place for enlarged private bay on starter forecourt.");
        Vector3 flatPickup=trip.parkedPickup.transform.position,flatBay=parking.transform.position;flatPickup.y=flatBay.y=0;
        Require(Vector3.Distance(flatPickup,flatBay)<.001f,"Personal car did not follow moved bay.");editing.Finish();
        var tile=Item("Assets/Prefabs/StoreUpgrade/floor_cream.asset");build.inventory.Add(tile,20);build.EnterBuildMode(tile);
        yield return null;
        var selector=Object.FindObjectsOfType<RectTransform>().Single(t=>t.name=="Floor selector");Require(selector.gameObject.activeInHierarchy,"Floor toolbar absent in Game UI.");
        Require(selector.GetComponentsInChildren<UnityEngine.UI.Button>().Length==4,"Floor toolbar controls incomplete.");
        selector.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="2").onClick.Invoke();
        Require(build.Storeys.ActiveFloor==1,"Floor toolbar button did not switch storey.");yield return null;
        ScreenCapture.CaptureScreenshot(Folder+"/FloorSelector.png");for(int i=0;i<4;i++)yield return null;
        build.ExitBuildMode();build.Storeys.SelectFloor(0);
        var before=build.BuildPlacedSaveData();var sourceWalls=Object.FindObjectsOfType<StoreWallOccluder>().ToArray();
        var source=sourceWalls.SelectMany(w=>w.GetComponentsInChildren<MeshFilter>()).Distinct().ToArray();Require(source.Length>0,"Starter store walls missing.");
        trip.Begin();while(trip.Transitioning)yield return null;Require(trip.InCity,"Trip failed.");
        var city=Object.FindObjectOfType<CityWorld>();var layout=city.town.layout;
        var copies=city.facadeRoot.GetComponentsInChildren<MeshFilter>();
        foreach(var original in source)
            Require(copies.Any(c=>c.sharedMesh==original.sharedMesh&&Vector3.Distance(c.transform.position,layout.MapShopPoint(original.transform.position))<.001f
                &&Vector3.Distance(c.transform.lossyScale,Vector3.Scale(original.transform.lossyScale,layout.ShopVisualScale))<.001f),"Store geometry changed scale in city: "+original.name);
        var expectedHome=layout.MapShopPoint(parking.transform.position);expectedHome.y=city.home.transform.position.y;
        Require(Vector3.Distance(expectedHome,city.home.transform.position)<.001f,"City return point does not follow moved private parking.");
        var centre=sourceWalls[0].WorldBounds;foreach(var wall in sourceWalls.Skip(1))centre.Encapsulate(wall.WorldBounds);
        var cityCentre=layout.MapShopPoint(centre.center);city.chaseCamera.enabled=false;
        var camera=city.chaseCamera.GetComponent<Camera>();camera.transform.SetPositionAndRotation(cityCentre+new Vector3(18,18,-24),Quaternion.LookRotation(new Vector3(-18,-18,24)));
        ScreenCapture.CaptureScreenshot(Folder+"/CityStore.png");for(int i=0;i<4;i++)yield return null;
        city.chaseCamera.enabled=true;trip.Return();while(trip.Transitioning)yield return null;
        Require(!trip.InCity&&build.BuildPlacedSaveData().Count==before.Count,"Return changed shop layout.");
        Require(build.BuildPlacedSaveData().Single(p=>p.playerParking).x==parking.anchorCell.x,"Return lost moved personal parking.");
        report.AppendLine("PASS real Game sandbox: catalogue, floor UI, private parking move and car relocation, City/Loading travel, exact facade transform and return-point mapping, safe return.");
    }
    private static void InspectOriginalScale()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");
        var trip=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityTrip>(true)).Single();
        var bounds=CityUpdateSetup.Measure(trip.parkedPickup);
        var wall=Item("Assets/Prefabs/Structures/Wall.asset");
        File.WriteAllText(Folder+"/source-scale.txt",$"Parked pickup size: {bounds.size}\nWall model bounds: {wall.placementBounds}\n");
    }
    private static void Capture(BuildController build)
    {
        build.ExitBuildMode();build.Storeys.SelectFloor(1);build.floorPainter.SetVisibleThroughLayer(1);
        var camera=build.worldCamera;camera.GetComponent<MainCamera>().enabled=false;
        camera.transform.SetPositionAndRotation(new Vector3(4,8,-5),Quaternion.LookRotation(new Vector3(3.9f,.5f,2.3f)-new Vector3(4,8,-5)));
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.77f,.82f,.75f);
        var sun=new GameObject("QA daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
        var rt=new RenderTexture(1100,780,24);var previous=RenderTexture.active;var image=new Texture2D(1100,780,TextureFormat.RGBA32,false);
        try{rt.Create();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1100,780),0,0);image.Apply();File.WriteAllBytes(Folder+"/SecondFloor.png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.Destroy(rt);Object.Destroy(image);}
    }
    private static BuildItemData Item(string path)=>AssetDatabase.LoadAssetAtPath<BuildItemData>(path)??throw new InvalidOperationException("Missing item: "+path);
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
