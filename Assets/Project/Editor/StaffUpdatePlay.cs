using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Real imported employees and a road-driven paid order round trip; no player save access.</summary>
[InitializeOnLoad]
public static class StaffUpdatePlay
{
    private const string Pending="StaffUpdate.Play";
    private static IEnumerator checks;private static double deadline;
    static StaffUpdatePlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(StaffUpdateBatch.Report);
        File.WriteAllText(StaffUpdateBatch.Report+"/play.txt","RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){checks=Check();deadline=EditorApplication.timeSinceStartup+900;}
            Require(EditorApplication.timeSinceStartup<deadline,"Staff journey checks timed out.");
            if(checks.MoveNext())return;StaffUpdateBatch.Record();Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<5;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Unexpected player save writer.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var trip=starter.cityTrip;var orders=trip.orders;orders.save=null;
        var data=starter.CreateNewGame();Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        starter.building.inventory.ApplySaveData(data.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();shop.enabled=false;
        var driver=Object.FindObjectOfType<DriverDelivery>();driver.enabled=false;
        var parking=Object.FindObjectOfType<ParkingTraffic>();parking.enabled=false;
        var roads=driver.traffic;roads.enabled=false;
        for(int i=0;i<5;i++)yield return null;
        shop.Advance(.05f);Require(shop.ReadyForStaff,"Store failed to configure.");
        // The modern starter has produce/bakery shelves; the older duty suite needs a cola-compatible fixture.
        var shelfItem=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/DoubleSidedShelfBuildItem.asset");
        starter.building.inventory.Add(shelfItem,1);starter.building.EnterBuildMode(shelfItem);bool testShelf=false;
        foreach(var tile in starter.floors.BuildSaveData().Where(t=>t.floor==0 && t.itemId!="asphalt_01"))
            if(starter.building.TryPlaceAt(new Vector3Int(tile.x,0,tile.z))){testShelf=true;break;}
        starter.building.ExitBuildMode();Require(testShelf,"Could not place the duty suite's cola shelf.");
        shop.Advance(.05f);
        foreach(var step in CheckDuties(shop,orders))yield return step;
        // Numeric roles from existing four-slot saves must keep their original meaning.
        var legacy=new StaffRoster();legacy.Restore(new[]{1,1,1,1},1);
        Require(legacy.Count(StaffRole.Driver)==0 && legacy.Snapshot().Take(4).All(n=>n==1),"Legacy staff migration changed existing roles.");
        foreach(StaffRole role in Enum.GetValues(typeof(StaffRole)))
        {
            Require(shop.TryHire(role),"Cannot hire "+role);
            var prefab=shop.ModelFor(role);Require(prefab!=null&&prefab.GetComponent<StaffAppearance>().role==role,"Wrong model for "+role);
            var animator=prefab.GetComponentInChildren<Animator>();Require(animator.avatar.isValid&&animator.runtimeAnimatorController.animationClips.Select(c=>c.name).Distinct().Count()==3,"Missing employee animations.");
        }
        Require(!shop.TryHire(StaffRole.Driver),"One player car acquired two drivers.");
        var hud=Object.FindObjectOfType<ShopOperationsHud>();hud.ToggleStaff();for(int i=0;i<3;i++)yield return null;
        CaptureUi("StaffRecruitment");trip.controls.CloseWindows();yield return null;
        shop.SetOpen(true);
        var product=orders.productCatalog.Products.First(p=>p!=null&&SupplierRouting.ForProduct(p.StorageType)==SupplierKind.Wholesale && new CargoCapacity(350,2).Fits(0,0,p.BoxAmount*p.UnitWeightKg,p.BoxAmount*p.UnitVolumeM3));
        int old=orders.products.GetCount(product),wallet=orders.money.Money;
        Require(orders.Order(product),"Could not pay for a delivery order.");
        Require(orders.money.Money==wallet-product.BuyPrice,"Order payment incorrect.");
        Physics.simulationMode=SimulationMode.Script;double clock=0;var stages=new System.Collections.Generic.HashSet<DriverStage>();
        bool busyChecked=false,resumeChecked=false,privateResumeChecked=false,mapChecked=false;int count=0;
        for(int frame=0;frame<20000;frame++)
        {
            driver.Advance(.05f);clock+=.05;Physics.SyncTransforms();
            roads.Simulate(.05f,clock,driver.Vehicle!=null?driver.Vehicle.transform.position:trip.parkedPickup.transform.position,true);
            Physics.Simulate(.05f);stages.Add(driver.Stage);
            if(driver.IsAway && !busyChecked)
            {
                Require(trip.Fleet.DriverHasVehicle,"Driver did not reserve the player car.");
                Require(!trip.Fleet.BuyAndSelect(trip.Fleet.Catalog[0].id) && trip.Fleet.Notice.Contains("driver"),"Player exchanged a vehicle in use by the driver.");
                Require(!shop.TryDismiss(StaffRole.Driver),"Driver dismissed in transit.");trip.Begin();Require(!trip.Transitioning,"Player drove the driver's reserved car.");busyChecked=true;
            }
            if(driver.Stage==DriverStage.Driving && !resumeChecked && frame>1000)
            {
                var snapshot=JsonUtility.FromJson<DriverJourneySaveData>(JsonUtility.ToJson(driver.Capture()));
                Require(snapshot.active,"Active trip absent from save data.");driver.Restore(snapshot);resumeChecked=true;
                yield return null;
            }
            if(driver.Stage==DriverStage.Driving && !mapChecked && frame>300)
            {
                var canvas=Object.FindObjectsOfType<Canvas>().First(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay);
                driver.OpenMap(canvas.transform);yield return null;CaptureUi("DriverMap");Object.Destroy(Object.FindObjectOfType<DriverMapView>().gameObject);yield return null;
                DriverFollowView.Open(driver,canvas.transform);for(int i=0;i<20;i++)yield return null;CaptureUi("DriverOnRoad");
                Require(!Camera.main.GetComponent<MainCamera>().enabled,"Follow view did not take over the camera.");
                Object.Destroy(Object.FindObjectOfType<DriverFollowView>().gameObject);yield return null;
                Require(Camera.main.GetComponent<MainCamera>().enabled,"Follow view did not restore the store camera.");mapChecked=true;
            }
            if(driver.Stage==DriverStage.LeavingParking && orders.PickupWeightKg>0 && !privateResumeChecked)
            {
                var snapshot=JsonUtility.FromJson<DriverJourneySaveData>(JsonUtility.ToJson(driver.Capture()));var cargo=JsonUtility.ToJson(orders.Ledger.Capture());
                driver.Restore(snapshot);
                typeof(DriverDelivery).GetField("route",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(driver,new DriverRoadRoute(roads.Network));
                driver.Advance(.001f);
                Require(Vector3.Distance(driver.NativePosition,snapshot.position)<.01f && JsonUtility.ToJson(orders.Ledger.Capture())==cargo,"Private access reload moved/lost the paid cargo.");
                privateResumeChecked=true;yield return null;
            }
            if(frame%100==0)
            {
                File.WriteAllText(StaffUpdateBatch.Report+"/journey-state.txt",$"{clock:0.0}s {driver.Stage}: {driver.Status}\nNative {driver.NativePosition}, stock {orders.products.GetCount(product)}, cargo {orders.PickupWeightKg}\n");
                yield return null;
            }
            if(driver.Stage==DriverStage.Idle && busyChecked && orders.products.GetCount(product)>old){count++;break;}
        }
        Require(count==1,"Real delivery failed: "+File.ReadAllText(StaffUpdateBatch.Report+"/journey-state.txt"));
        foreach(var stage in new[]{DriverStage.WalkingToCar,DriverStage.Boarding,DriverStage.Driving,DriverStage.Loading,DriverStage.Unloading,DriverStage.Exiting})Require(stages.Contains(stage),"Invisible/skipped journey stage: "+stage);
        Require(resumeChecked,"Saved trip restoration not exercised.");
        Require(privateResumeChecked,"Private access save restoration not exercised.");
        Require(mapChecked,"Driver map and real follow camera not exercised.");
        Require(orders.products.GetCount(product)==old+product.BoxAmount && orders.PickupWeightKg==0 && orders.DepotWeightKg==0,"Paid cargo lost or duplicated.");
        Require(!trip.Fleet.DriverHasVehicle && trip.parkedPickup.activeSelf && driver.Employee.gameObject.activeSelf,"Car or driver did not return to parking.");
        File.WriteAllText(StaffUpdateBatch.Report+"/suppliers.txt","PASS Wholesale paid product round trip.\n");
        foreach(var supplier in new[]{SupplierKind.Bakery,SupplierKind.Farmers,SupplierKind.Equipment,SupplierKind.Construction,SupplierKind.Decoration})
        {
            var goods=orders.productCatalog.Products.FirstOrDefault(p=>p!=null&&SupplierRouting.ForProduct(p.StorageType)==supplier&&new CargoCapacity(350,2).Fits(0,0,p.BoxAmount*p.UnitWeightKg,p.BoxAmount*p.UnitVolumeM3));
            var fixture=goods==null?orders.buildCatalog.Items.FirstOrDefault(b=>b!=null&&!b.hiddenFromShop&&SupplierRouting.ForBuilding(b)==supplier&&new CargoCapacity(350,2).Fits(0,0,b.weightKg,b.cargoVolumeM3)):null;
            Require(goods!=null||fixture!=null,"No vehicle-sized package at "+supplier);
            int before=goods!=null?orders.products.GetCount(goods):orders.buildings.GetCount(fixture);
            Require(goods!=null?orders.Order(goods):orders.Order(fixture),"Order refused at "+supplier);
            bool departed=false,returned=false;
            for(int frame=0;frame<30000;frame++)
            {
                driver.Advance(.05f);clock+=.05;Physics.SyncTransforms();
                roads.Simulate(.05f,clock,driver.Vehicle!=null?driver.Vehicle.transform.position:trip.parkedPickup.transform.position,true);Physics.Simulate(.05f);
                if(driver.IsAway)departed=true;
                if(frame%200==0){File.WriteAllText(StaffUpdateBatch.Report+"/journey-state.txt",$"{supplier} {clock:0.0}s {driver.Stage}: {driver.Status}\nNative {driver.NativePosition}, cargo {orders.PickupWeightKg}, blocker {driver.AccessObstacle}\n");yield return null;}
                if(departed && driver.Stage==DriverStage.Idle){returned=true;break;}
            }
            Require(returned,"Supplier journey failed: "+File.ReadAllText(StaffUpdateBatch.Report+"/journey-state.txt"));
            int after=goods!=null?orders.products.GetCount(goods):orders.buildings.GetCount(fixture);
            Require(after==before+(goods!=null?goods.BoxAmount:1)&&orders.DepotWeightKg==0&&orders.PickupWeightKg==0,"Supplier goods lost or duplicated: "+supplier);
            File.AppendAllText(StaffUpdateBatch.Report+"/suppliers.txt","PASS "+supplier+" physical access, loading, road return and exactly-once warehouse delivery.\n");
        }
        Require(shop.TryDismiss(StaffRole.Driver),"Returned driver could not be dismissed.");
        File.WriteAllText(StaffUpdateBatch.Report+"/journey.txt","PASS five role-specific rigs and animations; four-slot save migration; hire limit; player-car boarding; directed signal-controlled road journey; mid-trip save restore; actual paid package loaded and delivered exactly once; car, employee and ownership returned.\n");
    }
    private static System.Collections.Generic.IEnumerable<object> CheckDuties(RetailEmpireTycoon.StoreOperations.StoreOperations shop,DeliveryOrders orders)
    {
        var shelves=Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.PlacedShelfStock>();
        var product=AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/SodaCola_Product.asset");
        var shelf=shelves.First(s=>s.IsProductAllowed(product));
        var before=shop.BuildSaveData();var warehouse=orders.products.BuildSaveData();int money=orders.money.Money;
        var original=shop.Balance;var balance=Object.Instantiate(original);balance.thiefChance=balance.litterChance=0;balance.baseArrivalSeconds=10000;
        balance.wagesPerMinute=Vector4.zero;balance.staffTaskSeconds=1;
        ShopGameplaySetup.Set(shop,"balance",balance);
        try
        {
            foreach(var s in shelves)s.ClearStock();orders.products.Clear();orders.products.Add(product,30);
            shop.ApplySaveData(new ShopOperationsSaveData());shelf.SetStockFromSave(product,1);Require(shop.TryHire(StaffRole.Stocker),"Stocker hire failed.");shop.SetOpen(true);
            for(int i=0;i<600&&orders.products.GetCount(product)==30;i++)shop.Advance(.1f);
            Require(orders.products.GetCount(product)<30,"Stocker did not replenish from real warehouse stock.");yield return null;
            shop.ApplySaveData(new ShopOperationsSaveData());foreach(var s in shelves)s.ClearStock();
            var dirt=shop.SpawnDirt(shop.CheckoutPosition);Require(shop.TryHire(StaffRole.Cleaner),"Cleaner hire failed.");shop.SetOpen(true);
            for(int i=0;i<600&&shop.Dirt.Contains(dirt);i++)shop.Advance(.1f);
            Require(!shop.Dirt.Contains(dirt),"Cleaner did not remove dirt.");yield return null;
            shop.ApplySaveData(new ShopOperationsSaveData());shelf.SetStockFromSave(product,24);Require(shop.TryHire(StaffRole.Cashier),"Cashier hire failed.");shop.SetOpen(true);
            Require(shop.TrySpawnCustomer(),"Cashier has no accessible customer.");var buyer=shop.Visits[0];int wallet=orders.money.Money;
            for(int i=0;i<1000&&orders.money.Money==wallet;i++)shop.Advance(.1f);
            Require(orders.money.Money==wallet+buyer.Quantity*product.SellPrice && !shop.CompleteCheckout(buyer),"Cashier payment was missing or duplicated.");yield return null;
            shop.ApplySaveData(new ShopOperationsSaveData());shelf.SetStockFromSave(product,24);Require(shop.TryHire(StaffRole.Guard),"Security hire failed.");shop.SetOpen(true);
            Require(shop.TrySpawnCustomer(true),"Security has no accessible thief.");var thief=shop.Visits[0];
            for(int i=0;i<1000&&shop.Visits.Contains(thief);i++)shop.Advance(.1f);
            Require(!shop.Visits.Contains(thief)&&shelf.CurrentAmount==24,"Security failed to catch the thief and preserve stock.");
            File.WriteAllText(StaffUpdateBatch.Report+"/duties.txt","PASS stocker transfers real warehouse stock; cleaner walks to litter and removes it; cashier settles exactly once; security intercepts an escaping thief without lost goods. Real role models and store navigation, no mocked job completion.\n");
        }
        finally
        {
            ShopGameplaySetup.Set(shop,"balance",original);shop.ApplySaveData(before);orders.products.ApplySaveData(warehouse,orders.productCatalog);orders.money.SetMoney(money);Object.Destroy(balance);
            foreach(var s in shelves)s.ClearStock();
        }
    }
    private static void Finish(Exception error)
    {SessionState.SetBool(Pending,false);File.WriteAllText(StaffUpdateBatch.Report+"/play.txt",error==null?"PASS staff and real driver delivery. No player save read or written.\n":"FAIL "+error);EditorApplication.Exit(error==null?0:1);}
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void CaptureUi(string name)
    {
        Directory.CreateDirectory("Docs/StaffUpdate");var camera=Camera.main;var previous=RenderTexture.active;var old=camera.targetTexture;
        var target=new RenderTexture(1600,1000,24);var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
        var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
        try
        {
            target.Create();camera.targetTexture=target;
            foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=.6f;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();File.WriteAllBytes("Docs/StaffUpdate/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=old;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(texture);
        }
    }
}
