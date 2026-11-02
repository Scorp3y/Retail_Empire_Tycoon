using System;
using System.Collections.Generic;
using RetailEmpireTycoon.Logistics;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.City
{
    public enum TownMapFilter { All, Supplies, Services, ReadyOrders }

    /// <summary>Shared schematic renderer: real roads and footprints, readable symbols, no scenery camera.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CityMapGraphic : MaskableGraphic
    {
        // Existing driver-tracking and editor tools can still supply these bindings.
        public TownWorldPlaces town;
        public PickupDrive pickup;
        public DeliveryCheckpoint home, destination;
        public DriverDelivery driver;
        public TownMapData Data { get; private set; }
        public TownMapProjection Projection { get; private set; }
        public TownNavigation Navigation { get; private set; }
        public TownMapFilter Filter { get; set; }
        public bool IsMiniMap { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public float PlayerHeading { get; set; }
        public int LastVertexCount { get; private set; }
        public static Color RouteColor => new Color32(153, 100, 203, 255);
        private VertexHelper mesh;
        private Rect view;
        private static readonly Color Land = new Color32(224, 225, 211, 255);
        private static readonly Color RoadEdge = new Color32(155, 164, 153, 255);
        private static readonly Color RoadSurface = new Color32(251, 249, 240, 255);
        private static readonly Color Building = new Color32(174, 181, 170, 255);
        public void Initialize(TownMapData data, TownMapProjection projection, TownNavigation navigation = null)
        { Data = data; town = data.Town; Projection = projection; Navigation = navigation; raycastTarget = false; SetVerticesDirty(); }
        public bool Visible(TownMapPoint point) => Navigation?.Destination == point || point.Category == TownMapCategory.Store
            || Filter == TownMapFilter.All || Filter == TownMapFilter.Supplies && point.Category == TownMapCategory.Supplies
            || Filter == TownMapFilter.Services && (point.Category == TownMapCategory.Services || point.Symbol == TownMapSymbol.Fuel || point.Symbol == TownMapSymbol.Train)
            || Filter == TownMapFilter.ReadyOrders && Data.ReadyUnits(point) > 0;
        public Vector2 Project(Vector3 native) => Projection.Project(native, rectTransform.rect);
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (town == null || town.layout == null) return;
            if (Data == null || Data.Town != town)
            { Data = new TownMapData(town, null, home != null ? home.transform : null); Projection = new TownMapProjection(Data.Bounds); }
            mesh = helper; view = rectTransform.rect;
            Quad(view.min, view.max, Land);
            foreach(var field in Data.Fields)NativeRect(field,new Color32(210,208,177,255));
            // Water first; transport above it keeps bridge crossings visible.
            for (float z = Data.Bounds.yMin; z < Data.Bounds.yMax; z += 12)
                NativeLine(new Vector3(town.layout.RiverCenter(z), 0, z), new Vector3(town.layout.RiverCenter(z + 12), 0, z + 12), 28, new Color32(112, 174, 190, 255));
            foreach (var footprint in Data.Buildings) NativeRect(footprint, Building);
            if (Data.StoreFootprint.HasValue) NativeRect(Data.StoreFootprint.Value, new Color32(107, 139, 121, 255));
            foreach(var place in Data.Points) {var access=place.Access;if(access.Length>1)Path(access,4,RoadSurface);}
            foreach (var road in Data.Streets) Path(road.Points, road.Width + 1.5f, RoadEdge);
            foreach (var road in Data.Streets) Path(road.Points, road.Width, RoadSurface);
            Path(town.layout.countryRoad.points, town.layout.countryRoad.width, RoadSurface);
            Path(town.layout.railway.points, 2, new Color32(120, 125, 111, 255));
            Path(town.layout.freightSiding.points, 1.3f, new Color32(120, 125, 111, 255));
            var route = Navigation != null ? Navigation.Route : driver != null ? driver.MapRoute : Array.Empty<Vector3>();
            ScreenPath(route, IsMiniMap ? 7 : 6, new Color32(250, 247, 235, 240));
            ScreenPath(route, IsMiniMap ? 4 : 3.5f, RouteColor);
            foreach (var point in Data.Points) if (Visible(point)) Point(point, Navigation?.Destination == point);
            if (Navigation?.Destination != null && Navigation.Destination.Category == TownMapCategory.Waypoint) Point(Navigation.Destination, true);
            var player = pickup != null ? Data.ToNative(pickup.transform.position) : driver != null ? driver.NativePosition : PlayerPosition;
            float heading = PlayerHeading;
            if (pickup != null) { var forward = town.transform.InverseTransformDirection(pickup.transform.forward); heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg; }
            if (driver != null && driver.Vehicle != null) { var forward = town.transform.InverseTransformDirection(driver.Vehicle.transform.forward); heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg; }
            Player(player, heading);
            LastVertexCount = helper.currentVertCount;
        }
        private void Point(TownMapPoint point, bool selected)
        {
            var p = Project(point.Position); float size = selected ? 12 : IsMiniMap ? 9 : 10;
            if (IsMiniMap && (p - view.center).magnitude > view.width * .5f - 16)
            {
                if (!selected) return;
                var direction = (p - view.center).normalized; p = view.center + direction * (view.width * .5f - 17);
                var side = new Vector2(-direction.y, direction.x);
                Triangle(p + direction * 8, p - direction * 5 + side * 5, p - direction * 5 - side * 5, RouteColor); return;
            }
            if (!view.Contains(p)) return;
            Circle(p, size + 2, new Color32(35, 66, 53, 255)); Circle(p, size, selected ? RouteColor : PointColor(point));
            TownMapGlyph.Draw(mesh, point.Symbol, p, size * .72f, Color.white);
            if (Data.ReadyUnits(point) > 0) Circle(p + Vector2.one * (size - 1), 4, new Color32(244, 185, 58, 255));
        }
        public static Color PointColor(TownMapPoint point) => point.Category == TownMapCategory.Store ? new Color32(42, 98, 78, 255)
            : point.Category == TownMapCategory.Supplies ? new Color32(191, 130, 49, 255)
            : point.Category == TownMapCategory.Services ? new Color32(64, 124, 157, 255) : new Color32(87, 126, 88, 255);
        private void Player(Vector3 native, float heading)
        {
            var p = Project(native); if (!IsMiniMap && !view.Contains(p)) return;
            float a = (heading - Projection.Heading) * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); var side = new Vector2(forward.y, -forward.x);
            Circle(p, 10, new Color32(34, 67, 56, 180));
            Triangle(p + forward * 11, p - forward * 7 + side * 7, p - forward * 7 - side * 7, Color.white);
            Triangle(p + forward * 7, p - forward * 4 + side * 4, p - forward * 4 - side * 4, new Color32(58, 131, 170, 255));
        }
        private void NativeRect(Rect r, Color tint)
        {
            var a = Project(new Vector3(r.xMin, 0, r.yMin)); var b = Project(new Vector3(r.xMin, 0, r.yMax));
            var c = Project(new Vector3(r.xMax, 0, r.yMax)); var d = Project(new Vector3(r.xMax, 0, r.yMin));
            if (!Intersects(a, c, Mathf.Max(r.width, r.height) * Projection.PixelsPerMetre(view))) return;
            int start = mesh.currentVertCount; foreach (var p in new[] { a, b, c, d }) mesh.AddVert(p, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
        private void Path(IReadOnlyList<Vector3> points, float width, Color tint)
        { if (points == null) return; for (int i = 1; i < points.Count; i++) NativeLine(points[i - 1], points[i], width, tint); }
        private void NativeLine(Vector3 a, Vector3 b, float width, Color tint)
        { var x = Project(a); var y = Project(b); float pixels = Mathf.Max(.7f, width * Projection.PixelsPerMetre(view)); if (Intersects(x, y, pixels)) { Line(x, y, pixels, tint); Circle(y, pixels / 2, tint, 8); } }
        private void ScreenPath(IReadOnlyList<Vector3> points, float pixels, Color tint)
        { for (int i = 1; i < points.Count; i++) { var a = Project(points[i - 1]); var b = Project(points[i]); if (!Intersects(a, b, pixels)) continue; Line(a, b, pixels, tint); Circle(b, pixels / 2, tint, 8); } }
        private bool Intersects(Vector2 a, Vector2 b, float padding) => Rect.MinMaxRect(Mathf.Min(a.x, b.x) - padding, Mathf.Min(a.y, b.y) - padding, Mathf.Max(a.x, b.x) + padding, Mathf.Max(a.y, b.y) + padding).Overlaps(view);
        private void Line(Vector2 a, Vector2 b, float width, Color tint) => TownMapGlyph.Line(mesh, a, b, width, tint);
        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint) => TownMapGlyph.Triangle(mesh, a, b, c, tint);
        private void Circle(Vector2 center, float radius, Color tint, int segments = 16) => TownMapGlyph.Circle(mesh, center, radius, tint, segments);
        private void Quad(Vector2 min, Vector2 max, Color tint)
        { int i = mesh.currentVertCount; mesh.AddVert(min, tint, Vector2.zero); mesh.AddVert(new Vector2(min.x, max.y), tint, Vector2.zero); mesh.AddVert(max, tint, Vector2.zero); mesh.AddVert(new Vector2(max.x, min.y), tint, Vector2.zero); mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TownMapDisc : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        { mesh.Clear(); var r = rectTransform.rect; TownMapGlyph.Circle(mesh, r.center, Mathf.Min(r.width, r.height) / 2, color, 80); }
    }
}
