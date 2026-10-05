using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Presentation assets only; inventory, economy and saves retain their existing rules.</summary>
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/UI/Shop theme")]
    public sealed class ShopUiTheme : ScriptableObject
    {
        public TMP_FontAsset regularFont;
        public TMP_FontAsset headingFont;
        public Sprite roundedPanel;
        public Sprite employeePortrait;
        public Sprite[] staffPortraits = new Sprite[4];
        public static Color Paper => new Color32(245, 241, 229, 255);
        public static Color Ink => new Color32(32, 62, 50, 255);
        public static Color Green => new Color32(121, 187, 67, 255);
        public static Color Gold => new Color32(190, 128, 15, 255);
        public static Color Muted => new Color32(99, 117, 102, 255);
        public static Color Line => new Color32(211, 214, 193, 255);
        public static Color Warning => new Color32(178, 67, 47, 255);
    }
}
