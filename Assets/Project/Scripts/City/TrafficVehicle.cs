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
        private TrafficPolyline previousPath;
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
        public Vector3 Forward { get; private set; } = Vector3.forward;
        private TrafficPolyline CurrentPath => turn ?? network.Lanes[laneIndex].Path;

        public void Configure(string id, float bodyLength, float bodyWidth, float axleDistance, float radius, Wheel[] bindings)
        {
            if (string.IsNullOrEmpty(id) || bodyLength <= 0 || bodyWidth <= 0 || axleDistance <= 0 || radius <= 0 || bindings == null || bindings.Length < 4)
                throw new ArgumentException("Complete car dimensions and at least four wheel bindings required.");
            foreach (var wheel in bindings) if (wheel.visual == null) throw new ArgumentException("Missing wheel mesh.");
            designId=id; length=bodyLength; width=bodyWidth; wheelbase=axleDistance; wheelRadius=radius; wheels=bindings;
        }

        public void Place(TrafficRoadNetwork roads, int lane, float metres, int departure, float mergeSpeed = 0)
        {
            network=roads; laneIndex=lane; nextLane=departure; distance=metres; turn=null; previousPath=null;
            Speed=Mathf.Clamp(mergeSpeed, 0, roads.Lanes[lane].speedLimit);
            if (mergeSpeed <= 0) wheelRoll=steering=0;
            Travelled=0; CompletedTurns=0; ReservedJunction=-1; HoldSeconds=0;
            body=GetComponent<Rigidbody>();
            Pose(0);
        }
        public void ChooseDeparture(int lane) => nextLane=lane;

        /// <summary>Wheel visuals for the separate low-speed parking controller; does not advance a road lane.</summary>
        public void AnimateParkingWheels(float signedMetres,float steerDegrees,float seconds)
        {
            wheelRoll=Mathf.Repeat(wheelRoll+signedMetres/wheelRadius*Mathf.Rad2Deg,360);
            steering=Mathf.MoveTowards(steering,Mathf.Clamp(steerDegrees,-32,32),90*seconds);
            PoseWheels();
        }

        /// <summary>allowedMetres is front-bumper clearance after all road rules; movement never exceeds it.</summary>
        public void Advance(float seconds, float allowedMetres)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float clearance=Mathf.Max(0, allowedMetres);
            float curveSpeed = nextLane >= 0 ? network.TurnSpeed(laneIndex, nextLane) : network.Lanes[laneIndex].speedLimit;
            float limit = IsTurning ? curveSpeed : Mathf.Min(network.Lanes[laneIndex].speedLimit,
                Mathf.Sqrt(curveSpeed * curveSpeed + 3.6f * Remaining));
            float target=Mathf.Min(limit, Mathf.Min(Mathf.Sqrt(2*4*clearance), clearance / .65f));
            Speed=Mathf.MoveTowards(Speed,target,(target<Speed ? 4.3f : 1.8f)*seconds);
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
                    previousPath=CurrentPath;
                    turn=network.Turn(laneIndex,nextLane); distance=0;
                }
                else
                {
                    previousPath=turn;
                    laneIndex=nextLane; nextLane=-1; turn=null; distance=0; CompletedTurns++;
                    // The controller selects the next departure before the next simulation step.
                    if (remaining > 0) { distance=Mathf.Min(remaining,CurrentPath.Length-.001f); remaining=0; }
                }
            }
            wheelRoll=Mathf.Repeat(wheelRoll+travel/wheelRadius*Mathf.Rad2Deg,360);
            Pose(seconds);
        }
        private Vector3 RoutePosition(float metres)
        {
            var path = CurrentPath;
            if (metres < 0 && previousPath != null) return previousPath.Sample(previousPath.Length + metres, out _);
            if (metres > path.Length && nextLane >= 0)
            {
                var next = IsTurning ? network.Lanes[nextLane].Path : network.Turn(laneIndex, nextLane);
                return next.Sample(metres - path.Length, out _);
            }
            float within = Mathf.Clamp(metres, 0, path.Length);
            return path.Sample(within, out var direction) + direction * (metres - within);
        }

        private void Pose(float seconds)
        {
            var position = CurrentPath.Sample(distance, out _);
            // Sample either side of a joint. Segment boundaries and lane transitions share
            // the same heading instead of snapping to the next segment's tangent.
            var before = RoutePosition(distance - .8f);
            var after = RoutePosition(distance + .8f);
            Forward = (after - before).normalized;
            var halfAxle = wheelbase * .5f;
            var rear = RoutePosition(distance - halfAxle);
            var front = RoutePosition(distance + halfAxle);
            var rearTangent = RoutePosition(distance - halfAxle + .55f) - rear;
            var frontTangent = front - RoutePosition(distance + halfAxle - .55f);
            float targetSteer = Vector3.SignedAngle(rearTangent, frontTangent, Vector3.up);
            steering = Mathf.Lerp(steering, Mathf.Clamp(targetSteer, -32, 32), 1 - Mathf.Exp(-11 * seconds));
            var rotation=Quaternion.LookRotation(Forward,Vector3.up);
            if (body != null && Application.isPlaying && seconds > 0)
            {
                body.MovePosition(transform.parent.TransformPoint(position));
                body.MoveRotation(transform.parent.rotation*rotation);
            }
            else
            {
                transform.localPosition=position; transform.localRotation=rotation;
                if (body != null) { body.position=transform.position; body.rotation=transform.rotation; }
            }
            PoseWheels();
        }

        private void PoseWheels()
        {
            float curvature = Mathf.Tan(steering * Mathf.Deg2Rad) / wheelbase;
            foreach (var wheel in wheels)
            {
                float angle = wheel.front
                    ? Mathf.Atan2(wheelbase * curvature, 1 - wheel.visual.localPosition.x * curvature) * Mathf.Rad2Deg
                    : 0;
                wheel.visual.localRotation = Quaternion.Euler(0, angle, 0) * Quaternion.Euler(wheelRoll, 0, 0);
            }
        }
    }
}
