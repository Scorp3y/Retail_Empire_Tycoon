using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Real save-free Game/City purchase, cargo, wheel-physics and return scenario.</summary>
[InitializeOnLoad]
public static class PlayerVehicleValidation
{
    private const string Pending="PlayerVehicles.Pending";
    private static IEnumerator checks;
    private static double deadline;
    static PlayerVehicleValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(PlayerVehicleModelBatch.Report);
        foreach(string name in new[]{"play","purchase","rules","city","physics"})File.WriteAllText(PlayerVehicleModelBatch.Report+"/"+name+".txt","RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){checks=Check();deadline=EditorApplication.timeSinceStartup+240;}
            Require(EditorApplication.timeSinceStartup<deadline,"Vehicle integration timed out.");
            if(checks.MoveNext())return;
            Record();Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<5;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer exists.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var trip=starter.cityTrip;var orders=trip.orders;orders.save=null;
        var data=starter.CreateNewGame(50000);Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        starter.building.inventory.ApplySaveData(data.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>().enabled=false;
        Object.FindObjectOfType<RetailEmpireTycoon.Parking.ParkingTraffic>().enabled=false;
        orders.money.SetMoney(1000000);trip.Fleet.Restore(null);
        Require(trip.Fleet.Catalog.Count==5&&trip.Fleet.SelectedId==PlayerVehicleFleet.StarterId&&orders.pickupCapacityKg==350,"Legacy/new save starting pickup changed.");
        Domain(orders,trip.Fleet.Catalog.ToArray());
        orders.Ledger.Restore(null);trip.Fleet.Restore(null);orders.money.SetMoney(1000000);
        trip.Begin();
        while(trip.Transitioning||!trip.InCity)yield return null;
        var city=Object.FindObjectOfType<CityWorld>();city.pickup.ReadKeyboard=false;city.chaseCamera.ReadMouse=false;
        var checkpoint=city.town.suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
        city.pickup.Body.position=checkpoint.transform.position+Vector3.up*.12f;city.pickup.Body.rotation=checkpoint.transform.rotation;city.pickup.Stop();
        for(int i=0;i<4;i++)yield return null;
        city.pickup.Stop();Physics.SyncTransforms();
        typeof(CityWorld).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(city,null);
        Require(checkpoint.CanUse(city.pickup),"Showroom checkpoint unavailable: "+checkpoint.UnavailableReason(city.pickup));
        city.Interact();
        Require(Object.FindObjectsOfType<Button>().Any(b=>b.name.StartsWith("Buy ·")),"Showroom has no real purchase buttons.");
        foreach(var definition in trip.Fleet.Catalog)
        {
            var button=Object.FindObjectsOfType<Button>().Single(b=>b.name=="Buy · $"+definition.price.ToString("N0"));
            int before=orders.money.Money;button.onClick.Invoke();yield return null;
            Require(trip.Fleet.SelectedId==definition.id&&trip.Fleet.Owns(definition.id)&&orders.money.Money==before-definition.price,"UI purchase did not select/debit exactly once.");
            Require(orders.pickupCapacityKg==definition.payloadKg&&orders.pickupCapacityM3==definition.cargoM3,"Actual loading limits differ from vehicle card.");
            Require(city.pickup.wheels.Length==definition.model.GetComponent<PlayerVehicleModel>().wheels.Length,"Selected physical wheel rig missing.");
            Require(trip.parkedPickup.GetComponentsInChildren<PlayerVehicleModel>(true).Any(model=>model.gameObject.activeSelf&&model.gameObject.name.StartsWith(definition.model.name,StringComparison.Ordinal)),"Parked shop model was not updated.");
            CaptureUi(definition.id+"_ShowroomUI");
        }
        var saved=JsonUtility.ToJson(new GameData{vehicles=trip.Fleet.Capture()});
        var restored=JsonUtility.FromJson<GameData>(saved);trip.Fleet.Restore(restored.vehicles);
        Require(trip.Fleet.Capture().ownedIds.Count==5&&trip.Fleet.SelectedId=="heavy_box_truck","Fleet JSON roundtrip lost ownership or selected vehicle.");
        typeof(CityWorld).GetMethod("ClosePanel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(city,null);
        File.WriteAllText(PlayerVehicleModelBatch.Report+"/purchase.txt","PASS actual Game -> City dealership; all five purchase buttons debit once, switch physical/parked models and real cargo limits; ownership and heavy selection survive GameData JSON roundtrip; legacy pickup remains owned.\n");
        foreach(var step in Drive(city,trip))yield return step;
        // Return uses the same selected truck and never writes to the real player's save.
        trip.Return();while(trip.Transitioning||trip.InCity)yield return null;
        Require(trip.Fleet.SelectedId=="heavy_box_truck"&&orders.pickupCapacityKg==6000,"Return to shop lost truck or capacity.");
        Require(trip.parkedPickup.GetComponentsInChildren<Renderer>().Any(renderer=>renderer.enabled&&renderer.gameObject.activeInHierarchy),"Selected parked truck is invisible on return.");
        File.WriteAllText(PlayerVehicleModelBatch.Report+"/city.txt","PASS Game -> City -> Game retains selected heavy vehicle, capacities and parking anchor. No player save, preferences, commits or pushes used.\n");
    }
    private static void Domain(DeliveryOrders orders,PlayerVehicleDefinition[] catalog)
    {
        int persisted=0;string receipt=null;
        PlayerVehicleFleet fleet=null;fleet=new PlayerVehicleFleet(catalog,orders.money,orders,()=>{persisted++;receipt=JsonUtility.ToJson(new GameData{vehicles=fleet.Capture(),playerMoney=orders.money.Money});});
        orders.money.SetMoney(0);Require(!fleet.BuyAndSelect(catalog[0].id)&&persisted==0,"Unaffordable purchase mutated ownership.");
        orders.money.SetMoney(1000000);
        float kg=350,m3=2;
        foreach(var item in catalog)
        {
            Require(item.payloadKg>kg&&item.cargoM3>m3,"Upgrade capacities do not strictly increase.");kg=item.payloadKg;m3=item.cargoM3;
            int before=orders.money.Money;Require(fleet.BuyAndSelect(item.id),"Purchase rejected.");
            Require(orders.money.Money==before-item.price,"Payment incorrect.");
            Require(fleet.BuyAndSelect(item.id)&&orders.money.Money==before-item.price,"Owned selection charged again.");
            Require(JsonUtility.FromJson<GameData>(receipt).vehicles.selectedId==item.id,"Persistence captured stale selection.");
            var model=item.model.GetComponent<PlayerVehicleModel>();Require(model.wheels.Length==(item.id=="heavy_box_truck"?6:4),"Axle count incorrect.");
            Require(item.model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m.name=="OpaqueGlass").All(m=>m.GetColor("_BaseColor").a==1&&m.GetFloat("_Surface")==0),"Transparent windows found.");
        }
        var building=orders.buildCatalog.Items.First(item=>item!=null&&item.weightKg>0&&!item.hiddenFromShop);
        orders.Ledger.Order(building.id,DeliveryItemKind.Building,1);orders.Ledger.Transfer(building.id,DeliveryItemKind.Building,DeliveryLocation.Depot,DeliveryLocation.Pickup,1);
        int balance=orders.money.Money;Require(!fleet.Select(PlayerVehicleFleet.StarterId)&&fleet.SelectedId=="heavy_box_truck"&&orders.money.Money==balance,"Loaded vehicle was exchanged.");
        Require(orders.Ledger.Count(building.id,DeliveryItemKind.Building,DeliveryLocation.Pickup)==1,"Exchange removed cargo.");
        orders.Ledger.Restore(null);
        // A partial unload must preserve every unit that did not fit; retrying cannot duplicate delivery.
        orders.buildings.ApplySaveData(new System.Collections.Generic.List<BuildInventorySaveEntry>(),orders.buildCatalog);
        orders.products.ApplySaveData(new System.Collections.Generic.List<ProductInventorySaveEntry>(),orders.productCatalog);
        float capacity=orders.shopCapacityKg;orders.shopCapacityKg=building.weightKg*2.1f;
        orders.Ledger.Order(building.id,DeliveryItemKind.Building,5);orders.Ledger.Transfer(building.id,DeliveryItemKind.Building,DeliveryLocation.Depot,DeliveryLocation.Pickup,5);
        Require(orders.Unload()&&orders.Ledger.Count(building.id,DeliveryItemKind.Building,DeliveryLocation.Pickup)==3,"Partial unloading lost or failed to retain cargo.");
        Require(!orders.Unload()&&orders.Ledger.Count(building.id,DeliveryItemKind.Building,DeliveryLocation.Pickup)==3,"Full warehouse duplicated cargo.");
        orders.shopCapacityKg=capacity;orders.Ledger.Restore(null);
        File.WriteAllText(PlayerVehicleModelBatch.Report+"/rules.txt","PASS insufficient funds, strictly increasing limits, no second charge, persistence snapshot, opaque glass, 4/6 wheel rigs, loaded exchange refusal and cargo conservation across partial unload/retry.\n");
    }
    private static System.Collections.Generic.IEnumerable<object> Drive(CityWorld city,CityTrip trip)
    {
        Physics.simulationMode=SimulationMode.Script;
        File.WriteAllText(PlayerVehicleModelBatch.Report+"/physics.txt",string.Empty);
        var plane=GameObject.CreatePrimitive(PrimitiveType.Cube);plane.name="Isolated wheel-physics test surface";plane.transform.position=new Vector3(1000,-.25f,1000);plane.transform.localScale=new Vector3(130,.5f,130);
        city.GetComponent<CityRiverRecovery>().enabled=false;
        var drive=city.pickup;drive.enabled=false;
        var fixedTick=typeof(PickupDrive).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        var lateTick=typeof(PickupDrive).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(var definition in trip.Fleet.Catalog)
        {
            Require(trip.Fleet.Select(definition.id),"Empty vehicle selection rejected.");
            drive.Body.position=new Vector3(1000,.1f,1000);drive.Body.rotation=Quaternion.identity;drive.Stop();drive.InputEnabled=true;
            Physics.SyncTransforms();
            for(int step=0;step<130;step++){fixedTick.Invoke(drive,null);Physics.Simulate(.02f);lateTick.Invoke(drive,null);}
            Require(drive.GroundedWheels>=drive.wheels.Length-1,definition.id+" suspension is not grounded: "+drive.GroundedWheels);
            Vector3 start=drive.Body.position;Quaternion wheel=drive.wheels[0].visual.rotation;drive.SetInput(1,.16f,false);
            for(int step=0;step<200;step++){fixedTick.Invoke(drive,null);Physics.Simulate(.02f);lateTick.Invoke(drive,null);if(step%40==0)yield return null;}
            Require(Vector3.Distance(drive.Body.position,start)>3&&drive.Speed>1,definition.id+" cannot drive.");
            Require(Mathf.Abs(drive.SteeringAngle)>1&&Quaternion.Angle(wheel,drive.wheels[0].visual.rotation)>1,definition.id+" steering/wheel rotation frozen.");
            drive.SetInput(0,0,true);
            for(int step=0;step<180;step++){fixedTick.Invoke(drive,null);Physics.Simulate(.02f);lateTick.Invoke(drive,null);}
            Require(drive.Speed<.7f,definition.id+" cannot brake: "+drive.Speed);
            Require(Vector3.Dot(drive.transform.up,Vector3.up)>.9f,definition.id+" rolled over on level road.");
            File.AppendAllText(PlayerVehicleModelBatch.Report+"/physics.txt",$"PASS {definition.id}: {drive.wheels.Length} wheel contacts, grounded suspension, acceleration, visible steering/spin, braking, stable chassis.\n");
        }
        trip.Fleet.Select("heavy_box_truck");Physics.simulationMode=SimulationMode.FixedUpdate;drive.enabled=true;
        Object.Destroy(plane);
    }
    private static void CaptureUi(string name)
    {
        var camera=Camera.main;var target=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        var canvases=Object.FindObjectsOfType<Canvas>().Where(canvas=>canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(canvas=>canvas.worldCamera).ToArray();var planes=canvases.Select(canvas=>canvas.planeDistance).ToArray();
        var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
        try
        {
            target.Create();camera.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.6f;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
            File.WriteAllBytes(PlayerVehicleModelBatch.Pictures+"/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(texture);Canvas.ForceUpdateCanvases();
        }
    }
    private static void Record()
    {
        var files=File.ReadAllLines(PlayerVehicleModelBatch.Report+"/outputs.txt")
            .Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/")&&File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/PlayerVehicleModelBatch.cs","Assets/Project/Editor/PlayerVehicleShowroomBatch.cs","Assets/Project/Editor/PlayerVehicleValidation.cs","Assets/Project/Editor/LowPolyPropMesh.cs","Assets/Project/Editor/TownShowroomPlacement.cs"})
            .Distinct().OrderBy(path=>path);
        File.WriteAllLines(PlayerVehicleModelBatch.Report+"/inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);File.WriteAllText(PlayerVehicleModelBatch.Report+"/play.txt",error==null?"PASS full player fleet integration.\n":"FAIL "+error);
        EditorApplication.Exit(error==null?0:1);
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
