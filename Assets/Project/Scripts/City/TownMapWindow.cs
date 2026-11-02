using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.City
{
    /// <summary>Full-world map with pointer waypoints, categories, order readiness and one shared GPS selection.</summary>
    public sealed class TownMapWindow : IDisposable
    {
        private readonly ShopUi ui;
        private readonly TownMapData data;
        private readonly TownNavigation navigation;
        private readonly Action close;
        private readonly RectTransform panel, list;
        private readonly TMP_Text details, routeStatus;
        private readonly TMP_Text districtTitle;
        private readonly List<(TownMapPoint Point, Button Button, TMP_Text Label)> rows = new();
        private readonly List<(TownMapPoint Point, RectTransform Rect)> labels = new();
        private readonly List<(TownMapFilter Filter, Button Button)> filters = new();
        private float nextPaint;
        public CityMapGraphic Graphic { get; }
        public RectTransform Panel => panel;
        public TownMapWindow(ShopUi ui, Transform canvas, TownMapData data, TownNavigation navigation, Action close)
        {
            this.ui = ui; this.data = data; this.navigation = navigation; this.close = close;
            panel = ui.Modal("Town map", canvas, new Vector2(1460, 900));
            var header = ui.Rect("Map header", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(1436, 64));
            ui.Panel(header, ShopUiTheme.Ink);
            var title = ui.Heading(header, "Town map", "Town map", new Vector2(20, -10), new Vector2(330, 43), 29); title.color = ShopUiTheme.Paper;
            var sub = ui.Label(header, "Plan your supply run", new Vector2(360, -18), new Vector2(640, 30), 19); sub.color = ShopUiTheme.Paper;
            ui.IconButton(header, ShopIcon.Close, new Vector2(1382, -10), 44, close);
            string[] filterNames = { "All places", "Suppliers", "Services", "Ready orders" };
            for (int i = 0; i < filterNames.Length; i++)
            {
                var filter = (TownMapFilter)i; var button = ui.Button(panel, filterNames[i], new Vector2(24 + i * 148, -94), new Vector2(138, 36), () => SetFilter(filter));
                filters.Add((filter, button));
            }
            ui.Button(panel, "Fit world", new Vector2(641, -94), new Vector2(116, 36), () => { Graphic.Projection.Reset(); Refresh(); });
            ui.Button(panel, "Center player", new Vector2(765, -94), new Vector2(144, 36), () => { Graphic.Projection.ZoomAt(3 / Graphic.Projection.Zoom, Vector2.zero, Graphic.rectTransform.rect); Graphic.Projection.Follow(Graphic.PlayerPosition); Refresh(); });
            ui.Button(panel, "−", new Vector2(917, -94), new Vector2(58, 36), () => Zoom(1 / 1.25f));
            ui.Button(panel, "+", new Vector2(983, -94), new Vector2(58, 36), () => Zoom(1.25f));
            var viewport = ui.Rect("Map viewport", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -145), new Vector2(1017, 628));
            ui.Panel(viewport, ShopUiTheme.Line); viewport.gameObject.AddComponent<RectMask2D>();
            var area = ui.Rect("Town schematic", viewport, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(1017, 628));
            Graphic = area.gameObject.AddComponent<CityMapGraphic>(); Graphic.Initialize(data, new TownMapProjection(data.Bounds), navigation);
            area.gameObject.AddComponent<TownMapInput>().Initialize(Graphic, navigation, Refresh);
            foreach (var point in data.Points)
            {
                var label = ui.Label(area, point.Name, Vector2.zero, new Vector2(175, 24), 15);
                label.fontStyle = FontStyles.Bold; labels.Add((point, label.rectTransform));
            }
            if(data.Forest!=null)
            {districtTitle=ui.Label(area,"PINEBROOK",Vector2.zero,new Vector2(210,30),18);districtTitle.alignment=TextAlignmentOptions.Center;districtTitle.fontStyle=FontStyles.Bold;districtTitle.color=new Color(.19f,.34f,.26f,.72f);}
            ui.Heading(panel, "Destinations", "Destinations", new Vector2(1060, -94), new Vector2(376, 36), 23);
            var scrollArea = ui.Rect("Destinations list", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1060, -145), new Vector2(376, 475));
            ui.Panel(scrollArea, ShopUiTheme.Paper); scrollArea.gameObject.AddComponent<RectMask2D>();
            var scroll = scrollArea.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var bar = ui.Rect("Places scrollbar", scrollArea, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(8, 475));
            ui.Panel(bar, ShopUiTheme.Line); var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = ui.Rect("Places scroll handle", bar, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(8, 40));
            scrollbar.targetGraphic = ui.Panel(handle, ShopUiTheme.Muted); scrollbar.handleRect = handle;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            list = ui.Rect("Place rows", scrollArea, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(376, 800)); scroll.viewport = scrollArea; scroll.content = list;
            foreach (var point in data.Points)
            {
                var selected = point; var button = ui.Button(list, "", Vector2.zero, new Vector2(360, 46), () => { navigation.Select(selected); Refresh(); });
                var iconRect = ui.Rect("Place symbol", button.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -6), new Vector2(34, 34));
                var icon = iconRect.gameObject.AddComponent<TownMapSymbolGraphic>(); icon.Symbol = point.Symbol; icon.color = CityMapGraphic.PointColor(point); icon.raycastTarget = false;
                var label = ui.Label(button.transform, point.Name, new Vector2(54, -4), new Vector2(298, 39), 18);
                rows.Add((point, button, label));
            }
            var info = ui.Rect("Selected place", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1060, -636), new Vector2(376, 137));
            ui.Panel(info, new Color32(229, 232, 210, 255)); details = ui.Label(info, "", new Vector2(14, -12), new Vector2(348, 113), 18);
            routeStatus = ui.Label(panel, "", new Vector2(24, -790), new Vector2(1000, 50), 21);
            routeStatus.gameObject.SetActive(false);
            ui.Button(panel, "Clear route", new Vector2(1070, -802), new Vector2(168, 46), () => { navigation.Clear(); Refresh(); });
            ui.Button(panel, "Done", new Vector2(1254, -802), new Vector2(182, 46), close);
            data.OrdersChanged += Refresh; SetFilter(TownMapFilter.All); Refresh();
        }
        private void Zoom(float factor) { Graphic.Projection.ZoomAt(factor, Graphic.rectTransform.rect.center, Graphic.rectTransform.rect); Refresh(); }
        public void SetFilter(TownMapFilter filter) { Graphic.Filter = filter; Refresh(); }
        public void Refresh()
        {
            if (panel == null) return; int index = 0;
            foreach (var row in rows)
            {
                bool visible = Graphic.Visible(row.Point); row.Button.gameObject.SetActive(visible); if (!visible) continue;
                ((RectTransform)row.Button.transform).anchoredPosition = new Vector2(0, -index++ * 52);
                bool selected = navigation.Destination == row.Point;
                row.Button.image.color = selected ? ShopUiTheme.Ink : new Color32(230, 233, 214, 255);
                row.Label.color = selected ? ShopUiTheme.Paper : ShopUiTheme.Ink;
                row.Button.GetComponentInChildren<TownMapSymbolGraphic>().color = selected ? ShopUiTheme.Paper : CityMapGraphic.PointColor(row.Point);
                int ready = data.ReadyUnits(row.Point); row.Label.text = row.Point.Name + (ready > 0 ? "\n<size=14>Ready to collect · " + ready + " units</size>" : "");
            }
            list.sizeDelta = new Vector2(376, Mathf.Max(475, index * 52));
            foreach (var filter in filters) { filter.Button.image.color = filter.Filter == Graphic.Filter ? ShopUiTheme.Ink : new Color32(220, 226, 204, 255); filter.Button.GetComponentInChildren<TMP_Text>().color = filter.Filter == Graphic.Filter ? ShopUiTheme.Paper : ShopUiTheme.Ink; }
            var point = navigation.Destination;
            details.text = point == null ? "Select a place to set your GPS.\n\nGold dot: paid supplies ready for collection." : point.Name + "\n" + point.Description;
            routeStatus.text = point == null ? "No route selected · All districts shown" : navigation.State == TownNavigationState.NoRoute ? navigation.Instruction
                : navigation.State == TownNavigationState.Arrived ? navigation.Instruction : point.Name + "  ·  " + TownNavigation.FormatDistance(navigation.RemainingMetres) + "  ·  " + navigation.Instruction;
            Graphic.SetVerticesDirty(); PositionLabels();
        }
        private void PositionLabels()
        {
            var occupied = new List<Rect>(); var view = Graphic.rectTransform.rect;
            var markers=data.Points.Where(Graphic.Visible).Select(p=>new Rect(Graphic.Project(p.Position)-Vector2.one*13,Vector2.one*26)).ToArray();
            foreach (var label in labels.OrderByDescending(l => navigation.Destination == l.Point))
            {
                label.Rect.gameObject.SetActive(false);if(!Graphic.Visible(label.Point))continue;
                var origin=Graphic.Project(label.Point.Position);
                foreach(var offset in new[]{new Vector2(16,-10),new Vector2(16,14),new Vector2(16,-38),new Vector2(-label.Rect.sizeDelta.x-16,14)})
                {
                    var p=origin+offset;var rect=new Rect(p,label.Rect.sizeDelta);
                    if(!view.Contains(rect.min)||!view.Contains(rect.max)||occupied.Any(r=>r.Overlaps(rect))||markers.Any(r=>r.Overlaps(rect)))continue;
                    label.Rect.gameObject.SetActive(true);label.Rect.anchorMin=label.Rect.anchorMax=Vector2.one*.5f;label.Rect.pivot=Vector2.zero;label.Rect.anchoredPosition=p;occupied.Add(rect);break;
                }
            }
            if(districtTitle!=null)
            {
                var bounds=data.Forest.DistrictBounds;var p=Graphic.Project(new Vector3(bounds.center.x,0,bounds.yMax-35));
                districtTitle.rectTransform.anchorMin=districtTitle.rectTransform.anchorMax=districtTitle.rectTransform.pivot=Vector2.one*.5f;
                districtTitle.rectTransform.anchoredPosition=p;districtTitle.gameObject.SetActive(view.Contains(p));
            }
        }
        public void Tick(Vector3 player, float heading)
        {
            Graphic.PlayerPosition = player; Graphic.PlayerHeading = heading;
            if (Time.unscaledTime < nextPaint) return; nextPaint = Time.unscaledTime + .15f; Refresh();
        }
        public void Dispose() { data.OrdersChanged -= Refresh; if (panel != null) ui.CloseModal(panel); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TownMapSymbolGraphic : MaskableGraphic
    {
        public TownMapSymbol Symbol { get; set; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        { mesh.Clear(); var r = rectTransform.rect; TownMapGlyph.Draw(mesh, Symbol, r.center, Mathf.Min(r.width, r.height) * .46f, color); }
    }
}
