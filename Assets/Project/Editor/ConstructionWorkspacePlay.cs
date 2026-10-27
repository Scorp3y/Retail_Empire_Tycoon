using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ConstructionWorkspacePlay
{
    private const string Pending="ConstructionWorkspace.Pending";
    private static IEnumerator checks;
    private static double deadline;
    private static readonly System.Collections.Generic.HashSet<string> englishScreens=new();
    static ConstructionWorkspacePlay(){EditorApplication.update+=Tick;}
    private static bool StarterAccess=>SessionState.GetBool("ConstructionWorkspace.StarterAccess",false);
    private static string Report=>StarterAccess?StarterAccessBatch.Report:ConstructionWorkspaceBatch.Report;
    private static string Documentation=>StarterAccess?"Docs/StarterAccess":"Docs/ConstructionWorkspace";
    private static void RecordInputs(){if(StarterAccess)StarterAccessBatch.Record("play");else ConstructionWorkspaceBatch.Record("play");}
    public static void Run()=>Begin(false);
    public static void RunStarterAccess()=>Begin(true);
    private static void Begin(bool starterAccess)
    {
        TownWorldSafety.RequireIsolatedEditor();SessionState.SetBool("ConstructionWorkspace.StarterAccess",starterAccess);Directory.CreateDirectory(Report);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        File.WriteAllText(Report+"/play.txt","RUNNING\n");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+240;checks=Check();}
            Require(EditorApplication.timeSinceStartup<deadline,"Full parking/shop cycle timed out.");
            if(checks.MoveNext())return;Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();starter.cityTrip.orders.save=null;
        if(StarterAccess)SiteRevision(starter);
        var data=starter.CreateNewGame(50000);Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        StarterStock(starter,data);
        starter.building.inventory.ApplySaveData(data.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();shop.enabled=false;shop.SetOpen(false);
        var parking=Object.FindObjectOfType<ParkingTraffic>();parking.enabled=false;
        var roads=Object.FindObjectOfType<RetailEmpireTycoon.City.TownRoadTraffic>();roads.enabled=false;
        foreach(var roadCar in roads.ActiveCars)roadCar.gameObject.SetActive(false);
        Require(roads.MaximumCars==29,"Shared town retained its old traffic capacity.");
        for(int i=0;i<5;i++)yield return null;
        Physics.simulationMode=SimulationMode.Script;
        Require(Mathf.Abs(starter.building.grid.cellSize-ShopProportions.CellSize)<.00001f && Mathf.Abs(GridSystem.StoreyHeight-1.65f)<.0001f,"Construction grid/storeys were not enlarged.");
        var bays=Object.FindObjectsOfType<ParkingBay>().Where(bay=>bay.GetComponentInParent<BuildPreview>()==null).ToArray();
        Require(bays.Length==3 && bays.All(bay=>Mathf.Abs(bay.width-1.943271f)<.005f && Mathf.Abs(bay.length-3.923667f)<.005f),"Parking must match the updated 1.1x kit.");
        var editing=starter.building.GetComponent<BuildEditingController>();editing.ReadPointerInput=false;
        foreach(var bay in bays)
        {
            var placed=bay.GetComponent<PlacedObject>();editing.Begin();editing.Select(placed);
            var result=editing.PreviewAt(placed.anchorCell);
            Require(result.ok,"An existing asphalt bay cannot be placed: "+result.reason+" "+result.message+" anchor="+placed.anchorCell);
            Require(editing.Confirm(),"Preview and final parking relocation disagree.");editing.Finish();
        }
        var parkingItem=starter.catalog.GetById("parking_01");starter.building.inventory.Add(parkingItem,1);starter.building.EnterBuildMode(parkingItem);
        bool placedNew=false;
        foreach(var tile in starter.floors.BuildSaveData().Where(tile=>tile.itemId=="asphalt_01"))
        {
            var at=new Vector3Int(tile.x,0,tile.z);
            if(!starter.building.CanPlaceAt(at).ok)continue;
            Require(starter.building.TryPlaceAt(at),"Green parking preview refused placement.");placedNew=true;break;
        }
        Require(placedNew,"No free asphalt can accept a new full-size parking bay.");
        starter.building.EnterBuildMode(parkingItem);Require(!starter.building.CanPlaceAt(new Vector3Int(-800,0,-800)).ok,"Public/unowned terrain admitted parking.");starter.building.ExitBuildMode();
        var extraBay=Object.FindObjectsOfType<ParkingBay>().Single(bay=>bay.GetComponentInParent<BuildPreview>()==null && !bays.Contains(bay));
        editing.Begin();editing.Select(extraBay.GetComponent<PlacedObject>());Require(editing.DeleteSelected(),"New test bay cannot be returned to storage.");editing.Finish();
        foreach(var door in Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null&&placed.item.id=="door_01"))
        {
            Require(door.transform.Find("door-left")!=null && door.transform.Find("door-right")!=null,"New operable double door missing.");
            Require(Mathf.Abs(door.item.placementBounds.size.x-door.item.footprint.x*starter.building.grid.cellSize)<.003f,"Door does not fill enlarged opening.");
        }
        foreach(string id in new[]{"door_01","unloading_gate_01"})
        {
            var bounds=CityUpdateSetup.Measure(starter.catalog.GetById(id).prefab);
            Require(Vector3.Distance(bounds.size,new Vector3(1.65f,1.65f,.55f))<.003f,"Opening does not match wall envelope: "+id+" "+bounds);
            var band=starter.catalog.GetById(id).prefab.GetComponentsInChildren<MeshRenderer>(true).First(renderer=>renderer.name=="ContinuousTopBand");
            Require(band.sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(MobilityUpdateBatch.Root+"/Walls.mat"),"Opening uses a mismatched wall palette.");
        }
        foreach(var step in Workspace(starter))yield return step;
        Migration(starter);
        ShopGameplaySandbox.PrepareActivities();
        var shelves=Object.FindObjectsOfType<PlacedShelfStock>();
        var product=AssetDatabase.FindAssets("t:ProductItemData").Select(guid=>AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(item=>item!=null&&shelves.Any(shelf=>shelf.IsProductAllowed(item)));
        foreach(var shelf in shelves)shelf.ClearStock();var stocked=shelves.First(shelf=>shelf.IsProductAllowed(product));stocked.SetStockFromSave(product,24);
        var balance=Object.Instantiate(shop.Balance);balance.thiefChance=0;balance.litterChance=0;ShopGameplaySetup.Set(shop,"balance",balance);
        shop.Advance(.1f);shop.SetOpen(true);parking.arrivalInterval=9999;
        Directory.CreateDirectory(Documentation);
        File.WriteAllText(Report+"/routing.txt","entry="+shop.EntrancePosition+" arrival="+shop.ArrivalPosition+" shelf="+stocked.transform.position+" from street="+shop.TryShelfApproach(stocked,shop.ArrivalPosition,out _)+"\n");
        var map=new System.Text.StringBuilder();var grid=starter.building.grid;
        for(int z=-24;z<=12;z++)
        {
            for(int x=10;x<=26;x++)map.Append(shop.Navigation.IsWalkable(new Vector3Int(x,0,z),true)?'.':'#');
            map.AppendLine(" z="+z);
        }
        foreach(var placed in Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null && (placed.item.isDoorway || placed.GetComponent<PlacedShelfStock>()!=null)))
            map.AppendLine(placed.item.id+" at="+placed.transform.position+" bounds="+placed.item.placementBounds+" facing="+placed.facing);
        foreach(var z in new[]{-4.5f,-4.125f,-3.75f,-2f,0f})
        {
            var point=new Vector3(4.875f,0,z);map.AppendLine("at="+point+" hits="+string.Join(",",Physics.OverlapCapsule(point+Vector3.up*.36f,point+Vector3.up*.81f,.1f).Select(collider=>collider.name+":"+collider.GetComponentInParent<PlacedObject>()?.item?.id)));
        }
        File.AppendAllText(Report+"/routing.txt",map.ToString());
        Require(shop.TryShelfApproach(stocked,shop.ArrivalPosition,out _),"Entrance to shelf route is blocked; see routing.txt.");
        ShopperVisit customer=null;GameObject car=null;bool paid=false,walked=false,streetArrival=false,streetDeparture=false;int beforeMoney=shop.Money,beforeStock=stocked.CurrentAmount;
        for(int step=0;step<16000;step++)
        {
            parking.Advance(.05f);Physics.SyncTransforms();
            roads.Simulate(.05f,step*.05,parking.roadEntry.position,true);Physics.Simulate(.05f);
            shop.Advance(.05f);
            Require(shop.Visits.Count<=1,"A separate pedestrian arrival duplicated the parking buyer.");
            if(car==null && customer==null)
            {
                car=parking.VisitorCars.FirstOrDefault();
                if(car!=null)
                {
                    Require(Vector3.Distance(car.transform.position,parking.roadEntry.position)>6,"Visitor still spawns at the driveway.");
                    Require(!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main),new Bounds(car.transform.position+Vector3.up*.4f,Vector3.one*3)),"Visitor spawned in camera view.");
                    streetArrival=true;
                }
            }
            if(paid && car!=null && car.transform.position.z<parking.roadEntry.position.z-.5f && Mathf.Abs(car.transform.position.x-parking.roadEntry.position.x)>6)streetDeparture=true;
            if(customer==null)customer=shop.Visits.FirstOrDefault(visit=>visit.ArrivedByCar);
            if(customer!=null)
            {
                var animator=customer.Character!=null?customer.Character.GetComponentInChildren<Animator>():null;
                if(animator!=null){animator.Update(.05f);walked|=animator.GetBool("isWalking");}
                Require(customer.IsFinished || car!=null,"Driver's car left before the visit ended.");
                if(customer.Stage==VisitStage.Waiting && !paid)
                {
                    for(int wait=0;wait<600;wait++)parking.Advance(.05f);
                    Require(car!=null && !customer.IsFinished,"Car uses an unrelated parking timer rather than waiting for its buyer.");
                    TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/StoreAndParking.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
                    Require(shop.ClaimCheckout()==customer && shop.CompleteCheckout(customer),"Driver cannot buy through the real checkout.");paid=true;
                    Require(shop.Money==beforeMoney+customer.Quantity*product.SellPrice && stocked.CurrentAmount==beforeStock-customer.Quantity,"Payment or stock was applied incorrectly.");
                }
                if(paid && customer.IsFinished && parking.VisitorCars.Count==0)break;
            }
            if(step%500==0)
            {
                var lines=parking.MovementState+" arrival="+parking.ArrivalState+" notice="+shop.Notice+" stage="+customer?.Stage+"\n";
                foreach(var vehicle in parking.VisitorCars)
                    foreach(int side in new[]{-1,1})
                    {
                        var p=vehicle.transform.position+vehicle.transform.right*side*(vehicle.GetComponent<RetailEmpireTycoon.City.TrafficVehicle>().Width*.25f+.22f);p.y=.025f;
                        bool nearest=shop.Navigation.TryNearest(p,out var target,true,true);
                        lines+="car="+vehicle.transform.position+" door="+p+" nearest="+nearest+" at="+target+" streetpath="+shop.Navigation.TryPath(target,shop.ArrivalPosition,out _,true)+" shelfpath="+shop.TryShelfApproach(stocked,target,out _)+"\n";
                    }
                File.WriteAllText(Report+"/cycle-state.txt",lines);
                if(customer==null && parking.VisitorCars.Count>0)TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/RouteProbe.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
            }
            if(step%100==0)yield return null;
        }
        Require(customer!=null && walked && paid && customer.ReturnedToCar && streetArrival && streetDeparture && parking.VisitorCars.Count==0,"Incomplete street -> car -> shop -> purchase -> same car -> street cycle. "+parking.MovementState+" notice="+shop.Notice+" visit="+customer?.Stage);
        Require(parking.VisitorCars.Count==0,"Finished visitor retained the parking lease.");
        File.WriteAllText(Report+"/cycle.txt","PASS actual passenger car arrival; buyer exits beside its car, walks to shelf, browses, pays exactly once, returns to the same car, boards and leaves. Car waits for its buyer, not a timer; bay released only after departure.\n");
        File.WriteAllText(Report+"/placement.txt","PASS 10-percent larger StorePolish kit, shelves and storeys; 1.94x3.92 bays; proportional grid clearance; move all initial bays plus place and remove a test bay on service-yard asphalt; reject public/unowned ground; matching-width operable double door; idempotent version-one and legacy save conversion.\n");
        float shopWallTop=Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null && placed.item.isWall)
            .SelectMany(placed=>placed.GetComponentsInChildren<MeshRenderer>()).Max(renderer=>renderer.bounds.max.y);
        var gate=Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.item!=null && placed.item.isUnloadingGate);
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/Gate.png",gate.transform.position+new Vector3(-3,2.5f,-4),gate.transform.position+Vector3.up*.6f);
        starter.cityTrip.Begin();while(!starter.cityTrip.InCity || starter.cityTrip.Transitioning)yield return null;
        for(int i=0;i<6;i++)yield return null;
        var world=Object.FindObjectOfType<RetailEmpireTycoon.City.CityWorld>();world.pickup.ReadKeyboard=false;world.pickup.InputEnabled=false;
        var roof=world.facadeRoot.Find("Closed store roof");
        float expectedTop=world.town.layout.MapShopPoint(Vector3.up*shopWallTop).y;
        Require(roof==null,"City still grants an automatic roof.");
        Require(!world.facadeRoot.GetComponentsInChildren<MeshFilter>().Any(filter=>filter.name.Contains("Highlight_Tube_")),"City snapshot copies temporary editing highlights.");
        EnglishUi("city driving HUD");
        var cityMap=(RetailEmpireTycoon.City.CityMapHud)typeof(RetailEmpireTycoon.City.CityWorld).GetField("map",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(world);
        cityMap.Toggle();yield return null;EnglishUi("city map");cityMap.Toggle();
        var supplier=world.town.suppliers.First(point=>point.kind==RetailEmpireTycoon.City.CheckpointKind.Depot);
        typeof(RetailEmpireTycoon.City.CityWorld).GetField("activeSupplier",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(world,supplier);
        typeof(RetailEmpireTycoon.City.CityWorld).GetField("buying",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(world,true);
        typeof(RetailEmpireTycoon.City.CityWorld).GetMethod("ShowDepot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(world,null);
        yield return null;EnglishUi("supplier assortment");
        typeof(RetailEmpireTycoon.City.CityWorld).GetMethod("ClosePanel",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(world,null);
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/CityStore.png",world.pickup.transform.position+new Vector3(-8,5,-10),world.town.layout.MapShopPoint(starter.blueprint.cameraFocus)+Vector3.up);
        starter.cityTrip.Return();while(starter.cityTrip.Transitioning)yield return null;
        Require(!starter.cityTrip.InCity,"Return from City failed.");
        File.WriteAllText(Report+"/city.txt","PASS actual Game -> City -> Game; no automatic roof; scaled wall top "+expectedTop+"; uniform world scale, player-built surfaces only.\n");
    }
    private static void StarterStock(StarterStoreInitialization starter,GameData data)
    {
        Require(data.buildInventory.Sum(entry=>entry.count)==49,"Incorrect starter stock total.");
        Require(data.buildInventory.Where(entry=>starter.catalog.GetById(entry.itemId).category==BuildCategory.Shelf).Count()==5,"Five different starter shelves required.");
        foreach(var pair in new[]{("floor_cream",20),("wall_01",20),("door_01",1),("unloading_gate_01",1),("parking_01",2)})
            Require(data.buildInventory.Single(entry=>entry.itemId==pair.Item1).count==pair.Item2,"Incorrect stock: "+pair.Item1);
        Require(starter.blueprint.initialState.buildInventory.Count==0,"Grant mutated the reusable blueprint.");
        var reloaded=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
        starter.UpgradeUnmodifiedLayout(reloaded);
        Require(reloaded.buildInventory.Sum(entry=>entry.count)==49,"Loading/upgrading grants the starter stock again.");
        var stock=(StarterConstructionStock)typeof(StarterStoreBlueprint).GetField("constructionStock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(starter.blueprint);
        var choices=new System.Collections.Generic.HashSet<string>();
        for(int seed=1;seed<=12;seed++)
        {
            var game=new GameData();stock.AddTo(game,new System.Random(seed));
            choices.Add(string.Join(",",game.buildInventory.Where(entry=>starter.catalog.GetById(entry.itemId).category==BuildCategory.Shelf).Select(entry=>entry.itemId)));
        }
        Require(choices.Count>1,"Starter shelf selection is not random.");
        var layout=Object.FindObjectOfType<RetailEmpireTycoon.City.TownWorldPlaces>().layout;
        Require(layout.ShopVisualScale==Vector3.one*layout.shopToCityScale,"City shop is still stretched vertically.");
        File.WriteAllText(Report+"/stock.txt","PASS 5 distinct random shelves, 20 cream floor, 20 walls, 1 door, 1 gate, 2 bays; actual inventory restore; blueprint unchanged; existing-game upgrade grants nothing.\n");
    }
    private static System.Collections.Generic.IEnumerable<object> Workspace(StarterStoreInitialization starter)
    {
        var build=starter.building;var workspace=Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.BuildEditorHud>(true);
        Require(workspace.Launcher!=null,"Hammer launcher is not connected.");
        Require(!RetailEmpireTycoon.UI.Shop.ShopText.Russian,"An old language preference still selects Russian.");
        var russian=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales.First(locale=>locale.Identifier.Code=="ru");
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=russian;yield return null;
        Require(UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code=="en","A legacy locale selector can still switch the build away from English.");
        workspace.Launcher.onClick.Invoke();yield return null;
        Require(workspace.IsOpen && workspace.editor.IsActive,"Hammer does not open construction editing.");
        Require(!workspace.warehouseWindow.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(button=>
            button.GetComponentInChildren<TMPro.TMP_Text>(true)?.text=="Edit store" || button.GetComponentInChildren<TMPro.TMP_Text>(true)?.text=="Private parking"),"Construction tools remain in the warehouse.");
        var inventory=Object.FindObjectOfType<RetailEmpireTycoon.UI.Windows.BuildInventoryWindow>(true);
        workspace.Close();inventory.gameObject.SetActive(true);inventory.ShowFurniture();yield return null;
        Require(inventory.GetComponentsInChildren<RetailEmpireTycoon.UI.Windows.BuildInventoryItemRow>(true).All(row=>!row.placeButton.gameObject.activeSelf),"Warehouse still places furniture directly.");
        inventory.Close();workspace.Open();yield return null;
        Require(!workspace.uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(button=>button.name=="Apply" || button.name=="Parking"),"Redundant Apply or Parking button remains.");
        Canvas.ForceUpdateCanvases();CaptureUi("Workspace_Floor1");EnglishUi("construction");
        var lower=Object.FindObjectsOfType<StoreWallOccluder>().SelectMany(wall=>wall.GetComponentsInChildren<Renderer>()).ToArray();
        workspace.uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button=>button.name=="Floor up").onClick.Invoke();yield return null;
        Require(build.Storeys.ActiveFloor==1,"Floor-up arrow failed.");
        var fixture=build.inventory.Entries.First(entry=>entry.item.category==BuildCategory.Shelf && entry.count>0).item;
        workspace.BuildItem(fixture);yield return null;yield return null;
        Require(lower.All(renderer=>renderer.enabled && !renderer.forceRenderingOff),"Building upstairs hides a ground-floor wall.");
        var overlay=build.gridOverlay;var vertices=overlay.GetComponent<MeshFilter>().sharedMesh.vertices.Select(vertex=>overlay.transform.TransformPoint(vertex)).ToArray();
        Require(vertices.All(vertex=>Mathf.Abs(vertex.y-GridSystem.StoreyHeight-.002f)<.002f),"Grid floats above an empty second-floor wall top.");
        var ground=string.Join(";",starter.floors.BuildSaveData().Where(tile=>tile.floor==0).Select(tile=>tile.itemId+":"+tile.x+":"+tile.z).OrderBy(key=>key));
        var placed=JsonUtility.ToJson(new GameData{placedObjects=build.BuildPlacedSaveData()});
        var floor=starter.catalog.GetById("floor_cream");workspace.BuildItem(floor);build.Storeys.SetCeiling(false);
        var catalogContent=workspace.uiRoot.GetComponentsInChildren<RectTransform>(true).Single(rect=>rect.name=="Owned construction items");catalogContent.anchoredPosition=new Vector2(0,64);
        var source=starter.floors.BuildSaveData().First(tile=>tile.floor==0 && tile.itemId!="asphalt_01");
        var upper=new Vector3Int(source.x,1,source.z);int stock=build.inventory.GetCount(floor);
        Require(build.TryPaintCells(new System.Collections.Generic.List<Vector3Int>{upper}),"Cannot paint an upper-floor tile through construction.");
        Require(build.inventory.GetCount(floor)==stock-1 && starter.floors.HasSurface(upper),"Painting does not consume exactly one owned tile.");
        Require(Mathf.Abs(catalogContent.anchoredPosition.y-64)<.1f,"Painting resets the owned-item catalog scroll position.");
        Require(starter.floors.Surfaces.Where(filter=>filter.transform.parent.name.Contains("layer 1")).All(filter=>Mathf.Abs(filter.GetComponent<Renderer>().bounds.max.y-GridSystem.StoreyHeight-FloorPainter.UpperSurfaceLift)<.002f),"Built upper floor does not use the wall-cap separation.");
        Require(ground==string.Join(";",starter.floors.BuildSaveData().Where(tile=>tile.floor==0).Select(tile=>tile.itemId+":"+tile.x+":"+tile.z).OrderBy(key=>key))
            && placed==JsonUtility.ToJson(new GameData{placedObjects=build.BuildPlacedSaveData()}),"Upper-floor building changed the ground-floor data.");
        yield return null;CaptureUi("Workspace_Floor2");EnglishUi("upper-floor construction");
        var wallCell=build.BuildPlacedSaveData().First(item=>item.floor==0 && item.itemId=="wall_01");
        var overWall=new Vector3Int(wallCell.x,1,wallCell.z);
        Require(build.TryPaintCells(new System.Collections.Generic.List<Vector3Int>{overWall}),"Cannot build a ceiling over a wall.");
        Require(Mathf.Abs(starter.floors.SurfaceElevation(overWall)-GridSystem.StoreyHeight-FloorPainter.UpperSurfaceLift)<.0001f,"Upper surface still lies on the wall cap.");
        yield return null;CaptureUi("CeilingOverWall");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/WallSeparation.png",build.grid.CellToWorld(overWall)+new Vector3(-2,2,-3),build.grid.CellToWorld(overWall));
        var roundTrip=starter.floors.BuildSaveData();starter.floors.ApplySaveData(roundTrip,starter.catalog);
        Require(starter.floors.HasSurface(overWall) && starter.floors.HasSurface(upper),"Save/load lost ceiling tiles.");
        workspace.OpenEditing();build.Storeys.SelectFloor(0);
        var personal=Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.playerParking);
        Require(workspace.editor.SelectAtScreenPoint(build.worldCamera.WorldToScreenPoint(personal.transform.position)),"Clicking personal parking does not select it.");yield return null;
        Require(workspace.editor.Selected==personal && build.Storeys.ActiveFloor==0,"Pointer did not select the personal parking instance.");
        Require(workspace.editor.PreviewAt(personal.anchorCell).ok && workspace.editor.Confirm(),"Clicked personal parking cannot be placed.");
        Require(workspace.editor.SelectAtScreenPoint(build.worldCamera.WorldToScreenPoint(personal.transform.position)),"Cannot pick personal parking a second time.");
        Require(!workspace.editor.DeleteSelected(),"Personal return parking can be deleted.");workspace.editor.CancelSelection();
        var visitor=Object.FindObjectsOfType<PlacedObject>().First(item=>item.item!=null && item.item.isParkingSpace && !item.playerParking);
        Require(workspace.editor.SelectAtScreenPoint(build.worldCamera.WorldToScreenPoint(visitor.transform.position)) && workspace.editor.Selected==visitor,"Pointer cannot select ordinary parking.");
        workspace.editor.CancelSelection();workspace.Close();
        Require(!workspace.IsOpen && build.mode==BuildMode.Normal,"Construction close did not restore ordinary gameplay.");
        yield return null;CaptureUi("Overview");
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopWindow>(true);shop.gameObject.SetActive(true);shop.ShowMainTabs();yield return null;EnglishUi("store catalog");shop.gameObject.SetActive(false);
        inventory.gameObject.SetActive(true);inventory.ShowProducts();yield return null;EnglishUi("warehouse");inventory.Close();
        var settings=Object.FindObjectOfType<ControlPanel>(true);settings.OpenSetting();yield return null;EnglishUi("settings");settings.ExitSetting();
        File.WriteAllText(Report+"/workspace.txt","PASS hammer launcher; no Apply/Parking buttons; pointer selects personal and visitor bays; personal bay remains protected from deletion; upper-floor and over-wall tiles persist after save/load, stay 5 mm above caps, preserve ground-floor data; English interface.\n");
    }
    private static void SiteRevision(StarterStoreInitialization starter)
    {
        var previous=JsonUtility.FromJson<GameData>(File.ReadAllText(Report+"/previous-starter.json"));
        GameData Clone(GameData value)=>JsonUtility.FromJson<GameData>(JsonUtility.ToJson(value));
        var converted=Clone(previous);converted.playerMoney=27182;
        converted.buildInventory.Add(new BuildInventorySaveEntry{itemId="wall_01",count=13});
        var shelf=previous.placedObjects.First(item=>starter.catalog.GetById(item.itemId).category==BuildCategory.Shelf);
        converted.shelfStocks.Clear();converted.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=shelf.itemId,anchorX=shelf.x,anchorZ=shelf.z,amount=17,productId="site-stock"});
        var upper=previous.floorTiles.First(tile=>tile.itemId!="asphalt_01");
        converted.floorTiles.Add(new FloorTileSaveData{itemId=upper.itemId,x=upper.x,z=upper.z,floor=1});
        var revision=starter.blueprint.SiteRevision;var shift=revision.Shift;
        Require(shift==new Vector2Int(8,8) && revision.TryApply(converted,starter.building.grid,starter.catalog),"Unmodified starter cannot receive the site relocation.");
        Require(converted.playerMoney==27182 && converted.buildInventory.Single().count==13 && converted.shelfStocks.Single().amount==17
            && converted.shelfStocks.Single().anchorX==shelf.x+shift.x && converted.shelfStocks.Single().anchorZ==shelf.z+shift.y,"Relocation changed money, inventory or shelf stock.");
        Require(converted.floorTiles.Any(tile=>tile.floor==1 && tile.x==upper.x+shift.x && tile.z==upper.z+shift.y),"Upper player ceiling did not move with its building.");
        foreach(var parking in previous.placedObjects.Where(item=>item.itemId=="parking_01"))
            Require(converted.placedObjects.Any(item=>item.itemId==parking.itemId && item.x==parking.x && item.z==parking.z && item.playerParking==parking.playerParking),"Relocation moved an existing parking bay.");
        string once=JsonUtility.ToJson(converted);Require(!revision.TryApply(converted,starter.building.grid,starter.catalog) && once==JsonUtility.ToJson(converted),"Relocation runs twice.");
        var customized=Clone(previous);customized.placedObjects.First(item=>item.itemId=="wall_01").x++;
        string before=JsonUtility.ToJson(customized);Require(!revision.TryApply(customized,starter.building.grid,starter.catalog) && before==JsonUtility.ToJson(customized),"Migration overwrote a custom shop.");
        var conflict=Clone(previous);var bay=conflict.placedObjects.First(item=>item.itemId=="parking_01");bay.x=shelf.x+shift.x;bay.z=shelf.z+shift.y;
        before=JsonUtility.ToJson(conflict);Require(!revision.TryApply(conflict,starter.building.grid,starter.catalog) && before==JsonUtility.ToJson(conflict),"Rejected parking conflict partially mutated save data.");
        File.WriteAllText(Report+"/migration.txt","PASS exact starter moves once by (8,8); custom ground layouts and conflicting private parking opt out atomically; money, inventory, shelf stock, upper ceilings and existing parking survive. No player save accessed.\n");
    }
    private static void EnglishUi(string context)
    {
        var invalid=Object.FindObjectsOfType<TMPro.TMP_Text>().Where(text=>text.enabled && text.gameObject.activeInHierarchy && text.GetComponentInParent<Canvas>()!=null)
            .Where(text=>text.text.Any(character=>character>='А'&&character<='я'||character=='ё'||character=='Ё')).Select(text=>text.name+"="+text.text).ToArray();
        Require(invalid.Length==0,"Non-English UI in "+context+": "+string.Join("; ",invalid));
        englishScreens.Add(context);
    }
    private static void CaptureUi(string name)
    {
        Directory.CreateDirectory(Documentation);
        var camera=Camera.main;var target=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        var canvases=Object.FindObjectsOfType<Canvas>().Where(canvas=>canvas.isRootCanvas && canvas.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(canvas=>canvas.worldCamera).ToArray();var planes=canvases.Select(canvas=>canvas.planeDistance).ToArray();
        var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
        try
        {
            target.Create();camera.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.6f;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
            File.WriteAllBytes(Documentation+"/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(texture);Canvas.ForceUpdateCanvases();
        }
    }
    private static void Migration(StarterStoreInitialization starter)
    {
        var previous=new GameData{usesModularStore=true,shopProportionsVersion=1,playerMoney=27182};
        for(int x=0;x<40;x++)for(int z=-50;z<-20;z++)previous.floorTiles.Add(new FloorTileSaveData{itemId="asphalt_01",x=x,z=z});
        previous.floorTiles.Add(new FloorTileSaveData{itemId="floor_cream",x=3,floor=1,z=4});
        string fixture=starter.catalog.Items.First(item=>item!=null && item.category==BuildCategory.Shelf).id;
        previous.placedObjects.Add(new PlacedBuildSaveData{itemId=fixture,x=5,floor=1,z=5});
        previous.placedObjects.Add(new PlacedBuildSaveData{itemId=fixture,x=10,z=-40});
        previous.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=fixture,anchorX=10,anchorZ=-40,productId="fixture-stock",amount=17});
        previous.buildInventory.Add(new BuildInventorySaveEntry{itemId="wall_01",count=13});
        Require(ShopProportions.Upgrade(previous,starter.building.grid,starter.catalog),"Version-one save did not receive the ten-percent conversion.");
        var moved=previous.placedObjects.Single(item=>item.floor==0);
        Require(previous.shopProportionsVersion==2 && previous.playerMoney==27182 && previous.buildInventory.Single().count==13
            && previous.placedObjects.Single(item=>item.floor==1).x==5 && previous.floorTiles.Any(tile=>tile.floor==1 && tile.x==3 && tile.z==4),"Ten-percent conversion lost money, inventory or upper-storey addresses.");
        Require(previous.shelfStocks.Single().anchorX==moved.x && previous.shelfStocks.Single().anchorZ==moved.z && previous.shelfStocks.Single().amount==17,"Ten-percent conversion lost outdoor shelf stock.");
        string converted=JsonUtility.ToJson(previous);Require(!ShopProportions.Upgrade(previous,starter.building.grid,starter.catalog) && converted==JsonUtility.ToJson(previous),"Ten-percent conversion applied twice.");
        var old=new GameData{usesModularStore=true,playerMoney=31415};
        for(int x=0;x<12;x++)for(int z=-24;z<-12;z++)old.floorTiles.Add(new FloorTileSaveData{itemId="asphalt_01",x=x,z=z});
        var shelf=new PlacedBuildSaveData{itemId="wall_01",x=3,z=4,wallModuleVersion=3};old.placedObjects.Add(shelf);
        string shelfId=starter.catalog.Items.First(item=>item!=null && item.category==BuildCategory.Shelf).id;
        old.placedObjects.Add(new PlacedBuildSaveData{itemId=shelfId,x=4,z=-20});
        old.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=shelfId,anchorX=4,anchorZ=-20,productId="migration-fixture",amount=17});
        Require(ShopProportions.Upgrade(old,starter.building.grid,starter.catalog),"Old proportions were not converted.");
        Require(shelf.x==3 && shelf.z==4 && old.playerMoney==31415,"Conversion altered interior addresses or money.");
        var outdoor=old.placedObjects.Single(placed=>placed.itemId==shelfId);
        Require(old.shelfStocks.Single().anchorX==outdoor.x && old.shelfStocks.Single().anchorZ==outdoor.z && old.shelfStocks.Single().amount==17,"Outdoor shelf lost its saved stock identity.");
        string once=JsonUtility.ToJson(old);Require(!ShopProportions.Upgrade(old,starter.building.grid,starter.catalog) && once==JsonUtility.ToJson(old),"Save conversion runs twice.");
        Require(old.floorTiles.All(tile=>(tile.z+.5f)*ShopProportions.CellSize>=-4.001f && (tile.z+.5f)*ShopProportions.CellSize<-2.0004f),"Asphalt moved away from the original street.");
        var impossible=new GameData{usesModularStore=true,playerMoney=777};impossible.placedObjects.Add(new PlacedBuildSaveData{itemId="parking_01",playerParking=true});
        string unchanged=JsonUtility.ToJson(impossible);bool rejected=false;
        try{ShopProportions.Upgrade(impossible,starter.building.grid,starter.catalog);}catch(InvalidOperationException){rejected=true;}
        Require(rejected && unchanged==JsonUtility.ToJson(impossible),"Failed migration changed the original save snapshot.");
        string path=Report+"/must-not-save.json";Require(!File.Exists(path),"Stale guard-test file exists.");
        var root=new GameObject("Inactive save guard check");root.SetActive(false);var save=root.AddComponent<SaveManager>();
        typeof(SaveManager).GetField("saveFilePath",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(save,path);
        save.SaveGame();Require(!File.Exists(path),"Failed/incomplete load allowed a save write.");Object.DestroyImmediate(root);
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);File.WriteAllText(Report+"/play.txt",error==null?"PASS full construction, parking and buyer lifecycle without accessing the player's save.\n":"FAIL "+error);
        if(error==null)File.WriteAllLines(Report+"/language.txt",new[]{"PASS English-only UI, legacy locale changes cannot switch this build away from English. Verified screens:"}.Concat(englishScreens.OrderBy(name=>name)));
        if(error==null)RecordInputs();EditorApplication.Exit(error==null?0:1);
    }
}


