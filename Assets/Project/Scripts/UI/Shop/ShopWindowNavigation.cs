using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Keeps tab appearance in sync with the window's existing navigation state.</summary>
    public sealed class ShopWindowNavigation : MonoBehaviour
    {
        public ShopWindow shop;
        public BuildInventoryWindow inventory;
        public Button[] tabs;
        public Button[] categories;
        private void OnEnable()
        {
            if (shop != null) shop.ViewChanged += Refresh;
            if (inventory != null) inventory.ViewChanged += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (shop != null) shop.ViewChanged -= Refresh;
            if (inventory != null) inventory.ViewChanged -= Refresh;
        }
        public void Refresh()
        {
            int selected = shop != null ? shop.IsStaffView ? 2 : shop.IsProductsView ? 1 : shop.IsBuildView ? 0 : -1
                : inventory != null && inventory.IsProductsView ? 1 : 0;
            for (int i=0;i<tabs.Length;i++) Style(tabs[i],i==selected);
            for (int i=0;i<categories.Length;i++) Style(categories[i],shop != null && (i==0 ? shop.SelectedCategory==BuildCategory.Shelf : shop.SelectedCategory==BuildCategory.Structures));
        }
        private static void Style(Button button, bool selected)
        {
            if (button == null) return;
            button.image.color = selected ? ShopUiTheme.Ink : ShopUiTheme.Paper;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = selected ? ShopUiTheme.Paper : ShopUiTheme.Ink;
        }
    }
}
