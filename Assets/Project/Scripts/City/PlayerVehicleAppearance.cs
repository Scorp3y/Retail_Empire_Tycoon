using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Exchanges only a parked vehicle's visuals/collision; retains its selectable parking anchor.</summary>
    public sealed class PlayerVehicleAppearance : MonoBehaviour
    {
        private readonly Dictionary<Renderer, bool> originalRenderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Collider, bool> originalColliders = new Dictionary<Collider, bool>();
        private GameObject current;
        private bool captured;
        public void Show(PlayerVehicleDefinition vehicle, float worldScale)
        {
            if (!captured)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>(true)) originalRenderers.Add(renderer, renderer.enabled);
                foreach (var collider in GetComponentsInChildren<Collider>(true)) originalColliders.Add(collider, collider.enabled);
                captured = true;
            }
            if (current != null) { current.SetActive(false); Destroy(current); }
            foreach (var pair in originalRenderers) if (pair.Key != null) pair.Key.enabled = vehicle == null && pair.Value;
            foreach (var pair in originalColliders) if (pair.Key != null) pair.Key.enabled = vehicle == null && pair.Value;
            if (vehicle == null) return;
            current = Instantiate(vehicle.model, transform);
            var scale = transform.lossyScale;
            current.transform.localScale = new Vector3(worldScale / scale.x, worldScale / scale.y, worldScale / scale.z);
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            var body = current.AddComponent<BoxCollider>();
            body.center = new Vector3(0, vehicle.height * .5f + .1f, 0);
            body.size = new Vector3(vehicle.width, vehicle.height - .2f, vehicle.length);
        }
    }
}
