using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Shelves
{
    [DisallowMultipleComponent]
    public sealed class ShelfHighlight : MonoBehaviour
    {
        [Header("Bounds Source")]
        [SerializeField] private Collider boundsCollider;

        [Header("Tube Visual")]
        [SerializeField] private Material tubeMaterial;
        [SerializeField, Min(0.005f)] private float tubeRadius = 0.025f;
        [SerializeField, Min(0f)] private float padding = 0.08f;
        [SerializeField, Min(0f)] private float bottomOffset = 0.04f;
        [SerializeField, Min(0f)] private float topOffset = 0.10f;

        [Header("Color")]
        [SerializeField] private Color color = new Color(0f, 1f, 0.15f, 0.7f);

        private readonly List<GameObject> _tubes = new List<GameObject>(12);

        private Material _runtimeMaterial;
        private bool _built;

        private void Awake()
        {
            if (boundsCollider == null)
                boundsCollider = GetComponent<Collider>();

            BuildIfNeeded();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            BuildIfNeeded();
            Rebuild();

            SetObjectsVisible(_tubes, visible);
        }

        private void BuildIfNeeded()
        {
            if (_built)
                return;

            _runtimeMaterial = tubeMaterial != null ? tubeMaterial : CreateDefaultMaterial();

            CreateTubes();

            _built = true;
        }

        private void CreateTubes()
        {
            for (int i = 0; i < 12; i++)
            {
                var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tube.name = "Highlight_Tube_" + i;
                tube.transform.SetParent(transform, true);

                RemoveCollider(tube);
                ApplyMaterial(tube);

                _tubes.Add(tube);
            }
        }

        private void Rebuild()
        {
            Bounds bounds = GetBounds();

            float minX = bounds.min.x - padding;
            float maxX = bounds.max.x + padding;
            float minZ = bounds.min.z - padding;
            float maxZ = bounds.max.z + padding;

            float bottomY = bounds.min.y + bottomOffset;
            float topY = bounds.max.y + topOffset;

            Vector3 b0 = new Vector3(minX, bottomY, minZ);
            Vector3 b1 = new Vector3(maxX, bottomY, minZ);
            Vector3 b2 = new Vector3(maxX, bottomY, maxZ);
            Vector3 b3 = new Vector3(minX, bottomY, maxZ);

            Vector3 t0 = new Vector3(minX, topY, minZ);
            Vector3 t1 = new Vector3(maxX, topY, minZ);
            Vector3 t2 = new Vector3(maxX, topY, maxZ);
            Vector3 t3 = new Vector3(minX, topY, maxZ);

            SetTube(0, b0, b1);
            SetTube(1, b1, b2);
            SetTube(2, b2, b3);
            SetTube(3, b3, b0);

            SetTube(4, t0, t1);
            SetTube(5, t1, t2);
            SetTube(6, t2, t3);
            SetTube(7, t3, t0);

            SetTube(8, b0, t0);
            SetTube(9, b1, t1);
            SetTube(10, b2, t2);
            SetTube(11, b3, t3);

        }

        private void SetTube(int index, Vector3 start, Vector3 end)
        {
            if (index < 0 || index >= _tubes.Count)
                return;

            var tube = _tubes[index];
            if (tube == null)
                return;

            // Bounds are world-space, but the primitives inherit the shelf scale.
            // Build the segment in parent-local space so its ends still meet the
            // corners on small prefabs and on rotated/non-uniformly scaled shelves.
            Vector3 localStart = transform.InverseTransformPoint(start);
            Vector3 localEnd = transform.InverseTransformPoint(end);
            Vector3 direction = localEnd - localStart;
            float length = direction.magnitude;

            if (length <= 0.001f)
                return;

            tube.transform.localPosition = localStart + direction * 0.5f;
            tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            tube.transform.localScale = new Vector3(tubeRadius, length * 0.5f, tubeRadius);
        }

        private Bounds GetBounds()
        {
            return boundsCollider != null
                ? boundsCollider.bounds
                : new Bounds(transform.position, Vector3.one);
        }

        private void ApplyMaterial(GameObject target)
        {
            var renderer = target.GetComponent<Renderer>();

            if (renderer != null)
                renderer.sharedMaterial = _runtimeMaterial;
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();

            if (collider != null)
                Destroy(collider);
        }

        private static void SetObjectsVisible(List<GameObject> objects, bool visible)
        {
            foreach (var obj in objects)
            {
                if (obj != null)
                    obj.SetActive(visible);
            }
        }

        private Material CreateDefaultMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            var material = new Material(shader);
            material.color = color;
            return material;
        }
    }
}
