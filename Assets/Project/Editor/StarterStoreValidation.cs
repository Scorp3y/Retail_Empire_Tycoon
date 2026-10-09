using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StarterStoreValidation
{
    private const string Report = "Library/StoreUpgradeQA/starter.txt";
    private static int checks;
    private static void Require(bool condition, string message)
    {
        checks++;
        if (condition) return;
        File.WriteAllText(Report, "FAILED: " + message);
        throw new InvalidOperationException(message);
    }
    public static void Run() => Object.FindObjectOfType<CityTrip>().StartCoroutine(Check());
    private static IEnumerator Check()
    {
        Require(Object.FindObjectOfType<SaveManager>(true)==null, "Starter QA must not access player saves.");
        var initializer=Object.FindObjectOfType<StarterStoreInitialization>();
        var build=initializer.building;
        var blueprint=initializer.blueprint;
        var data=initializer.CreateNewGame(50000);
        Require(data.usesModularStore&&data.playerMoney==50000,"New-game data loses layout style or starting balance.");
        Require(data.placedObjects.Count(p=>p.itemId=="storage_rack_01")==3,"Warehouse must contain three racks.");
        Require(data.placedObjects.Count(p=>p.itemId=="unloading_gate_01")==1,"Required warehouse unloading gate is missing.");
        Require(data.placedObjects.Count(p=>p.playerParking)==1,"Personal vehicle bay is missing.");
        Require(data.floorTiles.Count==7056,"The complete store and service yard are not floored.");
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();
        spawner.UsesModularStore=true;
        initializer.land.progression.ApplySaveData(data.territory);
        initializer.ConfigureSite(true);
        spawner.Spawn(StoreLevelId.Lvl1);
        initializer.ApplyNewGameBuildings(data);
        yield return null;yield return null;
        var productSave=Object.FindObjectOfType<ProductSaveService>();
        productSave.ApplyWarehouseSaveData(data.productInventory);
        productSave.ApplyShelfSaveData(data.shelfStocks);
        Physics.SyncTransforms();
        Require(Object.FindObjectsOfType<PlacedObject>().Length==data.placedObjects.Count,"Starter building has missing or duplicate pieces.");
        Require(initializer.floors.BuildSaveData().Count==data.floorTiles.Count,"Starter floors failed to load.");
        var floorRenderers=initializer.floors.floorRoot.GetComponentsInChildren<MeshRenderer>();
        Require(floorRenderers.Length<100,"Starter floor still creates hundreds of individual draw objects.");
        Require(floorRenderers.All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off&&r.receiveShadows),"Floor casts redundant shadows or no longer receives furniture shadows.");
        Require(initializer.floors.floorRoot.GetComponentsInChildren<Collider>().Length==0,"Floor still creates a collider per tile.");
        Require(initializer.floors.floorRoot.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)==data.floorTiles.Count*2,"Floor contains missing cells or redundant cube faces.");
        var originalFloor=data.floorTiles.First(f=>f.itemId=="floor_wood");
        var paintCell=new Vector3Int(originalFloor.x,0,originalFloor.z);
        var paintSnapshot=initializer.floors.BuildSaveData().ToDictionary(f=>new Vector3Int(f.x,0,f.z),f=>f.itemId);
        var asphalt=initializer.catalog.GetById("asphalt_01");
        initializer.floors.PaintCells(new System.Collections.Generic.List<Vector3Int>{paintCell},asphalt);
        Require(initializer.floors.IsAsphalt(paintCell)&&initializer.floors.AsphaltSurfaces.Any(),"Repainting fails to update asphalt parking/city data.");
        var painted=initializer.floors.BuildSaveData();
        Require(painted.Count==data.floorTiles.Count&&painted.All(f=>f.x==paintCell.x&&f.z==paintCell.z?f.itemId==asphalt.id:f.itemId==paintSnapshot[new Vector3Int(f.x,0,f.z)]),"Single-cell painting changes other saved floor cells.");
        initializer.floors.PaintCells(new System.Collections.Generic.List<Vector3Int>{paintCell},initializer.catalog.GetById(originalFloor.itemId));
        yield return null;
        Require(initializer.floors.floorRoot.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)==data.floorTiles.Count*2,"Repeated repainting leaves duplicate floor geometry.");
        File.WriteAllText("Library/StoreUpgradeQA/floor-performance.txt",$"Starter floor: {data.floorTiles.Count} saved cells, {floorRenderers.Length} renderers (formerly {data.floorTiles.Count}), {data.floorTiles.Count*2} triangles (formerly {data.floorTiles.Count*12}), zero floor shadow casters/colliders.\n");
        var occupied=new System.Collections.Generic.HashSet<Vector3Int>();
        foreach(var placed in Object.FindObjectsOfType<PlacedObject>())
        {
            Require(placed.occupiedCells.All(occupied.Add),"Starter objects overlap grid cells: "+placed.item.id);
            if(placed.item.isWall)
            {
                Require(placed.occupiedCells.Count==1&&placed.wallModuleVersion==2,"Starter wall is not one fine grid cell.");
                Require(new Rule_WallJoint().Evaluate(new PlacementRequest(placed.item,placed.anchorCell,placed.rotated,placed.facing,placed)).ok,"Starter corner has a mismatched connection.");
            }
            if(placed.item.category==BuildCategory.Shelf)
                Require(new Rule_ShelfApproach(build.grid,build.territory).Evaluate(new PlacementRequest(placed.item,placed.anchorCell,placed.rotated,placed.facing,placed)).ok,"Starter shelf blocks customer access: "+placed.item.id);
            if(placed.item.isParkingSpace)
                Require(new RetailEmpireTycoon.Parking.Rule_ParkingSurface(build.grid,initializer.floors).Evaluate(new PlacementRequest(placed.item,placed.anchorCell,placed.rotated,placed.facing,placed)).ok,"Starter parking lacks a clear asphalt approach.");
        }
        var shelves=Object.FindObjectsOfType<PlacedShelfStock>().Where(s=>s.GetComponent<PlacedObject>()!=null).ToArray();
        Require(shelves.Length==7&&shelves.All(s=>s.CurrentAmount>0),"Starter sales shelves must be filled with valid stock.");
        var operations=Object.FindObjectOfType<StoreOperations>();
        Object.FindObjectOfType<DeliveryOrders>().money.SetMoney(data.playerMoney);
        operations.ApplySaveData(data.shopOperations);
        operations.Advance(3);
        Require(operations.CheckoutObject!=null,"Starter shop has no accessible checkout: "+operations.Notice+" entry="+operations.EntrancePosition);
        var warehouseDoor=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item.isDoorway).OrderBy(p=>p.anchorCell.x).First();
        Require(operations.Navigation.TryPath(operations.EntrancePosition,warehouseDoor.transform.position+Vector3.left*.6f,out _),"Warehouse staff entrance cannot be reached from the store.");
        var editing=build.GetComponent<BuildEditingController>();
        var bread=shelves.Single(s=>s.GetComponent<PlacedObject>().item.id=="shelf_fresh_01");
        var selected=bread.GetComponent<PlacedObject>();var oldCell=selected.anchorCell;var oldPosition=selected.transform.position;
        int units=bread.CurrentAmount;int inventory=build.inventory.GetCount(selected.item);
        editing.Begin();Require(editing.Select(selected),"Built starter shelf is not selectable.");
        Require(editing.PreviewAt(oldCell).ok,"Selecting an object collides with its own old position.");
        Require(!editing.PreviewAt(new Vector3Int(-900,0,-900)).ok&&!editing.Confirm(),"Out-of-bounds move was accepted.");
        Require(selected.anchorCell==oldCell&&selected.transform.position==oldPosition&&bread.CurrentAmount==units,"Rejected move mutated object/stock.");
        editing.CancelSelection();Require(selected.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff),"Cancelling leaves the original object hidden.");
        editing.Select(selected);editing.Rotate(1);
        var movedCell=oldCell+new Vector3Int(8,0,8);
        Require(editing.PreviewAt(movedCell).ok&&editing.Confirm(),"Valid shelf move/rotation could not be committed.");
        Require(selected.anchorCell==movedCell&&selected.facing==3&&bread.CurrentAmount==units&&build.inventory.GetCount(selected.item)==inventory,"Move/rotate consumes inventory or loses shelf stock.");
        Require(build.grid.IsOccupied(movedCell)&&!build.grid.IsOccupied(oldCell),"Moving fails to update grid occupancy.");
        var savedStock=productSave.BuildShelfSaveData().Single(s=>s.buildItemId==selected.item.id);
        Require(savedStock.anchorX==movedCell.x&&savedStock.facing==3&&savedStock.amount==units,"Moved shelf cannot save stock using its new coordinates.");
        var gate=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item.isUnloadingGate);
        editing.Select(gate);Require(!editing.DeleteSelected()&&gate.gameObject.activeSelf,"Last unloading gate can be deleted.");
        editing.Select(selected);
        var product=bread.AssignedProduct;int warehouseBefore=editing.deliveries.products.GetCount(product);
        Require(editing.DeleteSelected(),"Filled shelf cannot be returned to inventory.");
        Require(build.inventory.GetCount(selected.item)==inventory+1&&editing.deliveries.products.GetCount(product)==warehouseBefore+units,"Deleting loses furniture or remaining goods.");
        editing.Finish();yield return null;
        var snapshot=new GameData { usesModularStore=true, placedObjects=build.BuildPlacedSaveData(),floorTiles=initializer.floors.BuildSaveData(),shelfStocks=productSave.BuildShelfSaveData(),productInventory=productSave.BuildWarehouseSaveData() };
        snapshot=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(snapshot));
        spawner.Spawn(StoreLevelId.Lvl2);yield return null;
        Require(Object.FindObjectsOfType<PlacedObject>().Length==snapshot.placedObjects.Count,"Land progression replaced or duplicated the custom building.");
        initializer.ApplyNewGameBuildings(snapshot);yield return null;yield return null;
        productSave.ApplyWarehouseSaveData(snapshot.productInventory);productSave.ApplyShelfSaveData(snapshot.shelfStocks);
        Require(Object.FindObjectsOfType<PlacedObject>().Length==snapshot.placedObjects.Count&&productSave.BuildWarehouseSaveData().Any(p=>p.productId==product.Id&&p.count==warehouseBefore+units),"Edited starter shop fails JSON save/load roundtrip.");
        Require(Object.FindObjectsOfType<PlacedObject>().Count(p=>p.playerParking)==1,"Private parking reservation is not saved.");
        // Restore the authored new-game arrangement for the visual handoff.
        initializer.ApplyNewGameBuildings(data);yield return null;yield return null;
        productSave.ApplyWarehouseSaveData(data.productInventory);productSave.ApplyShelfSaveData(data.shelfStocks);
        var controls=Object.FindObjectOfType<GameplayControls>();controls.CloseWindows();
        // Previous regression deliberately produced an unloading error; do not leave its toast in handoff images.
        var noticePanel=initializer.cityTrip.uiRoot.Find("Delivery notice");
        if(noticePanel!=null)noticePanel.gameObject.SetActive(false);
        var input=Object.FindObjectOfType<MainCamera>();input.enabled=false;
        Camera.main.transform.position=blueprint.cameraFocus+new Vector3(-8,12,-14);
        Camera.main.transform.LookAt(blueprint.cameraFocus);
        yield return new WaitForEndOfFrame();Capture("StarterStore");
        editing.ReadPointerInput=false;
        editing.Begin();editing.Select(Object.FindObjectsOfType<PlacedObject>().First(p=>p.item.category==BuildCategory.Shelf));
        yield return new WaitForEndOfFrame();Capture("StoreEditor");editing.Finish();
        editing.ReadPointerInput=true;
        input.enabled=true;
        File.WriteAllText(Report,$"STARTER STORE PASSED: {checks} assertions. Player save components absent.\n");
    }
    private static void Capture(string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/StoreUpgradeQA/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
    }
}
