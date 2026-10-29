using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>One purchasable vehicle; dimensions and wheel pivots use town metres.</summary>
    public sealed class PlayerVehicleDefinition : ScriptableObject
    {
        public string id, displayName;
        public int price;
        public float payloadKg, cargoM3;
        public float length, width, height, mass, wheelRadius;
        public float topSpeed = 14, acceleration = 4;
        public GameObject model;
        public Sprite icon;
    }
}
