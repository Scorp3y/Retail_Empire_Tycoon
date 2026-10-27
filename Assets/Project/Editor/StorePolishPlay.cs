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
public static class StorePolishPlay
{
    private const string Pending="StorePolish.Pending";
    private static IEnumerator checks;
    private static double deadline;
    static StorePolishPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(StorePolishBatch.Report);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        File.WriteAllText(StorePolishBatch.Report+"/play.txt","RUNNING\n");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
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
        Require(Mathf.Abs(starter.building.grid.cellSize-ShopProportions.CellSize)<.00001f && GridSystem.StoreyHeight==1.5f,"Construction grid/storeys were not enlarged.");
        var bays=Object.FindObjectsOfType<ParkingBay>().Where(bay=>bay.GetComponentInParent<BuildPreview>()==null).ToArray();
        Require(bays.Length==3 && bays.All(bay=>Mathf.Abs(bay.width-1.76661f)<.005f && Mathf.Abs(bay.length-3.56697f)<.005f),"Parking must be exactly 90% of its preceding size.");
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
        foreach(var tile in starter.floors.BuildSaveData().Where(tile=>tile.itemId=="asphalt_01" && tile.x%4==0 && tile.z%4==0))
        {
            var at=new Vector3Int(tile.x,0,tile.z);
            if(!starter.building.CanPlaceAt(at).ok)continue;
            Require(starter.building.TryPlaceAt(at),"Green parking preview refused placement.");placedNew=true;break;
        }
        Require(placedNew,"No free asphalt can accept a new full-size parking bay.");
        starter.building.EnterBuildMode(parkingItem);Require(!starter.building.CanPlaceAt(new Vector3Int(-800,0,-800)).ok,"Public/unowned terrain admitted parking.");starter.building.ExitBuildMode();
        foreach(var door in Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null&&placed.item.id=="door_01"))
        {
            Require(door.transform.Find("door-left")!=null && door.transform.Find("door-right")!=null,"New operable double door missing.");
            Require(Mathf.Abs(door.item.placementBounds.size.x-door.item.footprint.x*starter.building.grid.cellSize)<.003f,"Door does not fill enlarged opening.");
        }
        foreach(string id in new[]{"door_01","unloading_gate_01"})
        {
            var bounds=CityUpdateSetup.Measure(starter.catalog.GetById(id).prefab);
            Require(Vector3.Distance(bounds.size,new Vector3(1.5f,1.5f,.5f))<.003f,"Opening does not match wall envelope: "+id+" "+bounds);
            var band=starter.catalog.GetById(id).prefab.transform.Find("ContinuousTopBand").GetComponent<MeshRenderer>();
            Require(band.sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(MobilityUpdateBatch.Root+"/Walls.mat"),"Opening uses a mismatched wall palette.");
        }
        Migration(starter);
        ShopGameplaySandbox.PrepareActivities();
        var shelves=Object.FindObjectsOfType<PlacedShelfStock>();
        var product=AssetDatabase.FindAssets("t:ProductItemData").Select(guid=>AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(item=>item!=null&&shelves.Any(shelf=>shelf.IsProductAllowed(item)));
        foreach(var shelf in shelves)shelf.ClearStock();var stocked=shelves.First(shelf=>shelf.IsProductAllowed(product));stocked.SetStockFromSave(product,24);
        var balance=Object.Instantiate(shop.Balance);balance.thiefChance=0;balance.litterChance=0;ShopGameplaySetup.Set(shop,"balance",balance);
        shop.Advance(.1f);shop.SetOpen(true);parking.arrivalInterval=9999;
        Directory.CreateDirectory("Docs/StorePolish");
        File.WriteAllText(StorePolishBatch.Report+"/routing.txt","entry="+shop.EntrancePosition+" arrival="+shop.ArrivalPosition+" shelf="+stocked.transform.position+" from street="+shop.TryShelfApproach(stocked,shop.ArrivalPosition,out _)+"\n");
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
        File.AppendAllText(StorePolishBatch.Report+"/routing.txt",map.ToString());
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
                    TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/StorePolish/StoreAndParking.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
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
                File.WriteAllText(StorePolishBatch.Report+"/cycle-state.txt",lines);
                if(customer==null && parking.VisitorCars.Count>0)TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/StorePolish/RouteProbe.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
            }
            if(step%100==0)yield return null;
        }
        Require(customer!=null && walked && paid && customer.ReturnedToCar && streetArrival && streetDeparture && parking.VisitorCars.Count==0,"Incomplete street -> car -> shop -> purchase -> same car -> street cycle. "+parking.MovementState+" notice="+shop.Notice+" visit="+customer?.Stage);
        Require(parking.VisitorCars.Count==0,"Finished visitor retained the parking lease.");
        File.WriteAllText(StorePolishBatch.Report+"/cycle.txt","PASS actual passenger car arrival; buyer exits beside its car, walks to shelf, browses, pays exactly once, returns to the same car, boards and leaves. Car waits for its buyer, not a timer; bay released only after departure.\n");
        File.WriteAllText(StorePolishBatch.Report+"/placement.txt","PASS enlarged 1.5x construction kit/storeys and shelves; 1.77x3.57 bays; unchanged safe grid clearance; move all initial bays plus place a new bay on service-yard asphalt; reject public/unowned ground; operable matching-width double door; idempotent save conversion.\n");
        float shopWallTop=Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null && placed.item.isWall)
            .SelectMany(placed=>placed.GetComponentsInChildren<MeshRenderer>()).Max(renderer=>renderer.bounds.max.y);
        var gate=Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.item!=null && placed.item.isUnloadingGate);
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/StorePolish/Gate.png",gate.transform.position+new Vector3(-3,2.5f,-4),gate.transform.position+Vector3.up*.6f);
        starter.cityTrip.Begin();while(!starter.cityTrip.InCity || starter.cityTrip.Transitioning)yield return null;
        for(int i=0;i<6;i++)yield return null;
        var world=Object.FindObjectOfType<RetailEmpireTycoon.City.CityWorld>();world.pickup.ReadKeyboard=false;world.pickup.InputEnabled=false;
        var roof=world.facadeRoot.Find("Closed store roof");
        float expectedTop=world.town.layout.MapShopPoint(Vector3.up*shopWallTop).y+.08f;
        Require(roof!=null && Mathf.Abs(roof.position.y-expectedTop)<.02f,"Actual City facade does not match Game proportions: "+roof?.position.y+" vs "+expectedTop);
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/StorePolish/CityStore.png",world.pickup.transform.position+new Vector3(-8,5,-10),roof.position+Vector3.down);
        starter.cityTrip.Return();while(starter.cityTrip.Transitioning)yield return null;
        Require(!starter.cityTrip.InCity,"Return from City failed.");
        File.WriteAllText(StorePolishBatch.Report+"/city.txt","PASS actual Game -> City -> Game; facade roof at "+expectedTop+"; uniform world scale, no extra vertical stretch.\n");
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
        File.WriteAllText(StorePolishBatch.Report+"/stock.txt","PASS 5 distinct random shelves, 20 cream floor, 20 walls, 1 door, 1 gate, 2 bays; actual inventory restore; blueprint unchanged; existing-game upgrade grants nothing.\n");
    }
    private static void Migration(StarterStoreInitialization starter)
    {
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
        Require(old.floorTiles.All(tile=>tile.z*ShopProportions.CellSize>=-4.1f && tile.z*ShopProportions.CellSize<-1.9f),"Asphalt moved away from the original street.");
        var impossible=new GameData{usesModularStore=true,playerMoney=777};impossible.placedObjects.Add(new PlacedBuildSaveData{itemId="parking_01",playerParking=true});
        string unchanged=JsonUtility.ToJson(impossible);bool rejected=false;
        try{ShopProportions.Upgrade(impossible,starter.building.grid,starter.catalog);}catch(InvalidOperationException){rejected=true;}
        Require(rejected && unchanged==JsonUtility.ToJson(impossible),"Failed migration changed the original save snapshot.");
        string path=StorePolishBatch.Report+"/must-not-save.json";Require(!File.Exists(path),"Stale guard-test file exists.");
        var root=new GameObject("Inactive save guard check");root.SetActive(false);var save=root.AddComponent<SaveManager>();
        typeof(SaveManager).GetField("saveFilePath",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(save,path);
        save.SaveGame();Require(!File.Exists(path),"Failed/incomplete load allowed a save write.");Object.DestroyImmediate(root);
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);File.WriteAllText(StorePolishBatch.Report+"/play.txt",error==null?"PASS full construction, parking and buyer lifecycle without accessing the player's save.\n":"FAIL "+error);
        if(error==null)StorePolishBatch.Record("play");EditorApplication.Exit(error==null?0:1);
    }
}
