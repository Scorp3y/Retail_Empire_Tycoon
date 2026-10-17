using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>One pooled civilian car: lane progress, acceleration and independently steered/rolling wheels.
    /// Kinematic traffic follows authored roads; it is not the player's suspension simulation.</summary>
    [DisallowMultipleComponent]
    public sealed class TrafficVehicle : MonoBehaviour
    {
        [Serializable]
        public sealed class Wheel
        {
            public Transform visual;
            public bool front;
        }
        [SerializeField] private string designId;
        [SerializeField] private float length, width, wheelbase, wheelRadius;
        [SerializeField] private Wheel[] wheels = Array.Empty<Wheel>();
        private TrafficRoadNetwork network;
        private TrafficPolyline turn;
        private Rigidbody body;
        private float distance, wheelRoll, steering;
        private int laneIndex, nextLane;
        public string DesignId => designId;
        public float Length => length;
        public float Width => width;
        public float Speed { get; private set; }
        public float Distance => distance;
        public int LaneIndex => laneIndex;
        public int NextLane => nextLane;
        public bool IsTurning => turn != null;
        public float Remaining => CurrentPath.Length - distance;
        public float Travelled { get; private set; }
        public float Steering => steering;
        public int CompletedTurns { get; private set; }
        public int ReservedJunction { get; set; } = -1;
        public float HoldSeconds { get; set; }
        public Vector3 LocalPosition => network == null ? transform.localPosition : CurrentPath.Sample(distance,out _);
        public Vector3 Forward { get { if(network==null)return transform.localRotation*Vector3.forward;CurrentPath.Sample(distance,out var direction);return direction; } }
        private TrafficPolyline CurrentPath => turn ?? network.Lanes[laneIndex].Path;

        public void Configure(string id, float bodyLength, float bodyWidth, float axleDistance, float radius, Wheel[] bindings)
        {
            if (string.IsNullOrEmpty(id) || bodyLength <= 0 || bodyWidth <= 0 || axleDistance <= 0 || radius <= 0 || bindings == null || bindings.Length != 4)
                throw new ArgumentException("Complete car dimensions and four wheel bindings required.");
            foreach (var wheel in bindings) if (wheel.visual == null) throw new ArgumentException("Missing wheel mesh.");
            designId=id; length=bodyLength; width=bodyWidth; wheelbase=axleDistance; wheelRadius=radius; wheels=bindings;
        }

        public void Place(TrafficRoadNetwork roads, int lane, float metres, int departure)
        {
            network=roads; laneIndex=lane; nextLane=departure; distance=metres; turn=null;
            Speed=0; wheelRoll=steering=0; Travelled=0; CompletedTurns=0; ReservedJunction=-1; HoldSeconds=0;
            body=GetComponent<Rigidbody>();
            Pose(false);
        }
        public void ChooseDeparture(int lane) => nextLane=lane;

        /// <summary>allowedMetres is front-bumper clearance after all road rules; movement never exceeds it.</summary>
        public void Advance(float seconds, float allowedMetres)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float clearance=Mathf.Max(0, allowedMetres);
            float limit=IsTurning ? 3.8f : network.Lanes[laneIndex].speedLimit;
            // Slow before a turn, not after entering its curve.
            if (!IsTurning && Remaining < 16) limit=Mathf.Min(limit, Mathf.Sqrt(3.8f*3.8f+4*Remaining));
            float target=Mathf.Min(limit, Mathf.Sqrt(2*4*clearance));
            Speed=Mathf.MoveTowards(Speed,target,(target<Speed ? 5 : 1.8f)*seconds);
            float travel=Mathf.Min(clearance,Speed*seconds);
            if (travel < .0001f) { Speed=0; HoldSeconds+=seconds; return; }
            HoldSeconds=0; Travelled+=travel;
            float remaining=travel;
            while (remaining > .00001f)
            {
                float step=Mathf.Min(remaining,CurrentPath.Length-distance);
                distance+=step; remaining-=step;
                if (distance < CurrentPath.Length-.00001f) break;
                if (turn == null)
                {
                    turn=network.Turn(laneIndex,nextLane); distance=0;
                }
                else
                {
                    laneIndex=nextLane; nextLane=-1; turn=null; distance=0; CompletedTurns++;
                    // The controller selects the next departure before the next simulation step.
                    if (remaining > 0) { distance=Mathf.Min(remaining,CurrentPath.Length-.001f); remaining=0; }
                }
            }
            wheelRoll=Mathf.Repeat(wheelRoll+travel/wheelRadius*Mathf.Rad2Deg,360);
            Pose(true);
        }
        private void Pose(bool interpolate)
        {
            var position=CurrentPath.Sample(distance,out var forward);
            CurrentPath.Sample(Mathf.Min(distance+wheelbase,CurrentPath.Length),out var ahead);
            float turnAngle=Vector3.SignedAngle(forward,ahead,Vector3.up);
            steering=Mathf.MoveTowards(steering,Mathf.Clamp(turnAngle,-32,32),5);
            var rotation=Quaternion.LookRotation(forward,Vector3.up);
            if (body != null && Application.isPlaying && interpolate)
            {
                body.MovePosition(transform.parent.TransformPoint(position));
                body.MoveRotation(transform.parent.rotation*rotation);
            }
            else
            {
                transform.localPosition=position; transform.localRotation=rotation;
                if (body != null) { body.position=transform.position; body.rotation=transform.rotation; }
            }
            foreach (var wheel in wheels)
                wheel.visual.localRotation=Quaternion.Euler(0,wheel.front ? steering : 0,0)*Quaternion.Euler(wheelRoll,0,0);
        }
    }
}
