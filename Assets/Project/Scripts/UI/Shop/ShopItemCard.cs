using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Core;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.UI.Shop
{
    [MovedFrom(false, "MyShopGame.UI.Shop", null, "ShopItemCard")]
    public sealed class ShopItemCard : MonoBehaviour
    {
        [Header("UI")]
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text descText;
        public TMP_Text priceText;
        public TMP_Text sizeText;
        public Button buyButton;

        [Header("Refs")]
        public MoneyController money;
        public BuildInventory inventory;
        private BuildItemData _item;

        public void Bind(BuildItemData item, MoneyController moneyController, BuildInventory buildInventory)
        {
            if (money != null) money.Changed -= OnMoneyChanged;
            money = moneyController;
            inventory = buildInventory;
            if (money != null) money.Changed += OnMoneyChanged;
            Bind(item);
        }
        public void Bind(BuildItemData item)
        {
            _item = item;

            ApplyTexts();
            ApplyIcon();
            HookButton();
        }

        private void ApplyTexts()
        {
            if (_item == null) return;

            if (nameText != null)
                nameText.text = ShopText.Item(_item);

            if (descText != null)
                descText.text = _item.description;

            if (priceText != null)
                priceText.text = MoneyFormat.Compact(_item.price);

            if (sizeText != null)
            {
                var f = _item.footprint;
                sizeText.text = ShopText.Get("Размер", "Size") + $"\n{f.x} × {f.y}";
            }
        }

        private void ApplyIcon()
        {
            if (icon == null) return;

            icon.sprite = _item != null ? _item.icon : null;
            icon.enabled = icon.sprite != null;
        }

        private void HookButton()
        {
            if (buyButton == null) return;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);

            RefreshButton();
        }

        private void OnMoneyChanged(int amount) { RefreshButton(); }
        private void RefreshButton()
        {
            if (buyButton != null) buyButton.interactable = _item != null && money != null && inventory != null && money.CanSpend(_item.price);
        }
        private void OnDisable() { if (money != null) money.Changed -= OnMoneyChanged; }
        private void OnEnable() { if (money != null) { money.Changed -= OnMoneyChanged; money.Changed += OnMoneyChanged; } RefreshButton(); }

        private void OnBuyClicked()
        {
            if (_item == null || money == null || inventory == null) return;
            if (!money.TrySpend(_item.price)) return;

            inventory.Add(_item, 1);
        }
    }
}
