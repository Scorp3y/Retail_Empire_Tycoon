using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public sealed class TownWorldPlaces : MonoBehaviour
    {
        public TownWorldLayout layout;
        public DeliveryCheckpoint[] suppliers=Array.Empty<DeliveryCheckpoint>();
        // Additional entrances share the showroom fleet; the town map keeps one destination per supplier.
        public DeliveryCheckpoint[] showroomEntrances=Array.Empty<DeliveryCheckpoint>();
        private Mesh sidewalkSource, croppedSidewalk;
        private float sidewalkCut;

        internal void SetStarterSidewalk(float? westInShop)
        {
            var filter = transform.Find("Player entrance sidewalk trim")?.GetComponent<MeshFilter>();
            if (filter == null || layout == null) return;
            sidewalkSource ??= filter.sharedMesh;
            if (!westInShop.HasValue)
            {
                filter.sharedMesh = sidewalkSource;
                return;
            }

            var native = layout.MapShopPoint(new Vector3(westInShop.Value, 0, 0));
            float cut = filter.transform.InverseTransformPoint(transform.TransformPoint(native)).x;
            if (cut <= sidewalkSource.bounds.min.x || cut >= sidewalkSource.bounds.max.x) return;
            if (croppedSidewalk == null || !Mathf.Approximately(sidewalkCut, cut))
            {
                if (croppedSidewalk != null) Destroy(croppedSidewalk);
                sidewalkCut = cut;
                croppedSidewalk = Instantiate(sidewalkSource);
                var points = sidewalkSource.vertices;
                var surfaceUv = new List<Vector2>();
                sidewalkSource.GetUVs(1, surfaceUv);
                // This authored sidewalk is a single rectangle. Preserve world texture coordinates as its west edge moves.
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i].x >= cut) continue;
                    if (surfaceUv.Count == points.Length) surfaceUv[i] += Vector2.right * (cut - points[i].x);
                    points[i].x = cut;
                }
                croppedSidewalk.vertices = points;
                if (surfaceUv.Count == points.Length) croppedSidewalk.SetUVs(1, surfaceUv);
                croppedSidewalk.RecalculateBounds();
            }
            filter.sharedMesh = croppedSidewalk;
        }

        private void OnDestroy() { if (croppedSidewalk != null) Destroy(croppedSidewalk); }

        // Clear aisle between the two pump islands in the existing fuel forecourt.
        private static readonly Rect FuelAisle = Rect.MinMaxRect(221.5f, -121f, 226.5f, -106f);
        internal static Vector3 FuelStop => new Vector3(224f, 0, -114f);
        internal static Vector3[] FuelAccess() => new[]
        {
            new Vector3(228, 0, -144), new Vector3(228, 0, -130),
            new Vector3(224, 0, -123), FuelStop
        };

        internal bool IsAtFuelPump(PickupDrive vehicle)
        {
            var p = transform.InverseTransformPoint(vehicle.transform.position);
            return p.y >= -.5f && p.y < 2f && FuelAisle.Contains(new Vector2(p.x, p.z));
        }
    }
}
