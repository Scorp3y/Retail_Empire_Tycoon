using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Shelves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class TownWorldValidation
{
    private const string Report="Library/TownWorldQA";
    public static void ValidateAssets()
    {
        var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownWorldSetup.LayoutPath);TownWorldPlan.Validate(layout);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(TownWorldAssembler.Folder+"/TownSurface.shader");
        Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Town surface shader did not compile.");
        var contents=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        int renderers=0,triangles=0;
        try
        {
            var places=contents.GetComponent<TownWorldPlaces>();Require(places!=null&&places.suppliers.Length==7,"Seven distinct supplier/showroom sites required.");
            Require(places.suppliers.Select(s=>s.supplier).Distinct().Count()==7,"Duplicate supplier destination.");
            Require(contents.GetComponentsInChildren<Renderer>().Length<180,"World render chunks exceeded budget.");
            Require(contents.GetComponentsInChildren<MonoBehaviour>().All(c=>c!=null),"Missing world script.");
            foreach(var renderer in contents.GetComponentsInChildren<Renderer>())
                Require(renderer.sharedMaterials.All(m=>m!=null&&m.shader.name.StartsWith("Universal Render Pipeline/")),"Broken world material.");
            var buildings=contents.transform.Find("Residential houses");Require(buildings!=null&&buildings.childCount>=20,"Residential district is not populated.");
            renderers=contents.GetComponentsInChildren<Renderer>(true).Length;
            triangles=contents.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3);
        }
        finally {PrefabUtility.UnloadPrefabContents(contents);}
        var game=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        Require(game.GetRootGameObjects().Any(r=>r.name=="Shared town environment"),"Town not installed in Game.");
        Require(game.GetRootGameObjects().Any(r=>r.name=="Town pedestrian promenade"),"Pedestrian sidewalk route missing.");
        var gameVolume=game.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Volume>()).Single(v=>v.name=="Town atmosphere").sharedProfile;
        var gameSun=RenderSettings.sun;var lightRotation=gameSun.transform.rotation;var lightColor=gameSun.color;
        foreach(var spawn in game.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AvatarSpawn>(true)))
            foreach(var point in spawn.waypoints)
            {
                Vector3 native=layout.MapShopPoint(point.position);
                Require(Mathf.Abs(native.z+137)<.01f,"Ambient pedestrian waypoint left the authored sidewalk.");
            }
        var city=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var world=city.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();
        Require(world.useFixedShopOrigin&&world.shopScale==layout.shopToCityScale,"Store/city coordinates diverge.");
        Require(world.pickup!=null&&world.chaseCamera!=null&&world.cargoRoot!=null,"Driving references lost.");
        var cityVolume=city.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Volume>()).Single(v=>v.name=="Town atmosphere").sharedProfile;
        Require(gameVolume==cityVolume&&Quaternion.Angle(RenderSettings.sun.transform.rotation,lightRotation)<.01f&&RenderSettings.sun.color==lightColor,"Shop and city art profiles diverged.");
        Require(world.chaseCamera.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing,"Driving camera ignores shared grading.");
        var graphics=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        Require(graphics.name.StartsWith("Town Graphics")&&graphics.msaaSampleCount>=2,"Town graphics quality profiles missing.");
        File.WriteAllText(Report+"/assets.txt",$"PASS connected road graph ({layout.roads.Count} tiles), reciprocal seams, expansion parcel kept free, seven supplier sites, shared environment and renderer budget.\nGeometry: {renderers} renderers, {triangles} triangles including all future-plot scenery.\n");
    }
    public static void Run()
    {
        Require(EditorApplication.isPlaying&&EditorSceneManager.GetActiveScene().path==ShopGameplaySandbox.Path,"Use isolated sandbox.");
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer found.");
        var trip=Object.FindObjectOfType<CityTrip>();trip.orders.save=null;trip.StartCoroutine(Check(trip));
    }
    private static IEnumerator Check(CityTrip trip)
    {
        var log=new StringBuilder();bool passed=false;var orders=trip.orders;
        try
        {
            orders.money.SetMoney(50000);orders.shopCapacityKg=1000000;
            var init=Object.FindObjectOfType<StarterStoreInitialization>();
            init.ConfigureSite(true);var start=init.CreateNewGame(50000);init.ApplyNewGameBuildings(start);
            Object.FindObjectOfType<RetailEmpireTycoon.SaveSystem.ProductSaveService>().ApplyShelfSaveData(start.shelfStocks);
            var bread=orders.productCatalog.Products.First(p=>p.StorageType==ProductStorageType.Bakery);
            var fruit=orders.productCatalog.Products.First(p=>p.StorageType==ProductStorageType.Produce);
            var equipment=orders.buildCatalog.Items.First(b=>b!=null&&b.category==BuildCategory.Shelf);
            Require(orders.buildCatalog.Items.Where(b=>b!=null&&b.isCheckout).All(b=>SupplierRouting.ForBuilding(b)==SupplierKind.Equipment),"Legacy cash registers routed to construction supplier.");
            float oldCapacity=orders.depotCapacityKg;
            orders.depotCapacityKg=Mathf.Max(bread.BoxAmount*bread.UnitWeightKg,fruit.BoxAmount*fruit.UnitWeightKg)+.001f;
            Require(orders.Order(bread)&&orders.Order(fruit),"Separate supplier capacities were pooled.");
            int moneyBeforeRejection=orders.money.Money;
            var heavier=bread.BoxAmount*bread.UnitWeightKg>=fruit.BoxAmount*fruit.UnitWeightKg?bread:fruit;
            Require(!orders.Order(heavier)&&orders.money.Money==moneyBeforeRejection,"Full supplier warehouse charged an invalid order.");
            orders.Ledger.Restore(new DeliverySaveData());orders.money.SetMoney(50000);orders.depotCapacityKg=oldCapacity;
            log.AppendLine("PASS independent supplier warehouse capacity and no-charge rejection");
            Require(orders.Order(bread)&&orders.Order(fruit)&&orders.Order(equipment),"Orders failed.");
            var breadEntry=orders.Ledger.Entries.First(e=>e.itemId==bread.Id);
            int paid=orders.money.Money;
            Require(!orders.LoadFrom(breadEntry,SupplierKind.Wholesale),"Wrong supplier released bakery stock.");
            Require(orders.money.Money==paid&&orders.Ledger.Count(bread.Id,DeliveryItemKind.Product,DeliveryLocation.Pickup)==0,"Rejected load changed stock or money.");
            log.AppendLine("PASS old save-compatible category routing and wrong-supplier rejection");
            yield return null;CaptureShop(Camera.main);
            trip.Begin();float deadline=Time.realtimeSinceStartup+90;
            while((!trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(trip.InCity&&!trip.Transitioning,"Loading transition failed.");
            var city=Object.FindObjectOfType<CityWorld>();city.pickup.ReadKeyboard=false;city.chaseCamera.ReadMouse=false;
            Require(Camera.allCameras.Count(c=>c.enabled)==1,"More than one live gameplay camera.");
            Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer appeared in City.");
            CheckRoadClearance(city);
            log.AppendLine("PASS real loading, one driving camera, clear roads and reachable supplier bays");
            var map=city.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="M — Карта города"));
            map.onClick.Invoke();yield return null;
            Require(!city.pickup.InputEnabled&&city.GetComponentInChildren<CityMapGraphic>()!=null,"Map did not block driving or display actual road layout.");
            var choose=city.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text==SupplierRouting.Name(SupplierKind.Bakery)));
            choose.onClick.Invoke();yield return null;
            Require(city.pickup.InputEnabled&&city.GetComponentInChildren<CityMapGraphic>()==null,"Map destination did not resume driving.");
            var scenery=city.town.GetComponentInChildren<TownExpansionScenery>();
            Require(scenery!=null&&scenery.plots.Length==city.town.layout.expansionPlots.Count,"Expansion landscapes missing.");
            var state=new ProgressState();state.Purchased.Add(scenery.plots[0].territory);scenery.ApplyState(state);
            Require(!scenery.plots[0].model.activeSelf&&scenery.plots.Skip(1).All(p=>p.model.activeSelf),"Buying one plot removed wrong greenery.");
            scenery.ApplyState(new ProgressState());
            log.AppendLine("PASS actual city map, selected supplier, driving input resume and purchase-aware greenery");
            foreach(var kind in new[]{SupplierKind.Wholesale,SupplierKind.Construction,SupplierKind.Decoration})
            {
                var station=city.town.suppliers.Single(c=>c.supplier==kind);
                city.pickup.Body.position=station.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();yield return null;
                city.Interact();yield return null;
                Click(city,"Ассортимент");yield return null;
                int beforeMoney=orders.money.Money;
                var purchase=city.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="Заказать"));
                Require(purchase!=null,"Empty supplier assortment: "+kind);purchase.onClick.Invoke();yield return null;
                Require(orders.money.Money<beforeMoney&&orders.Ledger.Entries.Any(e=>e.location==DeliveryLocation.Depot&&orders.SupplierFor(e)==kind),"Supplier order failed: "+kind);
                Click(city,"×");yield return null;
            }
            // These UI checks run only with disposable inventories; keep the subsequent three-stop cargo assertion exact.
            foreach(var entry in orders.Ledger.Entries.Where(e=>e.location==DeliveryLocation.Depot&&orders.SupplierFor(e)!=SupplierKind.Bakery&&orders.SupplierFor(e)!=SupplierKind.Farmers&&orders.SupplierFor(e)!=SupplierKind.Equipment).ToArray())
                Require(orders.Ledger.Transfer(entry.itemId,entry.kind,DeliveryLocation.Depot,DeliveryLocation.Pickup,entry.quantity)&&orders.Ledger.Deliver(entry.itemId,entry.kind,entry.quantity),"Cannot reset QA-only orders.");
            var showroom=city.town.suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
            city.pickup.Body.position=showroom.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();yield return null;
            city.Interact();yield return null;Click(city,"Продолжить осмотр");yield return null;
            Require(city.pickup.InputEnabled,"Showroom did not resume car controls.");
            log.AppendLine("PASS supplier assortment purchases at wholesale, construction and decoration sites; car-only showroom");
            var shelfCount=trip.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlacedShelfStock>(true)).Sum(s=>s.CurrentAmount);
            Require(shelfCount>0,"Starter shelf stock lost during trip.");
            foreach(var kind in new[]{SupplierKind.Bakery,SupplierKind.Farmers,SupplierKind.Equipment})
            {
                var station=city.town.suppliers.Single(c=>c.supplier==kind);
                city.pickup.Body.position=station.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();yield return null;
                Require(station.CanUse(city.pickup),"Supplier checkpoint unavailable: "+kind);
                city.Interact();yield return null;
                var load=city.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="Загрузить"||t.text=="Load"));
                Require(load!=null,"Supplier order UI did not expose loading: "+kind);load.onClick.Invoke();yield return null;
                var close=city.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="×"));close.onClick.Invoke();yield return null;
            }
            Require(orders.PickupWeightKg>0&&city.cargoRoot.childCount>0,"Loaded cargo not visible.");
            Require(orders.Ledger.Entries.All(e=>e.location==DeliveryLocation.Pickup),"Some supplier orders were not collected.");
            log.AppendLine("PASS real supplier order UI, three separate collection sites and visible cargo");
            Capture(city,"TownOverview",new Vector3(200,520,-490),new Vector3(0,0,10),310,true);
            Capture(city,"TownStreet",new Vector3(-92,13,37),new Vector3(-70,1.8f,74),0,false);
            Capture(city,"TownIndustrial",new Vector3(-125,15,-65),new Vector3(-168,2,-20),0,false);
            Capture(city,"TownFarm",new Vector3(177,18,-70),new Vector3(221,1,23),0,false);
            Capture(city,"TownPark",new Vector3(-5,19,139),new Vector3(24,1,183),0,false);
            city.pickup.Body.position=new Vector3(-77,.12f,49);city.pickup.Body.rotation=Quaternion.identity;city.pickup.Stop();
            yield return new WaitForFixedUpdate();yield return null;
            city.pickup.transform.rotation=Quaternion.identity;
            city.chaseCamera.ResetBehindVehicle();city.chaseCamera.Orbit(12,2);
            for(int i=0;i<18;i++)yield return new WaitForFixedUpdate();
            CaptureShop(city.chaseCamera.GetComponent<Camera>(),"TownDrive");
            int beforeProducts=orders.products.GetCount(bread),beforeBuildings=orders.buildings.GetCount(equipment);
            city.pickup.Body.position=city.unload.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();
            Require(city.unload.CanUse(city.pickup),"Shop unloading bay unavailable.");Require(orders.Unload(),"Shop unloading failed.");
            Require(orders.products.GetCount(bread)==beforeProducts+bread.BoxAmount&&orders.buildings.GetCount(equipment)==beforeBuildings+1,"Unloading granted wrong inventory.");
            var saved=JsonUtility.ToJson(orders.Ledger.Capture());orders.Ledger.Restore(JsonUtility.FromJson<DeliverySaveData>(saved));
            Require(orders.PickupWeightKg==0,"Delivered cargo returned after save roundtrip.");
            city.pickup.Body.position=city.home.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();
            Require(city.home.CanUse(city.pickup),"Return marker unavailable.");trip.Return();deadline=Time.realtimeSinceStartup+90;
            while((trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!trip.InCity&&!trip.Transitioning,"Return transition failed.");
            Require(Object.FindObjectOfType<PlacedShelfStock>()!=null,"Shop did not resume.");
            log.AppendLine("PASS unload to real shop inventories, save roundtrip and return with shop preserved");passed=true;
        }
        finally {File.WriteAllText(Report+"/integration.txt",(passed?"TOWN WORLD PASSED\n":"FAILED\n")+log);}
    }
    private static void CheckRoadClearance(CityWorld city)
    {
        Physics.SyncTransforms();var scene=city.gameObject.scene;
        foreach(var road in city.town.layout.roads)
        {
            var p=road.Position(12)+Vector3.up;
            var obstacles=Physics.OverlapBox(p,new Vector3(2.4f,.6f,2.4f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>c.gameObject.scene==scene&&!c.transform.IsChildOf(city.pickup.transform)).ToArray();
            Require(obstacles.Length==0,"Road obstructed: "+road.cell+" by "+string.Join(",",obstacles.Select(c=>c.name)));
        }
        foreach(var supplier in city.town.suppliers)
        {
            var point=supplier.transform.position+Vector3.up;
            var obstacles=Physics.OverlapBox(point,new Vector3(1.25f,.65f,2.3f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>c.gameObject.scene==scene&&!c.transform.IsChildOf(city.pickup.transform)).ToArray();
            Require(obstacles.Length==0,"Loading bay obstructed: "+supplier.name);
        }
    }
    private static void Click(CityWorld city,string text)
    {
        city.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text==text)).onClick.Invoke();
    }
    private static void CaptureShop(Camera camera,string name="TownShop")
    {
        Require(camera!=null,"Shop camera missing.");
        var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
        var texture=new RenderTexture(1600,1100,24);var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        try
        {
            texture.Create();camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
            image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Report+"/"+name+".png",image.EncodeToPNG());
        }
        finally {camera.targetTexture=previousTarget;RenderTexture.active=previousActive;texture.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(image);}
    }
    private static void Capture(CityWorld city,string name,Vector3 position,Vector3 focus,float orthographic,bool overview)
    {
        var root=new GameObject("QA camera",typeof(Camera));SceneManager.MoveGameObjectToScene(root,city.gameObject.scene);
        var camera=root.GetComponent<Camera>();camera.enabled=false;camera.farClipPlane=1600;camera.nearClipPlane=.1f;
        camera.backgroundColor=new Color(.7f,.82f,.9f);camera.clearFlags=overview?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camera.orthographic=orthographic>0;camera.orthographicSize=orthographic;camera.fieldOfView=65;
        root.transform.position=position;root.transform.LookAt(focus);
        var texture=new RenderTexture(1600,1100,24);var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;bool fog=RenderSettings.fog;
        try
        {
            if(overview)RenderSettings.fog=false;texture.Create();camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
            image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Report+"/"+name+".png",image.EncodeToPNG());
        }
        finally {RenderSettings.fog=fog;RenderTexture.active=previous;texture.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(image);Object.DestroyImmediate(root);}
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
