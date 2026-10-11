using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Visual passenger service on a single directional line; never changes delivery stock.</summary>
    public sealed class TownRailTraffic : MonoBehaviour
    {
        public TownWorldLayout layout;
        public Transform[] cars=Array.Empty<Transform>();
        public float metresPerSecond=9;
        public float carSpacing=12;
        public float stationWaitSeconds=12;
        private float distance=35;
        private float waiting;
        private bool stoppedAtStation;
        public float TravelDistance=>distance;
        public bool IsWaiting=>waiting>0;

        private void Update()=>Advance(Time.deltaTime);

        /// <summary>Advances a local-coordinate journey. Wrapping happens only after every car leaves the map.</summary>
        public void Advance(float seconds)
        {
            if(layout==null||layout.railway.points.Length<2||seconds<=0)return;
            if(waiting>0)waiting=Mathf.Max(0,waiting-seconds);
            else
            {
                float next=distance+metresPerSecond*seconds;
                if(!stoppedAtStation&&distance<=layout.stationDistance&&next>=layout.stationDistance)
                {distance=layout.stationDistance;waiting=stationWaitSeconds;stoppedAtStation=true;}
                else distance=next;
                if(distance>Length(layout.railway.points)+cars.Length*carSpacing)
                {distance=-carSpacing;stoppedAtStation=false;}
            }
            for(int i=0;i<cars.Length;i++)
            {
                if(cars[i]==null)continue;
                var p=Sample(layout.railway.points,distance-i*carSpacing,out var forward);
                cars[i].localPosition=p;cars[i].localRotation=Quaternion.LookRotation(forward);
            }
        }

        public static float Length(Vector3[] points)
        {
            float length=0;for(int i=1;i<points.Length;i++)length+=Vector3.Distance(points[i-1],points[i]);return length;
        }

        public static Vector3 Sample(Vector3[] points,float metres,out Vector3 forward)
        {
            if(points==null||points.Length<2)throw new ArgumentException("Rail path needs at least two points.");
            for(int i=1;i<points.Length;i++)
            {
                var delta=points[i]-points[i-1];float length=delta.magnitude;
                if(length<.001f)continue;
                if(metres<=length||i==points.Length-1)
                {forward=delta/length;return points[i-1]+forward*metres;}
                metres-=length;
            }
            throw new ArgumentException("Rail path has no usable segments.");
        }
    }
}
