using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.UI.Shop;

namespace RetailEmpireTycoon.UI.Products
{
    public sealed class ProductShopItemCard : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text boxAmountText;
        [SerializeField] private TMP_Text ownedAmountText;
        [SerializeField] private TMP_Text shelfHintText;
        [SerializeField] private Button buyButton;

        private ProductItemData _item;
        private MoneyController _money;
        private ProductInventory _inventory;

        public void Bind(ProductItemData item, MoneyController money, ProductInventory inventory)
        {
            Unsubscribe();

            _item = item;
            _money = money;
            _inventory = inventory;

            Subscribe();
            HookButton();
            RefreshView();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_money != null)
                _money.Changed += HandleMoneyChanged;

            if (_inventory != null)
                _inventory.Changed += RefreshView;
        }

        private void Unsubscribe()
        {
            if (_money != null)
                _money.Changed -= HandleMoneyChanged;

            if (_inventory != null)
                _inventory.Changed -= RefreshView;
        }

        private void HookButton()
        {
            if (buyButton == null)
                return;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(Buy);
        }

        private void HandleMoneyChanged(int value)
        {
            RefreshButtonState();
        }

        private void RefreshView()
        {
            RefreshTexts();
            RefreshIcon();
            RefreshButtonState();
        }

        private void RefreshTexts()
        {
            if (shelfHintText != null) shelfHintText.text = ShopText.ShelfHint(_item);
            if (nameText != null)
                nameText.text = ShopText.Item(_item);

            if (priceText != null)
                priceText.text = _item != null ? MoneyFormat.Compact(_item.BuyPrice) : "$0";

            if (boxAmountText != null)
                boxAmountText.text = ShopText.Get("В упаковке: ", "Pack: ") + (_item != null ? _item.BoxAmount : 0);

            if (ownedAmountText != null)
                ownedAmountText.text = GetOwnedText();
        }

        private string GetOwnedText()
        {
            if (_item == null || _inventory == null)
                return ShopText.Get("На складе: 0", "Owned: 0");

            return ShopText.Get("На складе: ", "Owned: ") + _inventory.GetCount(_item);
        }

        private void RefreshIcon()
        {
            if (icon == null)
                return;

            icon.sprite = _item != null ? _item.Icon : null;
            icon.enabled = icon.sprite != null;
        }

        private void RefreshButtonState()
        {
            if (buyButton == null)
                return;

            buyButton.interactable = CanBuy();
        }

        private bool CanBuy()
        {
            return _item != null
                && _money != null
                && _inventory != null
                && _money.CanSpend(_item.BuyPrice);
        }

        private void Buy()
        {
            if (!CanBuy())
                return;

            if (!_money.TrySpend(_item.BuyPrice))
                return;

            _inventory.Add(_item, _item.BoxAmount);
            RefreshView();
        }
    }
}
