using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;

namespace RetailEmpireTycoon.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class ProductSaveService : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ProductInventory productInventory;
        [SerializeField] private ProductCatalog productCatalog;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private void Awake()
        {
            FindMissingRefs();
        }

        public List<ProductInventorySaveEntry> BuildWarehouseSaveData()
        {
            FindMissingRefs();

            if (productInventory == null)
                return new List<ProductInventorySaveEntry>();

            return productInventory.BuildSaveData();
        }

        public void ApplyWarehouseSaveData(List<ProductInventorySaveEntry> data)
        {
            FindMissingRefs();

            if (productInventory == null || productCatalog == null)
            {
                LogWarning("Cannot load warehouse. ProductInventory or ProductCatalog is missing.");
                return;
            }

            productInventory.ApplySaveData(data, productCatalog);
        }

        public List<ShelfStockSaveEntry> BuildShelfSaveData()
        {
            List<ShelfStockSaveEntry> data = new List<ShelfStockSaveEntry>();

            PlacedShelfStock[] shelves = FindObjectsOfType<PlacedShelfStock>(true);

            foreach (PlacedShelfStock shelf in shelves)
            {
                if (shelf == null || shelf.AssignedProduct == null)
                    continue;

                PlacedObject placedObject = shelf.GetComponent<PlacedObject>();

                if (placedObject == null || placedObject.item == null)
                {
                    data.Add(new ShelfStockSaveEntry
                    {
                        sceneShelfId = SceneShelfKey(shelf), productId = shelf.AssignedProduct.Id, amount = shelf.CurrentAmount
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(placedObject.item.id))
                {
                    LogWarning("Shelf BuildItemData has empty id: " + shelf.name);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(shelf.AssignedProduct.Id))
                {
                    LogWarning("Shelf product has empty id: " + shelf.AssignedProduct.name);
                    continue;
                }

                data.Add(new ShelfStockSaveEntry
                {
                    buildItemId = placedObject.item.id,

                    anchorX = placedObject.anchorCell.x,
                    anchorY = placedObject.anchorCell.y,
                    anchorZ = placedObject.anchorCell.z,

                    rotated = placedObject.rotated,
                    facing = placedObject.facing,

                    productId = shelf.AssignedProduct.Id,
                    amount = shelf.CurrentAmount
                });
            }

            return data;
        }

        public void ApplyShelfSaveData(List<ShelfStockSaveEntry> data)
        {
            FindMissingRefs();

            if (productCatalog == null)
            {
                LogWarning("Cannot load shelves. ProductCatalog is missing.");
                return;
            }

            PlacedShelfStock[] shelves = FindObjectsOfType<PlacedShelfStock>(true);

            foreach (PlacedShelfStock shelf in shelves)
            {
                if (shelf != null)
                    shelf.ClearStock();
            }

            if (data == null)
                return;

            Dictionary<string, PlacedShelfStock> shelvesByKey = BuildShelfLookup(shelves);

            foreach (ShelfStockSaveEntry entry in data)
            {
                if (entry == null)
                    continue;

                if (entry.amount < 0)
                    continue;

                string key = BuildKey(entry);

                if (!shelvesByKey.TryGetValue(key, out PlacedShelfStock shelf))
                {
                    LogWarning("Saved shelf not found. Key: " + key);
                    continue;
                }

                ProductItemData product = productCatalog.GetById(entry.productId);

                if (product == null)
                {
                    LogWarning("Saved product not found. Product id: " + entry.productId);
                    continue;
                }

                shelf.SetStockFromSave(product, entry.amount);
            }
        }

        private Dictionary<string, PlacedShelfStock> BuildShelfLookup(PlacedShelfStock[] shelves)
        {
            Dictionary<string, PlacedShelfStock> lookup = new Dictionary<string, PlacedShelfStock>();

            foreach (PlacedShelfStock shelf in shelves)
            {
                if (shelf == null)
                    continue;

                PlacedObject placedObject = shelf.GetComponent<PlacedObject>();

                if (placedObject == null || placedObject.item == null)
                {
                    string sceneKey = SceneShelfKey(shelf);
                    if (!lookup.ContainsKey(sceneKey)) lookup.Add(sceneKey, shelf);
                    continue;
                }

                string key = BuildKey(placedObject);

                if (!lookup.ContainsKey(key))
                    lookup.Add(key, shelf);
            }

            return lookup;
        }

        private static string BuildKey(PlacedObject placedObject)
        {
            string itemId = placedObject.item != null ? placedObject.item.id : string.Empty;

            return itemId
                + "|"
                + placedObject.anchorCell.x
                + "|"
                + placedObject.anchorCell.y
                + "|"
                + placedObject.anchorCell.z
                + "|"
                + placedObject.rotated
                + "|"
                + placedObject.facing;
        }

        private static string SceneShelfKey(PlacedShelfStock shelf)
        {
            // Prebuilt store furniture has no build-inventory identity. Its type and stable world pose identify it.
            Vector3 position = shelf.transform.position;
            return "scene|" + shelf.ShelfType + "|" + Mathf.RoundToInt(position.x * 1000)
                + "|" + Mathf.RoundToInt(position.y * 1000) + "|" + Mathf.RoundToInt(position.z * 1000)
                + "|" + Mathf.RoundToInt(shelf.transform.eulerAngles.y);
        }

        private static string BuildKey(ShelfStockSaveEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.sceneShelfId)) return entry.sceneShelfId;
            return entry.buildItemId
                + "|"
                + entry.anchorX
                + "|"
                + entry.anchorY
                + "|"
                + entry.anchorZ
                + "|"
                + entry.rotated
                + "|"
                + entry.facing;
        }

        private void FindMissingRefs()
        {
            if (productInventory == null)
                productInventory = FindObjectOfType<ProductInventory>(true);

            if (productCatalog == null)
                productCatalog = FindObjectOfType<ProductCatalog>(true);
        }

        private void LogWarning(string message)
        {
            if (!debugLogs)
                return;

            Debug.LogWarning("[ProductSaveService] " + message, this);
        }
    }
}
