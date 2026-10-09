using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.UI.Products;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.UI.Shop
{
    [MovedFrom(false, "MyShopGame.UI.Shop", null, "ShopWindow")]
    public sealed class ShopWindow : MonoBehaviour
    {
        private enum ViewMode
        {
            MainTabs,
            BuildCategories,
            BuildItems,
            Products,
            Staff,
            Vehicles
        }

        [Header("Build Data")]
        [SerializeField] private List<BuildItemData> buildCatalog = new();

        [Header("Product Data")]
        [SerializeField] private List<ProductItemData> productCatalog = new();

        [Header("Scene Refs")]
        [SerializeField] private MoneyController money;
        [SerializeField] private BuildInventory buildInventory;
        [SerializeField] private ProductInventory productInventory;

        [Header("UI - Main Flow")]
        [SerializeField] private GameObject mainCategoriesPanel;
        [SerializeField] private GameObject mainTabsPanel;
        [SerializeField] private GameObject tabsCategoriesPanel;
        [SerializeField] private GameObject categoryViewPanel;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject emptyLabel;

        [Header("UI - List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private ShopItemCard buildCardPrefab;
        [SerializeField] private ProductShopItemCard productCardPrefab;
        [SerializeField] private VehicleShopCard vehicleCardPrefab;
        [SerializeField] private Sprite pickupIcon;
        [SerializeField] private GameObject buildInventoryWindow;

        [Header("State")]
        [SerializeField] private BuildCategory buildFilter = BuildCategory.Shelf;

        [Header("Camera Lock")]
        [SerializeField] private Behaviour[] cameraBehavioursToDisable;

        private readonly List<(Behaviour behaviour, bool wasEnabled)> _cameraState = new();
        private ViewMode _viewMode = ViewMode.MainTabs;
        private GameObject _staffPage;
        [SerializeField] private RetailEmpireTycoon.Logistics.DeliveryOrders deliveryOrders;
        public Transform StaffHost => mainCategoriesPanel.transform;
        public Transform MainTabsHost => mainTabsPanel.transform;
        public Transform CategoryTabsHost => tabsCategoriesPanel.transform;
        public bool IsStaffView => isActiveAndEnabled && _viewMode == ViewMode.Staff;
        public bool IsMainView => _viewMode == ViewMode.MainTabs;
        public bool IsBuildView => _viewMode == ViewMode.BuildItems || _viewMode == ViewMode.BuildCategories;
        public bool IsProductsView => _viewMode == ViewMode.Products;
        public bool IsVehiclesView => _viewMode == ViewMode.Vehicles;
        public BuildCategory SelectedCategory => buildFilter;
        public event System.Action ViewChanged;
        public event System.Action StaffRequested;

        public void AttachStaffPage(GameObject page) { _staffPage = page; }
        public void OpenStaff()
        {
            ClearList();
            _viewMode = ViewMode.Staff;
            SetPanel(mainCategoriesPanel, true);
            SetPanel(mainTabsPanel, false);
            SetPanel(tabsCategoriesPanel, false);
            SetPanel(categoryViewPanel, false);
            SetEmpty(false);
            StaffRequested?.Invoke();
            SetPanel(_staffPage, true);
            ViewChanged?.Invoke();
        }

        private void Awake()
        {
            FindMissingRefs();
            PrepareCameraLock();
        }

        private void OnEnable()
        {
            FindMissingRefs();
            LockCamera(true);
            HookBackButton();
            ShowMainTabs();

            if (buildInventoryWindow != null)
                buildInventoryWindow.SetActive(false);
        }

        private void OnDisable()
        {
            LockCamera(false);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void ShowMainTabs()
        {
            SetPanel(_staffPage, false);
            _viewMode = ViewMode.MainTabs;

            ClearList();
            SetPanel(mainCategoriesPanel, true);
            SetPanel(mainTabsPanel, true);
            SetPanel(tabsCategoriesPanel, false);
            SetPanel(categoryViewPanel, false);
            SetEmpty(false);
            ViewChanged?.Invoke();
        }

        public void OpenBuildCategories()
        {
            OpenCategory(buildFilter == BuildCategory.Decoration ? BuildCategory.Shelf : buildFilter);
        }

        public void OpenProducts()
        {
            SetPanel(_staffPage, false);
            _viewMode = ViewMode.Products;

            SetPanel(mainCategoriesPanel, true);
            SetPanel(mainTabsPanel, false);
            SetPanel(tabsCategoriesPanel, false);
            SetPanel(categoryViewPanel, true);

            RefreshProducts();
            ViewChanged?.Invoke();
        }

        public void BackToMainTabs()
        {
            ShowMainTabs();
        }

        public void Back()
        {
            ShowMainTabs();
        }

        public void OpenCategory_Shelves()
        {
            OpenCategory(BuildCategory.Shelf);
        }

        public void OpenCategory_Structures()
        {
            OpenCategory(BuildCategory.Structures);
        }

        public void OpenCategory_Decoration()
        {
            OpenCategory(BuildCategory.Decoration);
        }

        public void OpenCategory(BuildCategory category)
        {
            SetPanel(_staffPage, false);
            _viewMode = ViewMode.BuildItems;
            buildFilter = category;

            SetPanel(mainCategoriesPanel, true);
            SetPanel(mainTabsPanel, false);
            SetPanel(tabsCategoriesPanel, true);
            SetPanel(categoryViewPanel, true);

            RefreshBuildItems();
            ViewChanged?.Invoke();
        }

        public void Refresh()
        {
            switch (_viewMode)
            {
                case ViewMode.Vehicles:
                    OpenVehicles();
                    break;
                case ViewMode.Staff:
                    OpenStaff();
                    break;
                case ViewMode.Products:
                    RefreshProducts();
                    break;

                case ViewMode.BuildItems:
                    RefreshBuildItems();
                    break;

                case ViewMode.BuildCategories:
                    OpenBuildCategories();
                    break;

                default:
                    ShowMainTabs();
                    break;
            }
        }

        public void OpenVehicles()
        {
            SetPanel(_staffPage, false);
            _viewMode = ViewMode.Vehicles;
            ClearList();
            SetPanel(mainCategoriesPanel, true);
            SetPanel(mainTabsPanel, false);
            SetPanel(tabsCategoriesPanel, false);
            SetPanel(categoryViewPanel, true);
            SetEmpty(vehicleCardPrefab == null);
            if (vehicleCardPrefab != null) Instantiate(vehicleCardPrefab, listRoot).Bind(pickupIcon, deliveryOrders);
            ResetCatalogScroll();
            ViewChanged?.Invoke();
        }

        private void RefreshBuildItems()
        {
            ClearList();

            if (listRoot == null || buildCardPrefab == null)
            {
                SetEmpty(true);
                Debug.LogWarning("[ShopWindow] Build list root or build card prefab is missing.");
                return;
            }

            int shown = 0;

            // Order the visible cards only; stable catalog entries and save identifiers remain unchanged.
            foreach (var item in buildCatalog.OrderBy(BuildDisplayPriority))
            {
                if (item == null || item.hiddenFromShop || item.category != buildFilter)
                    continue;

                var card = Instantiate(buildCardPrefab, listRoot);
                card.Bind(item, money, buildInventory, deliveryOrders);
                shown++;
            }

            SetEmpty(shown == 0);
            ResetCatalogScroll();
        }

        private static int BuildDisplayPriority(BuildItemData item)
        {
            if (item == null || item.category != BuildCategory.Shelf) return 2;
            if (item.id == "shelf_produce_01") return 0;
            if (item.id == "shelf_fresh_01") return 1;
            return 2;
        }

        private void RefreshProducts()
        {
            ClearList();

            if (listRoot == null || productCardPrefab == null)
            {
                SetEmpty(true);
                Debug.LogWarning("[ShopWindow] Product list root or product card prefab is missing.");
                return;
            }

            if (productCatalog.Count == 0)
                Debug.LogWarning("[ShopWindow] Product catalog is empty. Add ProductItemData assets to Product Data.");

            int shown = 0;

            foreach (var product in productCatalog.Where(p => p != null).OrderBy(p => p.StorageType).ThenBy(ShopText.Item))
            {
                if (product == null)
                    continue;

                var card = Instantiate(productCardPrefab, listRoot);
                card.Bind(product, money, productInventory, deliveryOrders);
                shown++;
            }

            SetEmpty(shown == 0);
            ResetCatalogScroll();
        }

        private void ResetCatalogScroll()
        {
            var scroll = listRoot != null ? listRoot.GetComponentInParent<ScrollRect>() : null;
            if (scroll == null) return;
            scroll.StopMovement();
            // Reset position directly: newly created cards have not been laid out yet.
            ((RectTransform)listRoot).anchoredPosition = Vector2.zero;
        }

        private void ClearList()
        {
            if (listRoot == null)
                return;

            for (int i = listRoot.childCount - 1; i >= 0; i--)
            {
                var card = listRoot.GetChild(i).gameObject;
                card.SetActive(false);
                Destroy(card);
            }
        }

        private void FindMissingRefs()
        {
            if (money == null)
                money = FindObjectOfType<MoneyController>(true);

            if (buildInventory == null)
                buildInventory = FindObjectOfType<BuildInventory>(true);

            if (productInventory == null)
                productInventory = FindObjectOfType<ProductInventory>(true);
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

        private void HookBackButton()
        {
            if (backButton == null)
                return;

            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(BackToMainTabs);
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

        private static void SetPanel(GameObject panel, bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        private void SetEmpty(bool visible)
        {
            if (emptyLabel != null)
                emptyLabel.SetActive(visible);
        }
    }
}
