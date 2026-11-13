using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    public static class DecorSign
    {
        public static int CharacterLimit(BuildItemData item) => item?.id switch
        {
            "decor_aisle_numbers" or "decor_number_lightbox" => 8,
            "decor_department_sign" => 16,
            _ => 0
        };

        public static string Caption(BuildItemData item, string customText) => customText ?? (item?.id switch
        {
            "decor_aisle_numbers" => "04",
            "decor_number_lightbox" => "03",
            "decor_department_sign" => "FRESH",
            _ => ""
        });

        internal static string LimitText(BuildItemData item, string value)
        {
            var text = (value ?? "").Replace("\r", "").Replace("\n", " ");
            int limit = CharacterLimit(item);
            if (text.Length <= limit) return text;
            if (limit > 0 && char.IsHighSurrogate(text[limit - 1])) limit--;
            return text.Substring(0, limit);
        }

        internal static TextMeshPro Create(Transform parent, BuildItemData item, string customText)
        {
            if (CharacterLimit(item) == 0) return null;
            var bounds = item.placementBounds;
            var sign = new GameObject("Sign lettering", typeof(RectTransform), typeof(TextMeshPro));
            sign.transform.SetParent(parent, false);
            var label = sign.GetComponent<TextMeshPro>();
            var theme = Resources.Load<ShopUiTheme>("ShopUi/Theme");
            if (theme != null && theme.regularFont != null) label.font = theme.regularFont;
            bool lightbox = item.id == "decor_number_lightbox";
            float y = lightbox ? .17f : .125f;
            float z = item.id == "decor_department_sign" ? .037f : item.id == "decor_aisle_numbers" ? .034f : bounds.max.z + .002f;
            label.rectTransform.localPosition = new Vector3(bounds.center.x, bounds.min.y + y, z);
            label.rectTransform.localRotation = Quaternion.Euler(0, 180, 0);
            label.rectTransform.sizeDelta = new Vector2(bounds.size.x * (lightbox ? .70f : .85f), .165f);
            label.color = lightbox ? new Color32(38, 69, 48, 255) : new Color32(244, 225, 168, 255);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.richText = false;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = .5f;
            label.fontSizeMax = 100;
            label.overflowMode = TextOverflowModes.Truncate;
            label.text = Caption(item, customText);
            return label;
        }
    }
}
