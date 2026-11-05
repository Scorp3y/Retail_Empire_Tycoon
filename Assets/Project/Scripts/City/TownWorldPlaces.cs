using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public sealed class TownWorldPlaces : MonoBehaviour
    {
        public TownWorldLayout layout;
        public DeliveryCheckpoint[] suppliers=Array.Empty<DeliveryCheckpoint>();
        // Additional entrances share the showroom fleet; the town map keeps one destination per supplier.
        public DeliveryCheckpoint[] showroomEntrances=Array.Empty<DeliveryCheckpoint>();

        // Clear aisle between the two pump islands in the existing fuel forecourt.
        private static readonly Rect FuelAisle = Rect.MinMaxRect(221.5f, -121f, 226.5f, -106f);
        internal static Vector3 FuelStop => new Vector3(224f, 0, -114f);
        internal static Vector3[] FuelAccess() => new[]
        {
            new Vector3(228, 0, -144), new Vector3(228, 0, -130),
            new Vector3(224, 0, -123), FuelStop
        };

        internal bool IsAtFuelPump(PickupDrive vehicle)
        {
            var p = transform.InverseTransformPoint(vehicle.transform.position);
            return p.y >= -.5f && p.y < 2f && FuelAisle.Contains(new Vector2(p.x, p.z));
        }
    }
}
