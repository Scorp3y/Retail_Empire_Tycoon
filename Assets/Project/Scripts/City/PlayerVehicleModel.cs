using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Visual-only prefab shared by dealership, parked vehicle and physical driving rig.</summary>
    public sealed class PlayerVehicleModel : MonoBehaviour
    {
        [Serializable]
        public sealed class Wheel
        {
            public Transform pivot;
            public bool front;
        }
        public Wheel[] wheels = Array.Empty<Wheel>();
    }
}
