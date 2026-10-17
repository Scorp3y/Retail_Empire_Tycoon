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
    public static void ValidateAssets(float expectedShopPickupLength)
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
            var western=places.suppliers.Where(s=>s.supplier!=SupplierKind.Farmers&&s.supplier!=SupplierKind.Showroom).ToArray();
            Require(western.All(s=>Mathf.Abs(s.transform.localPosition.x+252)<.01f),"City suppliers are not aligned along the western street.");
            Require(western.Select(s=>s.transform.localPosition.z).Distinct().Count()==western.Length,"Western supplier plots overlap.");
            Require(contents.GetComponentsInChildren<Renderer>().Length<480,"World render chunks exceeded budget.");
            Require(contents.GetComponentsInChildren<MonoBehaviour>().All(c=>c!=null),"Missing world script.");
            Require(contents.transform.Find("Mixed low-rise streets")?.GetComponentsInChildren<BoxCollider>().Length==13,"Thirteen mixed low-rise buildings are required.");
            Require(contents.transform.Find("Residential playgrounds")!=null&&contents.transform.Find("Residential sport courts")!=null
                &&contents.transform.Find("Riverfront promenade")!=null,"Playgrounds, sport courts or river walk missing.");
            Require(contents.GetComponentsInChildren<TownAmbientWalker>(true).Length==0
                &&contents.transform.Find("Ambient townspeople")==null,"Removed decorative townspeople returned to the saved world.");
            Require(contents.transform.Find("Street tree beds")!=null&&contents.transform.Find("Station arrival amenities")!=null
                &&contents.transform.Find("District paving detail")!=null,"Completed street paving, tree beds or station square missing.");
            foreach(var renderer in contents.GetComponentsInChildren<Renderer>())
                Require(renderer.sharedMaterials.All(m=>m!=null&&m.shader.name.StartsWith("Universal Render Pipeline/")),"Broken world material.");
            var buildings=contents.transform.Find("Urban buildings");Require(buildings!=null&&buildings.childCount>=30,"Dense urban blocks are missing.");
            Require(buildings.GetComponentsInChildren<BoxCollider>().Count(c=>c.size.x>=24)>=20,"Long buildings are missing.");
            Require(layout.shopToCityScale==2,"Compact store representation was not applied.");
            var courts=contents.GetComponentsInChildren<SupplierLoadingCourt>();
            Require(courts.Length==6&&courts.Select(c=>c.checkpoint.supplier).Distinct().Count()==6,"Six distinct rear collection courts required.");
            foreach(var court in courts)
            {
                Require(court.checkpoint.requiresRearDocking&&court.bounds.Contains(new Vector2(court.staging.x,court.staging.z)),"Rear court lacks docking contract or staging space.");
                float minimumDepth=court.designVehicleLength>10?35:20;
                var heading=court.checkpoint.transform.forward;
                float longitudinalDepth=Mathf.Abs(heading.x)*court.bounds.width+Mathf.Abs(heading.z)*court.bounds.height;
                Require(longitudinalDepth>=minimumDepth&&!court.bounds.Overlaps(layout.reservedShopPlot),"Service court is too shallow for its design vehicle or overlaps shop land.");
                if(court.checkpoint.supplier!=SupplierKind.Farmers)
                {
                    if(court.designVehicleLength>10)Require(Mathf.Abs(court.streetAccess.x+216)<.01f,"Industrial supplier access does not meet the western street.");
                    Require(layout.roads.Any(r=>Mathf.Abs(r.Position(layout.roadTileSize).x-court.streetAccess.x)<=layout.roadTileSize/2
                        &&Mathf.Abs(r.Position(layout.roadTileSize).z-court.streetAccess.z)<=layout.roadTileSize/2),"Supplier driveway misses the connected street.");
                }
            }
            Require(contents.transform.Find("Construction plot").GetComponentsInChildren<BoxCollider>().Any(c=>c.transform.position.x>120&&c.transform.position.x<164),"Construction site is not inside the town.");
            renderers=contents.GetComponentsInChildren<Renderer>(true).Length;
            triangles=contents.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3);
            Require(triangles<1400000,"World triangle budget exceeded.");
            var service=contents.GetComponentsInChildren<TownRailTraffic>().Single();
            Require(service.cars.Length==4&&service.cars.All(c=>c!=null&&c.GetComponent<MeshRenderer>()!=null&&c.GetComponent<MeshFilter>().sharedMesh!=null),"Saved train cars or visual meshes missing.");
            var farm=places.suppliers.Single(s=>s.supplier==SupplierKind.Farmers);
            Require(farm.transform.localPosition.x>500&&Vector3.Distance(farm.transform.localPosition,layout.cityShopOrigin)>650,"Farm is not separated from store.");
            var gas=contents.transform.Find("Roadside gas station");Require(gas!=null,"Approved gas station missing.");
            Require(contents.transform.Find("Rocky river banks")==null,"Removed river stones returned.");
            Require(!contents.GetComponentsInChildren<Transform>().Any(t=>t.name.Contains("Warehouse")&&t.position.x>160&&t.position.x<280&&t.position.z<50),"Removed warehouse returned beside gas station.");
        }
        finally {PrefabUtility.UnloadPrefabContents(contents);}
        var game=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        var asphalt=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/asphalt_01.asset").floorMaterial;
        var parking=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/parking_01.asset");
        Require(parking.footprint==new Vector2Int(6,11)&&Mathf.Abs(parking.placementBounds.size.x-.8034f)<.001f
            &&Mathf.Abs(parking.placementBounds.size.z-1.664f)<.001f,"Parking paint or occupancy was not enlarged 1.3 times.");
        var parked=Object.FindObjectOfType<CityTrip>(true).parkedPickup;
        var parkedBounds=CityUpdateSetup.Measure(parked);
        Require(Mathf.Abs(Mathf.Max(parkedBounds.size.x,parkedBounds.size.z)-expectedShopPickupLength)<.001f,"Shop pickup size differs from the requested 1.3 enlargement.");
        Require(asphalt.shader==shader&&asphalt.GetFloat("_PaletteIndex")==7
            &&asphalt.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(TownWorldAssembler.Folder+"/TownPalette.asset"),"Store asphalt does not use the road palette/shader.");
        Require(game.GetRootGameObjects().Any(r=>r.name=="Shared town environment"),"Town not installed in Game.");
        Require(!game.GetRootGameObjects().Any(r=>r.name=="Town pedestrian promenade"),"Removed background passerby route returned.");
        var gameVolume=game.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Volume>()).Single(v=>v.name=="Town atmosphere").sharedProfile;
        var gameSun=RenderSettings.sun;var lightRotation=gameSun.transform.rotation;var lightColor=gameSun.color;
        foreach(var spawn in game.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AvatarSpawn>(true)))
            Require(!spawn.enabled&&spawn.spawnPoint==null&&spawn.waypoints.Length==0,"Background passerby spawner is still configured.");
        var city=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var world=city.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();
        Require(world.useFixedShopOrigin&&world.shopScale==layout.shopToCityScale,"Store/city coordinates diverge.");
        Require(world.pickup!=null&&world.chaseCamera!=null&&world.cargoRoot!=null,"Driving references lost.");
        Require(Mathf.Abs(CityUpdateSetup.Measure(world.pickup.gameObject).size.z-4.4f)<.01f,"Driving pickup was not enlarged 1.3 times.");
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
        var trip=Object.FindObjectOfType<CityTrip>();trip.orders.save=null;trip.StartCoroutine(GuardChecks(Check(trip)));
    }
    // Unity does not propagate an exception from a yielded child iterator to the parent's finally block.
    // Flatten nested checks so a failed physics scenario ends the batch immediately instead of timing out.
    private static IEnumerator GuardChecks(IEnumerator routine)
    {
        var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(routine);
        while(stack.Count>0)
        {
            bool moved=false;object current=null;Exception failure=null;
            try {moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
            catch(Exception error){failure=error;}
            if(failure!=null)
            {
                while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
                File.AppendAllText(Report+"/integration.txt","\nFAILED nested check: "+failure+"\n");
                Debug.LogException(failure);yield break;
            }
            if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
            if(current is IEnumerator nested&&!(current is CustomYieldInstruction))stack.Push(nested);
            else yield return current;
        }
    }
    private static IEnumerator Check(CityTrip trip)
    {
        var log=new StringBuilder();bool passed=false;var orders=trip.orders;
        try
        {
            orders.money.SetMoney(50000);orders.shopCapacityKg=1000000;
            var init=Object.FindObjectOfType<StarterStoreInitialization>();
            init.ConfigureSite(true);var start=init.CreateNewGame(50000);init.ApplyNewGameBuildings(start);
            CheckFrontageUpgrade(init);
            CheckStructuralProportions(init);
            log.AppendLine("PASS two-cell wall/frame thickness, unchanged six-cell door aperture, non-overlapping starter structure and historical/custom wall save compatibility");
            yield return CheckStarterParking(init);
            log.AppendLine("PASS three functional starter bays (two visitors, one private), asphalt approaches, physical customer arrival/departure through the street entrance and non-destructive legacy exterior upgrade");
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
            CheckPublicSidewalk(city);
            Require(Object.FindObjectsOfType<AvatarSpawn>(true).All(spawn=>!spawn.enabled),
                "Background passersby can still spawn in a loaded gameplay scene.");
            Require(city.town.GetComponentsInChildren<TownAmbientWalker>(true).Length==0,
                "Removed decorative townspeople returned to the driving scene.");
            log.AppendLine("PASS no decorative townspeople in saved world or driving scene; store customer systems unchanged");
            Require(Camera.allCameras.Count(c=>c.enabled)==1,"More than one live gameplay camera.");
            Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer appeared in City.");
            yield return CheckStoreAccess(city);
            yield return TownPilotGameplayValidation.Check(city);
            log.AppendLine("PASS five-body pilot, full-width pedestrian passages, enlarged pickup parking envelopes and real 51m street drive; actual chase-camera capture");
            CheckFuelAccess(city);
            log.AppendLine("PASS clear gas station entry, forecourt and exit collision envelope");
            yield return CheckCollisionRecovery(city);
            log.AppendLine("PASS repeated off-centre wall/post impacts, no residual spin, working steering and reverse recovery");
            Capture(city,"TownStoreFrontage",new Vector3(-46,37,-162),new Vector3(-44,0,-114),0,false);
            Capture(city,"TownFuelStation",new Vector3(190,30,-161),new Vector3(216,1,-110),0,false);
            Capture(city,"TownFacades",new Vector3(-37,7,17),new Vector3(-37,2,2),0,false);
            Capture(city,"TownFarmParking",new Vector3(491,17,267),new Vector3(518,0,287),0,false);
            Capture(city,"TownFarmJunction",new Vector3(231,49,-96),new Vector3(290,0,-40),0,false);
            Capture(city,"TownSupplierRow",new Vector3(-405,250,-230),new Vector3(-245,0,0),0,true);
            Capture(city,"TownCivicSquare",new Vector3(-53,55,130),new Vector3(-78,1,165),0,false);
            Capture(city,"TownPlayground",new Vector3(-57,25,74),new Vector3(-69,1,100),0,false);
            Capture(city,"TownSportsCourt",new Vector3(54,24,83),new Vector3(25,1,100),0,false);
            Capture(city,"TownRiverfront",new Vector3(270,28,-141),new Vector3(298,0,-95),0,false);
            Capture(city,"TownMixedStreets",new Vector3(-156,22,-204),new Vector3(-173,2,-169),0,false);
            Capture(city,"TownCornerHousing",new Vector3(-151,27,-88),new Vector3(-171,2,-49),0,false);
            Capture(city,"TownLocalShops",new Vector3(-64,24,-209),new Vector3(-78,1,-173),0,false);
            Capture(city,"TownStationSquare",new Vector3(-157,32,181),new Vector3(-189,1,211),0,false);
            Capture(city,"TownQuietCourt",new Vector3(-150,83,40),new Vector3(-165,1,12),0,false);
            log.AppendLine("PASS clear shop forecourt, short loading-wall access and physical pickup exit through the connected city driveway");
            CheckRoadClearance(city);
            yield return CheckWesternSupplierStreet(city);
            log.AppendLine("PASS actual enlarged pickup drove the full western supplier street and showroom access remained clear");
            yield return CheckLoadingCourts(city);
            yield return CheckTransport(city,orders);
            log.AppendLine("PASS real loading, one driving camera, clear roads and reachable supplier bays");
            log.AppendLine("PASS physical country-road continuity, driven river bridge, safe vehicle recovery with cargo preserved, train station stop/resume and off-map wrap");
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
                city.pickup.Body.position=station.transform.position+Vector3.up*.12f;city.pickup.Body.rotation=station.transform.rotation;city.pickup.Stop();yield return new WaitForFixedUpdate();yield return null;
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
                city.pickup.Body.position=station.transform.position+Vector3.up*.12f;city.pickup.Body.rotation=station.transform.rotation;city.pickup.Stop();yield return new WaitForFixedUpdate();yield return null;
                Require(station.CanUse(city.pickup),"Supplier checkpoint unavailable: "+kind);
                city.Interact();yield return null;
                var load=city.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="Загрузить"||t.text=="Load"));
                Require(load!=null,"Supplier order UI did not expose loading: "+kind);load.onClick.Invoke();yield return null;
                var close=city.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="×"));close.onClick.Invoke();yield return null;
            }
            Require(orders.PickupWeightKg>0&&city.cargoRoot.childCount>0,"Loaded cargo not visible.");
            CheckCargoPlacement(city);
            Capture(city,"TownCargo",city.pickup.transform.TransformPoint(new Vector3(3,6,-5)),city.cargoRoot.position,0,false);
            log.AppendLine("PASS visible cargo remains inside the rear bed, clear of the cabin");
            Require(orders.Ledger.Entries.All(e=>e.location==DeliveryLocation.Pickup),"Some supplier orders were not collected.");
            log.AppendLine("PASS real supplier order UI, three separate collection sites and visible cargo");
            log.AppendLine("PASS rear docking orientation, unobstructed court routes, physical reverse parking at all six collection courts and provisional 20m truck staging/docking envelope (not articulated driving)");
            yield return CheckRiverRecovery(city,orders);
            Capture(city,"TownOverview",new Vector3(580,930,-770),new Vector3(175,0,135),470,true);
            Capture(city,"TownStreet",new Vector3(80,35,-135),new Vector3(12,8,30),0,false);
            Capture(city,"TownIndustrial",new Vector3(-302,40,-80),new Vector3(-252,3,-38),0,false);
            Capture(city,"TownFarm",new Vector3(505,58,257),new Vector3(565,1,340),0,false);
            Capture(city,"TownPark",new Vector3(-12,23,137),new Vector3(24,1,171),0,false);
            var train=city.town.GetComponentInChildren<TownRailTraffic>();
            if(train.IsWaiting)train.Advance(train.stationWaitSeconds);
            train.Advance(10000);
            if(train.IsWaiting){train.Advance(train.stationWaitSeconds);train.Advance(10000);}
            Require(train.TravelDistance<0,"Live train did not leave the map before restarting.");
            train.Advance(100);yield return null;
            Require(train.IsWaiting&&Vector3.Distance(train.cars[0].localPosition,new Vector3(-190,2.2f,228))<.1f,"Live train did not position at its station.");
            Capture(city,"TownRailway",new Vector3(-226,18,252),new Vector3(-185,3,222),0,false);
            Capture(city,"TownBridge",new Vector3(283,25,-78),new Vector3(331,1,-37),0,false);
            city.pickup.Body.position=new Vector3(-120,.12f,-20);city.pickup.Body.rotation=Quaternion.identity;city.pickup.Stop();
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
            var obstacles=Physics.OverlapBox(point,new Vector3(1.25f,.65f,2.3f),supplier.transform.rotation,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>c.gameObject.scene==scene&&!c.transform.IsChildOf(city.pickup.transform)).ToArray();
            Require(obstacles.Length==0,"Loading bay obstructed: "+supplier.name);
        }
    }
    private static void CheckPublicSidewalk(CityWorld city)
    {
        Physics.SyncTransforms();
        for(float x=-112;x<=-28;x+=.5f)
        {
            var obstacles=Physics.OverlapBox(new Vector3(x,1,-137),new Vector3(.3f,.7f,.3f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>c.gameObject.scene==city.gameObject.scene&&!c.transform.IsChildOf(city.pickup.transform));
            Require(!obstacles.Any(),"Existing public pedestrian route is blocked at x="+x+" by "+string.Join(",",obstacles.Select(c=>c.name)));
        }
    }
    private static void CheckStructuralProportions(StarterStoreInitialization init)
    {
        var blueprint=init.blueprint;var grid=init.building.grid;var catalog=init.catalog;
        StoreUpgradeValidation.ValidateCornerConnections(grid,catalog);
        var current=blueprint.CreateNewGame(43210);
        var occupied=new System.Collections.Generic.HashSet<Vector3Int>();
        foreach(var placed in Object.FindObjectsOfType<PlacedObject>())
            Require(placed.occupiedCells.All(occupied.Add),"Thick starter structure overlaps another object: "+placed.item.id);
        foreach(string id in new[]{"wall_01","wallcorner_01"})
        {
            var item=catalog.GetById(id);
            Require(item.footprint==new Vector2Int(id=="wall_01"?1:2,2),"Incorrect thick wall footprint.");
            Require(Mathf.Abs(item.placementBounds.size.z-grid.cellSize*2)<.001f,"Wall does not have two-cell physical thickness.");
            Require(item.previousWallPrefab!=null&&item.WallFootprint(2)==Vector2Int.one,"Historical one-cell geometry is missing.");
        }
        var door=catalog.GetById("door_01");
        Require(door.footprint==new Vector2Int(6,2)&&Mathf.Abs(door.placementBounds.size.z-grid.cellSize*2)<.001f,"Door aperture changed or frame is not two cells thick.");
        var old=blueprint.CreateNewGame(43210);
        bool IsStructure(PlacedBuildSaveData p)=>p.itemId=="wall_01"||p.itemId=="wallcorner_01"||p.itemId=="door_01"||p.itemId=="unloading_gate_01";
        old.placedObjects.RemoveAll(IsStructure);
        old.placedObjects.AddRange(blueprint.previousStarterStructure.Select(p=>JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(p))));
        var custom=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(old));custom.placedObjects.First(p=>p.itemId=="wall_01").x++;
        string before=JsonUtility.ToJson(custom);
        Require(!blueprint.TryUpgradeUnmodifiedStructure(custom,grid,catalog)&&JsonUtility.ToJson(custom)==before,"Custom player walls were overwritten.");
        Require(blueprint.TryUpgradeUnmodifiedStructure(old,grid,catalog)&&old.playerMoney==43210,"Untouched starter walls did not upgrade safely.");
        before=JsonUtility.ToJson(old);
        Require(!blueprint.TryUpgradeUnmodifiedStructure(old,grid,catalog)&&JsonUtility.ToJson(old)==before,"Structure upgrade is not idempotent.");
        foreach(int version in new[]{0,1,2})
        {
            var entry=new PlacedBuildSaveData{itemId="wall_01",x=0,z=0,wallModuleVersion=version};
            init.building.ApplyPlacedSaveData(new System.Collections.Generic.List<PlacedBuildSaveData>{entry},catalog);
            var wall=Object.FindObjectsOfType<PlacedObject>().Single();
            Require(wall.occupiedCells.Count==(version==0?8:version==1?4:1),"Saved wall occupancy changed for version "+version);
            if(version==2)Require((CityUpdateSetup.Measure(wall.gameObject).size-catalog.GetById("wall_01").previousWallBounds.size).sqrMagnitude<.00001f,"Old one-cell wall was stretched.");
        }
        init.ApplyNewGameBuildings(current);
    }
    private static void CheckFuelAccess(CityWorld city)
    {
        var route=new[]{new Vector3(204,0,-144),new Vector3(204,0,-104),new Vector3(228,0,-104),new Vector3(228,0,-144)};
        Physics.SyncTransforms();
        for(int leg=1;leg<route.Length;leg++)
        {
            var a=route[leg-1];var b=route[leg];int steps=Mathf.CeilToInt(Vector3.Distance(a,b));
            for(int step=0;step<=steps;step++)
            {
                var position=Vector3.Lerp(a,b,(float)step/steps)+Vector3.up*1.1f;
                var obstacles=Physics.OverlapBox(position,new Vector3(1,.6f,1.9f),Quaternion.LookRotation(b-a),~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.gameObject.scene==city.gameObject.scene&&!c.transform.IsChildOf(city.pickup.transform)).ToArray();
                Require(obstacles.Length==0,"Gas station entry or circulation is blocked at "+position+" by "+string.Join(",",obstacles.Select(c=>c.name)));
            }
        }
    }
    private static void CheckFrontageUpgrade(StarterStoreInitialization init)
    {
        var blueprint=init.blueprint;
        var data=blueprint.CreateNewGame(43210);
        data.placedObjects.RemoveAll(p=>p.itemId=="parking_01");
        data.placedObjects.Add(JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(blueprint.legacyPlayerParking)));
        data.floorTiles.RemoveAll(f=>f.itemId=="asphalt_01");
        var interior=JsonUtility.ToJson(data.shelfStocks);var territory=JsonUtility.ToJson(data.territory);
        var customized=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));customized.placedObjects.Last().x++;
        string before=JsonUtility.ToJson(customized);
        Require(!blueprint.TryUpgradeUnmodifiedFrontage(customized,init.building.grid,init.catalog)&&before==JsonUtility.ToJson(customized),"Custom parking was overwritten by frontage upgrade.");
        customized=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
        customized.floorTiles.Add(new FloorTileSaveData{itemId="floor_cream",x=blueprint.initialState.floorTiles.First(f=>f.itemId=="asphalt_01").x,z=blueprint.initialState.floorTiles.First(f=>f.itemId=="asphalt_01").z});
        before=JsonUtility.ToJson(customized);
        Require(!blueprint.TryUpgradeUnmodifiedFrontage(customized,init.building.grid,init.catalog)&&before==JsonUtility.ToJson(customized),"Custom forecourt surface was overwritten.");
        Require(blueprint.TryUpgradeUnmodifiedFrontage(data,init.building.grid,init.catalog),"Untouched old frontage did not upgrade.");
        Require(data.playerMoney==43210&&JsonUtility.ToJson(data.shelfStocks)==interior&&JsonUtility.ToJson(data.territory)==territory,"Frontage upgrade altered money, stock or land.");
        before=JsonUtility.ToJson(data);
        Require(!blueprint.TryUpgradeUnmodifiedFrontage(data,init.building.grid,init.catalog)&&JsonUtility.ToJson(data)==before,"Frontage upgrade is not idempotent.");
    }
    private static void CheckCargoPlacement(CityWorld city)
    {
        var pickup=city.pickup.transform;
        foreach(var renderer in city.cargoRoot.GetComponentsInChildren<Renderer>())
        {
            var bounds=renderer.bounds;
            foreach(float x in new[]{bounds.min.x,bounds.max.x})
            foreach(float y in new[]{bounds.min.y,bounds.max.y})
            foreach(float z in new[]{bounds.min.z,bounds.max.z})
            {
                var local=pickup.InverseTransformPoint(new Vector3(x,y,z));
                Require(local.z>=-2.2f&&local.z<=-.55f,"Cargo protrudes beyond rear bed or enters cabin: "+local);
                Require(Mathf.Abs(local.x)<.7f,"Cargo protrudes beyond the bed side.");
            }
        }
    }

    private static IEnumerator CheckCollisionRecovery(CityWorld city)
    {
        var pickup=city.pickup;var body=pickup.Body;
        var savedPosition=body.position;var savedRotation=body.rotation;
        var track=new GameObject("QA collision track");SceneManager.MoveGameObjectToScene(track,city.gameObject.scene);
        track.transform.position=new Vector3(800,19.5f,-300);
        track.AddComponent<BoxCollider>().size=new Vector3(60,1,70);
        var obstacle=new GameObject("QA wall or post");SceneManager.MoveGameObjectToScene(obstacle,city.gameObject.scene);
        var obstacleCollider=obstacle.AddComponent<BoxCollider>();
        var probe=pickup.gameObject.AddComponent<TownPickupImpactProbe>();probe.Target=obstacleCollider;
        try
        {
            // Freeze physics yaw must not disable the explicitly commanded steering rotation.
            pickup.Stop();body.position=new Vector3(800,20.15f,-322);body.rotation=Quaternion.identity;
            for(int tick=0;tick<15;tick++)yield return new WaitForFixedUpdate();
            pickup.SetInput(1,.5f,false);
            for(int tick=0;tick<70;tick++)yield return new WaitForFixedUpdate();
            Require(Quaternion.Angle(Quaternion.identity,body.rotation)>10,"Collision rotation constraint also disabled steering.");
            for(int impact=0;impact<3;impact++)
            {
                pickup.Stop();
                obstacle.transform.position=new Vector3(800,21,-305);
                obstacle.transform.rotation=Quaternion.Euler(0,impact==2?25:0,0);
                obstacleCollider.size=impact==1?new Vector3(.3f,3,.3f):new Vector3(4,2,1);
                body.position=new Vector3(impact==1?800.12f:798.8f,20.15f,-317);body.rotation=Quaternion.identity;
                for(int tick=0;tick<15;tick++)yield return new WaitForFixedUpdate();
                probe.Hit=false;pickup.SetInput(1,0,false);
                for(int tick=0;tick<210;tick++)
                {
                    yield return new WaitForFixedUpdate();
                }
                Require(probe.Hit,"Collision scenario never hit its obstacle: "+impact);
                pickup.SetInput(0,0,false);
                for(int tick=0;tick<45;tick++)yield return new WaitForFixedUpdate();
                Require(body.angularVelocity.magnitude<.05f&&Quaternion.Angle(probe.ContactHeading,body.rotation)<2,"Collision left residual yaw or uncontrolled rotation.");
                var stopped=body.position;pickup.SetInput(-1,0,false);
                for(int tick=0;tick<80;tick++)yield return new WaitForFixedUpdate();
                Require(body.position.z<stopped.z-2,"Pickup cannot reverse out after impact: "+impact);
            }
        }
        finally
        {
            pickup.Stop();body.position=savedPosition;body.rotation=savedRotation;
            Object.DestroyImmediate(obstacle);Object.DestroyImmediate(track);
            Object.DestroyImmediate(probe);
        }
        yield return new WaitForFixedUpdate();
    }

    private static IEnumerator CheckStarterParking(StarterStoreInitialization init)
    {
        var parking=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item!=null&&p.item.isParkingSpace).ToArray();
        Require(parking.Length==3&&parking.Count(p=>p.playerParking)==1,"Expected two visitor bays and one pickup bay.");
        var privateBay=parking.Single(p=>p.playerParking);
        var paint=CityUpdateSetup.Measure(privateBay.gameObject);
        var vehicle=CityUpdateSetup.Measure(init.cityTrip.parkedPickup);
        Require(vehicle.min.x>=paint.min.x&&vehicle.max.x<=paint.max.x
            &&vehicle.min.z>=paint.min.z&&vehicle.max.z<=paint.max.z,"Enlarged shop pickup protrudes beyond its enlarged parking markings.");
        foreach(var bay in parking)
            Require(new RetailEmpireTycoon.Parking.Rule_ParkingSurface(init.building.grid,init.floors)
                .Evaluate(new PlacementRequest(bay.item,bay.anchorCell,bay.rotated,bay.facing,bay)).ok,"Starter bay lacks asphalt or clear approach.");
        var original=Object.FindObjectOfType<RetailEmpireTycoon.Parking.ParkingTraffic>();
        bool enabled=original.enabled;original.enabled=false;
        var root=new GameObject("QA real store parking visit");
        float timeScale=Time.timeScale;
        try
        {
            var traffic=root.AddComponent<RetailEmpireTycoon.Parking.ParkingTraffic>();
            traffic.grid=original.grid;traffic.asphalt=original.asphalt;traffic.carPrefab=original.carPrefab;
            traffic.roadEntry=original.roadEntry;traffic.drivewayEntry=original.drivewayEntry;
            traffic.arrivalInterval=1000;traffic.parkingStaySeconds=.3f;
            Time.timeScale=4;
            bool arrived=false,parked=false,departed=false;
            float deadline=Time.realtimeSinceStartup+65;
            while(Time.realtimeSinceStartup<deadline)
            {
                yield return new WaitForFixedUpdate();var car=root.transform.Find("Parking visitor");
                if(car!=null)
                {
                    arrived=true;
                    if(parking.Where(p=>!p.playerParking).Any(p=>Vector3.Distance(car.position,p.GetComponent<RetailEmpireTycoon.Parking.ParkingBay>().ParkPosition+Vector3.up*.02f)<.06f))parked=true;
                }
                else if(arrived){departed=true;break;}
            }
            Require(arrived&&parked&&departed,$"Real store visit failed: arrived={arrived}, parked={parked}, departed={departed}.");
        }
        finally {Time.timeScale=timeScale;Object.Destroy(root);original.enabled=enabled;}
        yield return null;
    }
    private static IEnumerator CheckStoreAccess(CityWorld city)
    {
        var savedPosition=city.pickup.Body.position;var savedRotation=city.pickup.Body.rotation;
        var route=new[]{savedPosition,new Vector3(savedPosition.x,0,-120),new Vector3(-24,0,-120),new Vector3(-24,0,-147)};
        for(int leg=1;leg<route.Length;leg++)
        {
            var a=route[leg-1];var b=route[leg];a.y=b.y=0;
            var direction=(b-a).normalized;var rotation=Quaternion.LookRotation(direction);
            int samples=Mathf.CeilToInt(Vector3.Distance(a,b));
            for(int step=0;step<=samples;step++)
            {
                var p=Vector3.Lerp(a,b,(float)step/samples)+Vector3.up*1.1f;
                var obstacles=Physics.OverlapBox(p,new Vector3(1,.6f,1.9f),rotation,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.gameObject.scene==city.gameObject.scene&&!c.transform.IsChildOf(city.pickup.transform)).ToArray();
                Require(obstacles.Length==0,"Store driveway blocked at "+p+" by "+string.Join(",",obstacles.Select(c=>c.name)));
            }
            city.pickup.Stop();city.pickup.Body.position=a+Vector3.up*.12f;city.pickup.Body.rotation=rotation;
            yield return new WaitForFixedUpdate();city.pickup.SetInput(.4f,0,false);
            int frames=0,maxFrames=Mathf.CeilToInt((Vector3.Distance(a,b)/(city.pickup.topSpeed*.4f)+4)/Time.fixedDeltaTime);
            while(Vector3.Dot(city.pickup.Body.position-a,direction)<Vector3.Distance(a,b)-.5f&&frames++<maxFrames)yield return new WaitForFixedUpdate();
            city.pickup.Stop();
            Require(Vector3.Dot(city.pickup.Body.position-a,direction)>=Vector3.Distance(a,b)-1,"Pickup failed to traverse the store driveway.");
        }
        city.pickup.Body.position=savedPosition;city.pickup.Body.rotation=savedRotation;city.pickup.Stop();
        yield return new WaitForFixedUpdate();
    }
    private static IEnumerator CheckWesternSupplierStreet(CityWorld city)
    {
        var pickup=city.pickup;var position=pickup.Body.position;var rotation=pickup.Body.rotation;
        try
        {
            pickup.Stop();pickup.Body.position=new Vector3(-216,.15f,-186);pickup.Body.rotation=Quaternion.identity;
            yield return new WaitForFixedUpdate();pickup.SetInput(.8f,0,false);
            int frames=0;
            while(pickup.Body.position.z<180&&frames++<2100)yield return new WaitForFixedUpdate();
            Require(pickup.Body.position.z>=180&&Mathf.Abs(pickup.Body.position.x+216)<.5f,"Enlarged pickup cannot traverse the western supplier street.");
            pickup.Stop();
            var showroom=city.town.suppliers.Single(s=>s.supplier==SupplierKind.Showroom);
            var road=TownWorldPlan.NearestRoad(city.town.layout,showroom.transform.position);
            var delta=showroom.transform.position-road;
            var street=Mathf.Abs(delta.x)>Mathf.Abs(delta.z)?new Vector3(road.x,0,showroom.transform.position.z):new Vector3(showroom.transform.position.x,0,road.z);
            var route=new[]{street,showroom.transform.position};
            for(int i=1;i<route.Length;i++)
            {
                var a=route[i-1];var b=route[i];int samples=Mathf.CeilToInt(Vector3.Distance(a,b));
                for(int j=0;j<=samples;j++)
                {
                    var p=Vector3.Lerp(a,b,(float)j/samples)+Vector3.up*1.1f;
                    var obstacles=Physics.OverlapBox(p,new Vector3(1.35f,.65f,2.5f),Quaternion.LookRotation(b-a),~0,QueryTriggerInteraction.Ignore)
                        .Where(c=>c.gameObject.scene==city.gameObject.scene&&!c.transform.IsChildOf(pickup.transform));
                    Require(!obstacles.Any(),"Showroom access is blocked after relocation.");
                }
            }
        }
        finally {pickup.Stop();pickup.Body.position=position;pickup.Body.rotation=rotation;}
        yield return new WaitForFixedUpdate();
    }

    private static IEnumerator CheckLoadingCourts(CityWorld city)
    {
        var originalPosition=city.pickup.Body.position;var originalRotation=city.pickup.Body.rotation;
        foreach(var court in city.town.GetComponentsInChildren<SupplierLoadingCourt>())
        {
            var station=court.checkpoint;
            var roadSurface=city.town.transform.Find("Road infrastructure/Country road physical surface").GetComponent<MeshCollider>();
            // Industrial yards reserve the original 20 m envelope; a compact urban bakery explicitly
            // supports pickups, not an articulated truck. Its access may bend around the storefront.
            float vehicleLength=court.designVehicleLength;
            Require(vehicleLength>=5,"Invalid court design vehicle length: "+station.supplier);
            foreach(var centre in new[]{court.staging,station.transform.position+station.transform.forward*7.5f})
            {
                var truckObstacles=Physics.OverlapBox(new Vector3(centre.x,2.2f,centre.z),new Vector3(1.7f,2.1f,vehicleLength/2),station.transform.rotation,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.gameObject.scene==city.gameObject.scene&&!c.transform.IsChildOf(city.pickup.transform)&&c!=roadSurface).ToArray();
                Require(truckObstacles.Length==0,"Future truck staging/docking envelope blocked: "+station.supplier);
            }
            city.pickup.Body.position=station.transform.position+Vector3.up*.12f;
            city.pickup.Body.rotation=station.transform.rotation*Quaternion.Euler(0,180,0);city.pickup.Stop();
            yield return new WaitForFixedUpdate();
            Require(!station.CanUse(city.pickup)&&station.UnavailableReason(city.pickup).Contains("задом"),"Wrong-way parking was accepted: "+station.supplier);
            city.pickup.Body.rotation=station.transform.rotation;city.pickup.Stop();yield return new WaitForFixedUpdate();
            Require(station.CanUse(city.pickup),"Correct docking rejected: "+station.supplier);
            // Inspect the complete access corridor, then physically reverse the real pickup to its bay.
            var route=new[]{court.streetAccess,court.entrance}.Concat(court.approachWaypoints??Array.Empty<Vector3>())
                .Concat(new[]{court.staging,station.transform.position}).ToArray();
            for(int i=1;i<route.Length;i++)
            {
                var a=route[i-1];var b=route[i];int samples=Mathf.CeilToInt(Vector3.Distance(a,b));
                for(int j=0;j<=samples;j++)
                {
                    var p=Vector3.Lerp(a,b,(float)j/samples)+Vector3.up*1.1f;
                    var obstacles=Physics.OverlapBox(p,new Vector3(1.35f,.65f,2.5f),Quaternion.LookRotation(b-a),~0,QueryTriggerInteraction.Ignore)
                        .Where(c=>c.gameObject.scene==city.gameObject.scene&&!c.transform.IsChildOf(city.pickup.transform)).ToArray();
                    Require(obstacles.Length==0,"Court route blocked: "+station.supplier+" at "+p+" by "+string.Join(",",obstacles.Select(c=>c.name)));
                }
            }
            city.pickup.Body.position=court.staging+Vector3.up*.12f;city.pickup.Body.rotation=station.transform.rotation;city.pickup.Stop();
            yield return new WaitForFixedUpdate();
            city.pickup.SetInput(-.4f,0,false);
            int maxFrames=Mathf.CeilToInt((Vector3.Distance(court.staging,station.transform.position)/(city.pickup.reverseSpeed*.4f)+5)/Time.fixedDeltaTime);
            int frames=0;
            while(station.DistanceTo(city.pickup)>1&&frames++<maxFrames)yield return new WaitForFixedUpdate();
            city.pickup.Stop();yield return new WaitForFixedUpdate();
            Require(station.CanUse(city.pickup),"Pickup failed physical reverse docking: "+station.supplier+" at "+city.pickup.Body.position);
        }
        city.pickup.Body.position=originalPosition;city.pickup.Body.rotation=originalRotation;city.pickup.Stop();
        yield return new WaitForFixedUpdate();
    }
    private static IEnumerator CheckTransport(CityWorld city,DeliveryOrders orders)
    {
        var layout=city.town.layout;var scene=city.gameObject.scene;
        var roadSurface=city.town.transform.Find("Road infrastructure/Country road physical surface").GetComponent<MeshCollider>();
        var points=layout.countryRoad.points;
        for(int i=1;i<points.Length;i++)
        {
            var a=points[i-1];var b=points[i];int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/2);
            for(int step=0;step<=steps;step++)
            {
                var p=Vector3.Lerp(a,b,(float)step/steps);
                var hits=Physics.RaycastAll(p+Vector3.up*5,Vector3.down,8,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider.gameObject.scene==scene&&!h.transform.IsChildOf(city.pickup.transform)).ToArray();
                Require(hits.Any(h=>h.normal.y>.8f&&Mathf.Abs(h.point.y-p.y)<.3f),"Country road has no physical surface at "+p);
                var obstacles=Physics.OverlapBox(p+Vector3.up*1.15f,new Vector3(1.2f,.6f,2.2f),Quaternion.LookRotation(b-a),~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.gameObject.scene==scene&&!c.transform.IsChildOf(city.pickup.transform)&&c!=roadSurface).ToArray();
                Require(obstacles.Length==0,"Country road obstructed at "+p+": "+string.Join(",",obstacles.Select(c=>c.name)));
            }
        }
        var test=new GameObject("QA railway journey");var rail=test.AddComponent<TownRailTraffic>();rail.enabled=false;rail.layout=layout;
        rail.Advance(100);Require(rail.IsWaiting&&Mathf.Abs(rail.TravelDistance-layout.stationDistance)<.01f,"Train did not stop at station.");
        rail.Advance(rail.stationWaitSeconds);rail.Advance(1);Require(!rail.IsWaiting&&rail.TravelDistance>layout.stationDistance,"Train did not resume.");
        rail.Advance(10000);Require(rail.TravelDistance<0,"Train did not restart outside map.");Object.Destroy(test);
        var savedPosition=city.pickup.Body.position;var savedRotation=city.pickup.Body.rotation;
        city.pickup.Stop();city.pickup.Body.position=new Vector3(313,2.4f,-40);city.pickup.Body.rotation=Quaternion.Euler(0,90,0);
        for(int frame=0;frame<20;frame++)yield return new WaitForFixedUpdate();
        city.pickup.SetInput(1,0,false);
        for(int frame=0;frame<205;frame++)yield return new WaitForFixedUpdate();
        city.pickup.Stop();Require(city.pickup.Body.position.x>345&&city.pickup.Body.position.y>-.5f,"Pickup failed to drive across physical bridge.");
        city.pickup.Body.position=savedPosition;city.pickup.Body.rotation=savedRotation;city.pickup.Stop();
        yield return new WaitForFixedUpdate();
    }
    private static IEnumerator CheckRiverRecovery(CityWorld city,DeliveryOrders orders)
    {
        var layout=city.town.layout;var savedPosition=city.pickup.Body.position;var savedRotation=city.pickup.Body.rotation;
        city.pickup.Stop();city.pickup.Body.position=new Vector3(264,.2f,-180);city.pickup.Body.rotation=Quaternion.identity;
        for(int frame=0;frame<12;frame++)yield return new WaitForFixedUpdate();
        float weight=orders.PickupWeightKg;
        var cargo=JsonUtility.ToJson(orders.Ledger.Capture());int visibleBoxes=city.cargoRoot.childCount;
        Require(weight>0&&visibleBoxes>0,"Recovery test must exercise a loaded pickup.");
        city.pickup.Body.position=new Vector3(layout.RiverCenter(0),-3.4f,0);city.pickup.Stop();
        for(int frame=0;frame<3;frame++)yield return new WaitForFixedUpdate();
        Require(city.pickup.Body.position.y>-1&&city.GetComponent<CityRiverRecovery>().ShowRecoveryNotice,"River vehicle recovery failed.");
        Require(Mathf.Abs(orders.PickupWeightKg-weight)<.001f,"River recovery changed cargo.");
        Require(JsonUtility.ToJson(orders.Ledger.Capture())==cargo&&city.cargoRoot.childCount==visibleBoxes,"River recovery changed cargo entries or visible boxes.");
        city.pickup.Body.position=savedPosition;city.pickup.Body.rotation=savedRotation;city.pickup.Stop();
        yield return new WaitForFixedUpdate();
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
