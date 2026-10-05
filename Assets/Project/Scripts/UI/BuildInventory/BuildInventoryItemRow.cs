using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEngine.Scripting.APIUpdating;
using RetailEmpireTycoon.UI.Shop;

namespace RetailEmpireTycoon.UI.Windows
{
    [MovedFrom(false, "MyShopGame.UI.Windows", null, "BuildInventoryItemRow")]
    public sealed class BuildInventoryItemRow : MonoBehaviour
    {
        [Header("UI")]
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text countText;
        public Button placeButton;

        private BuildItemData _item;
        private BuildController _controller;
        private Action _onPlace;

        public void Bind(BuildItemData item, int count, BuildController controller, Action onPlace)
        {
            _item = item;
            _controller = controller;
            _onPlace = onPlace;

            ApplyTexts(count);
            ApplyIcon();
            HookButton();
        }

        private void ApplyTexts(int count)
        {
            if (nameText != null) nameText.text = ShopText.Item(_item);
            if (countText != null) countText.text = ShopText.Get("В наличии", "Owned") + $"\n×{count}";
        }

        private void ApplyIcon()
        {
            if (icon == null) return;
            icon.sprite = _item != null ? _item.icon : null;
            icon.enabled = icon.sprite != null;
        }

        private void HookButton()
        {
            if (placeButton == null) return;

            placeButton.onClick.RemoveAllListeners();
            placeButton.onClick.AddListener(OnPlaceClicked);
            placeButton.interactable = _item != null && _controller != null;
        }

        private void OnPlaceClicked()
        {
            if (_item == null || _controller == null) return;

            _onPlace?.Invoke();
            _controller.EnterBuildMode(_item);
        }
    }
}
