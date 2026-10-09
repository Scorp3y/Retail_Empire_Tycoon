using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Integration validation only in a disposable shop copy, with all player-save writers absent.</summary>
public static class CityUpdateValidation
{
    private const string Folder="Library/CityUpdateQA";
    [MenuItem("Retail Empire/City/Validate assets and bakery layout")]
    public static void ValidateAssets()
    {
        Directory.CreateDirectory(Folder);
        var products=AssetDatabase.FindAssets("t:ProductItemData",new[]{"Assets/Prefabs"}).Select(g=>AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Require(products.Count(p=>p.StorageType==ProductStorageType.Bakery)==6,"Expected exactly six selected bakery products, including the saved legacy bread ID.");
        Require(products.Single(p=>p.Id=="product_bread").ShelfDisplayPrefab!=null,"Legacy bread identity was lost.");
        foreach(var product in products.Where(p=>p.StorageType==ProductStorageType.Bakery))
            Require(product.ShelfDisplayPrefab.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")!=null)),"Bakery texture conversion failed: "+product.Id);
        foreach(string path in new[]{"Assets/Prefabs/Shelf/Fresh Market Shelf.prefab","Assets/Prefabs/Shelf/Assortment/ProduceRack.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var slots=root.GetComponentsInChildren<ShelfProductSlot>();Require(slots.Length>=24,"New tray slots are missing.");
                foreach(var product in products.Where(p=>root.GetComponent<PlacedShelfStock>().CanAccept(p)))
                {
                    var item=root.GetComponent<ShelfProductDisplay>().CreateWorkProduct(product,slots[0].transform);
                    Require(item!=null,"Product preview could not be seated.");
                    var bounds=new Bounds();bool first=true;
                    foreach(var filter in item.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh=filter.sharedMesh.bounds;
                        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                        {
                            var point=slots[0].transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.center+Vector3.Scale(mesh.extents,new Vector3(x,y,z))));
                            if(first) {bounds=new Bounds(point,Vector3.zero);first=false;} else bounds.Encapsulate(point);
                        }
                    }
                    Require(bounds.min.y>=-.0001f && bounds.size.x<=slots[0].usableSize.x+.001f
                        && bounds.size.z<=slots[0].usableSize.z+.001f,"Product clips through a tray or crate divider: "+product.Id);
                    Object.DestroyImmediate(item);
                }
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(CityUpdateSetup.CityPath)!=null,"City scene was not built.");
        Require(EditorBuildSettings.scenes.Any(s=>s.path==CityUpdateSetup.CityPath&&s.enabled),"City is missing in Build Settings.");
        CheckParkingSurface();
        File.WriteAllText(Folder+"/assets.txt","PASS six selected bakery models, stable legacy ID, tray bounds, parking surface/approach rules in four orientations and city Build Settings.\n");
    }
    [MenuItem("Retail Empire/City/Validate full delivery trip in sandbox %#&F5")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!=ShopGameplaySandbox.Path)
            throw new InvalidOperationException("Requires isolated sandbox Play Mode.");
        if(Object.FindObjectOfType<SaveManager>(true)!=null) throw new InvalidOperationException("Tests must not write player saves.");
        var trip=Object.FindObjectOfType<CityTrip>();Require(trip!=null,"City migration has not been applied.");trip.orders.save=null;
        trip.StartCoroutine(Check(trip));
    }
    private static IEnumerator Check(CityTrip trip)
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/integration.txt","Integration validation is running.\n");
        var log=new StringBuilder();bool passed=false;var orders=trip.orders;
        try
        {
            yield return CheckParkingTraffic(trip);
            log.AppendLine("PASS physical parking arrival, exclusive bay, departure and reservation release");
            var product=orders.productCatalog.Products.First(p=>p.Id=="product_bread");orders.money.SetMoney(50000);
            var shelfPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shelf/Fresh Market Shelf.prefab");
            var shelf=Object.Instantiate(shelfPrefab,new Vector3(1000,0,1000),Quaternion.identity).GetComponent<PlacedShelfStock>();
            shelf.SetStockFromSave(product,5);
            var productSave=Object.FindObjectOfType<RetailEmpireTycoon.SaveSystem.ProductSaveService>();
            int shelfUnits=productSave.BuildShelfSaveData().Sum(s=>s.amount);
            var shop=Object.FindObjectOfType<ShopWindow>(true);shop.gameObject.SetActive(true);shop.OpenProducts();yield return null;
            var card=shop.GetComponentsInChildren<RetailEmpireTycoon.UI.Products.ProductShopItemCard>()
                .First(c=>c.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text==ShopText.Item(product)));
            int initialCount=orders.products.GetCount(product);int initialMoney=orders.money.Money;
            card.GetComponentInChildren<Button>().onClick.Invoke();
            Require(orders.products.GetCount(product)==initialCount,"Buying still grants stock to the shop before delivery.");
            Require(orders.money.Money==initialMoney-product.BuyPrice,"Paid order price is incorrect.");
            Require(orders.Ledger.Count(product.Id,DeliveryItemKind.Product,DeliveryLocation.Depot)==product.BoxAmount,"Order did not reach the depot.");
            log.AppendLine("PASS real shop purchase -> paid depot order, no immediate shop stock");
            var building=orders.buildCatalog.GetById("cash_register_01");
            if(building==null) building=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/CashRegister.asset");
            int initialBuildings=orders.buildings.GetCount(building);shop.OpenCategory_Structures();yield return null;
            var buildCard=shop.GetComponentsInChildren<ShopItemCard>().First(c=>c.nameText.text==ShopText.Item(building));
            buildCard.buyButton.onClick.Invoke();Require(orders.buildings.GetCount(building)==initialBuildings,"Furniture arrived before collection.");
            Require(orders.Ledger.Count(building.id,DeliveryItemKind.Building,DeliveryLocation.Depot)==1,"Furniture purchase did not reach depot.");
            log.AppendLine("PASS real furniture purchase -> depot, no immediate building inventory");
            shop.Close();trip.Begin();float deadline=Time.realtimeSinceStartup+60;
            while(trip.Transitioning&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(trip.InCity&&!trip.Transitioning,"The loading scene did not finish the city transition.");
            var city=Object.FindObjectOfType<CityWorld>();Require(city!=null,"City world is absent.");
            yield return null;
            Require(UnityEngine.EventSystems.EventSystem.current!=null && UnityEngine.EventSystems.EventSystem.current.gameObject.scene==city.gameObject.scene,"City input disappeared with the loading scene.");
            Require(productSave.BuildShelfSaveData().Sum(s=>s.amount)==shelfUnits,"Suspending the shop removes shelf stock from saves.");
            log.AppendLine("PASS city UI input and inactive-shop shelf save preservation");
            Require(Object.FindObjectsOfType<Camera>().Count(c=>c.isActiveAndEnabled&&c.CompareTag("MainCamera"))==1,"City has duplicate active gameplay cameras.");
            log.AppendLine("PASS asynchronous loading, city activation and single third-person camera");
            CaptureCity(city);
            city.pickup.ReadKeyboard=false;city.pickup.SetInput(1,0,false);Vector3 before=city.pickup.transform.position;
            for(int step=0;step<75;step++) yield return new WaitForFixedUpdate();
            var after=city.pickup.transform.position;city.pickup.Stop();
            Require(Vector3.Distance(before,after)>1,$"The pickup cannot drive forward: {before} -> {after}; time scale {Time.timeScale}.");
            log.AppendLine("PASS real pickup physics and stopping");
            city.pickup.Body.position=city.depot.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();
            Require(city.depot.CanUse(city.pickup),"Depot cannot be used with a stopped pickup.");
            var entry=orders.Ledger.Entries.First(e=>e.itemId==product.Id);Require(orders.Load(entry),"Depot collection failed.");yield return null;
            Require(orders.Load(orders.Ledger.Entries.First(e=>e.itemId==building.id)),"Furniture collection failed.");yield return null;
            Require(city.cargoRoot.childCount>0,"Loaded crates are not visible in the pickup.");
            Require(orders.products.GetCount(product)==initialCount,"Collection grants stock before unloading.");
            var saved=JsonUtility.ToJson(orders.Ledger.Capture());orders.Ledger.Restore(JsonUtility.FromJson<DeliverySaveData>(saved));
            Require(orders.Ledger.Count(product.Id,DeliveryItemKind.Product,DeliveryLocation.Pickup)==product.BoxAmount,"Cargo was lost after save roundtrip.");
            log.AppendLine("PASS collection, visible crates and in-transit save roundtrip");
            var camera=city.chaseCamera.transform;
            camera.position=city.pickup.transform.position-city.pickup.transform.forward*8+Vector3.up*4.2f;
            camera.LookAt(city.pickup.transform.position+Vector3.up*1.8f);CaptureCity(city);
            city.pickup.Body.position=city.unload.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();
            Require(city.unload.CanUse(city.pickup),"Unloading checkpoint is unreachable.");Require(orders.Unload(),"Unloading failed.");
            Require(orders.products.GetCount(product)==initialCount+product.BoxAmount,"Delivered units did not enter the shop warehouse.");
            Require(orders.buildings.GetCount(building)==initialBuildings+1,"Furniture did not enter the building inventory after unloading.");
            Require(!orders.Unload()&&orders.products.GetCount(product)==initialCount+product.BoxAmount,"Repeated unloading duplicated goods.");
            log.AppendLine("PASS exact unloading and duplicate-delivery protection");
            city.pickup.Body.position=city.home.transform.position+Vector3.up*.12f;city.pickup.Stop();yield return new WaitForFixedUpdate();
            Require(city.home.CanUse(city.pickup),"Home checkpoint cannot be used.");trip.Return();deadline=Time.realtimeSinceStartup+60;
            while(trip.Transitioning&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!trip.InCity&&!trip.Transitioning&&trip.controls.isActiveAndEnabled,"Return failed to resume the same store.");
            Require(orders.products.GetCount(product)==initialCount+product.BoxAmount,"Returning lost delivered stock.");
            log.AppendLine("PASS return loading and original shop resume without reloading inventory");
            log.AppendLine("CITY DELIVERY INTEGRATION PASSED");passed=true;
        }
        finally {if(!passed)log.AppendLine("FAILED: inspect the Unity exception; no player save was written.");File.WriteAllText(Folder+"/integration.txt",log.ToString());}
    }
    private static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    private static IEnumerator CheckParkingTraffic(CityTrip trip)
    {
        var original=trip.GetComponent<ParkingTraffic>();bool enabled=original.enabled;original.enabled=false;
        var root=new GameObject("Isolated parking traffic validation");
        try
        {
            var grid=root.AddComponent<GridSystem>();grid.cellSize=.1667f;grid.origin=new Vector3(2000,0,2000);
            var floor=root.AddComponent<FloorPainter>();floor.grid=grid;floor.floorRoot=root.transform;
            var asphalt=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/asphalt_01.asset");floor.floorTilePrefab=asphalt.prefab;
            var cells=Enumerable.Range(0,16).SelectMany(x=>Enumerable.Range(0,20).Select(z=>new Vector3Int(x,0,z))).ToList();floor.PaintCells(cells,asphalt);
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/parking_01.asset");var anchor=new Vector3Int(5,0,0);
            var bay=Object.Instantiate(item.prefab,grid.origin+new Vector3(7*grid.cellSize,0,4*grid.cellSize),Quaternion.identity,root.transform).GetComponent<ParkingBay>();
            var placed=bay.GetComponent<PlacedObject>();placed.item=item;placed.anchorCell=anchor;
            placed.occupiedCells=grid.GetFootprintCells(anchor,item.footprint,false,item.pivotOffset).ToList();grid.Occupy(placed.occupiedCells);
            var traffic=root.AddComponent<ParkingTraffic>();traffic.grid=grid;traffic.asphalt=floor;
            traffic.carPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/ParkingCar.prefab");traffic.arrivalInterval=1000;traffic.parkingStaySeconds=.5f;
            traffic.roadEntry=new GameObject("Entry").transform;traffic.roadEntry.SetParent(root.transform);traffic.roadEntry.position=grid.CellToWorld(new Vector3Int(7,0,17));
            bool arrived=false,parked=false,departed=false;
            for(int step=0;step<1600;step++)
            {
                yield return new WaitForFixedUpdate();var car=root.transform.Find("Parking visitor");
                if(car!=null)
                {
                    arrived=true;if(Vector3.Distance(car.position,bay.ParkPosition+Vector3.up*.02f)<.06f) parked=true;
                    Require(root.GetComponentsInChildren<Transform>().Count(t=>t.name=="Parking visitor")==1,"A parking bay spawned overlapping visitors.");
                }
                else if(arrived) {departed=true;break;}
            }
            Require(arrived&&parked&&departed,$"Parking visit incomplete: arrived={arrived}, parked={parked}, departed={departed}.");
        }
        finally {Object.Destroy(root);original.enabled=enabled;}
        yield return null;
    }
    private static void CheckParkingSurface()
    {
        var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/parking_01.asset");
        var asphalt=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/asphalt_01.asset");
        var normal=AssetDatabase.FindAssets("t:BuildItemData",new[]{"Assets/Prefabs"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(g))).First(i=>i.id=="floor_01");
        var root=new GameObject("Temporary parking validation");
        try
        {
            var grid=root.AddComponent<GridSystem>();grid.cellSize=.1667f;
            var floor=root.AddComponent<FloorPainter>();floor.grid=grid;floor.floorRoot=root.transform;floor.floorTilePrefab=asphalt.prefab;
            var rule=new Rule_ParkingSurface(grid,floor);
            for(int facing=0;facing<4;facing++)
            {
                var anchor=new Vector3Int(2000+facing*100,0,2000);bool rotated=facing%2==1;
                var request=new PlacementRequest(item,anchor,rotated,facing);
                var footprint=grid.GetFootprintCells(anchor,item.footprint,rotated,item.pivotOffset).ToList();
                var approach=Rule_ParkingSurface.ApproachCells(grid,item,anchor,rotated,facing).ToList();
                Require(!rule.Evaluate(request).ok,"Parking was accepted without asphalt.");
                floor.PaintCells(footprint,asphalt);
                Require(!rule.Evaluate(request).ok,"Parking was accepted without a driveway.");
                floor.PaintCells(approach,asphalt);
                Require(rule.Evaluate(request).ok,"A valid asphalt bay was rejected in orientation "+facing);
                grid.Occupy(approach.Take(1));Require(!rule.Evaluate(request).ok,"Parking accepted a blocked driveway.");grid.Release(approach);
                var parked=new GameObject("Reserved bay").AddComponent<PlacedObject>();parked.transform.SetParent(root.transform);
                parked.item=item;parked.anchorCell=anchor;parked.rotated=rotated;parked.facing=facing;parked.occupiedCells=footprint;
                Require(!floor.AreCellsValid(footprint,normal)&&!floor.AreCellsValid(approach,normal),"Ordinary floor can overwrite a parking surface or driveway.");
                var blocking=new PlacementRequest(item,approach[0],rotated,facing);
                Require(!rule.Evaluate(blocking).ok,"A new object can cover another bay's driveway.");
            }
        }
        finally {Object.DestroyImmediate(root);}
    }
    private static void CaptureCity(CityWorld city)
    {
        var camera=city.chaseCamera.GetComponent<Camera>();var target=new RenderTexture(1280,720,24);
        var canvas=city.GetComponentInChildren<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.8f;
        var previous=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try {frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();File.WriteAllBytes(Folder+"/city.png",frame.EncodeToPNG());}
        finally {camera.targetTexture=null;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;RenderTexture.active=previous;Object.Destroy(frame);Object.Destroy(target);}
    }
}
