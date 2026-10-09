using System;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.UI.HUD;
using RetailEmpireTycoon.UI.Windows;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.StoreOperations
{
    public enum ShopAction { Inventory, Warehouse, Catalog, Territory, Staff, OpenStore, Checkout, TopView, ResetView, RotateClockwise, RotateCounterclockwise, Pause }

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
        private readonly KeyCode[] _keys = { KeyCode.E, KeyCode.U, KeyCode.B, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.F, KeyCode.V, KeyCode.Home, KeyCode.R, KeyCode.Q, KeyCode.Space };
        private readonly KeyCode[] _defaults = { KeyCode.E, KeyCode.U, KeyCode.B, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.F, KeyCode.V, KeyCode.Home, KeyCode.R, KeyCode.Q, KeyCode.Space };
        public ShopAction? Capturing { get; private set; }
        private int _captureCancelFrame = -1;
        public bool CaptureCancelledThisFrame => _captureCancelFrame == Time.frameCount;
        public string BindingNotice { get; private set; }
        public bool LegacyWindowVisible => hud.shopWindow.activeInHierarchy || hud.buildInventoryWindow.activeInHierarchy
            || settings != null && settings.settingPanel.activeInHierarchy;
        public bool IsConstructionActive => building.mode != BuildMode.Normal || territoryMode.IsActive;
        public event Action Changed;
        public KeyCode Key(ShopAction action) => _keys[(int)action];
        public bool Pressed(ShopAction action) => !Capturing.HasValue && Input.GetKeyDown(Key(action));
        public static string Name(ShopAction action)
        {
            switch (action)
            {
                case ShopAction.Inventory: return ShopText.Get("Инвентарь", "Inventory");
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
            for (int i = 0; i < _keys.Length; i++)
            {
                var saved = (KeyCode)PlayerPrefs.GetInt("RetailEmpire.Control." + (ShopAction)i, (int)_defaults[i]);
                if (Allowed(saved)) _keys[i] = saved;
            }
        }
        private void Update()
        {
            if (Capturing.HasValue)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { _captureCancelFrame = Time.frameCount; CancelCapture(); return; }
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) if (Input.GetKeyDown(key)) { Rebind(Capturing.Value, key); return; }
                return;
            }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
            if (work.IsActive) return;
            if (operationsHud.ControlsVisible || settings != null && settings.settingPanel != null && settings.settingPanel.activeInHierarchy) return;
            if (operationsHud.StaffVisible)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Pressed(ShopAction.Staff)) operationsHud.CloseStaff();
                return;
            }
            if (Pressed(ShopAction.TopView) && cameraInput.enabled) cameraInput.ToggleTopView();
            if (Pressed(ShopAction.ResetView) && cameraInput.enabled) cameraInput.ResetView();
            if (Pressed(ShopAction.Pause)) pause?.TogglePause();
            if (building.mode != BuildMode.Normal)
            {
                if (Pressed(ShopAction.Inventory) || Pressed(ShopAction.Warehouse))
                {
                    building.GetComponent<BuildEditingController>()?.Finish();
                    building.ExitBuildMode();
                }
                else return;
            }
            if (Pressed(ShopAction.Territory)) { bool wasActive = territoryMode.IsActive; CloseWindows(); if (wasActive) territoryMode.Exit(); else territoryMode.Enter(); return; }
            if (territoryMode.IsActive) return;
            if (Pressed(ShopAction.Inventory)) ToggleInventory(false);
            if (Pressed(ShopAction.Warehouse)) ToggleInventory(true);
            if (Pressed(ShopAction.Catalog)) { hud.buildInventoryWindow.SetActive(false); hud.ToggleShop(); }
            if (Pressed(ShopAction.Staff)) operationsHud.ToggleStaff();
            if (Pressed(ShopAction.OpenStore)) shop.SetOpen(!shop.IsOpen);
            if (Pressed(ShopAction.Checkout)) { CloseWindows(); work.BeginCheckout(); }
        }
        private void ToggleInventory(bool products)
        {
            bool visible = inventoryWindow.gameObject.activeSelf;
            CloseWindows(); inventoryWindow.gameObject.SetActive(!visible);
            if (products) inventoryWindow.ShowProducts(); else inventoryWindow.ShowFurniture();
        }
        public void CloseWindows()
        {
            hud.shopWindow.SetActive(false); hud.buildInventoryWindow.SetActive(false); settings?.ExitSetting(); operationsHud.CloseStaff();
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
