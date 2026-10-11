using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>The opening faces local +Z; keep the shared approach lane clear.</summary>
    public sealed class ParkingBay : MonoBehaviour
    {
        [Min(1)] public float dimensionsScale=1;
        public Vector3 ParkPosition => transform.position + transform.forward * (-.1f*dimensionsScale);
        public Vector3 ApproachPosition => transform.position + transform.forward * (1.05f*dimensionsScale);
    }
}
