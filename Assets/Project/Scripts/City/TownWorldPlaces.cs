using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public sealed class TownWorldPlaces : MonoBehaviour
    {
        public TownWorldLayout layout;
        public DeliveryCheckpoint[] suppliers=Array.Empty<DeliveryCheckpoint>();
    }
}
