using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.HUD;
using RetailEmpireTycoon.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Edge-only gameplay controls. Detailed menus keep their original window controllers.</summary>
    public sealed class ShopHudPresenter : MonoBehaviour
    {
        public Canvas canvas;
        public MoneyController money;
        public GameplayControls controls;
        public HUDController windows;
        public BuildInventoryWindow inventory;
        public TerritoryPurchaseModeManager territory;
        public ControlPanel settings;
        public WorkMinigame work;
        public PauseManager pause;
        private RectTransform _root;
        private TMP_Text _balance;
        private Button _territoryBack;
        private void Start()
        {
            var ui = new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            _root = ui.Rect("Game edge HUD", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _root.anchorMax = Vector2.one;
            var badge = ui.Rect("Wallet", _root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(250, 52));
            ui.Panel(badge, ShopUiTheme.Paper); ui.Border(badge);
            ui.Icon(badge, ShopIcon.Coin, new Vector2(-96, 0), new Vector2(42, 42));
            _balance = ui.Heading(badge, "", "", new Vector2(58, -10), new Vector2(182, 34), 26);
            Destroy(_balance.GetComponent<ShopLocalizedLabel>());
            money.Changed += OnMoney; OnMoney(money.Money);
            Edge(ui.IconButton(_root, ShopIcon.Settings, Vector2.zero, 48, OpenSettings), new Vector2(1, 1), new Vector2(-16, -16));
            Edge(ui.IconButton(_root, ShopIcon.Territory, Vector2.zero, 48, ToggleTerritory), new Vector2(1, 1), new Vector2(-73, -16));
            Edge(ui.IconButton(_root, ShopIcon.Warehouse, Vector2.zero, 48, OpenInventory), new Vector2(1, 0), new Vector2(-16, 16));
            Edge(ui.IconButton(_root, ShopIcon.Cart, Vector2.zero, 48, OpenShop), new Vector2(1, 0), new Vector2(-73, 16));
            _territoryBack = ui.Button(_root, "К магазину", "Back to store", Vector2.zero, new Vector2(190, 40), territory.Exit);
            Edge(_territoryBack, new Vector2(.5f, 1), new Vector2(0, -62));
            ui.Icon(_territoryBack.transform, ShopIcon.Back, new Vector2(-76, 0), new Vector2(20, 20));
            _territoryBack.gameObject.name = "Return from territories";
            _territoryBack.gameObject.SetActive(false);
        }
        private static void Edge(Button button, Vector2 anchor, Vector2 position)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position;
        }
        private void Update()
        {
            if (_root == null) return;
            // Territory selection needs the return button even though it locks building controls.
            _root.gameObject.SetActive(!work.IsActive && (!controls.IsConstructionActive || territory.IsActive));
            _territoryBack.gameObject.SetActive(territory.IsActive);
        }
        private void OnMoney(int amount) { if (_balance != null) _balance.text = MoneyFormat.Compact(amount); }
        private void OpenShop() { bool visible = windows.shopWindow.activeSelf; controls.CloseWindows(); windows.shopWindow.SetActive(!visible); }
        private void OpenInventory() { bool visible = inventory.gameObject.activeSelf; controls.CloseWindows(); inventory.gameObject.SetActive(!visible); if (!visible) inventory.ShowFurniture(); }
        private void OpenSettings() { bool visible = settings.settingPanel.activeSelf; controls.CloseWindows(); if (!visible) settings.OpenSetting(); }
        private void ToggleTerritory() { controls.CloseWindows(); if (territory.IsActive) territory.Exit(); else territory.Enter(); }
        private void OnDestroy() { if (money != null) money.Changed -= OnMoney; }
    }
}
