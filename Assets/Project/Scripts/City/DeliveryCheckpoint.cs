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
        public float DistanceTo(PickupDrive pickup)
        {
            if(pickup==null)return float.PositiveInfinity;
            var body=pickup.Body;
            Vector3 position=body!=null?body.position:pickup.transform.position;
            Quaternion rotation=body!=null?body.rotation:pickup.transform.rotation;
            var chassis=kind==CheckpointKind.Showroom?pickup.GetComponent<BoxCollider>():null;
            if(chassis!=null)
            {
                // A long truck can have its nose over the marker while its origin is outside the radius.
                var scale=pickup.transform.lossyScale;
                var local=Quaternion.Inverse(rotation)*(transform.position-position);
                var point=new Vector3(local.x/scale.x,local.y/scale.y,local.z/scale.z)-chassis.center;
                var edge=new Vector3(Mathf.Clamp(point.x,-chassis.size.x*.5f,chassis.size.x*.5f),point.y,Mathf.Clamp(point.z,-chassis.size.z*.5f,chassis.size.z*.5f));
                var delta=rotation*Vector3.Scale(point-edge,scale);return new Vector2(delta.x,delta.z).magnitude;
            }
            return Vector2.Distance(new Vector2(position.x,position.z),new Vector2(transform.position.x,transform.position.z));
        }
        public string UnavailableReason(PickupDrive pickup)
        {
            if (pickup == null || DistanceTo(pickup) > radius) return "Drive to the loading zone";
            var velocity=pickup.Body!=null?pickup.Body.velocity:Vector3.zero;
            if (new Vector2(velocity.x,velocity.z).magnitude >= .5f) return kind==CheckpointKind.Showroom?"Stop at the showroom to view vehicles":"Stop the vehicle in the loading zone";
            if (requiresRearDocking)
            {
                var heading=Vector3.ProjectOnPlane((pickup.Body!=null?pickup.Body.rotation:pickup.transform.rotation)*Vector3.forward,Vector3.up);
                var bayHeading=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
                if (Vector3.Angle(heading,bayHeading)>dockingAngleTolerance)
                    return "Reverse towards the unloading gate";
            }
            return string.Empty;
        }
        public bool CanUse(PickupDrive pickup) => isActiveAndEnabled && string.IsNullOrEmpty(UnavailableReason(pickup));
    }
}
