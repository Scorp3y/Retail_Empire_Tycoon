using System;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    public enum DoorMechanism { Swing, Slide, Fold, Arc, Revolve, Lift, Roll, Sectional, Tilt, SideSectional }

    /// <summary>Shared approach, obstruction and timing policy for the buildable door collection.</summary>
    [DisallowMultipleComponent]
    public sealed class CollectionDoor : MonoBehaviour
    {
        [Serializable]
        public struct Part
        {
            public Transform node;
            public Vector3 rest;
            public Vector3 travel;
            public float angle;
        }

        [SerializeField] private DoorMechanism mechanism;
        [SerializeField] private Part[] parts;
        [SerializeField] private BoxCollider[] approaches;
        [SerializeField] private BoxCollider[] barriers;
        [SerializeField] private Renderer[] indicators;
        [SerializeField] private Transform rollDrum;
        [SerializeField] private bool vehicleAccess;
        [SerializeField] private bool approachSwing;
        [SerializeField] private bool springReturn;
        [SerializeField] private bool sensorEnabled = true;
        [SerializeField] private LayerMask actorLayers = ~0;
        [SerializeField] private string additionalActorTag;
        [SerializeField, Min(.15f)] private float travelSeconds = .8f;
        [SerializeField, Min(0)] private float closeDelay = 1.2f;
        [SerializeField] private AudioClip openingSound, closingSound, motorSound;
        [SerializeField] private float sectionSize, bendStart, bendRadius, travelDistance;
        [SerializeField] private Bounds[] motionClearances;
        private readonly Collider[] contacts = new Collider[48];
        private float progress, passageHold, idle, rotorSpeed, rotorAngle;
        private int motion, side = 1;
        private AudioSource audioSource;
        public bool IsOpen => progress >= .98f;
        public bool IsClosed => progress <= .001f;
        public bool AllowsVehicles => vehicleAccess;
        public bool HasSensor => sensorEnabled;
        public DoorMechanism Mechanism => mechanism;
        public Bounds[] MotionClearances => motionClearances;

        public void Configure(DoorMechanism kind, Part[] movingParts, BoxCollider[] sensors, BoxCollider[] blockers,
            Renderer[] lamps, Bounds[] clearances, bool vehicle, bool swingFromApproach, bool pendulum,
            float duration, float segment, float trackBend, float trackRadius, float distance, Transform drum)
        {
            mechanism = kind; parts = movingParts; approaches = sensors; barriers = blockers;
            indicators = lamps; motionClearances = clearances; vehicleAccess = vehicle;
            approachSwing = swingFromApproach; springReturn = pendulum; travelSeconds = duration;
            sectionSize = segment; bendStart = trackBend; bendRadius = trackRadius; travelDistance = distance; rollDrum = drum;
        }

        private void Awake() => audioSource = GetComponent<AudioSource>();
        private void Start() => SetPose(0);
        private void FixedUpdate() => Advance(Time.fixedDeltaTime);

        public bool IsBarrier(Collider candidate) => Array.IndexOf(barriers, candidate) >= 0;

        public bool Approaches(Vector3 point)
        {
            foreach (var zone in approaches)
            {
                var local = zone.transform.InverseTransformPoint(point) - zone.center;
                if (Mathf.Abs(local.x) <= zone.size.x * .5f && Mathf.Abs(local.z) <= zone.size.z * .5f &&
                    Mathf.Abs(local.y) <= zone.size.y * .5f + .15f) return true;
            }
            return false;
        }

        public bool RequestPassage(Vector3 actorPosition)
        {
            if (!sensorEnabled || !Approaches(actorPosition)) return false;
            SelectSide(actorPosition);
            passageHold = .3f;
            return IsOpen;
        }

        public bool CrossesClosedBarrier(Vector3 start, Vector3 end)
        {
            if (IsOpen) return false;
            foreach (var barrier in barriers)
            {
                var a = barrier.transform.InverseTransformPoint(start) - barrier.center;
                var b = barrier.transform.InverseTransformPoint(end) - barrier.center;
                var extent = barrier.size * .5f;
                if (Mathf.Min(Mathf.Abs(a.y), Mathf.Abs(b.y)) > extent.y + .15f) continue;
                if (Mathf.Min(a.z, b.z) > extent.z + .12f || Mathf.Max(a.z, b.z) < -extent.z - .12f) continue;
                if (Mathf.Min(a.x, b.x) <= extent.x && Mathf.Max(a.x, b.x) >= -extent.x) return true;
            }
            return false;
        }

        private void SelectSide(Vector3 position)
        {
            if (progress <= .001f) side = transform.InverseTransformPoint(position).z >= 0 ? 1 : -1;
        }

        public void Advance(float seconds)
        {
            bool requested = passageHold > 0;
            passageHold = Mathf.Max(0, passageHold - seconds);
            bool sensed = false, occupied = false;
            foreach (var zone in approaches)
            {
                int count = Scan(zone);
                for (int i = 0; i < count; i++)
                {
                    var candidate = contacts[i];
                    if (candidate.transform.IsChildOf(transform)) continue;
                    if (!OpensFor(candidate))
                    {
                        occupied |= !IsClosed && candidate.GetComponentInParent<PickupDrive>() != null;
                        continue;
                    }
                    sensed = true;
                    SelectSide(candidate.bounds.center);
                    break;
                }
                if (sensed) break;
            }
            // An obstruction in the aperture also stops a closing door with its sensor switched off.
            foreach (var barrier in barriers)
            {
                int count = Scan(barrier);
                for (int i = 0; i < count; i++)
                {
                    var candidate = contacts[i];
                    if (candidate.transform.IsChildOf(transform) ||
                        (!OpensFor(candidate) && candidate.attachedRigidbody == null && candidate.GetComponentInParent<CharacterController>() == null))
                        continue;
                    occupied = true;
                    break;
                }
                if (occupied) break;
            }
            bool open = sensorEnabled && (requested || sensed) || occupied && !IsClosed;
            idle = open ? 0 : idle + seconds;
            int nextMotion = open && progress < 1 ? 1 : !open && idle >= closeDelay && progress > 0 ? -1 : 0;
            UpdateAudio(nextMotion);
            progress = Mathf.Clamp01(progress + nextMotion * seconds / travelSeconds);
            if (mechanism == DoorMechanism.Revolve)
            {
                rotorSpeed = Mathf.MoveTowards(rotorSpeed, open ? 62 : 0, seconds * 115);
                rotorAngle = Mathf.Repeat(rotorAngle + rotorSpeed * seconds, 360);
            }
            if (nextMotion != 0 || mechanism == DoorMechanism.Revolve) SetPose(progress);
            foreach (var lamp in indicators)
                if (lamp.enabled != sensorEnabled) lamp.enabled = sensorEnabled;
        }

        private int Scan(BoxCollider zone)
        {
            var t = zone.transform;
            var scale = t.lossyScale;
            var half = Vector3.Scale(zone.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return Physics.OverlapBoxNonAlloc(t.TransformPoint(zone.center), half, contacts, t.rotation,
                actorLayers, QueryTriggerInteraction.Collide);
        }

        private bool OpensFor(Collider actor)
        {
            if (actor.GetComponentInParent<PickupDrive>() != null) return vehicleAccess;
            return actor.GetComponentInParent<Avatar>() != null || actor.GetComponentInParent<ShopCharacter>() != null ||
                actor.GetComponentInParent<CharacterController>() != null ||
                (!string.IsNullOrEmpty(additionalActorTag) && actor.transform.root.CompareTag(additionalActorTag));
        }

        private void UpdateAudio(int nextMotion)
        {
            if (nextMotion == motion) return;
            motion = nextMotion;
            if (audioSource == null) return;
            audioSource.Stop();
            if (motion == 0) return;
            var clip = motion > 0 ? openingSound : closingSound;
            if (motorSound != null) { audioSource.clip = motorSound; audioSource.loop = true; audioSource.Play(); }
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        /// <summary>Poses the mechanism in local coordinates, also used to render catalog previews.</summary>
        public void SetPose(float normalized)
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(normalized));
            if (springReturn && motion < 0 && normalized < .27f)
                t -= Mathf.Sin(normalized / .27f * Mathf.PI * 2) * normalized * .28f;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                switch (mechanism)
                {
                    case DoorMechanism.Sectional:
                        GarageDoor.SetSectionPose(part.node, i, t * travelDistance, sectionSize, bendStart, bendRadius);
                        break;
                    case DoorMechanism.SideSectional:
                        PoseSideSection(part, i, t);
                        break;
                    case DoorMechanism.Roll:
                        PoseSlat(part, t);
                        break;
                    case DoorMechanism.Revolve:
                        part.node.localRotation = Quaternion.Euler(0, Application.isPlaying ? rotorAngle : 40 * t, 0);
                        break;
                    case DoorMechanism.Fold:
                        float radians = Mathf.Abs(part.angle) * t * Mathf.Deg2Rad;
                        part.node.localPosition = part.rest + new Vector3(part.travel.x * (1 - Mathf.Cos(radians)),
                            0, part.travel.z * Mathf.Sin(radians));
                        part.node.localRotation = Quaternion.Euler(0, part.angle * t, 0);
                        break;
                    default:
                        part.node.localPosition = part.rest + part.travel * t;
                        part.node.localRotation = mechanism == DoorMechanism.Tilt
                            ? Quaternion.Euler(part.angle * t, 0, 0)
                            : Quaternion.Euler(0, part.angle * t * (approachSwing ? side : 1), 0);
                        break;
                }
            }
            if (rollDrum != null) rollDrum.localRotation = Quaternion.Euler(-t * 420, 0, 0);
            foreach (var barrier in barriers) barrier.enabled = normalized < .98f;
        }

        private void PoseSideSection(Part part, int index, float t)
        {
            Vector3 Point(float distance)
            {
                var along = GarageDoor.Track(distance, bendStart, bendRadius);
                return part.rest + new Vector3(along.y, 0, along.z);
            }
            var a = Point(index * sectionSize + t * travelDistance);
            var b = Point((index + 1) * sectionSize + t * travelDistance);
            part.node.localPosition = (a + b) * .5f;
            part.node.localRotation = Quaternion.FromToRotation(Vector3.right, b - a);
            part.node.localScale = new Vector3((b - a).magnitude / sectionSize, 1, 1);
        }

        private void PoseSlat(Part part, float t)
        {
            float y = part.rest.y + t * travelDistance;
            float bend = Mathf.Max(0, y - bendStart) / bendRadius;
            part.node.localPosition = new Vector3(part.rest.x, Mathf.Min(y, bendStart) + bendRadius * Mathf.Sin(bend),
                part.rest.z - bendRadius * (1 - Mathf.Cos(bend)));
            part.node.localRotation = Quaternion.Euler(-bend * Mathf.Rad2Deg, 0, 0);
            part.node.gameObject.SetActive(bend < Mathf.PI * 1.85f);
        }
    }
}
