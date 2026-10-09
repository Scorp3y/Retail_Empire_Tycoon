using System;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Territory;
using UnityEngine;
using System.Linq;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class StarterStoreInitialization : MonoBehaviour
    {
        public StarterStoreBlueprint blueprint;
        public TerritoryPlotLayout land;
        public CityTrip cityTrip;
        public BuildController building;
        public FloorPainter floors;
        public BuildItemCatalog catalog;
        public MainCamera cameraInput;
        private bool modular;
        private void OnEnable() { if (building != null) building.LayoutChanged += RefreshPickupParking; }
        private void OnDisable() { if (building != null) building.LayoutChanged -= RefreshPickupParking; }

        public GameData CreateNewGame(int startingMoney)
        {
            if (blueprint == null) throw new InvalidOperationException("Starter store blueprint is not configured.");
            return blueprint.CreateNewGame(startingMoney);
        }
        public void ConfigureSite(bool modularStore)
        {
            modular = modularStore;
            land.SetInitialServiceYard(modularStore ? blueprint.serviceYard : default);
            if (modularStore && cameraInput != null)
            {
                var position = blueprint.cameraFocus + new Vector3(-8, 12, -14);
                cameraInput.SetHomeView(position, Quaternion.LookRotation(blueprint.cameraFocus - position));
            }
            if (!modularStore || cityTrip.parkedPickup == null) return;
            cityTrip.parkedPickup.transform.SetPositionAndRotation(blueprint.pickupPosition, Quaternion.Euler(blueprint.pickupEuler));
        }
        public void ApplyNewGameBuildings(GameData data)
        {
            // Floor is applied first: parking needs existing asphalt. This consumes no purchased inventory.
            floors.ApplySaveData(data.floorTiles, catalog);
            building.ApplyPlacedSaveData(data.placedObjects, catalog);
            RefreshPickupParking();
        }
        public void RefreshPickupParking()
        {
            if (!modular || cityTrip.parkedPickup == null) return;
            var parking = FindObjectsOfType<PlacedObject>().FirstOrDefault(p => p.playerParking);
            if (parking == null) return;
            var pickup = cityTrip.parkedPickup.transform;
            pickup.SetPositionAndRotation(parking.transform.position, parking.transform.rotation);
            var renderers = pickup.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0) pickup.position += Vector3.up * (.025f - renderers.Min(r => r.bounds.min.y));
        }
    }
}
