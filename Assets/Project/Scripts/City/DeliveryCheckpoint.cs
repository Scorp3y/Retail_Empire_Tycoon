using UnityEngine;
using RetailEmpireTycoon.Logistics;

namespace RetailEmpireTycoon.City
{
    public enum CheckpointKind { Depot, Unload, Return, Showroom }
    public sealed class DeliveryCheckpoint : MonoBehaviour
    {
        public CheckpointKind kind;
        public float radius = 5;
        public SupplierKind supplier;
        public string displayName;
        public bool CanUse(PickupDrive pickup) => pickup != null && pickup.Speed < .5f
            && Vector3.Distance(new Vector3(pickup.transform.position.x,0,pickup.transform.position.z),
                new Vector3(transform.position.x,0,transform.position.z)) <= radius;
    }
}
