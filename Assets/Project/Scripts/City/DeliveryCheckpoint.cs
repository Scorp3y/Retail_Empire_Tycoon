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
        // Forward points away from the loading door: the vehicle's rear must face that door.
        public bool requiresRearDocking;
        [Range(5,60)] public float dockingAngleTolerance = 22;
        public float DistanceTo(PickupDrive pickup) => pickup == null ? float.PositiveInfinity
            : Vector2.Distance(new Vector2(pickup.transform.position.x,pickup.transform.position.z),
                new Vector2(transform.position.x,transform.position.z));
        public string UnavailableReason(PickupDrive pickup)
        {
            if (pickup == null || DistanceTo(pickup) > radius) return "Подъедьте к зоне погрузки";
            if (pickup.Speed >= .5f) return "Остановите машину в зоне погрузки";
            if (requiresRearDocking)
            {
                var heading=Vector3.ProjectOnPlane(pickup.transform.forward,Vector3.up);
                var bayHeading=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
                if (Vector3.Angle(heading,bayHeading)>dockingAngleTolerance)
                    return "Поставьте машину задом к воротам по стрелке";
            }
            return string.Empty;
        }
        public bool CanUse(PickupDrive pickup) => string.IsNullOrEmpty(UnavailableReason(pickup));
    }
}
