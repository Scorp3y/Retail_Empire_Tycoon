using System;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.City
{
    /// <summary>A circular, heading-up local view of the exact same map and selected road route.</summary>
    public sealed class TownMiniMap
    {
        public RectTransform Root { get; }
        public CityMapGraphic Graphic { get; }
        private readonly TownMapData data;
        private readonly TownNavigation navigation;
        private readonly TMP_Text north, location, guidance;
        private float nextPaint;
        public TownMiniMap(ShopUi ui, Transform canvas, TownMapData data, TownNavigation navigation, Vector2 position, Action open)
        {
            this.data = data; this.navigation = navigation;
            Root = ui.Rect("Circular town radar", canvas, new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(256, 330));
            var rim = ui.Rect("Radar dark rim", Root, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, Vector2.one * 256);
            rim.gameObject.AddComponent<TownMapDisc>().color = ShopUiTheme.Ink;
            var border = ui.Rect("Radar cream rim", rim, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * 246);
            border.gameObject.AddComponent<TownMapDisc>().color = ShopUiTheme.Paper;
            var mask = ui.Rect("Circular viewport", border, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * 234);
            var disc = mask.gameObject.AddComponent<TownMapDisc>(); disc.color = Color.white;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var area = ui.Rect("Live local roads", mask, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * 234);
            Graphic = area.gameObject.AddComponent<CityMapGraphic>(); Graphic.IsMiniMap = true;
            Graphic.Initialize(data, new TownMapProjection(data.Bounds) { LocalDiameter = 200 }, navigation);
            var button = rim.gameObject.AddComponent<Button>(); button.targetGraphic = rim.GetComponent<TownMapDisc>(); button.onClick.AddListener(() => open());
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            north = ui.Label(rim, "N", Vector2.zero, new Vector2(22, 22), 17); north.alignment = TextAlignmentOptions.Center;
            north.color = ShopUiTheme.Ink; north.fontStyle = FontStyles.Bold;
            var placeBar = ui.Rect("Radar location", Root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -261), new Vector2(256, 25));
            ui.Panel(placeBar, ShopUiTheme.Paper).raycastTarget = false;
            location = ui.Label(placeBar, "Town map", Vector2.zero, new Vector2(256, 25), 17); location.alignment = TextAlignmentOptions.Center;
            var card = ui.Rect("GPS instruction", Root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -290), new Vector2(256, 64));
            ui.Panel(card, ShopUiTheme.Paper).raycastTarget = false;
            guidance = ui.Label(card, "", new Vector2(12, -7), new Vector2(232, 52), 17);
            placeBar.gameObject.SetActive(false); card.gameObject.SetActive(false);
        }
        public void Tick(Vector3 nativePosition, float heading, float speed, bool rotate)
        {
            if (!Root.gameObject.activeInHierarchy || Time.unscaledTime < nextPaint) return; nextPaint = Time.unscaledTime + .05f;
            var projection = Graphic.Projection; projection.Follow(nativePosition);
            projection.Heading = rotate ? Mathf.LerpAngle(projection.Heading, heading, .35f) : 0;
            projection.LocalDiameter = Mathf.Lerp(projection.LocalDiameter, 170 + Mathf.Clamp(speed * 7, 0, 150), .08f);
            Graphic.PlayerPosition = nativePosition; Graphic.PlayerHeading = heading; Graphic.SetVerticesDirty();
            float a = projection.Heading * Mathf.Deg2Rad;
            north.rectTransform.anchorMin = north.rectTransform.anchorMax = north.rectTransform.pivot = Vector2.one * .5f;
            north.rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 119;
            location.text = navigation.Destination != null ? navigation.Destination.Name
                : !rotate ? "MY STORE · Tab" : data.Forest != null && data.Forest.DistrictBounds.Contains(new Vector2(nativePosition.x, nativePosition.z)) ? "PINEBROOK · M" : "TOWN MAP · M";
            guidance.text = navigation.State == TownNavigationState.None ? "Click radar to open map"
                : navigation.Instruction + (navigation.State == TownNavigationState.Following ? "\n" + TownNavigation.FormatDistance(navigation.RemainingMetres) + " to destination" : "");
        }
        public void SetVisible(bool visible) { if (Root != null) Root.gameObject.SetActive(visible); }
    }
}
