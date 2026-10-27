using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Lift platform follows a routed passenger; escalator tread markers loop without moving colliders.</summary>
    public sealed class StoreyTransportVisual : MonoBehaviour
    {
        public Transform platform;
        public Transform[] treadMarkers;
        private float requestedHeight, lastPassenger;
        private RetailEmpireTycoon.StoreOperations.ShopCharacter passenger;
        public bool TryBoard(RetailEmpireTycoon.StoreOperations.ShopCharacter actor,Vector3 position)
        {
            if(platform==null)return true;
            if(passenger!=null&&passenger!=actor&&Time.time-lastPassenger<.6f)return false;
            passenger=actor;FollowPassenger(position);
            // Call an empty cabin to the landing before allowing the next rider inside it.
            return Mathf.Abs(platform.localPosition.y-requestedHeight)<.04f;
        }
        public void FollowPassenger(Vector3 position)
        {
            requestedHeight=Mathf.Clamp(transform.InverseTransformPoint(position).y-.105f,0,GridSystem.StoreyHeight/transform.lossyScale.y);
            lastPassenger=Time.time;
        }
        private void Update()
        {
            if(platform!=null)
            {
                float target=Time.time-lastPassenger<.5f?requestedHeight:0;
                var point=platform.localPosition;point.y=Mathf.MoveTowards(point.y,target,Time.deltaTime*.65f);platform.localPosition=point;
            }
            if(treadMarkers==null)return;
            for(int i=0;i<treadMarkers.Length;i++)
            {
                float t=Mathf.Repeat(Time.time*.22f+(float)i/treadMarkers.Length,1);
                treadMarkers[i].localPosition=new Vector3(0,.105f+t,Mathf.Lerp(-.8335f,.8335f,t));
            }
        }
    }
}
