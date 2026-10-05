using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.UI.Shop;

namespace RetailEmpireTycoon.UI.Products
{
    public sealed class ProductInventoryItemRow : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text productTypeText;
        [SerializeField] private Button stockButton;

        [Header("Optional")]
        [SerializeField] private TMP_Text buttonText;

        private ProductItemData _item;
        private ProductAssignMode _assignMode;
        private Action _onSelected;

        public void Bind(ProductItemData item, int count, ProductAssignMode assignMode, Action onSelected)
        {
            _item = item;
            _assignMode = assignMode;
            _onSelected = onSelected;

            HookButton();
            RefreshView(count);
        }

        private void OnDestroy()
        {
            if (stockButton != null)
                stockButton.onClick.RemoveListener(Select);
        }

        private void HookButton()
        {
            if (stockButton == null)
                return;

            stockButton.onClick.RemoveAllListeners();
            stockButton.onClick.AddListener(Select);
        }

        private void RefreshView(int count)
        {
            RefreshIcon();
            RefreshTexts(count);
            RefreshButton(count);
        }

        private void RefreshIcon()
        {
            if (icon == null)
                return;

            icon.sprite = _item != null ? _item.Icon : null;
            icon.enabled = icon.sprite != null;
        }

        private void RefreshTexts(int count)
        {
            if (nameText != null)
                nameText.text = ShopText.Item(_item);

            if (countText != null)
                countText.text = ShopText.Get("На складе", "Owned") + "\n×" + Mathf.Max(0, count);

            if (productTypeText != null)
                productTypeText.text = ShopText.ShelfHint(_item);

            if (buttonText != null)
                buttonText.text = ShopText.Get("Выложить", "Restock");
        }

        private void RefreshButton(int count)
        {
            if (stockButton == null)
                return;

            stockButton.interactable = _item != null
                && _assignMode != null
                && count > 0;
        }

        private void Select()
        {
            if (_item == null || _assignMode == null)
                return;

            _onSelected?.Invoke();
            _assignMode.BeginAssign(_item);
        }
    }
}
