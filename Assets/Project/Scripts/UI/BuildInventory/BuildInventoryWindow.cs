using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.UI.Products;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.UI.Windows
{
    [MovedFrom(false, "MyShopGame.UI.Windows", null, "BuildInventoryWindow")]
    public sealed class BuildInventoryWindow : MonoBehaviour
    {
        private enum InventoryTab
        {
            Furniture,
            Products
        }

        [Header("Build Refs")]
        [SerializeField] private BuildInventory inventory;
        [SerializeField] private BuildController buildController;

        [Header("Product Refs")]
        [SerializeField] private ProductInventory productInventory;
        [SerializeField] private ProductAssignMode productAssignMode;

        [Header("Windows")]
        [SerializeField] private GameObject shopWindow;

        [Header("UI")]
        [SerializeField] private GameObject mainCategoriesPanel;
        [SerializeField] private GameObject categoryViewPanel;
        [SerializeField] private GameObject emptyLabel;

        [Header("UI - List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private BuildInventoryItemRow rowPrefab;
        [SerializeField] private ProductInventoryItemRow productRowPrefab;

        [Header("Camera Lock (optional)")]
        [SerializeField] private Behaviour[] cameraBehavioursToDisable;

        private readonly List<GameObject> _spawnedRows = new();
        private readonly List<(Behaviour behaviour, bool wasEnabled)> _cameraState = new();

        [SerializeField] private InventoryTab currentTab = InventoryTab.Furniture;
        public bool IsProductsView => currentTab == InventoryTab.Products;
        public event System.Action ViewChanged;
        public void Close() { gameObject.SetActive(false); }

        private void Awake()
        {
            FindMissingRefs();
            PrepareCameraLock();
        }

        private void OnEnable()
        {
            CloseShopWindow();
            Subscribe();
            ShowPanels();
            LockCamera(true);
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            LockCamera(false);
        }

        public void ShowFurniture()
        {
            currentTab = InventoryTab.Furniture;
            Refresh();
            ResetScroll();
        }

        public void ShowProducts()
        {
            currentTab = InventoryTab.Products;
            Refresh();
            ResetScroll();
        }

        private void ResetScroll()
        {
            var scroll = listRoot != null ? listRoot.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (scroll == null) return;
            scroll.StopMovement();
            ((RectTransform)listRoot).anchoredPosition = Vector2.zero;
        }

        public void Refresh()
        {
            Clear();

            switch (currentTab)
            {
                case InventoryTab.Products:
                    RefreshProducts();
                    ViewChanged?.Invoke();
                    return;

                default:
                    RefreshFurniture();
                    ViewChanged?.Invoke();
                    return;
            }
        }

        private void RefreshFurniture()
        {
            if (inventory == null || rowPrefab == null || listRoot == null)
            {
                SetEmpty(true);
                return;
            }

            int shown = 0;

            foreach (var entry in inventory.Entries)
            {
                if (!ShouldShowFurniture(entry))
                    continue;

                var row = Instantiate(rowPrefab, listRoot);

                row.Bind(
                    entry.item,
                    entry.count,
                    buildController,
                    onPlace: () => gameObject.SetActive(false)
                );

                _spawnedRows.Add(row.gameObject);
                shown++;
            }

            SetEmpty(shown == 0);
        }

        private void RefreshProducts()
        {
            if (productInventory == null || productRowPrefab == null || listRoot == null)
            {
                SetEmpty(true);
                return;
            }

            int shown = 0;

            foreach (var entry in productInventory.Entries)
            {
                if (entry == null || entry.Item == null || entry.Count <= 0)
                    continue;

                var row = Instantiate(productRowPrefab, listRoot);

                row.Bind(
                    entry.Item,
                    entry.Count,
                    productAssignMode,
                    onSelected: () => gameObject.SetActive(false)
                );

                _spawnedRows.Add(row.gameObject);
                shown++;
            }

            SetEmpty(shown == 0);
        }

        private bool ShouldShowFurniture(BuildInventory.Entry entry)
        {
            if (entry == null || entry.item == null || entry.count <= 0)
                return false;

            return IsFurnitureItem(entry.item);
        }

        private bool IsFurnitureItem(BuildItemData item)
        {
            if (item == null)
                return false;

            return item.category == BuildCategory.Shelf
                || item.category == BuildCategory.Structures
                || item.category == BuildCategory.Decoration;
        }

        private void Clear()
        {
            foreach (var row in _spawnedRows)
            {
                if (row != null)
                {
                    row.SetActive(false);
                    Destroy(row);
                }
            }

            _spawnedRows.Clear();

            if (listRoot == null)
                return;

            for (int i = listRoot.childCount - 1; i >= 0; i--)
            {
                listRoot.GetChild(i).gameObject.SetActive(false);
                Destroy(listRoot.GetChild(i).gameObject);
            }
        }

        // =========================
        // Setup
        // =========================

        private void FindMissingRefs()
        {
            if (inventory == null)
                inventory = FindObjectOfType<BuildInventory>(true);

            if (buildController == null)
                buildController = FindObjectOfType<BuildController>(true);

            if (productInventory == null)
                productInventory = FindObjectOfType<ProductInventory>(true);

            if (productAssignMode == null)
                productAssignMode = FindObjectOfType<ProductAssignMode>(true);
        }

        private void PrepareCameraLock()
        {
            if (cameraBehavioursToDisable != null && cameraBehavioursToDisable.Length > 0)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var mainCamera = cam.GetComponent<global::MainCamera>();
            if (mainCamera == null)
                return;

            cameraBehavioursToDisable = new Behaviour[] { mainCamera };
        }

        private void Subscribe()
        {
            if (inventory != null)
                inventory.Changed += Refresh;

            if (productInventory != null)
                productInventory.Changed += Refresh;
        }

        private void Unsubscribe()
        {
            if (inventory != null)
                inventory.Changed -= Refresh;

            if (productInventory != null)
                productInventory.Changed -= Refresh;
        }

        private void CloseShopWindow()
        {
            if (shopWindow != null)
                shopWindow.SetActive(false);
        }

        private void ShowPanels()
        {
            if (mainCategoriesPanel != null)
                mainCategoriesPanel.SetActive(true);

            if (categoryViewPanel != null)
                categoryViewPanel.SetActive(true);
        }

        private void SetEmpty(bool visible)
        {
            if (emptyLabel != null)
                emptyLabel.SetActive(visible);
        }

        private void LockCamera(bool locked)
        {
            if (locked)
            {
                SaveAndDisableCameraBehaviours();
                return;
            }

            RestoreCameraBehaviours();
        }

        private void SaveAndDisableCameraBehaviours()
        {
            _cameraState.Clear();

            if (cameraBehavioursToDisable == null)
                return;

            foreach (var behaviour in cameraBehavioursToDisable)
            {
                if (behaviour == null)
                    continue;

                if (behaviour is Camera || behaviour is AudioListener)
                    continue;

                _cameraState.Add((behaviour, behaviour.enabled));
                behaviour.enabled = false;
            }
        }

        private void RestoreCameraBehaviours()
        {
            foreach (var state in _cameraState)
            {
                if (state.behaviour == null)
                    continue;

                state.behaviour.enabled = state.wasEnabled;
            }

            _cameraState.Clear();
        }
    }
}
