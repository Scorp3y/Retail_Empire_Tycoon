using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>The opening faces local +Z; keep the shared approach lane clear.</summary>
    public sealed class ParkingBay : MonoBehaviour
    {
        public Vector3 ParkPosition => transform.position + transform.forward * -.1f;
        public Vector3 ApproachPosition => transform.position + transform.forward * 1.05f;
    }
}
