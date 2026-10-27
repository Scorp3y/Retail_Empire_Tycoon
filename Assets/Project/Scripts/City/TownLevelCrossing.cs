using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>
    /// Coordinates the existing visual train with physical road barriers. Closes before arrival,
    /// keeps the train outside an occupied crossing and reopens only after the last carriage clears.
    /// The authored scene has one crossing; this is not a general railway signalling system.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TownLevelCrossing : MonoBehaviour
    {
        public TownRailTraffic train;
        public float trackDistance;
        public Transform[] arms=System.Array.Empty<Transform>();
        public GameObject[] redLamps=System.Array.Empty<GameObject>();
        public Vector3 deckCenter;
        public Vector3 occupancyHalfSize=new(5,2,7);
        public float closingSeconds=3;
        private readonly Collider[] occupants=new Collider[64];
        private float closure;
        public bool IsClosed=>closure>=.999f;
        public bool IsWarning{get;private set;}
        public bool IsOccupied{get;private set;}

        public static bool RequiresClosure(float headDistance,float crossingDistance,int cars,float spacing)
            => headDistance>=crossingDistance-100 && headDistance<=crossingDistance+Mathf.Max(0,cars-1)*spacing+14;

        private void Update()=>Advance(Time.deltaTime);
        public void Advance(float seconds)
        {
            if(train==null||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            IsWarning=RequiresClosure(train.TravelDistance,trackDistance,train.cars.Length,train.carSpacing);
            IsOccupied=false;
            int count=Physics.OverlapBoxNonAlloc(transform.TransformPoint(deckCenter),Vector3.Scale(occupancyHalfSize,transform.lossyScale),occupants,transform.rotation,~0,QueryTriggerInteraction.Ignore);
            // A full buffer is treated as occupied: never release the train on incomplete evidence.
            if(count==occupants.Length)IsOccupied=true;
            for(int i=0;i<count;i++)
                if(occupants[i].attachedRigidbody!=null &&
                    (!occupants[i].attachedRigidbody.isKinematic || occupants[i].attachedRigidbody.GetComponent<TrafficVehicle>()!=null))IsOccupied=true;
            // Let a vehicle already inside leave before lowering barriers. Incoming traffic is warned.
            closure=Mathf.MoveTowards(closure,IsWarning&&!IsOccupied?1:0,seconds/Mathf.Max(.5f,closingSeconds));
            for(int i=0;i<arms.Length;i++)if(arms[i]!=null)arms[i].localRotation=Quaternion.Euler(0,0,90*(1-closure));
            bool flash=IsWarning&&Mathf.Repeat(Time.time,1)<.5f;
            foreach(var lamp in redLamps)if(lamp!=null)lamp.SetActive(flash);
            float remaining=trackDistance-train.TravelDistance;
            train.SetCrossingHold(IsWarning&&remaining>0&&remaining<20&&(!IsClosed||IsOccupied));
        }
        private void OnDisable(){if(train!=null)train.SetCrossingHold(false);}
    }
}
