using RetailEmpireTycoon.StoreOperations;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Non-interactive build identifier, deliberately independent of language and tutorial hints.</summary>
    public sealed class GameVersionLabel : MonoBehaviour
    {
        public const string CurrentVersion = "0.12v d9m10y2026t8:00";
        public Transform uiRoot;
        private RectTransform versionRect;

        private void Start()
        {
            if (uiRoot == null) return;
            var ui = new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            versionRect = ui.Rect("Game version", uiRoot, Vector2.zero, Vector2.zero, new Vector2(14, 8), new Vector2(300, 22));
            var label = ui.Label(versionRect, CurrentVersion, Vector2.zero, new Vector2(300, 22), 13);
            label.color = ShopUiTheme.Paper;
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .8f);
            shadow.effectDistance = new Vector2(1, -1);
        }

        private void OnDestroy()
        {
            if (versionRect != null) Destroy(versionRect.gameObject);
        }
    }
}
