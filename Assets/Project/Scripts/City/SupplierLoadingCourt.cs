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
    }
}
