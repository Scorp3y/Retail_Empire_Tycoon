using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;

namespace RetailEmpireTycoon.Shelves
{
    [DisallowMultipleComponent]
    public sealed class ProductAssignMode : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private ProductInventory inventory;
        [SerializeField] private RetailEmpireTycoon.StoreOperations.WorkMinigame workMinigame;

        [Header("Raycast")]
        [SerializeField] private LayerMask shelfMask;
        [SerializeField, Min(1f)] private float maxDistance = 2000f;
        [SerializeField] private bool blockWhenPointerOverUI = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private readonly List<PlacedShelfStock> _shelves = new List<PlacedShelfStock>();
        private ProductItemData _selectedProduct;

        public bool IsActive => _selectedProduct != null;
        public ProductItemData SelectedProduct => _selectedProduct;

        private void Awake()
        {
            FindMissingRefs();
        }

        private void Update()
        {
            if (workMinigame != null && workMinigame.IsActive) return;
            if (!IsActive)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            // Right drag belongs to camera navigation and must not refill a shelf.
            if (!Input.GetMouseButtonDown(0))
                return;

            TryAssignByMouse();
        }

        public void BeginAssign(ProductItemData product)
        {
            if (product == null)
                return;

            FindMissingRefs();

            _selectedProduct = product;

            CacheShelves();
            RefreshHighlights();

            Log("Begin assign: " + product.DisplayName + " / Type: " + product.StorageType);
            Log("Shelves found: " + _shelves.Count);
            Log("Inventory count: " + inventory.GetCount(product));
        }

        public void Cancel()
        {
            if (_selectedProduct != null)
                Log("Cancel assign: " + _selectedProduct.DisplayName);

            _selectedProduct = null;
            ClearHighlights();
        }

        private void TryAssignByMouse()
        {
            if (IsPointerBlockedByUI())
            {
                Log("Click blocked by UI.");
                return;
            }

            if (!TryRaycastShelf(out var shelf))
            {
                Log("No shelf hit.");
                return;
            }

            Log("Hit shelf: " + shelf.name);

            if (!shelf.CanAccept(_selectedProduct))
            {
                Log("Shelf cannot accept: " + _selectedProduct.DisplayName + " / " + _selectedProduct.StorageType);
                return;
            }

            if (workMinigame == null || !workMinigame.BeginRestock(shelf, _selectedProduct))
            {
                Log("Refill failed. Inventory count: " + inventory.GetCount(_selectedProduct));
                return;
            }

            Log("Refill success.");
            Cancel();
        }

        private bool TryRaycastShelf(out PlacedShelfStock shelf)
        {
            shelf = null;

            if (worldCamera == null)
                worldCamera = Camera.main;

            if (worldCamera == null)
                return false;

            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            int mask = shelfMask.value == 0 ? ~0 : shelfMask.value;

            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, mask, QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                shelf = hit.collider.GetComponentInParent<PlacedShelfStock>();

                if (shelf != null)
                    return true;
            }

            Log("Ray hit objects, but no PlacedShelfStock found.");
            return false;
        }

        private void CacheShelves()
        {
            _shelves.Clear();
            _shelves.AddRange(FindObjectsOfType<PlacedShelfStock>(true));
        }

        private void RefreshHighlights()
        {
            int available = 0;

            foreach (var shelf in _shelves)
            {
                if (shelf == null)
                    continue;

                bool canAccept = shelf.CanAccept(_selectedProduct);
                SetHighlight(shelf, canAccept);

                if (canAccept)
                    available++;
            }

            Log("Available shelves: " + available);
        }

        private void ClearHighlights()
        {
            foreach (var shelf in _shelves)
                SetHighlight(shelf, false);
        }

        private static void SetHighlight(PlacedShelfStock shelf, bool visible)
        {
            if (shelf == null)
                return;

            var highlight = shelf.GetComponent<ShelfHighlight>();

            if (highlight != null)
                highlight.SetVisible(visible);
        }

        private bool IsPointerBlockedByUI()
        {
            return blockWhenPointerOverUI
                && EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
        }

        private void FindMissingRefs()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;

            if (inventory == null)
                inventory = FindObjectOfType<ProductInventory>(true);
        }

        private void Log(string message)
        {
            if (!debugLogs)
                return;

            Debug.Log("[ProductAssignMode] " + message);
        }
    }
}
