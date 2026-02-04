using UnityEngine;
using UnityEngine.UI;
using MyShopGame.BuildSystem;
using MyShopGame.Core;

namespace MyShopGame.UI.Windows
{
    public sealed class BuildInventoryItemRow : MonoBehaviour
    {
        [Header("UI")]
        public Image icon;
        public Text nameText;
        public Text countText;
        public Button placeButton;

        private BuildItemData _item;
        private BuildController _controller;

        public void Bind(BuildItemData item, int count, BuildController controller)
        {
            _item = item;
            _controller = controller;

            ApplyTexts(count);
            ApplyIcon();
            HookButton();
        }

        private void ApplyTexts(int count)
        {
            if (nameText != null) nameText.text = _item != null ? _item.displayName : "Unknown";
            if (countText != null) countText.text = $"x{count}";
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
            _controller.EnterBuildMode(_item);
        }
    }
}
