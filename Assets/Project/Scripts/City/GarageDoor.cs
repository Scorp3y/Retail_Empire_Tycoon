using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>One sectional door. Its approach and throat open it; the common hall only holds an open door.</summary>
    [DisallowMultipleComponent]
    public sealed class GarageDoor : MonoBehaviour
    {
        public const int SectionCount = 5;
        public const float ClearHeight = 4.7f;
        public const float SectionHeight = ClearHeight / SectionCount;
        public const float BendStart = 4.73f;
        public const float BendRadius = .48f;
        public const float OpenTravel = 5.55f;

        [SerializeField] private Transform[] sections;
        [SerializeField] private BoxCollider approach;
        [SerializeField] private BoxCollider throat;
        [SerializeField] private BoxCollider hall;
        [SerializeField] private BoxCollider closedBarrier;
        [SerializeField, Min(.1f)] private float travelSeconds = 1.75f;
        [SerializeField, Min(0)] private float closeDelay = 2.5f;
        [SerializeField, Range(5.45f, 5.7f)] private float liftTravel = OpenTravel;
        [SerializeField] private string playerTag = "";
        [SerializeField] private bool playSound;
        [SerializeField] private AudioClip openingSound;
        [SerializeField] private AudioClip closingSound;
        private readonly Collider[] taggedOverlaps = new Collider[64];
        private CityWorld city;
        private AudioSource speaker;
        private float progress;
        private float emptyFor;
        private int motion;

        public bool IsFullyOpen => progress >= 1f;
        public bool IsFullyClosed => progress <= 0f;
        public bool BlocksPassage => closedBarrier.enabled;

        public bool CanService(PickupDrive pickup)
        {
            if (pickup == null || pickup.Speed > .45f || !pickup.TryGetComponent<BoxCollider>(out var body))
                return false;
            var bounds = body.bounds;
            if (!hall.bounds.Contains(bounds.min) || !hall.bounds.Contains(bounds.max)) return false;
            var center = transform.InverseTransformPoint(bounds.center);
            return Mathf.Abs(center.x) < 1.65f &&
                Mathf.Abs(Vector3.Dot(pickup.transform.forward, transform.forward)) > .88f;
        }

        private void Start()
        {
            city = FindObjectOfType<CityWorld>();
            speaker = GetComponent<AudioSource>();
            Pose(0);
        }

        private void FixedUpdate()
        {
            var pickup = city != null ? city.pickup : null;
            bool incoming = ContainsPlayer(approach, pickup) || ContainsPlayer(throat, pickup);
            bool hold = incoming || progress > 0f && ContainsPlayer(hall, pickup);
            emptyFor = hold ? 0 : emptyFor + Time.fixedDeltaTime;
            int nextMotion = hold && progress < 1f ? 1 : !hold && emptyFor >= closeDelay && progress > 0f ? -1 : 0;
            if (nextMotion != 0 && nextMotion != motion && playSound && speaker != null)
            {
                var clip = nextMotion > 0 ? openingSound : closingSound;
                if (clip != null) speaker.PlayOneShot(clip);
            }

            motion = nextMotion;
            if (motion == 0) return;
            progress = Mathf.Clamp01(progress + motion * Time.fixedDeltaTime / travelSeconds);
            Pose(Mathf.SmoothStep(0, 1, progress) * liftTravel);
        }

        private bool ContainsPlayer(BoxCollider zone, PickupDrive pickup)
        {
            if (pickup != null && pickup.isActiveAndEnabled &&
                pickup.TryGetComponent<BoxCollider>(out var body) && body.enabled)
                return zone.bounds.Intersects(body.bounds);

            if (string.IsNullOrEmpty(playerTag)) return false;
            var t = zone.transform;
            int count = Physics.OverlapBoxNonAlloc(t.TransformPoint(zone.center),
                Vector3.Scale(zone.size * .5f, t.lossyScale), taggedOverlaps, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = taggedOverlaps[i];
                if (hit.attachedRigidbody != null && hit.attachedRigidbody.gameObject.tag == playerTag) return true;
            }
            return false;
        }

        private void Pose(float travel)
        {
            for (int i = 0; i < sections.Length; i++)
                SetSectionPose(sections[i], i, travel);
            // A simple stationary blocker protects the entire aperture until the last panel is clear.
            // The safety volumes are evaluated before this changes, including during a closing reversal.
            closedBarrier.enabled = travel < 5.45f;
        }

        public static Vector3 Track(float distance) => Track(distance, BendStart, BendRadius);

        public static Vector3 Track(float distance, float bendStart, float bendRadius)
        {
            if (distance <= bendStart) return new Vector3(0, distance, 0);
            float arc = Mathf.PI * .5f * bendRadius;
            float over = distance - bendStart;
            if (over >= arc) return new Vector3(0, bendStart + bendRadius, -bendRadius - (over - arc));
            float angle = over / bendRadius;
            return new Vector3(0, bendStart + bendRadius * Mathf.Sin(angle), -bendRadius * (1 - Mathf.Cos(angle)));
        }

        // Shared by the editor generator, the capture scene and runtime.
        public static void SetSectionPose(Transform section, int index, float travel) =>
            SetSectionPose(section, index, travel, SectionHeight, BendStart, BendRadius);

        public static void SetSectionPose(Transform section, int index, float travel, float height, float bendStart, float bendRadius)
        {
            var a = Track(index * height + travel, bendStart, bendRadius);
            var b = Track((index + 1) * height + travel, bendStart, bendRadius);
            section.localPosition = (a + b) * .5f;
            section.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            section.localScale = new Vector3(1, (b - a).magnitude / height, 1);
        }
    }
}
