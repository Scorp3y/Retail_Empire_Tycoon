using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Places a rigid car between its two rail-following bogies. Wheels are visual,
    /// while a kinematic body supplies the existing road vehicle physics with a solid obstacle.</summary>
    public sealed class RegionalTrainCar : MonoBehaviour
    {
        public Transform frontBogie, rearBogie;
        public Transform[] wheels = System.Array.Empty<Transform>();
        public GameObject headlights, markerLights;
        public float bogieOffset = 5.65f, wheelRadius = .4f, railHeadOffset = .10f;
        [SerializeField] private bool headlightsOn = true, markerLightsOn;
        private float lastDistance, wheelAngle;
        private bool sampled;
        private Rigidbody body;
        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (headlights != null)
                foreach (var light in headlights.GetComponentsInChildren<Light>(true))
                    light.range *= Mathf.Abs(transform.lossyScale.x);
            SetHeadlights(headlightsOn);
            SetMarkerLights(markerLightsOn);
        }

        public void SetHeadlights(bool enabled)
        {
            headlightsOn = enabled;
            SetLamps(headlights, enabled);
        }

        public void SetMarkerLights(bool enabled)
        {
            markerLightsOn = enabled;
            SetLamps(markerLights, enabled);
        }

        private static void SetLamps(GameObject group, bool enabled)
        {
            if (group == null) return;
            // Turning off a lamp must keep its lens and housing on the train.
            group.SetActive(true);
            foreach (var light in group.GetComponentsInChildren<Light>(true)) light.enabled = enabled;
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in group.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (!material.HasProperty("_EmissionColor")) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_EmissionColor", enabled ? material.GetColor("_EmissionColor") : Color.black);
                renderer.SetPropertyBlock(properties);
            }
        }

        public void PlaceOnRail(TownRailTraffic service, float distance)
        {
            var points = service.layout.railway.points;
            var front = TownRailTraffic.Sample(points, distance + bogieOffset, out var frontDirection);
            var rear = TownRailTraffic.Sample(points, distance - bogieOffset, out var rearDirection);
            var forward = (front - rear).normalized;
            var carRotation = Quaternion.LookRotation(forward);
            var railHead = (front + rear) * .5f + carRotation * Vector3.up * railHeadOffset;
            transform.SetPositionAndRotation(service.transform.TransformPoint(railHead),
                service.transform.rotation * carRotation);
            PlaceBogie(frontBogie, front, frontDirection, service.transform);
            PlaceBogie(rearBogie, rear, rearDirection, service.transform);
            // A new departure resets position. Never spin the wheels backwards across that teleport.
            float moved = sampled && distance >= lastDistance ? distance - lastDistance : 0;
            wheelAngle = Mathf.Repeat(wheelAngle + moved / Mathf.Max(.01f, wheelRadius) * Mathf.Rad2Deg, 360);
            foreach (var wheel in wheels)
                if (wheel != null) wheel.localRotation = Quaternion.AngleAxis(wheelAngle, Vector3.right);
            lastDistance = distance; sampled = true;
            if (body == null) body = GetComponent<Rigidbody>();
            if (body != null) { body.position = transform.position; body.rotation = transform.rotation; }
        }

        private void PlaceBogie(Transform pivot, Vector3 point, Vector3 direction, Transform service)
        {
            if (pivot == null) return;
            var rotation = Quaternion.LookRotation(direction);
            pivot.SetPositionAndRotation(service.TransformPoint(point + rotation * Vector3.up * (railHeadOffset + wheelRadius)),
                service.rotation * rotation);
        }
    }
}
