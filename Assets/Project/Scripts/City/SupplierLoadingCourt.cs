using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Authored service-yard geometry used by route validation; not a simulated truck service.</summary>
    public sealed class SupplierLoadingCourt : MonoBehaviour
    {
        public Rect bounds;
        public Vector3 streetAccess;
        public Vector3 entrance;
        public Vector3 staging;
        public DeliveryCheckpoint checkpoint;
        [Tooltip("Intermediate vehicle approach points; empty keeps the original direct entrance-to-staging route.")]
        public Vector3[] approachWaypoints = System.Array.Empty<Vector3>();
        [Tooltip("Maximum design vehicle length in native town metres; compact city bakeries are pickup-only.")]
        [Min(5)] public float designVehicleLength = 20;
    }
}
