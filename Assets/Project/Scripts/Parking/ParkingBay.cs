using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>The opening faces local +Z; keep the shared approach lane clear.</summary>
    public sealed class ParkingBay : MonoBehaviour
    {
        [Min(1)] public float dimensionsScale=1;
        public float length;
        public float width;
        public Vector3 ParkPosition => transform.position + transform.forward * (length>0?0:-.1f*dimensionsScale);
        public Vector3 ApproachPosition
        {
            get
            {
                var placed=GetComponent<RetailEmpireTycoon.BuildSystem.PlacedObject>();
                // Paint may be smaller than its occupied cells. The manoeuvre must start outside both.
                float occupiedDepth=placed!=null && placed.item!=null
                    ? placed.item.footprint.y*RetailEmpireTycoon.BuildSystem.ShopProportions.CellSize : 0;
                return transform.position+transform.forward*(length>0?Mathf.Max(length,occupiedDepth)*.5f+.08335f:1.05f*dimensionsScale);
            }
        }
        public Quaternion ParkRotation=>Quaternion.LookRotation(-transform.forward,transform.up);
        public void ApplyPaint(bool personal)
        {
            var block=new MaterialPropertyBlock();
            block.SetColor("_BaseColor",personal?new Color(1,.76f,.08f):new Color(.96f,.93f,.82f));
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>())renderer.SetPropertyBlock(block);
        }
        public bool Fits(float vehicleWidth,float vehicleLength)
        {
            var placed=GetComponent<RetailEmpireTycoon.BuildSystem.PlacedObject>();
            float availableWidth=width>0?width:placed!=null&&placed.item!=null
                ? (placed.parkingModuleVersion<placed.item.parkingGeometryVersion?placed.item.previousParkingBounds:placed.item.placementBounds).size.x:0;
            float availableLength=length>0?length:placed!=null&&placed.item!=null
                ? (placed.parkingModuleVersion<placed.item.parkingGeometryVersion?placed.item.previousParkingBounds:placed.item.placementBounds).size.z:0;
            return vehicleWidth+.06f<=availableWidth && vehicleLength+.08f<=availableLength;
        }
        public void Resize(float width, float depth, Bounds previous)
        {
            float x=width/previous.size.x,z=depth/previous.size.z;
            foreach(Transform child in transform)
            {
                child.localPosition=Vector3.Scale(child.localPosition,new Vector3(x,1,z));
                child.localScale=Vector3.Scale(child.localScale,new Vector3(x,1,z));
            }
            length=depth;
            this.width=width;
        }
    }
}
