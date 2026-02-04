using UnityEngine;
using UnityEngine.UI;
using MyShopGame.BuildSystem;
using MyShopGame.Economy;
using MyShopGame.Core;

namespace MyShopGame.UI.Shop
{
    public sealed class ShopItemCard : MonoBehaviour
    {
        [Header("UI")]
        public Image icon;
        public Text nameText;
        public Text descText;
        public Text priceText;
        public Text sizeText;
        public Button buyButton;

        [Header("Refs")]
        public MoneyController money;
        public BuildInventory inventory;

        private BuildItemData _item;

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

            if (nameText != null) nameText.text = _item.displayName;
            if (descText != null) descText.text = _item.description;
            if (priceText != null) priceText.text = _item.price.ToString();

            if (sizeText != null)
            {
                var f = _item.footprint;
                sizeText.text = $"{f.x}x{f.y}";
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
            buyButton.interactable = _item != null && money != null && inventory != null;
        }

        private void OnBuyClicked()
        {
            if (_item == null || money == null || inventory == null) return;
            if (!money.TrySpend(_item.price)) return;

            inventory.Add(_item, 1);
        }
    }
}
