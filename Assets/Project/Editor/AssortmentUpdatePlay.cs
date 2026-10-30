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
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Products;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real catalogue UI, paid deliveries, storage, stock display, grid placement and shopper checkout in a disposable scene.</summary>
[InitializeOnLoad]
public static class AssortmentUpdatePlay
{
    private const string Pending = "AssortmentUpdate.Play";
    private static IEnumerator checks;
    private static double deadline;
    private static readonly StringBuilder trace = new StringBuilder();
    static AssortmentUpdatePlay() { EditorApplication.update += Tick; }
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        File.WriteAllText(AssortmentUpdateBatch.Report + "/play.txt", "RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath); ShopGameplaySandbox.Open();
        foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (checks == null) { checks = Check(); deadline = EditorApplication.timeSinceStartup + 600; }
            Require(EditorApplication.timeSinceStartup < deadline, "Assortment checks timed out.");
            if (checks.MoveNext()) return;
            AssortmentUpdateBatch.Record(); Finish(null);
        }
        catch (Exception error) { Debug.LogException(error); Finish(error); }
    }
    public static IEnumerator Check()
    {
        for (int i = 0; i < 5; i++) yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true) == null, "Player save writer present.");
        var starter = Object.FindObjectOfType<StarterStoreInitialization>(); var orders = starter.cityTrip.orders; orders.save = null;
        var data = starter.CreateNewGame(); Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        starter.building.inventory.ApplySaveData(data.buildInventory, starter.catalog);
        var spawner = Object.FindObjectOfType<StorePrefabSpawner>(); spawner.UsesModularStore = true;
        starter.ConfigureSite(true); spawner.Spawn(StoreLevelId.Lvl1); starter.ApplyNewGameBuildings(data);
        var store = Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>(); store.SetOpen(false);
        Object.FindObjectOfType<DriverDelivery>().enabled = false;
        Object.FindObjectOfType<ParkingTraffic>().enabled = false;
        foreach (var roads in Object.FindObjectsOfType<TownRoadTraffic>()) roads.enabled = false;
        // Keep layout event subscriptions alive while advancing simulation manually.
        Time.timeScale = 0;
        for (int i = 0; i < 5; i++) yield return null;
        store.Advance(.05f);
        var pack = StoreAssortmentPack.Load(); var inventory = orders.products; var catalog = orders.productCatalog;
        var building = starter.building; var shop = Object.FindObjectOfType<ShopWindow>(true);
        Require(pack.Products.Count >= 48 && pack.Fixtures.Count >= 4, "Incomplete assortment pack.");
        Require((int)ProductStorageType.Frozen == 8 && (int)ShelfStorageType.Produce == 6, "Legacy enum identities changed.");
        var oldIds = new[] { "product_bread", "product_milk", "product_sodacola", "product_apple", "product_steak" };
        foreach (var id in oldIds) Require(catalog.GetById(id) != null, "Old save ID missing: " + id);
        int count = catalog.Products.Count; catalog.RebuildCache(); catalog.RebuildCache();
        Require(catalog.Products.Count == count && catalog.Products.Select(p => p.Id).Distinct().Count() == count, "Repeated pack loading duplicated products.");
        foreach (var p in pack.Products) Require(catalog.GetById(p.Id) == p, "Save catalogue missing " + p.Id);
        foreach (var f in pack.Fixtures) Require(orders.buildCatalog.GetById(f.id) == f, "Build/save catalogue missing " + f.id);
        inventory.Clear();
        var legacyShelfItems = AssetDatabase.FindAssets("t:BuildItemData", new[] { "Assets/Prefabs/Shelf" })
            .Select(g => AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i.prefab != null && i.prefab.GetComponent<PlacedShelfStock>() != null).ToArray();
        var shelves = legacyShelfItems.Concat(pack.Fixtures).Distinct().ToArray();
        shop.gameObject.SetActive(true); shop.OpenProducts(); yield return null;
        foreach (var product in pack.Products)
        {
            Require(product.Icon != null && product.ShelfDisplayPrefab != null && product.UnitWeightKg > 0 && product.UnitVolumeM3 > 0, "Incomplete product: " + product.Id);
            Require(product.SellPrice * product.BoxAmount > product.BuyPrice, "Invalid selling margin: " + product.Id);
            var card = shop.GetComponentsInChildren<ProductShopItemCard>().Single(c => c.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == product.DisplayName));
            var button = card.GetComponentInChildren<Button>(); orders.money.SetMoney(product.BuyPrice - 1); button.onClick.Invoke();
            Require(orders.Ledger.Entries.Count == 0 && inventory.GetCount(product) == 0, "Unaffordable order was granted: " + product.Id);
            orders.money.SetMoney(product.BuyPrice); button.onClick.Invoke();
            Require(orders.money.Money == 0 && inventory.GetCount(product) == 0, "Purchase bypassed delivery or charged incorrectly.");
            var paid = JsonUtility.FromJson<DeliverySaveData>(JsonUtility.ToJson(orders.Ledger.Capture())); orders.Ledger.Restore(paid);
            var entry = orders.Ledger.Entries.Single(); Require(entry.quantity == product.BoxAmount, "Carton save lost units.");
            var supplier = SupplierRouting.ForProduct(product.StorageType);
            Require(!orders.LoadFrom(entry, supplier == SupplierKind.Farmers ? SupplierKind.Wholesale : SupplierKind.Farmers), "Wrong supplier released cargo.");
            Require(orders.LoadFrom(entry, supplier), "Correct supplier could not load " + product.Id);
            Require(orders.PickupWeightKg <= 350 && orders.PickupVolumeM3 <= 2, "One carton exceeds starter vehicle.");
            Require(orders.Unload() && inventory.GetCount(product) == product.BoxAmount, "Delivery not placed in warehouse.");
            Require(!orders.Unload() && inventory.GetCount(product) == product.BoxAmount, "Duplicate unload granted more goods.");
            inventory.ApplySaveData(inventory.BuildSaveData(), catalog);
            Require(inventory.GetCount(product) == product.BoxAmount, "Warehouse save lost " + product.Id);
            var compatible = shelves.Where(s => s.prefab.GetComponent<PlacedShelfStock>().IsProductAllowed(product)).ToArray();
            Require(compatible.Length > 0, "No storage for " + product.Id);
            foreach (var fixture in pack.Fixtures)
            {
                if(fixture.id.StartsWith("shelf_fixture_")) continue;
                bool expected = fixture.id == "shelf_chilled_counter_01" ? product.StorageType == ProductStorageType.Meat
                    : fixture.id == "shelf_chest_freezer_01" ? product.StorageType == ProductStorageType.Frozen
                    : fixture.id == "shelf_household_rack_01" ? product.StorageType == ProductStorageType.Household
                        || (int)product.StorageType >= (int)ProductStorageType.PetSupplies
                    : product.StorageType == ProductStorageType.Snacks || product.StorageType == ProductStorageType.Groceries || product.StorageType == ProductStorageType.Preserves;
                if(product.Id == "product_broom" || product.Id == "product_mop") expected = false;
                Require(fixture.prefab.GetComponent<PlacedShelfStock>().IsProductAllowed(product) == expected, "Storage rule mismatch: " + fixture.id + "/" + product.Id);
            }
            if (product.StorageType == ProductStorageType.Household)
                Require(compatible.All(f => f.id == "shelf_household_rack_01" || f.id.StartsWith("shelf_fixture_")), "Household chemical allowed on food shelves.");
            var instance = Object.Instantiate(compatible[0].prefab, new Vector3(1000, 0, 1000), Quaternion.identity);
            var stock = instance.GetComponent<PlacedShelfStock>(); stock.ClearStock();
            Require(stock.RefillFromInventory(inventory, product), "Cannot restock " + product.Id);
            yield return null;
            var slots = instance.transform.Find("ProductSlots");
            Require(slots.GetComponentsInChildren<MeshRenderer>().Length == stock.CurrentAmount, "Visible units differ from stored stock.");
            foreach (var slot in slots.GetComponentsInChildren<ShelfProductSlot>())
                foreach (var filter in slot.GetComponentsInChildren<MeshFilter>()) CheckFit(slot, filter);
            int before = stock.CurrentAmount;
            var visit = new ShopperVisit(null, stock, 1, false, 1);
            Require(visit.Product == product && visit.CommitGoods() && stock.CurrentAmount == before - 1, "Customer cannot take new goods.");
            stock.SetStockFromSave(product, stock.CurrentAmount); Require(stock.AssignedProduct == product && stock.CurrentAmount == before - 1, "Shelf restore lost product.");
            Object.Destroy(instance); inventory.Clear(); yield return null;
            trace.AppendLine("PASS " + product.Id + ": UI affordability, paid carton, supplier routing, cargo, unload once, save, compatible stock display, fit and shopper basket.");
        }
        shop.Close();
        int compatiblePairs = 0;
        foreach (var fixture in pack.Fixtures.Where(f => f.id.StartsWith("shelf_fixture_")))
        {
            var instance = Object.Instantiate(fixture.prefab, new Vector3(1000,0,1000), Quaternion.Euler(0,90,0));
            var stock = instance.GetComponent<PlacedShelfStock>();
            var display = instance.GetComponent<ShelfProductDisplay>();
            foreach (var product in pack.Products.Where(stock.IsProductAllowed))
            {
                stock.SetStockFromSave(product, 999);
                yield return null;
                var filters = instance.transform.Find("ProductSlots").GetComponentsInChildren<MeshFilter>();
                Require(filters.Length == stock.CurrentAmount, "Visible quantity differs for " + fixture.id + "/" + product.Id);
                foreach(var filter in filters) CheckFit(filter.GetComponentInParent<ShelfProductSlot>(), filter);
                foreach(var workSlot in display.GetWorkSlots(stock.MaxAmount))
                    Require(workSlot.GetComponent<ShelfProductSlot>().Supports(product), "Stocker targets incompatible slot.");
                compatiblePairs++;
            }
            Object.Destroy(instance);yield return null;
        }
        trace.AppendLine("PASS " + compatiblePairs + " compatible new fixture/product combinations: full-capacity seating and stocker targets.");
        foreach (var stock in Object.FindObjectsOfType<PlacedShelfStock>()) stock.ClearStock();
        foreach (var fixture in pack.Fixtures)
        {
            shop.gameObject.SetActive(true); shop.OpenCategory_Shelves(); yield return null;
            var card = shop.GetComponentsInChildren<ShopItemCard>().Single(c => c.nameText.text == fixture.displayName);
            orders.money.SetMoney(fixture.price); int old = building.inventory.GetCount(fixture); card.buyButton.onClick.Invoke();
            Require(orders.money.Money == 0 && building.inventory.GetCount(fixture) == old, "Fixture order skipped delivery.");
            var entry = orders.Ledger.Entries.Single(); Require(orders.SupplierFor(entry) == SupplierKind.Equipment, "Fixture routed to wrong supplier.");
            Require(orders.LoadFrom(entry, SupplierKind.Equipment) && orders.Unload(), "Equipment delivery failed.");
            building.inventory.ApplySaveData(building.inventory.BuildSaveData(), orders.buildCatalog);
            Require(building.inventory.GetCount(fixture) == old + 1, "Fixture inventory save lost identity."); shop.Close();
            for (int facing = 0; facing < 4; facing++)
            {
                if (facing > 0) building.inventory.Add(fixture, 1);
                building.EnterBuildMode(fixture); building.RotateSelected(facing); bool placed = false;
                foreach (var tile in starter.floors.BuildSaveData().Where(t => t.floor == 0 && t.itemId != "asphalt_01"))
                    if (building.TryPlaceAt(new Vector3Int(tile.x, 0, tile.z))) { placed = true; break; }
                building.ExitBuildMode(); Require(placed, "Fixture cannot be placed on real floor: " + fixture.id + " facing " + facing);
                var placedObject = Object.FindObjectsOfType<PlacedObject>().Single(p => p.item == fixture);
                Require(placedObject.facing == facing && building.BuildPlacedSaveData().Any(p => p.itemId == fixture.id), "Placed/save pose mismatch.");
                Physics.SyncTransforms(); store.Advance(.05f); store.Advance(.05f);
                if (facing == 0)
                {
                    var product = pack.Products.First(p => placedObject.GetComponent<PlacedShelfStock>().IsProductAllowed(p));
                    var stock = placedObject.GetComponent<PlacedShelfStock>(); inventory.Clear(); inventory.Add(product, 10); stock.RefillFromInventory(inventory, product);
                    var productSave = Object.FindObjectOfType<ProductSaveService>();
                    var stockSnapshot = productSave.BuildShelfSaveData(); var layoutSnapshot = building.BuildPlacedSaveData();
                    Require(stockSnapshot.Any(e => e.buildItemId == fixture.id && e.productId == product.Id && e.amount == Mathf.Min(10, stock.MaxAmount)), "Real shelf save omitted new fixture stock.");
                    building.ApplyPlacedSaveData(layoutSnapshot, orders.buildCatalog);
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    productSave.ApplyShelfSaveData(stockSnapshot);
                    placedObject = Object.FindObjectsOfType<PlacedObject>().Single(p => p.item == fixture);
                    stock = placedObject.GetComponent<PlacedShelfStock>();
                    Require(stock.CurrentProduct == product && stock.CurrentAmount == Mathf.Min(10, stock.MaxAmount), "Layout and stock reconstruction lost new goods.");
                    Physics.SyncTransforms(); store.Advance(.05f); store.Advance(.05f);
                    yield return null;
                    store.Balance.thiefChance = 0; store.SetOpen(true);
                    Require(store.TrySpawnCustomer(), "Customer cannot route to new fixture: " + fixture.id + " / " + store.Notice);
                    var visit = store.Visits.Single(); store.SetOpen(false);
                    for (int frame = 0; frame < 2000 && visit.Stage != VisitStage.Waiting && !visit.IsFinished; frame++)
                    { store.Advance(.05f); if (frame % 20 == 0) yield return null; }
                    Require(visit.Stage == VisitStage.Waiting, "Customer failed to browse and reach checkout: " + fixture.id);
                    int wallet = orders.money.Money; int remaining = stock.CurrentAmount;
                    Require(store.ClaimCheckout() == visit && store.CompleteCheckout(visit), "Checkout rejected new product.");
                    Require(orders.money.Money == wallet + product.SellPrice * visit.Quantity && stock.CurrentAmount == remaining - visit.Quantity, "Checkout quantities/value mismatch.");
                    Require(!store.CompleteCheckout(visit), "Checkout paid twice.");
                    for (int frame = 0; frame < 2000 && !visit.IsFinished; frame++) { store.Advance(.05f); if (frame % 20 == 0) yield return null; }
                    Require(visit.IsFinished, "Customer could not leave new fixture.");
                    trace.AppendLine("PASS " + fixture.id + ": real customer walk, browse, queue, pay exactly once, leave.");
                }
                building.grid.Release(placedObject.occupiedCells); Object.DestroyImmediate(placedObject.gameObject); Physics.SyncTransforms(); building.NotifyLayoutChanged();
            }
            trace.AppendLine("PASS " + fixture.id + ": UI order, equipment delivery, inventory save, floor placement in four rotations, placed save entry.");
        }
        File.WriteAllText(AssortmentUpdateBatch.Report + "/details.txt", trace.ToString());
    }
    private static void CheckFit(ShelfProductSlot slot, MeshFilter filter)
    {
        var bounds = filter.sharedMesh.bounds;
        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
        {
            var point = slot.transform.InverseTransformPoint(filter.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
            Require(Mathf.Abs(point.x) <= slot.usableSize.x / 2 + .002f && Mathf.Abs(point.z) <= slot.usableSize.z / 2 + .002f
                && point.y >= (slot.hanging ? -slot.usableSize.y : 0) - .002f && point.y <= (slot.hanging ? 0 : slot.usableSize.y) + .002f, "Product clips shelf slot: " + filter.name);
        }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending, false); Time.timeScale = 1; checks = null;
        File.WriteAllText(AssortmentUpdateBatch.Report + "/play.txt", error == null ? $"PASS {StoreAssortmentPack.Load().Products.Count} UI order/delivery/warehouse/stock/basket/save checks; {StoreAssortmentPack.Load().Fixtures.Count} fixtures placed in every rotation and used by real checkout customers. No player-save access.\n" : "FAIL " + error + "\n" + trace);
        EditorApplication.isPlaying = false; EditorApplication.Exit(error == null ? 0 : 1);
    }
    private static void Require(bool condition, string message) => AssortmentUpdateBatch.Require(condition, message);
}
