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
        public BuildEditorHud construction;
        private RectTransform _root;
        private RectTransform _actionRow;
        private TMP_Text _balance;
        private void Start()
        {
            var ui = new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            controls.ConfigurePause(ui, canvas.transform);
            var operations = GetComponent<ShopOperationsHud>();
            _root = ui.Rect("Game edge HUD", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _root.anchorMax = Vector2.one;
            var badge = ui.Rect("Wallet", _root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(250, 52));
            ui.Panel(badge, ShopUiTheme.Paper); ui.Border(badge);
            ui.Icon(badge, ShopIcon.Coin, new Vector2(-96, 0), new Vector2(42, 42));
            _balance = ui.Heading(badge, "", "", new Vector2(58, -10), new Vector2(182, 34), 26);
            Destroy(_balance.GetComponent<ShopLocalizedLabel>());
            money.Changed += OnMoney; OnMoney(money.Money);
            var bottomRight = new Vector2(1, 0);
            _actionRow = ui.Rect("Shop action row", _root, bottomRight, bottomRight, new Vector2(-16, 16), Vector2.zero);
            float rightOffset = 0;
            Action("Open warehouse", ShopIcon.Warehouse, controls.RequestWarehouse);
            Action("Open store catalog", ShopIcon.Cart, controls.RequestShop);
            construction.Launcher = Action("Open construction", ShopIcon.Hammer, controls.RequestConstruction);
            rightOffset += 7;
            Place(operations.CreateStoreStatusButton(ui, _actionRow));
            rightOffset += 7;
            Action("Open store statistics", ShopIcon.Statistics, () => operations.OpenStatistics(false));
            Action("Open territories", ShopIcon.Territory, controls.RequestTerritory);
            _actionRow.sizeDelta = new Vector2(rightOffset - 9, 48);

            Button Action(string name, ShopIcon icon, System.Action onClick)
            {
                var button = ui.IconButton(_actionRow, icon, Vector2.zero, 48, onClick);
                button.name = name;
                Place(button);
                return button;
            }

            void Place(Button button)
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = bottomRight;
                rect.anchoredPosition = new Vector2(-rightOffset, 0);
                rightOffset += rect.sizeDelta.x + 9;
            }
        }
        private void LateUpdate()
        {
            if (_root == null) return;
            _root.gameObject.SetActive(!work.IsActive &&
                (!controls.IsConstructionActive || territory.IsActive || construction != null && construction.IsOpen));
            _actionRow.gameObject.SetActive(controls.CanOpenWindow);
        }
        private void OnMoney(int amount) { if (_balance != null) _balance.text = MoneyFormat.Compact(amount); }
        private void OnDestroy() { if (money != null) money.Changed -= OnMoney; }
    }
}
