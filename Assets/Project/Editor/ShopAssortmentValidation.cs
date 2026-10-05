using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Products;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ShopAssortmentValidation
{
    [MenuItem("Retail Empire/Products/Validate expanded assortment in sandbox %#&F12")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != ShopGameplaySandbox.Path
            || Object.FindObjectOfType<SaveManager>(true) != null)
            throw new InvalidOperationException("Requires isolated sandbox Play Mode without SaveManager.");
        Object.FindObjectOfType<StoreOperations>().StartCoroutine(Check());
    }

    private static IEnumerator Check()
    {
        const string folder = "Library/ShopAssortmentQA";
        Directory.CreateDirectory(folder);
        var log = new StringBuilder();
        File.WriteAllText(folder + "/validation.txt", "Assortment validation is running.\n");
        var catalog = Object.FindObjectOfType<ProductCatalog>();
        var inventory = Object.FindObjectOfType<ProductInventory>();
        var wallet = Object.FindObjectOfType<RetailEmpireTycoon.Economy.MoneyController>();
        var shop = Object.FindObjectOfType<ShopWindow>(true);
        var store = Object.FindObjectOfType<StoreOperations>();
        var building = Object.FindObjectOfType<BuildController>();
        var oldBuildInventory = building.inventory.BuildSaveData();
        PlacedObject installedRack = null;
        var oldWarehouse = inventory.BuildSaveData(); int oldMoney = wallet.Money; bool oldOpen = store.IsOpen;
        var temporary = new List<GameObject>(); bool completed = false;
        try
        {
            store.SetOpen(false); Object.FindObjectOfType<GameplayControls>().CloseWindows();
            var shelves = AssetDatabase.FindAssets("t:BuildItemData", new[] { "Assets/Prefabs/Shelf" })
                .Select(g => AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i => i.prefab != null && i.prefab.GetComponent<PlacedShelfStock>() != null).ToArray();
            Require(shelves.Length >= 6, "Separate produce rack missing.");
            Require(catalog.Products.Count >= 25, "Expanded product catalog missing.");
            Require(catalog.Products.Select(p => p.Id).Distinct().Count() == catalog.Products.Count, "Duplicate product IDs.");
            shop.gameObject.SetActive(true); shop.OpenProducts(); yield return null;
            foreach (var product in catalog.Products)
            {
                Require(product != null && catalog.GetById(product.Id) == product && product.Icon != null && product.ShelfDisplayPrefab != null,
                    "Product lacks catalog/art/model: " + product?.Id);
                Require(product.SellPrice * product.BoxAmount > product.BuyPrice, "No selling margin: " + product.Id);
                var card = shop.GetComponentsInChildren<ProductShopItemCard>().FirstOrDefault(c =>
                    c.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == ShopText.Item(product)));
                Require(card != null, "Product missing from store UI: " + product.Id);
                int before = inventory.GetCount(product);
                wallet.SetMoney(product.BuyPrice - 1); card.GetComponentInChildren<Button>().onClick.Invoke();
                Require(inventory.GetCount(product) == before, "Unaffordable pack was added: " + product.Id);
                wallet.SetMoney(product.BuyPrice); card.GetComponentInChildren<Button>().onClick.Invoke();
                Require(wallet.Money == 0 && inventory.GetCount(product) == before + product.BoxAmount, "Pack purchase failed: " + product.Id);
                var saved = inventory.BuildSaveData(); inventory.ApplySaveData(saved, catalog);
                Require(inventory.GetCount(product) == before + product.BoxAmount, "Warehouse save round trip failed: " + product.Id);
                var compatible = shelves.Where(s => s.prefab.GetComponent<PlacedShelfStock>().IsProductAllowed(product)).ToArray();
                Require(compatible.Length > 0, "No shelf accepts " + product.Id);
                foreach (var item in shelves)
                {
                    bool expected = Accepts(item.prefab.GetComponent<PlacedShelfStock>().ShelfType, product.StorageType);
                    Require(item.prefab.GetComponent<PlacedShelfStock>().IsProductAllowed(product) == expected,
                        "Wrong storage compatibility: " + item.id + " / " + product.Id);
                }
                var instance = Object.Instantiate(compatible[0].prefab, new Vector3(1000, 0, 1000), Quaternion.identity);
                temporary.Add(instance);
                var stock = instance.GetComponent<PlacedShelfStock>();
                stock.ClearStock(); Require(stock.RefillFromInventory(inventory, product), "Restocking failed: " + product.Id);
                Require(stock.CurrentProduct == product && stock.CurrentAmount > 0, "Shelf did not own stocked product: " + product.Id);
                var display = instance.GetComponent<ShelfProductDisplay>();
                yield return null;
                Require(instance.transform.Find("ProductSlots").GetComponentsInChildren<Renderer>().Length > 0, "Stock has no models: " + product.Id);
                Require(stock.TryTakeOne(out var bought) && bought == product, "Customer cannot take stocked product: " + product.Id);
                int remaining = stock.CurrentAmount;
                stock.SetStockFromSave(product, remaining);
                Require(stock.CurrentProduct == product && stock.CurrentAmount == remaining, "Shelf save restore failed: " + product.Id);
                if (product.Id == "product_bread" || product.Id == "product_apple")
                {
                    var work = Object.FindObjectOfType<WorkMinigame>();
                    stock.ClearStock(); inventory.Add(product, 10);
                    int initialAmount = inventory.GetCount(product);
                    Require(work.BeginRestock(stock, product), "New rack cannot start player restocking: " + product.Id);
                    Require(work.CompleteStep(0), "New rack cannot receive first gesture.");
                    work.Cancel();
                    Require(stock.IsEmpty && inventory.GetCount(product) == initialAmount, "Cancelling new rack restock consumed goods.");
                    Require(work.BeginRestock(stock, product), "New rack cannot restart player restocking.");
                    for (int gesture = 0; gesture < 5; gesture++) Require(work.CompleteStep(gesture), "New rack gesture failed.");
                    Require(!work.IsActive && stock.CurrentProduct == product && stock.CurrentAmount > 0,
                        "New rack did not commit player restocking.");
                    log.AppendLine("PASS " + product.Id + ": player work start, cancel without consumption, five gestures and commit");
                }
                log.AppendLine("PASS " + product.Id + ": purchase, warehouse/save, " + string.Join(", ", compatible.Select(s => s.id)) + ", restock, model, customer take, shelf restore");
                Object.Destroy(instance); yield return null;
            }
            shop.Close();
            var produce = shelves.First(s => s.id == "shelf_produce_01");
            Require(Object.FindObjectOfType<BuildItemCatalog>().GetById(produce.id) == produce, "Produce rack missing from build/save catalog.");
            Require(produce.footprint == shelves.First(s => s.id == "shelf_fresh_01").footprint, "Rack footprint unexpectedly changed.");
            shop.gameObject.SetActive(true); shop.OpenCategory_Shelves(); yield return null;
            var rackCard = shop.GetComponentsInChildren<ShopItemCard>().First(c => c.nameText.text == ShopText.Item(produce));
            int oldRackCount = building.inventory.GetCount(produce);
            wallet.SetMoney(produce.price); rackCard.buyButton.onClick.Invoke();
            Require(wallet.Money == 0 && building.inventory.GetCount(produce) == oldRackCount + 1, "Produce rack purchase failed.");
            building.inventory.ApplySaveData(building.inventory.BuildSaveData(), Object.FindObjectOfType<BuildItemCatalog>());
            Require(building.inventory.GetCount(produce) == oldRackCount + 1, "Produce rack inventory save round trip failed.");
            shop.Close(); building.EnterBuildMode(produce);
            bool placed = false;
            foreach (var rect in building.territory.PurchasedRects)
            {
                for (int x = rect.min.x + 2; x < rect.max.x - produce.footprint.x - 2 && !placed; x++)
                    for (int z = rect.min.z + 2; z < rect.max.z - produce.footprint.y - 2 && !placed; z++)
                        placed = building.TryPlaceAt(new Vector3Int(x, 0, z));
                if (placed) break;
            }
            Require(placed, "Produce rack could not be installed through the real grid rules.");
            installedRack = Object.FindObjectsOfType<PlacedObject>().First(p => p.item == produce);
            building.ExitBuildMode(); Physics.SyncTransforms(); store.Advance(.1f);
            Require(building.BuildPlacedSaveData().Any(p => p.itemId == produce.id), "Installed produce rack is absent from save data.");
            log.AppendLine("PASS produce rack: UI purchase, inventory save round trip, real grid placement and placed save entry");
            completed = true;
            File.WriteAllText(folder + "/validation.txt", log + "ASSORTMENT VALIDATION PASSED\n");
            Debug.Log("Assortment validation PASSED: all 25 products, compatibility, purchases, restock, models and save IDs.");
        }
        finally
        {
            building.ExitBuildMode();
            if (installedRack != null)
            {
                building.grid.Release(installedRack.occupiedCells); Object.DestroyImmediate(installedRack.gameObject);
                Physics.SyncTransforms();
            }
            building.inventory.ApplySaveData(oldBuildInventory, Object.FindObjectOfType<BuildItemCatalog>());
            shop.Close(); foreach (var instance in temporary) if (instance != null) Object.Destroy(instance);
            Object.FindObjectOfType<WorkMinigame>().Cancel();
            inventory.ApplySaveData(oldWarehouse, catalog); wallet.SetMoney(oldMoney); store.SetOpen(oldOpen);
            if (!completed) File.WriteAllText(folder + "/validation.txt", log + "ASSORTMENT VALIDATION DID NOT COMPLETE\n");
        }
    }

    private static bool Accepts(ShelfStorageType shelf, ProductStorageType product)
    {
        switch (shelf)
        {
            case ShelfStorageType.Fresh: return product == ProductStorageType.Bakery;
            case ShelfStorageType.Produce: return product == ProductStorageType.Produce;
            case ShelfStorageType.Refrigerated: return product == ProductStorageType.Dairy;
            case ShelfStorageType.ColdPantry: return product == ProductStorageType.Meat || product == ProductStorageType.Frozen;
            case ShelfStorageType.WallGoods:
            case ShelfStorageType.DoubleSided: return product == ProductStorageType.Oil || product == ProductStorageType.Groceries || product == ProductStorageType.Drinks;
            default: return false;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
