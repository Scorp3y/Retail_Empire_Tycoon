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
        private bool heldAtCrossing;
        [Min(60)] public float serviceIntervalSeconds=420;
        private double clock,nextDeparture;
        private bool scheduleInitialized;
        private bool inService=true;
        public bool IsInService=>inService;
        public double NextDeparture=>nextDeparture;
        public double ServiceClock=>clock;
        public float TravelDistance=>distance;
        public bool IsWaiting=>waiting>0;
        public bool IsHeldAtCrossing=>heldAtCrossing;

        /// <summary>The single authored level crossing can hold the train before an occupied road deck.</summary>
        public void SetCrossingHold(bool hold)=>heldAtCrossing=hold;

        private void Update()=>Advance(Time.deltaTime);

        /// <summary>
        /// Advances the service clock in gameplay seconds. Every car must leave the map before
        /// the next journey; a delayed crossing skips missed departures instead of spawning a second train.
        /// </summary>
        public void Advance(float seconds)
        {
            if (layout == null || layout.railway.points.Length < 2 || seconds <= 0 ||
                float.IsNaN(seconds) || float.IsInfinity(seconds)) return;

            double interval = Math.Max(60, serviceIntervalSeconds);
            if (!scheduleInitialized)
            {
                nextDeparture = interval;
                scheduleInitialized = true;
            }
            // Double-precision time keeps exact departure boundaries stable after many trips.
            double remaining = seconds;
            float exitDistance = Length(layout.railway.points) + cars.Length * carSpacing;
            float speed = Mathf.Max(.1f, metresPerSecond);
            while (remaining > 0)
            {
                if (!inService)
                {
                    double pause = Math.Min(remaining, Math.Max(0, nextDeparture - clock));
                    clock += pause;
                    remaining -= pause;
                    if (clock + 1e-7 < nextDeparture) break;
                    clock = Math.Max(clock, nextDeparture);
                    inService = true;
                    distance = -carSpacing;
                    stoppedAtStation = false;
                    nextDeparture += interval;
                }
                if (waiting > 0)
                {
                    double pause = Math.Min(remaining, waiting);
                    waiting = Mathf.Max(0, waiting - (float)pause);
                    clock += pause;
                    remaining -= pause;
                    continue;
                }
                if (heldAtCrossing)
                {
                    clock += remaining;
                    break;
                }
                float destination = !stoppedAtStation ? layout.stationDistance : exitDistance;
                double travelTime = Math.Max(0, (double)(destination - distance) / speed);
                double travel = Math.Min(remaining, travelTime);
                distance += speed * (float)travel;
                clock += travel;
                remaining -= travel;
                if (travel < travelTime) break;
                distance = destination;
                if (!stoppedAtStation)
                {
                    stoppedAtStation = true;
                    waiting = Mathf.Max(0, stationWaitSeconds);
                }
                else
                {
                    inService = false;
                    distance = -1000;
                    if (nextDeparture < clock)
                        nextDeparture += Math.Ceiling((clock - nextDeparture) / interval) * interval;
                }
            }
            for(int i=0;i<cars.Length;i++)
            {
                if(cars[i]==null)continue;
                if(cars[i].gameObject.activeSelf!=inService)cars[i].gameObject.SetActive(inService);
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
