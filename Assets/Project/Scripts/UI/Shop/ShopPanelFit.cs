using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Fits the complete window to the canvas without cropping fixed-height controls.</summary>
    [ExecuteAlways]
    public sealed class ShopPanelFit : MonoBehaviour
    {
        [SerializeField] private Vector2 maximumSize = new Vector2(620, 620);
        [SerializeField] private GridLayoutGroup grid;
        public Vector2 MaximumSize { get => maximumSize; set => maximumSize = value; }
        public void SetGrid(GridLayoutGroup value) { grid = value; }
        private void LateUpdate()
        {
            var rect = transform as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || rect == null) return;
            var area = ((RectTransform)canvas.transform).rect.size;
            // Staff and settings share fixed-height rows. Shrinking just their background
            // clips the footer on ultrawide screens; scale the entire composition instead.
            rect.sizeDelta = maximumSize;
            float scale = Mathf.Min(1f, Mathf.Max(1f, area.x - 32) / maximumSize.x,
                Mathf.Max(1f, area.y - 104) / maximumSize.y);
            rect.localScale = Vector3.one * scale;
            if (grid == null) return;
            float width = ((RectTransform)grid.transform.parent).rect.width;
            int columns = width >= 480 ? 2 : 1;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(Mathf.Floor((width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns), 220);
        }
    }
}
