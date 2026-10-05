using System.Collections.Generic;
using RetailEmpireTycoon.Core;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Visual-only placement ghost and occupied cells; no gameplay components or HUD.</summary>
    [DisallowMultipleComponent]
    [MovedFrom(false, "MyShopGame.BuildSystem", null, "BuildPreview")]
    public sealed class BuildPreview : MonoBehaviour
    {
        [SerializeField] private Material validGhostMaterial;
        [SerializeField] private Material invalidGhostMaterial;
        [SerializeField] private Material validFootprintMaterial;
        [SerializeField] private Material invalidFootprintMaterial;
        private GameObject _instance, _footprintObject;
        private Renderer[] _renderers = new Renderer[0];
        private Mesh _footprintMesh;
        private MeshRenderer _footprintRenderer;
        private bool? _lastValid;

        public void SetItem(BuildItemData item)
        {
            Clear();
            if (item == null || item.prefab == null) return;
            // Copy only static visual geometry. Even a disabled gameplay script
            // can run Awake, so a preview must not instantiate gameplay components.
            _instance = CopyVisualTree(item.prefab.transform, transform);
            // Instantiate(prefab, worldPosition, rotation) also replaces the root pose.
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            // Instantiate(prefab, worldPosition, rotation) also replaces the root pose.
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            _instance.name = "Preview_" + item.name;
            _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in _renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            CreateFootprint();
            SetValid(false);
            SetVisible(false);
        }

        private static GameObject CopyVisualTree(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name);
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            copy.SetActive(source.gameObject.activeSelf);
            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var visual = copy.AddComponent<MeshRenderer>();
                visual.sharedMaterials = renderer.sharedMaterials;
                visual.enabled = renderer.enabled;
            }
            foreach (Transform child in source) CopyVisualTree(child, copy.transform);
            return copy;
        }

        public void SetPose(Vector3 worldPos, Quaternion rotation)
        {
            if (_instance == null) return;
            transform.SetPositionAndRotation(worldPos, rotation);
        }

        public void SetVisible(bool visible)
        {
            if (_instance != null) _instance.SetActive(visible);
            if (_footprintObject != null) _footprintObject.SetActive(visible);
        }

        public void SetValid(bool valid)
        {
            if (_lastValid == valid) return;
            _lastValid = valid;
            Material ghost = valid ? validGhostMaterial : invalidGhostMaterial;
            foreach (var renderer in _renderers)
            {
                if (renderer == null || ghost == null) continue;
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = ghost;
                renderer.sharedMaterials = materials;
            }
            if (_footprintRenderer != null)
                _footprintRenderer.sharedMaterial = valid ? validFootprintMaterial : invalidFootprintMaterial;
        }

        public void ShowPlacement(GridSystem grid, PlacementRequest request, PlacementResult result)
        {
            if (_instance == null || grid == null) return;
            SetVisible(true);
            SetValid(result.ok);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var cell in grid.GetFootprintCells(request.anchorCell, request.item.footprint, request.rotated, request.item.pivotOffset))
            {
                Vector3 center = grid.CellToWorld(cell) + Vector3.up * 0.115f;
                float half = grid.cellSize * 0.47f;
                int first = vertices.Count;
                vertices.Add(transform.InverseTransformPoint(center + new Vector3(-half, 0f, -half)));
                vertices.Add(transform.InverseTransformPoint(center + new Vector3(-half, 0f, half)));
                vertices.Add(transform.InverseTransformPoint(center + new Vector3(half, 0f, half)));
                vertices.Add(transform.InverseTransformPoint(center + new Vector3(half, 0f, -half)));
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            _footprintMesh.Clear();
            _footprintMesh.SetVertices(vertices);
            _footprintMesh.SetTriangles(triangles, 0);
            _footprintMesh.RecalculateBounds();
        }

        public static string FailureText(PlacementResult result)
        {
            switch (result.reason)
            {
                case PlaceFailReason.NotPurchased: return "За пределами доступной площади";
                case PlaceFailReason.Overlap: return "Место занято";
                case PlaceFailReason.NoAccess: return "Нет подхода к объекту";
                case PlaceFailReason.InsufficientInventory: return "Нет предмета на складе";
                default: return string.IsNullOrWhiteSpace(result.message) ? "Нельзя поставить здесь" : result.message;
            }
        }

        private void CreateFootprint()
        {
            _footprintObject = new GameObject("Placement footprint", typeof(MeshFilter), typeof(MeshRenderer));
            _footprintObject.transform.SetParent(transform, false);
            _footprintMesh = new Mesh { name = "PlacementFootprint" };
            _footprintObject.GetComponent<MeshFilter>().sharedMesh = _footprintMesh;
            _footprintRenderer = _footprintObject.GetComponent<MeshRenderer>();
            _footprintRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Clear()
        {
            if (_instance != null) { _instance.SetActive(false); Release(_instance); }
            if (_footprintObject != null) { _footprintObject.SetActive(false); Release(_footprintObject); }
            if (_footprintMesh != null) Release(_footprintMesh);
            _instance = _footprintObject = null;
            _footprintMesh = null;
            _footprintRenderer = null;
            _renderers = new Renderer[0];
            _lastValid = null;
        }

        private void OnDisable() { SetVisible(false); }
        private void OnDestroy() { Clear(); }

        private static void Release(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
