using System;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.UI.HUD;
using RetailEmpireTycoon.UI.Windows;
using RetailEmpireTycoon.UI.Shop;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Shelves;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.StoreOperations
{
    public enum ShopAction { Inventory, Warehouse, Catalog, Territory, Staff, OpenStore, Checkout, TopView, ResetView, RotateClockwise, RotateCounterclockwise, Pause }

    [DefaultExecutionOrder(-110)]
    public sealed class GameplayControls : MonoBehaviour
    {
        [SerializeField] private HUDController hud;
        [SerializeField] private BuildInventoryWindow inventoryWindow;
        [SerializeField] private TerritoryPurchaseModeManager territoryMode;
        [SerializeField] private ControlPanel settings;
        [SerializeField] private PauseManager pause;
        [SerializeField] private MainCamera cameraInput;
        [SerializeField] private BuildController building;
        [SerializeField] private WorkMinigame work;
        [SerializeField] private StoreOperations shop;
        [SerializeField] private ShopOperationsHud operationsHud;
        [SerializeField] private BuildEditorHud construction;
        private readonly KeyCode[] _keys = { KeyCode.E, KeyCode.U, KeyCode.B, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.F, KeyCode.V, KeyCode.Home, KeyCode.R, KeyCode.Q, KeyCode.Space };
        private readonly KeyCode[] _defaults = { KeyCode.E, KeyCode.U, KeyCode.B, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.F, KeyCode.V, KeyCode.Home, KeyCode.R, KeyCode.Q, KeyCode.Space };
        public ShopAction? Capturing { get; private set; }
        private ShopPauseMenu _pauseMenu;
        private Action _auxiliaryClose;
        private StoreTownMapHud _townMap;
        private ShelfInfoWindow _shelfInfo;
        private ProductAssignMode _productAssignment;
        public string BindingNotice { get; private set; }
        public bool WorldMapOpen { get; private set; }
        public void SetWorldMapOpen(bool open) { WorldMapOpen=open; }
        private bool SettingsVisible => settings != null && settings.settingPanel != null && settings.settingPanel.activeInHierarchy;
        public bool PauseMenuVisible => _pauseMenu?.IsOpen == true;
        internal bool PrimaryWindowVisible => WorldMapOpen || hud.shopWindow.activeInHierarchy || hud.buildInventoryWindow.activeInHierarchy
            || SettingsVisible || PauseMenuVisible || _auxiliaryClose != null || operationsHud != null && operationsHud.ModalVisible;
        public bool LegacyWindowVisible => PrimaryWindowVisible || _shelfInfo != null && _shelfInfo.gameObject.activeInHierarchy;
        public bool CanOpenWindow => !LegacyWindowVisible && !IsConstructionActive && !work.IsActive && _productAssignment?.IsActive != true;
        internal bool CanShowControls => SettingsVisible || CanOpenWindow;
        public bool IsConstructionActive => building.mode != BuildMode.Normal || territoryMode.IsActive;
        public bool IsFloorNavigationActive => building.mode != BuildMode.Normal;
        public event Action Changed;
        public KeyCode Key(ShopAction action) => _keys[(int)action];
        public bool Pressed(ShopAction action) => !Capturing.HasValue && Input.GetKeyDown(Key(action));
        public static string Name(ShopAction action)
        {
            switch (action)
            {
                case ShopAction.Inventory: return "Construction";
                case ShopAction.Warehouse: return ShopText.Get("Склад товаров", "Warehouse");
                case ShopAction.Catalog: return ShopText.Get("Закупки", "Store catalog");
                case ShopAction.Territory: return ShopText.Get("Участки", "Territories");
                case ShopAction.Staff: return ShopText.Get("Персонал", "Staff");
                case ShopAction.OpenStore: return ShopText.Get("Открыть / закрыть", "Open / close store");
                case ShopAction.Checkout: return ShopText.Get("Работа на кассе", "Checkout");
                case ShopAction.TopView: return ShopText.Get("Вид сверху", "Top view");
                case ShopAction.ResetView: return ShopText.Get("Вернуть камеру", "Reset camera");
                case ShopAction.RotateClockwise: return ShopText.Get("Повернуть вправо", "Rotate right");
                case ShopAction.RotateCounterclockwise: return ShopText.Get("Повернуть влево", "Rotate left");
                default: return ShopText.Get("Пауза", "Pause");
            }
        }
        private void Awake()
        {
            _shelfInfo = FindObjectOfType<ShelfInfoWindow>(true);
            _productAssignment = FindObjectOfType<ProductAssignMode>(true);
            for (int i = 0; i < _keys.Length; i++)
            {
                var saved = (KeyCode)PlayerPrefs.GetInt("RetailEmpire.Control." + (ShopAction)i, (int)_defaults[i]);
                if (Allowed(saved)) _keys[i] = saved;
            }
        }
        private void Update()
        {
            _pauseMenu?.Tick();
            if (Input.GetKeyDown(KeyCode.Escape)) { HandleEscape(); return; }
            if (Capturing.HasValue)
            {
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) if (Input.GetKeyDown(key)) { Rebind(Capturing.Value, key); return; }
                return;
            }
            if (PauseMenuVisible)
            {
                if (Pressed(ShopAction.Pause) && !SettingsVisible && !operationsHud.ControlsVisible) HandleEscape();
                return;
            }
            if (LegacyWindowVisible || work.IsActive || _productAssignment?.IsActive == true) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
            if (Pressed(ShopAction.Territory)) { RequestTerritory(); return; }
            if (Pressed(ShopAction.Inventory)) { RequestConstruction(); return; }
            if (IsConstructionActive) return;
            if (Pressed(ShopAction.Pause)) { RequestPause(); return; }
            if (Pressed(ShopAction.TopView) && cameraInput.enabled) cameraInput.ToggleTopView();
            if (Pressed(ShopAction.ResetView) && cameraInput.enabled) cameraInput.ResetView();
            if (Pressed(ShopAction.Warehouse)) { RequestWarehouse(); return; }
            if (Pressed(ShopAction.Catalog)) { RequestShop(); return; }
            if (Pressed(ShopAction.Staff)) { operationsHud.ToggleStaff(); return; }
            if (Pressed(ShopAction.OpenStore)) shop.SetOpen(!shop.IsOpen);
            else if (Pressed(ShopAction.Checkout)) work.BeginCheckout();
        }

        public void HandleEscape()
        {
            if (Capturing.HasValue) CancelCapture();
            else if (WorldMapOpen) _townMap?.Close();
            else if (construction?.IsOpen == true && building.mode == BuildMode.Edit &&
                building.GetComponent<BuildEditingController>()?.Selected == null) construction.Close();
            else if (IsConstructionActive || work.IsActive) return; // Active placement, territory and work own their cancellation.
            else if (_productAssignment?.IsActive == true) _productAssignment.Cancel();
            else if (_auxiliaryClose != null) _auxiliaryClose();
            else if (operationsHud.ControlsVisible) operationsHud.CloseControls();
            else if (PauseMenuVisible) _pauseMenu.Escape();
            else if (SettingsVisible) settings.ExitSetting();
            else if (operationsHud.CloseStatistics()) return;
            else if (hud.shopWindow.activeInHierarchy) hud.shopWindow.SetActive(false);
            else if (hud.buildInventoryWindow.activeInHierarchy) hud.buildInventoryWindow.SetActive(false);
            else if (_shelfInfo != null && _shelfInfo.gameObject.activeInHierarchy) _shelfInfo.Hide();
            else RequestPause();
        }

        internal void AttachTownMap(StoreTownMapHud map) => _townMap = map;
        internal void ConfigurePause(ShopUi ui, Transform canvas) => _pauseMenu = new ShopPauseMenu(ui, canvas, settings, cameraInput);
        internal void SuspendSettingsForBindings() => _pauseMenu?.SuspendSettingsForBindings();
        internal void ResumeSettingsFromBindings() => _pauseMenu?.ResumeSettingsFromBindings();
        public void RequestPause() { if (CanOpenWindow) _pauseMenu?.Open(); }
        public void RequestShop() { if (CanOpenWindow) hud.shopWindow.SetActive(true); }
        public void RequestWarehouse()
        {
            if (!CanOpenWindow) return;
            inventoryWindow.gameObject.SetActive(true);
            inventoryWindow.ShowProducts();
        }
        public void RequestConstruction()
        {
            if (construction?.IsOpen == true) construction.Close();
            else if (CanOpenWindow) construction?.Open();
        }
        public void RequestTerritory()
        {
            if (territoryMode.IsActive) territoryMode.Exit();
            else if (CanOpenWindow) territoryMode.Enter();
        }
        internal bool TryOpenAuxiliary(Action close)
        {
            if (!CanOpenWindow) return false;
            _auxiliaryClose = close;
            return true;
        }
        internal void EndAuxiliary(Action close)
        {
            if (_auxiliaryClose == close) _auxiliaryClose = null;
        }

        public void CloseWindows(bool includeConstruction=true)
        {
            _auxiliaryClose?.Invoke();
            if(includeConstruction)construction?.Close();
            operationsHud.CloseControls();
            _pauseMenu?.Close();
            if (SettingsVisible) settings.ExitSetting();
            _shelfInfo?.Hide();
            operationsHud.CloseStatistics();
            hud.shopWindow.SetActive(false); hud.buildInventoryWindow.SetActive(false); operationsHud.CloseStaff();
        }
        private void OnDisable()
        {
            if (!PauseMenuVisible) return;
            operationsHud.CloseControls();
            _pauseMenu.Close();
        }
        public void Capture(ShopAction action) { Capturing = action; BindingNotice = ShopText.Get("Нажми новую клавишу. Esc — отмена.", "Press a new key. Esc cancels."); Changed?.Invoke(); }
        public bool Rebind(ShopAction action, KeyCode key, bool persist = true)
        {
            if (!Allowed(key)) { BindingNotice = ShopText.Get("WASD, стрелки и Esc зарезервированы. Выбери другую клавишу.", "WASD, arrow keys and Esc are reserved. Choose another key."); Changed?.Invoke(); return false; }
            int current = (int)action; KeyCode previous = _keys[current];
            for (int i = 0; i < _keys.Length; i++) if (i != current && _keys[i] == key) _keys[i] = previous;
            _keys[current] = key; Capturing = null; BindingNotice = ShopText.Get("Готово. Совпадающие клавиши поменяны местами.", "Done. Conflicting keys have been swapped.");
            if (persist) SaveKeys(); Changed?.Invoke(); return true;
        }
        public void ResetBindings() { Array.Copy(_defaults, _keys, _keys.Length); Capturing = null; BindingNotice = ShopText.Get("Стандартные клавиши восстановлены.", "Default controls restored."); SaveKeys(); Changed?.Invoke(); }
        private void SaveKeys() { for (int i = 0; i < _keys.Length; i++) PlayerPrefs.SetInt("RetailEmpire.Control." + (ShopAction)i, (int)_keys[i]); PlayerPrefs.Save(); }
        public void CancelCapture() { Capturing = null; BindingNotice = null; Changed?.Invoke(); }
        private static bool Allowed(KeyCode key) => Enum.IsDefined(typeof(KeyCode), key) && key != KeyCode.None && key != KeyCode.Escape
            && key != KeyCode.W && key != KeyCode.A && key != KeyCode.S && key != KeyCode.D
            && key != KeyCode.UpArrow && key != KeyCode.DownArrow && key != KeyCode.LeftArrow && key != KeyCode.RightArrow
            && key != KeyCode.LeftShift && key != KeyCode.RightShift && (int)key < (int)KeyCode.Mouse0;
    }
}
