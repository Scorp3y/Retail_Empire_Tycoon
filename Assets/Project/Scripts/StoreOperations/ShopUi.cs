using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.UI.Shop;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Shared visual language for the shop HUD and work panels, using the game's font and sprites.</summary>
    public sealed class ShopUi
    {
        public static Color Surface => ShopUiTheme.Paper;
        public static Color Accent => ShopUiTheme.Green;
        public static Color Muted => ShopUiTheme.Muted;
        private readonly TMP_FontAsset Font;
        private readonly Sprite PanelSprite;
        private readonly ShopUiTheme Theme;
        public ShopUi(TMP_FontAsset font, Sprite panelSprite, Sprite buttonSprite, Sprite windowSprite = null)
            : this(Resources.Load<ShopUiTheme>("ShopUi/Theme"), font, panelSprite) { }
        public ShopUi(ShopUiTheme theme, TMP_FontAsset font = null, Sprite panelSprite = null)
        {
            Theme = theme; Font = theme != null ? theme.regularFont : font;
            PanelSprite = theme != null ? theme.roundedPanel : panelSprite;
        }
        public void Border(RectTransform rect)
        {
            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>(); outline.effectColor = ShopUiTheme.Line;
            outline.effectDistance = new Vector2(1, -1); outline.useGraphicAlpha = true;
        }
        public RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        public Image Panel(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.sprite = PanelSprite; image.type = Image.Type.Sliced; return image;
        }
        public RectTransform Modal(string name, Transform parent, Vector2 size)
        {
            var backdrop = Rect(name + " backdrop", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            backdrop.anchorMax = Vector2.one;
            Panel(backdrop, new Color(0.02f, 0.06f, 0.04f, 0.35f)).sprite = null;
            var panel = Rect(name, backdrop, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Panel(panel, Surface); Border(panel);
            panel.gameObject.AddComponent<ShopPanelFit>().MaximumSize = size;
            return panel;
        }
        public void CloseModal(RectTransform panel)
        {
            if (panel == null) return;
            // Deactivate immediately: deferred Destroy must not swallow the next frame's input.
            panel.parent.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(panel.parent.gameObject);
        }
        public TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize = 18)
        {
            var rect = Rect("Label", parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); if (Font != null) label.font = Font;
            label.text = text; label.fontSize = fontSize; label.color = ShopUiTheme.Ink; label.raycastTarget = false;
            rect.gameObject.AddComponent<LocalizedFontControl>().excludeFromFontChange = true;
            label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        public Button Button(Transform parent, string text, Vector2 position, Vector2 size, Action action)
        {
            var rect = Rect(text, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            var image = Panel(rect, Accent);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors; colors.highlightedColor = new Color(1.08f, 1.08f, .94f);
            colors.pressedColor = new Color(.75f, .84f, .72f); colors.disabledColor = new Color(.78f, .82f, .72f, .5f); button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            var label = Label(rect, text, new Vector2(6, -2), size - new Vector2(12, 4), 17); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        public TMP_Text Copy(Transform parent, string ru, string en, Vector2 position, Vector2 size, float fontSize = 18)
        {
            var label = Label(parent, ShopText.Get(ru, en), position, size, fontSize);
            label.gameObject.AddComponent<ShopLocalizedLabel>().Set(ru, en); return label;
        }
        public TMP_Text Heading(Transform parent, string ru, string en, Vector2 position, Vector2 size, float fontSize = 26)
        {
            var label = Copy(parent, ru, en, position, size, fontSize);
            if (Theme != null && Theme.headingFont != null) label.font = Theme.headingFont;
            label.fontStyle = Theme != null && Theme.headingFont != null ? FontStyles.Normal : FontStyles.Bold;
            return label;
        }
        public Button Button(Transform parent, string ru, string en, Vector2 position, Vector2 size, Action action)
        {
            var button = Button(parent, ShopText.Get(ru, en), position, size, action);
            button.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<ShopLocalizedLabel>().Set(ru, en); return button;
        }
        public ShopIconGraphic Icon(Transform parent, ShopIcon icon, Vector2 position, Vector2 size, Color? color = null)
        {
            var rect = Rect(icon.ToString(), parent, Vector2.one * .5f, Vector2.one * .5f, position, size);
            var graphic = rect.gameObject.AddComponent<ShopIconGraphic>(); graphic.Icon = icon;
            graphic.color = color ?? ShopUiTheme.Ink; graphic.raycastTarget = false; return graphic;
        }
        public Button IconButton(Transform parent, ShopIcon icon, Vector2 position, float size, Action action)
        {
            var button = Button(parent, "", position, Vector2.one * size, action);
            button.image.color = Surface; Border((RectTransform)button.transform);
            Icon(button.transform, icon, Vector2.zero, Vector2.one * size * .56f); return button;
        }
    }
}
