using UnityEngine;

namespace RetailEmpireTycoon.Shelves
{
    /// <summary>Usable space above a tray, measured in the slot's local coordinates.</summary>
    [DisallowMultipleComponent]
    public sealed class ShelfProductSlot : MonoBehaviour
    {
        public Vector3 usableSize = new Vector3(.12f, .15f, .20f);

        public void Seat(GameObject product)
        {
            var filters = product.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return;
            var bounds = new Bounds();
            bool first = true;
            foreach (var filter in filters)
            {
                // Mesh-space corners avoid the enlarged world AABB when the rack is rotated.
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.center + Vector3.Scale(mesh.extents, new Vector3(x,y,z))));
                    if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; }
                    else bounds.Encapsulate(corner);
                }
            }
            float fit = Mathf.Min(1, usableSize.x / Mathf.Max(.0001f, bounds.size.x),
                usableSize.y / Mathf.Max(.0001f, bounds.size.y), usableSize.z / Mathf.Max(.0001f, bounds.size.z));
            product.transform.localScale *= fit;
            product.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * fit;
        }
    }
}
