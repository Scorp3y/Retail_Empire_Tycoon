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
public static class StoreCompletionPlay
{
    private const string Pending="StoreCompletion.Pending";
    private static IEnumerator checks;
    private static double deadline;
    static StoreCompletionPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(StoreCompletionBatch.Report);
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        File.WriteAllText(StoreCompletionBatch.Report+"/play.txt","RUNNING\n");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
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
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();shop.enabled=false;shop.SetOpen(false);
        var parking=Object.FindObjectOfType<ParkingTraffic>();parking.enabled=false;
        for(int i=0;i<5;i++)yield return null;
        Require(Mathf.Abs(starter.building.grid.cellSize-ShopProportions.CellSize)<.00001f && GridSystem.StoreyHeight==1.5f,"Construction grid/storeys were not enlarged.");
        var bays=Object.FindObjectsOfType<ParkingBay>().Where(bay=>bay.GetComponentInParent<BuildPreview>()==null).ToArray();
        Require(bays.Length==3 && bays.All(bay=>bay.width>1.9f && bay.length>3.9f),"Default bays remain undersized.");
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
        Migration(starter);
        ShopGameplaySandbox.PrepareActivities();
        var shelves=Object.FindObjectsOfType<PlacedShelfStock>();
        var product=AssetDatabase.FindAssets("t:ProductItemData").Select(guid=>AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(item=>item!=null&&shelves.Any(shelf=>shelf.IsProductAllowed(item)));
        foreach(var shelf in shelves)shelf.ClearStock();var stocked=shelves.First(shelf=>shelf.IsProductAllowed(product));stocked.SetStockFromSave(product,24);
        var balance=Object.Instantiate(shop.Balance);balance.thiefChance=0;balance.litterChance=0;ShopGameplaySetup.Set(shop,"balance",balance);
        shop.Advance(.1f);shop.SetOpen(true);parking.arrivalInterval=9999;
        Directory.CreateDirectory("Docs/StoreCompletion");
        File.WriteAllText(StoreCompletionBatch.Report+"/routing.txt","entry="+shop.EntrancePosition+" arrival="+shop.ArrivalPosition+" shelf="+stocked.transform.position+" from street="+shop.TryShelfApproach(stocked,shop.ArrivalPosition,out _)+"\n");
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
        File.AppendAllText(StoreCompletionBatch.Report+"/routing.txt",map.ToString());
        Require(shop.TryShelfApproach(stocked,shop.ArrivalPosition,out _),"Entrance to shelf route is blocked; see routing.txt.");
        ShopperVisit customer=null;GameObject car=null;bool paid=false,walked=false;int beforeMoney=shop.Money,beforeStock=stocked.CurrentAmount;
        for(int step=0;step<16000;step++)
        {
            parking.Advance(.05f);Physics.SyncTransforms();shop.Advance(.05f);
            Require(shop.Visits.Count<=1,"A separate pedestrian arrival duplicated the parking buyer.");
            if(car==null && customer==null)car=parking.VisitorCars.FirstOrDefault();
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
                    TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/StoreCompletion/StoreAndParking.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
                    Require(shop.ClaimCheckout()==customer && shop.CompleteCheckout(customer),"Driver cannot buy through the real checkout.");paid=true;
                    Require(shop.Money==beforeMoney+customer.Quantity*product.SellPrice && stocked.CurrentAmount==beforeStock-customer.Quantity,"Payment or stock was applied incorrectly.");
                }
                if(paid && customer.IsFinished && car==null)break;
            }
            if(step%500==0)
            {
                var lines=parking.MovementState+" notice="+shop.Notice+" stage="+customer?.Stage+"\n";
                foreach(var vehicle in parking.VisitorCars)
                    foreach(int side in new[]{-1,1})
                    {
                        var p=vehicle.transform.position+vehicle.transform.right*side*(vehicle.GetComponent<RetailEmpireTycoon.City.TrafficVehicle>().Width*.25f+.22f);p.y=.025f;
                        bool nearest=shop.Navigation.TryNearest(p,out var target,true,true);
                        lines+="car="+vehicle.transform.position+" door="+p+" nearest="+nearest+" at="+target+" streetpath="+shop.Navigation.TryPath(target,shop.ArrivalPosition,out _,true)+" shelfpath="+shop.TryShelfApproach(stocked,target,out _)+"\n";
                    }
                File.WriteAllText(StoreCompletionBatch.Report+"/cycle-state.txt",lines);
                if(customer==null && parking.VisitorCars.Count>0)TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/StoreCompletion/RouteProbe.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
            }
            if(step%100==0)yield return null;
        }
        Require(customer!=null && walked && paid && customer.ReturnedToCar && car==null,"Incomplete car -> shop -> purchase -> same car -> departure cycle. "+parking.MovementState+" notice="+shop.Notice+" visit="+customer?.Stage);
        Require(parking.VisitorCars.Count==0,"Finished visitor retained the parking lease.");
        File.WriteAllText(StoreCompletionBatch.Report+"/cycle.txt","PASS actual passenger car arrival; buyer exits beside its car, walks to shelf, browses, pays exactly once, returns to the same car, boards and leaves. Car waits for its buyer, not a timer; bay released only after departure.\n");
        File.WriteAllText(StoreCompletionBatch.Report+"/placement.txt","PASS enlarged 1.5x construction kit/storeys and shelves; >=1.9x3.9 bays; move all initial bays plus place a new bay on service-yard asphalt; reject public/unowned ground; operable matching-width double door; idempotent save conversion.\n");
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
        string path=StoreCompletionBatch.Report+"/must-not-save.json";Require(!File.Exists(path),"Stale guard-test file exists.");
        var root=new GameObject("Inactive save guard check");root.SetActive(false);var save=root.AddComponent<SaveManager>();
        typeof(SaveManager).GetField("saveFilePath",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(save,path);
        save.SaveGame();Require(!File.Exists(path),"Failed/incomplete load allowed a save write.");Object.DestroyImmediate(root);
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);File.WriteAllText(StoreCompletionBatch.Report+"/play.txt",error==null?"PASS full construction, parking and buyer lifecycle without accessing the player's save.\n":"FAIL "+error);
        if(error==null)StoreCompletionBatch.Record("play");EditorApplication.Exit(error==null?0:1);
    }
}
