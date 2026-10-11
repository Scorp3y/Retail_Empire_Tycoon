using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Recovers a vehicle that enters the river; cargo and supplier state remain owned by deliveries.</summary>
    public sealed class CityRiverRecovery : MonoBehaviour
    {
        public PickupDrive pickup;
        public TownWorldPlaces town;
        private Vector3 safePosition;
        private Quaternion safeRotation;
        private bool hasSafePosition;
        private float noticeUntil;
        public bool ShowRecoveryNotice=>Time.unscaledTime<noticeUntil;

        private void FixedUpdate()
        {
            if(pickup==null||town==null||town.layout==null)return;
            var local=town.transform.InverseTransformPoint(pickup.Body.position);
            if(local.y<-2&&hasSafePosition)
            {
                pickup.Stop();pickup.Body.position=safePosition+Vector3.up*.15f;
                pickup.Body.rotation=safeRotation;noticeUntil=Time.unscaledTime+5;return;
            }
            if(town.layout.IsRiver(local)||local.y<-.25f||local.y>1.5f)return;
            foreach(var hit in Physics.RaycastAll(pickup.Body.position+Vector3.up*1.5f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(pickup.transform)||hit.transform==pickup.transform||hit.normal.y<=.8f)continue;
                safePosition=pickup.Body.position;safeRotation=Quaternion.Euler(0,pickup.Body.rotation.eulerAngles.y,0);
                hasSafePosition=true;break;
            }
        }
    }
}
