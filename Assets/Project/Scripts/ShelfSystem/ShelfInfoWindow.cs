using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.UI.Shop;

namespace RetailEmpireTycoon.Shelves
{
    [DisallowMultipleComponent]
    public sealed class ShelfInfoWindow : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform panel;

        [SerializeField] private Image productIcon;
        [SerializeField] private TMP_Text productText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Button closeButton;

        [Header("Empty State")]
        [SerializeField] private Sprite emptyIcon;

        [Header("Position")]
        [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);

        private Camera _uiCamera;
        private readonly Vector3[] _panelCorners = new Vector3[4];

        private void Awake()
        {
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (panel == null)
                panel = transform as RectTransform;

            HookCloseButton();
            Hide();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Hide);
        }

        public void Show(PlacedShelfStock shelf, Vector2 screenPosition)
        {
            if (shelf == null)
                return;

            RefreshView(shelf);
            gameObject.SetActive(true);
            SetPosition(screenPosition);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HookCloseButton()
        {
            if (closeButton == null)
                return;

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        private void RefreshView(PlacedShelfStock shelf)
        {
            ProductItemData product = shelf.CurrentProduct;

            RefreshIcon(product);
            RefreshTexts(shelf, product);
        }

        private void RefreshIcon(ProductItemData product)
        {
            if (productIcon == null)
                return;

            Sprite sprite = product != null ? product.Icon : emptyIcon;

            productIcon.sprite = sprite;
            productIcon.enabled = sprite != null;
        }

        private void RefreshTexts(PlacedShelfStock shelf, ProductItemData product)
        {
            if (productText != null)
                productText.text = product != null ? ShopText.Item(product) : ShopText.Get("Полка пуста", "Empty shelf");

            if (amountText != null)
                amountText.text = shelf.CurrentAmount + "/" + shelf.MaxAmount;
        }

        private void SetPosition(Vector2 screenPosition)
        {
            if (panel == null || canvas == null)
                return;

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            _uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition + screenOffset,
                _uiCamera,
                out Vector2 localPoint
            );

            panel.position = canvasRect.TransformPoint(localPoint);

            // The panel follows the clicked shelf, but its actions must stay on screen.
            panel.GetWorldCorners(_panelCorners);
            Vector2 minimum = canvasRect.InverseTransformPoint(_panelCorners[0]);
            Vector2 maximum = canvasRect.InverseTransformPoint(_panelCorners[2]);
            Rect available = canvasRect.rect;
            const float margin = 12f;
            float x = minimum.x < available.xMin + margin ? available.xMin + margin - minimum.x
                : maximum.x > available.xMax - margin ? available.xMax - margin - maximum.x : 0;
            float y = minimum.y < available.yMin + margin ? available.yMin + margin - minimum.y
                : maximum.y > available.yMax - margin ? available.yMax - margin - maximum.y : 0;
            panel.position += canvasRect.TransformVector(new Vector3(x, y, 0));
        }
    }
}
