using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Lift platform follows a routed passenger; escalator tread markers loop without moving colliders.</summary>
    public sealed class StoreyTransportVisual : MonoBehaviour
    {
        public Transform platform;
        public LiftLandingDoors liftDoors;
        public Transform[] treadMarkers;
        public float lowerFeet=.105f, travelHeight=1f, treadDepth=1.667f, treadTop=.105f;
        public StoreyTravelDirection direction;
        public const float TreadCyclesPerSecond=.176f;
        public int BeltDirection => direction==StoreyTravelDirection.Down?-1:direction==StoreyTravelDirection.Up?1:beltDirection;
        private float requestedHeight, lastPassenger;
        private int beltDirection=1;
        private RetailEmpireTycoon.StoreOperations.ShopCharacter passenger;
        public bool TryBoard(RetailEmpireTycoon.StoreOperations.ShopCharacter actor,Vector3 position,Vector3 target)
        {
            if(passenger!=null&&passenger!=actor&&Time.time-lastPassenger<.6f)return false;
            if(platform==null)
            {
                bool upward=target.y>position.y;
                if(Mathf.Abs(target.y-position.y)>.01f && (direction==StoreyTravelDirection.Up&&!upward || direction==StoreyTravelDirection.Down&&upward))return false;
                passenger=actor;lastPassenger=Time.time;
                if(Mathf.Abs(target.y-position.y)>.01f)beltDirection=target.y>position.y?1:-1;
                return true;
            }
            passenger=actor;FollowPassenger(position);
            // Call an empty cabin to the landing before allowing the next rider inside it.
            bool aligned=Mathf.Abs(platform.localPosition.y-requestedHeight)<.04f;
            if(liftDoors==null)return aligned;
            var local=transform.InverseTransformPoint(position);var destination=transform.InverseTransformPoint(target);
            bool crossing=Mathf.Abs(destination.y-local.y)<.05f && Mathf.Min(local.z,destination.z)<liftDoors.DoorPlane+.2f;
            int floor=local.y-lowerFeet>travelHeight*.5f?1:0;
            bool atLanding=Mathf.Abs(platform.localPosition.y-(floor==0?0:travelHeight))<.012f;
            liftDoors.Request(crossing && atLanding?floor:-1);
            return aligned && (crossing?atLanding&&liftDoors.IsOpen(floor):liftDoors.AllClosed);
        }
        public void FollowPassenger(Vector3 position)
        {
            requestedHeight=Mathf.Clamp(transform.InverseTransformPoint(position).y-lowerFeet,0,travelHeight);
            lastPassenger=Time.time;
        }
        private void Update()
        {
            if(platform!=null)
            {
                float target=Time.time-lastPassenger<.5f?requestedHeight:0;
                if(Time.time-lastPassenger>=.5f)liftDoors?.Request(-1);
                if(liftDoors==null||liftDoors.AllClosed)
                {
                    var point=platform.localPosition;point.y=Mathf.MoveTowards(point.y,target,Time.deltaTime*.85f);platform.localPosition=point;
                }
            }
            if(treadMarkers==null)return;
            for(int i=0;i<treadMarkers.Length;i++)
            {
                float t=Mathf.Repeat(BeltDirection*Time.time*TreadCyclesPerSecond+(float)i/treadMarkers.Length,1);
                treadMarkers[i].localPosition=new Vector3(0,treadTop+t*travelHeight,Mathf.Lerp(-treadDepth*.5f,treadDepth*.5f,t));
            }
        }
    }
}
