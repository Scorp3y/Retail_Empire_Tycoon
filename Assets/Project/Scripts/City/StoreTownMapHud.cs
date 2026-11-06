using System;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.BuildSystem;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Store-world entry to the shared map, with native coordinates and the existing territory shortcut preserved.</summary>
    public sealed class StoreTownMapHud : IDisposable
    {
        private readonly ShopUi ui;
        private readonly Transform canvas;
        private readonly CityTrip trip;
        private readonly MainCamera camera;
        private TownMapWindow window;
        private bool savedCamera;
        private float nextNavigation;
        public TownMapData Data { get; }
        public TownNavigation Navigation { get; }
        public TownMiniMap MiniMap { get; }
        public bool IsOpen => window != null;
        public StoreTownMapHud(ShopUi ui, Transform canvas, TownWorldPlaces town, CityTrip trip, MainCamera camera)
        {
            this.ui = ui; this.canvas = canvas; this.trip = trip; this.camera = camera;
            Data = new TownMapData(town, trip.orders, trip.parkedPickup.transform, null, trip.MapHomeAccess); Navigation = new TownNavigation(Data);

        }
        public void Tick()
        {
            if (trip.InCity || trip.Transitioning) { MiniMap?.SetVisible(false); return; }
            bool blocked = trip.work != null && trip.work.IsActive || trip.controls != null && !trip.controls.CanOpenWindow && !IsOpen;
            if (!blocked && !Input.GetKeyDown(KeyCode.Escape) && Input.GetKeyDown(KeyCode.Tab)) Toggle();
            if (IsOpen && trip.controls == null && Input.GetKeyDown(KeyCode.Escape)) Close();
            var position = Data.ToNative(trip.parkedPickup.transform.position);
            if (Time.unscaledTime >= nextNavigation) { nextNavigation = Time.unscaledTime + .2f; Navigation.Tick(position, Vector3.zero, 0, Time.unscaledTime); }
            MiniMap?.SetVisible(!blocked && !IsOpen); MiniMap?.Tick(position, 0, 0, false); window?.Tick(position, 0);
        }
        public void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (trip.InCity || trip.Transitioning || trip.controls != null && !trip.controls.CanOpenWindow || trip.work != null && trip.work.IsActive) return;
            if (trip.controls != null) { trip.controls.CloseWindows(); trip.controls.SetWorldMapOpen(true); }
            var walls = trip.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StoreWallOccluder>(true))
                .Where(w => w.gameObject.activeInHierarchy && w.GetComponentInParent<BuildPreview>() == null).ToArray();
            if (walls.Length > 0)
            {
                var bounds = walls[0].WorldBounds; foreach (var wall in walls.Skip(1)) bounds.Encapsulate(wall.WorldBounds);
                var min = Data.ToNative(bounds.min); var max = Data.ToNative(bounds.max); Data.SetStoreFootprint(Rect.MinMaxRect(min.x, min.z, max.x, max.z));
                if (Navigation.Destination != null) Navigation.Select(Navigation.Destination);
            }
            if (camera != null) { savedCamera = camera.enabled; camera.enabled = false; }
            window = new TownMapWindow(ui, canvas, Data, Navigation, Close); MiniMap?.SetVisible(false);
            window.Tick(Data.ToNative(trip.parkedPickup.transform.position),0);
        }
        public void Close()
        {
            if (window == null) return; window.Dispose(); window = null;
            if (trip.controls != null) trip.controls.SetWorldMapOpen(false);
            if (camera != null) camera.enabled = savedCamera; MiniMap?.SetVisible(true);
        }
        public void Dispose() { Close(); Data.Dispose(); if (MiniMap?.Root != null) UnityEngine.Object.Destroy(MiniMap?.Root.gameObject); }
    }
}
