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
    }
}
