using System;
using System.Collections.Generic;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Owns the driving map's lifetime, GPS selection and input hand-off to the full map.</summary>
    public sealed class CityMapHud : IDisposable
    {
        private readonly ShopUi ui;
        private readonly RectTransform canvas;
        private readonly PickupDrive pickup;
        private readonly PickupCamera camera;
        private readonly Func<bool> mayOpen;
        private TownMapWindow window;
        private bool restoreInput;
        private float nextNavigation;
        public TownMapData Data { get; }
        public TownNavigation Navigation { get; }
        public TownMiniMap MiniMap { get; }
        public TownMapWindow Window => window;
        public bool IsOpen => window != null;
        public CityMapHud(ShopUi ui, RectTransform canvas, TownWorldPlaces town, PickupDrive pickup, DeliveryCheckpoint home,
            PickupCamera camera, Func<bool> mayOpen, DeliveryOrders orders = null, IEnumerable<DeliveryCheckpoint> unloading = null, Func<Vector3[]> homeAccess = null)
        {
            this.ui = ui; this.canvas = canvas; this.pickup = pickup; this.camera = camera; this.mayOpen = mayOpen;
            Data = new TownMapData(town, orders, home != null ? home.transform : null, unloading, homeAccess);
            Navigation = new TownNavigation(Data);
            MiniMap = new TownMiniMap(ui, canvas, Data, Navigation, new Vector2(24, -24), Toggle);
        }
        public void Tick()
        {
            var position = Data.ToNative(pickup.transform.position); var forward = Data.Town.transform.InverseTransformDirection(pickup.transform.forward);
            float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, speed = pickup.Speed / Data.Town.transform.lossyScale.x;
            if (Time.unscaledTime >= nextNavigation) { nextNavigation = Time.unscaledTime + .1f; Navigation.Tick(position, forward, speed, Time.unscaledTime); }
            if (Input.GetKeyDown(KeyCode.M)) Toggle();
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
            MiniMap.SetVisible(!IsOpen); MiniMap.Tick(position, heading, speed, true); window?.Tick(position, heading);
        }
        public void Toggle()
        {
            if (IsOpen) { Close(); return; } if (!mayOpen()) return;
            restoreInput = pickup.InputEnabled; pickup.Stop(); pickup.InputEnabled = false; camera.SetInteractionBlocked(true);
            window = new TownMapWindow(ui, canvas, Data, Navigation, Close); MiniMap.SetVisible(false);
            var forward=Data.Town.transform.InverseTransformDirection(pickup.transform.forward);
            window.Tick(Data.ToNative(pickup.transform.position),Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg);
        }
        public void Close()
        {
            if (window == null) return;
            window.Dispose(); window = null; if (pickup != null) pickup.InputEnabled = restoreInput;
            if (camera != null) camera.SetInteractionBlocked(false); MiniMap.SetVisible(true);
        }
        public void Dispose() { Close(); Data.Dispose(); if (MiniMap.Root != null) UnityEngine.Object.Destroy(MiniMap.Root.gameObject); }
    }
}
