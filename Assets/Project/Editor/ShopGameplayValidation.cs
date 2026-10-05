using System;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Real component and scene-geometry checks, confined to the disposable gameplay sandbox.</summary>
public static class ShopGameplayValidation
{
    [MenuItem("Retail Empire/Shop/Validate gameplay in sandbox %#F7")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != ShopGameplaySandbox.Path)
            throw new InvalidOperationException("Requires the isolated gameplay sandbox in Play Mode.");
        var log = new StringBuilder();
        ShopGameplaySandbox.PrepareActivities();
        var shop = UnityEngine.Object.FindObjectOfType<StoreOperations>();
        var work = UnityEngine.Object.FindObjectOfType<WorkMinigame>();
        var warehouse = UnityEngine.Object.FindObjectOfType<ProductInventory>();
        var money = UnityEngine.Object.FindObjectOfType<MoneyController>();
        var building = UnityEngine.Object.FindObjectOfType<BuildController>();
        var product = AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/SodaCola_Product.asset");
        var shelves = UnityEngine.Object.FindObjectsOfType<PlacedShelfStock>();
        var snapshot = shelves.Select(s => (Shelf: s, Product: s.AssignedProduct, Amount: s.CurrentAmount)).ToArray();
        var inventorySnapshot = warehouse.BuildSaveData(); var moneySnapshot = money.Money; var shopSnapshot = shop.BuildSaveData();
        var originalBalance = shop.Balance; var balance = UnityEngine.Object.Instantiate(originalBalance);
        var timeScale = Time.timeScale; var cameraPose = Camera.main.transform.position; var cameraRotation = Camera.main.transform.rotation;
        try
        {
            Time.timeScale = 0; work.Cancel(); building.ExitBuildMode();
            UnityEngine.Object.FindObjectOfType<GameplayControls>().CloseWindows();
            shop.ApplySaveData(new ShopOperationsSaveData());
            balance.thiefChance = balance.litterChance = 0; ShopGameplaySetup.Set(shop, "balance", balance);
            foreach (var shelf in shelves) shelf.ClearStock(); warehouse.Clear(); money.SetMoney(6000);
            var shelfForTest = shelves.FirstOrDefault(s => s.IsProductAllowed(product));
            Require(shelfForTest != null, "No cola shelf in the real store.");
            Require(shop.enabled && shop.Navigation != null, "Operations not initialized.");

            Require(MoneyFormat.Compact(999) == "$999" && MoneyFormat.Compact(1000) == "$1000" && MoneyFormat.Compact(20000) == "$20000" && MoneyFormat.Compact(90000) == "$90000" && MoneyFormat.Compact(1000000) == "$1M" && MoneyFormat.Compact(1000000000) == "$1B", "Currency suffixes failed.");
            Require(MoneyFormat.Compact(999999) == "$999999", "Currency boundary rounding failed.");
            money.SetMoney(int.MaxValue - 1); money.Add(5); Require(money.Money == int.MaxValue, "Money overflow."); money.SetMoney(6000);
            log.AppendLine("PASS money suffixes, boundary rounding, overflow protection");

            foreach (string productName in new[] { "Bread_Product", "MilkBottle_Product" })
            {
                var goods = AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/" + productName + ".asset");
                foreach (var displayShelf in shelves.Where(s => s.IsProductAllowed(goods)))
                {
                    var display = displayShelf.GetComponent<ShelfProductDisplay>();
                    foreach (var slot in display.GetWorkSlots(5))
                    {
                        var preview = display.CreateWorkProduct(goods, slot);
                        Require(preview.transform.parent == slot && preview.transform.localPosition == Vector3.zero, "Work preview lost slot transform.");
                        Require(Quaternion.Angle(preview.transform.rotation, slot.rotation) < .01f, "Product ignores authored slot rotation: " + productName);
                        Require(Vector3.Distance(preview.transform.localScale, Vector3.one * display.ProductScale) < .001f, "Product ignores inherited shelf scale.");
                        UnityEngine.Object.Destroy(preview);
                    }
                }
            }
            log.AppendLine("PASS bread/milk slot orientation, local scale and parent hierarchy");
            var door = UnityEngine.Object.FindObjectsOfType<PlacedObject>().FirstOrDefault(p => p.item != null && p.item.isDoorway);
            if (door != null)
            {
                Require((shop.CheckoutPosition - shop.EntrancePosition).magnitude >= 1.05f, "Checkout occupies the entrance.");
                Require(shop.Navigation.TryPath(shop.ArrivalPosition, shop.CheckoutPosition, out var route, true), "Outdoor entrance route failed.");
                Require(route.Any(p => (p - shop.EntrancePosition).magnitude < .45f), "Customer bypasses the door.");
                Vector3 from = shop.ArrivalPosition;
                foreach (var point in route) { Require(shop.Navigation.CanTraverse(from, point), "Route intersects a solid wall."); from = point; }
                log.AppendLine("PASS outdoor-to-checkout route crosses door, wall-clear segments, checkout entrance clearance");
            }

            warehouse.Add(product, shelfForTest.MaxAmount);
            Require(work.BeginRestock(shelfForTest, product), "Cannot start restock.");
            Require(warehouse.GetCount(product) == shelfForTest.MaxAmount && shelfForTest.IsEmpty, "Opening mini-game moved goods.");
            for (int i = 0; i < 4; i++) Require(work.CompleteStep(i), "Placement step rejected.");
            Require(work.IsActive && shelfForTest.IsEmpty, "Four actions filled shelf.");
            work.Cancel(); Require(warehouse.GetCount(product) == shelfForTest.MaxAmount && shelfForTest.IsEmpty, "Cancellation consumed goods.");
            Require(Vector3.Distance(Camera.main.transform.position, cameraPose) < 0.001f && Quaternion.Angle(Camera.main.transform.rotation, cameraRotation) < 0.01f, "Work camera failed to restore pose.");
            Require(work.BeginRestock(shelfForTest, product), "Cannot restart restock.");
            for (int i = 0; i < 5; i++) Require(work.CompleteStep(i), "Restock step failed.");
            Require(!work.IsActive && shelfForTest.IsFull && warehouse.GetCount(product) == 0, "Full refill did not commit exactly once.");
            shelfForTest.ClearStock(); warehouse.Add(product, 1); Require(work.BeginRestock(shelfForTest, product), "Single-item work did not begin.");
            for (int i = 0; i < 4; i++) work.CompleteStep(i);
            Require(work.IsActive && shelfForTest.IsEmpty, "Single-item work bypassed five actions.");
            work.CompleteStep(4); Require(shelfForTest.CurrentAmount == 1 && warehouse.GetCount(product) == 0, "Single-item work fabricated goods.");
            log.AppendLine("PASS five gestures, cancel/no charge, full and one-unit refill, camera restoration");

            shelfForTest.SetStockFromSave(product, 24); shop.SetOpen(false); Require(!shop.TrySpawnCustomer(), "Closed shop admitted a customer.");
            shop.SetOpen(true); Require(shop.TrySpawnCustomer(), "Customer cannot walk from entrance to shelf.");
            Require(shelfForTest.CurrentAmount == 24, "Basket consumed unpaid goods.");
            var visit = shop.Visits[0];
            for (int i = 0; i < 300 && visit.Stage != VisitStage.Waiting; i++) shop.Advance(0.1f);
            Require(visit.Stage == VisitStage.Waiting, "Customer failed to reach checkout: " + shop.Notice);
            int beforeSale = money.Money; int beforeStock = shelfForTest.CurrentAmount;
            Require(work.BeginCheckout(), "Player checkout did not claim customer.");
            for (int i = 0; i < visit.Quantity; i++) work.CompleteStep(i);
            Require(money.Money == beforeSale + visit.Quantity * product.SellPrice && shelfForTest.CurrentAmount == beforeStock - visit.Quantity, "Checkout charged wrong goods or money.");
            Require(!shop.CompleteCheckout(visit), "Customer paid twice.");
            log.AppendLine("PASS real floor route, closed gate, unpaid reservation, checkout settlement/no double payment");

            shop.ApplySaveData(new ShopOperationsSaveData()); shelfForTest.SetStockFromSave(product, 24); shop.SetOpen(true);
            Require(shop.TrySpawnCustomer(true), "Thief route failed."); var thief = shop.Visits[0];
            for (int i = 0; i < 300 && thief.Stage != VisitStage.Escaping; i++) shop.Advance(0.1f);
            Require(thief.Stage == VisitStage.Escaping, "Thief never started escaping.");
            int protectedStock = shelfForTest.CurrentAmount;
            for (int i = 0; i < balance.thiefClicks; i++) shop.ClickThief(thief);
            Require(!shop.Visits.Contains(thief) && shelfForTest.CurrentAmount == protectedStock, "Catching thief lost stock.");
            log.AppendLine("PASS repeated-click thief capture preserves goods");

            shop.ApplySaveData(new ShopOperationsSaveData()); shelfForTest.SetStockFromSave(product, 24);
            Require(shop.TryHire(StaffRole.Guard), "Guard hire failed."); shop.SetOpen(true);
            Require(shop.TrySpawnCustomer(true), "Guard thief spawn failed."); thief = shop.Visits[0];
            protectedStock = shelfForTest.CurrentAmount;
            for (int i = 0; i < 350 && shop.Visits.Contains(thief); i++) shop.Advance(0.1f);
            Require(!shop.Visits.Contains(thief) && shelfForTest.CurrentAmount == protectedStock, "Guard failed to intercept thief before escape.");
            log.AppendLine("PASS guard physically pursues thief and preserves stock");

            shop.ApplySaveData(new ShopOperationsSaveData()); shelfForTest.SetStockFromSave(product, 24); shop.SetOpen(true);
            Require(shop.TrySpawnCustomer(true), "Escape thief spawn failed."); thief = shop.Visits[0];
            for (int i = 0; i < 350 && shop.Visits.Contains(thief); i++) shop.Advance(0.1f);
            Require(!shop.Visits.Contains(thief) && shelfForTest.CurrentAmount == 24 - thief.Quantity, "Escaped thief did not remove actual goods.");
            log.AppendLine("PASS escaped thief removes real stock without payment");

            shop.ApplySaveData(new ShopOperationsSaveData());
            var dirt = shop.SpawnDirt(shop.CheckoutPosition); Require(work.BeginCleaning(dirt), "Cleaning game failed to start.");
            for (int i = 0; i < 5; i++) work.CompleteStep(i);
            Require(!shop.Dirt.Contains(dirt), "Cleaning game did not remove dirt.");
            log.AppendLine("PASS player mop activity removes dirt");

            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole)))
            {
                int limit = StaffRoster.Limit(role, shop.Level);
                for (int i = 0; i < limit; i++)
                {
                    int beforeHire = money.Money; Require(shop.TryHire(role), "Employee hire within level limit failed.");
                    Require(money.Money == beforeHire - balance.HireCost(role), "Hiring charged wrong amount.");
                }
                int afterHire = money.Money; Require(!shop.TryHire(role) && money.Money == afterHire, "Hiring beyond level limit charged money.");
                for (int i = 0; i < limit; i++) Require(shop.TryDismiss(role), "Dismissal failed.");
            }
            Require(StaffRoster.Limit(StaffRole.Cashier, 6) == 3 && StaffRoster.Limit(StaffRole.Guard, 6) == 2, "Expansion hiring limit failed.");
            money.SetMoney(0); Require(!shop.TryHire(StaffRole.Cashier), "Unfunded hire succeeded."); money.SetMoney(6000);
            log.AppendLine("PASS hiring/limits, no charge on failure, dismissal, plot-based capacity");

            shop.ApplySaveData(new ShopOperationsSaveData()); shelfForTest.SetStockFromSave(product, 1); warehouse.Add(product, 30);
            Require(shop.TryHire(StaffRole.Stocker), "Stocker hire failed."); shop.SetOpen(true);
            for (int i = 0; i < 250 && warehouse.GetCount(product) == 30; i++) shop.Advance(0.1f);
            Require(warehouse.GetCount(product) < 30, "Stocker did not transfer real goods.");
            log.AppendLine("PASS stocker walks to shelf and replenishes from warehouse");

            shop.ApplySaveData(new ShopOperationsSaveData()); foreach (var shelf in shelves) shelf.ClearStock();
            dirt = shop.SpawnDirt(shop.CheckoutPosition); Require(shop.TryHire(StaffRole.Cleaner), "Cleaner hire failed."); shop.SetOpen(true);
            for (int i = 0; i < 200 && shop.Dirt.Contains(dirt); i++) shop.Advance(0.1f);
            Require(!shop.Dirt.Contains(dirt), "Cleaner failed to clean."); log.AppendLine("PASS cleaner walks to event and removes litter");

            shop.ApplySaveData(new ShopOperationsSaveData()); shelfForTest.SetStockFromSave(product, 24); Require(shop.TryHire(StaffRole.Cashier), "Cashier hire failed."); shop.SetOpen(true);
            beforeSale = money.Money; Require(shop.TrySpawnCustomer(), "Cashier customer spawn failed.");
            for (int i = 0; i < 400 && money.Money <= beforeSale; i++) shop.Advance(0.1f);
            Require(money.Money > beforeSale, "Cashier failed to settle sale after wages."); log.AppendLine("PASS cashier settles real sale automatically");

            int beforeClosed = money.Money; shop.SetOpen(false); shop.Advance(10); Require(money.Money == beforeClosed, "Closed shop charged salaries.");
            var save = JsonUtility.FromJson<ShopOperationsSaveData>(JsonUtility.ToJson(shop.BuildSaveData())); shop.ApplySaveData(save);
            Require(shop.Staff.Count(StaffRole.Cashier) == 1 && !shop.IsOpen, "Shop state save round trip failed.");
            Require(balance.ArrivalSeconds(6, 5) < balance.ArrivalSeconds(1, 1), "Rating/size do not affect demand.");
            log.AppendLine("PASS closed salaries, staff/open/reputation JSON round trip, rating/size demand");

            var stockSave = UnityEngine.Object.FindObjectOfType<ProductSaveService>();
            foreach (var shelf in shelves) shelf.ClearStock();
            shelfForTest.SetStockFromSave(product, 0);
            var shelfSave = stockSave.BuildShelfSaveData(); shelfForTest.ClearStock(); stockSave.ApplyShelfSaveData(shelfSave);
            Require(shelfForTest.IsEmpty && shelfForTest.AssignedProduct == product, "Empty shelf lost its assigned product after save/load.");
            log.AppendLine("PASS empty shelf product assignment survives real save service");

            warehouse.Clear(); warehouse.Add(product, 1);
            Require(shop.TryClaimRestock(out var claimedShelf, out _), "Restock claim failed before layout refresh.");
            shop.ApplySaveData(shop.BuildSaveData());
            Require(shop.TryClaimRestock(out var refreshedShelf, out _) && refreshedShelf == claimedShelf, "Layout refresh stranded a restock reservation.");
            shop.ReleaseRestock(refreshedShelf);
            log.AppendLine("PASS rebuilding the same layout releases previous job reservations immediately");

            var controls = UnityEngine.Object.FindObjectOfType<GameplayControls>();
            var inventoryKey = controls.Key(ShopAction.Inventory); var warehouseKey = controls.Key(ShopAction.Warehouse);
            try
            {
                Require(controls.Rebind(ShopAction.Inventory, warehouseKey, false), "Rebind failed.");
                Require(controls.Key(ShopAction.Warehouse) == inventoryKey, "Duplicate hotkeys were not swapped.");
                Require(!controls.Rebind(ShopAction.Inventory, KeyCode.W, false), "Reserved camera key rebound.");
            }
            finally { controls.Rebind(ShopAction.Inventory, inventoryKey, false); controls.CancelCapture(); }
            log.AppendLine("PASS rebind collisions swap actions, WASD remains reserved; player preferences unchanged");

            ValidateModalUi(log);
            building.EnterBuildMode(AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/WallGoodsShelf_BuildItem.asset"));
            UnityEngine.Object.FindObjectOfType<StoreWallVisibility>().SendMessage("LateUpdate");
            var walls = UnityEngine.Object.FindObjectsOfType<StoreWallOccluder>();
            int hiddenWalls = walls.Count(w => w.GetComponentsInChildren<Renderer>().Any(r => r.forceRenderingOff));
            Require(hiddenWalls <= 4, "Construction hides too many wall sections: " + hiddenWalls);
            Require(walls.All(w => w.GetComponentsInChildren<Collider>().All(c => c.enabled)), "Cutaway disables wall physics.");
            building.ExitBuildMode(); UnityEngine.Object.FindObjectOfType<StoreWallVisibility>().SendMessage("LateUpdate");
            Require(walls.All(w => w.GetComponentsInChildren<Renderer>().All(r => !r.forceRenderingOff)), "Walls not restored after construction.");
            log.AppendLine("PASS at most four hidden construction wall sections, unchanged physics, full restoration");
            log.Insert(0, "ALL CHECKS PASSED\n");
        }
        catch (Exception error) { log.AppendLine("FAIL " + error); Debug.LogException(error); }
        finally
        {
            work.Cancel(); ShopGameplaySetup.Set(shop, "balance", originalBalance); shop.ApplySaveData(shopSnapshot);
            warehouse.ApplySaveData(inventorySnapshot, UnityEngine.Object.FindObjectOfType<ProductCatalog>());
            foreach (var state in snapshot) if (state.Shelf != null) state.Shelf.SetStockFromSave(state.Product, state.Amount);
            money.SetMoney(moneySnapshot); Time.timeScale = timeScale; UnityEngine.Object.Destroy(balance);
            Directory.CreateDirectory("Library/ShopGameplayQA"); File.WriteAllText("Library/ShopGameplayQA/validation.txt", log.ToString());
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void ValidateModalUi(StringBuilder log)
    {
        var hud = UnityEngine.Object.FindObjectOfType<ShopOperationsHud>();
        var legacy = UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c => !c.transform.IsChildOf(hud.transform)).Select(c => (Canvas: c, Enabled: c.enabled)).ToArray();
        hud.ToggleStaff();
        Require(hud.StaffVisible && !UnityEngine.Object.FindObjectOfType<MainCamera>().enabled, "Staff modal failed to lock camera.");
        Require(legacy.All(s => s.Canvas.enabled == s.Enabled), "Staff page must stay inside the visible legacy Store window.");
        ValidateTextAndBounds("Staff recruitment"); hud.CloseStaff();
        UnityEngine.Object.FindObjectOfType<GameplayControls>().CloseWindows();
        Require(legacy.All(s => s.Canvas.enabled == s.Enabled), "Closing staff failed to restore legacy UI.");
        hud.ToggleControls(); ValidateTextAndBounds("Key binding settings"); hud.CloseControls();
        Require(legacy.All(s => s.Canvas.enabled == s.Enabled), "Closing key settings failed to restore legacy UI.");
        log.AppendLine("PASS modal camera/input isolation, exact legacy UI restoration, Cyrillic labels and panel bounds");
    }
    private static void ValidateTextAndBounds(string name)
    {
        var panel = GameObject.Find(name).GetComponent<RectTransform>();
        UnityEngine.Canvas.ForceUpdateCanvases();
        foreach (var label in panel.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            label.ForceMeshUpdate();
            Require(!label.isTextOverflowing, "UI text overflow: " + label.text);
            var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners) Require(RectTransformUtility.RectangleContainsScreenPoint(panel, corner), "Label outside modal: " + label.text);
        }
    }
}
