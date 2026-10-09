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
        [SerializeField] private Material accessMaterial;
        private GameObject _instance, _footprintObject;
        private Renderer[] _renderers = new Renderer[0];
        private Mesh _footprintMesh;
        private MeshRenderer _footprintRenderer;
        private bool? _lastValid;
        private readonly Dictionary<Material, Material> _ghostMaterials = new();
        private readonly Dictionary<Material, Color> _sourceColors = new();
        private const float GhostOpacity = .65f;

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
            _instance.name = "Preview_" + item.name;
            _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in _renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = GhostMaterial(materials[i]);
                renderer.sharedMaterials = materials;
            }
            CreateFootprint();
            SetValid(false);
            SetVisible(false);
        }

        private Material GhostMaterial(Material source)
        {
            if (source == null) return null;
            if (_ghostMaterials.TryGetValue(source, out var existing)) return existing;
            var ghost = new Material(source) { name = source.name + " placement preview" };
            string colorProperty = ghost.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            _sourceColors.Add(ghost, ghost.HasProperty(colorProperty) ? ghost.GetColor(colorProperty) : Color.white);
            // Support the project's Standard materials and URP Lit without replacing their texture/palette.
            if (ghost.HasProperty("_Mode")) ghost.SetFloat("_Mode", 2);
            if (ghost.HasProperty("_Surface")) ghost.SetFloat("_Surface", 1);
            if (ghost.HasProperty("_Blend")) ghost.SetFloat("_Blend", 0);
            if (ghost.HasProperty("_SrcBlend")) ghost.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (ghost.HasProperty("_DstBlend")) ghost.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (ghost.HasProperty("_ZWrite")) ghost.SetFloat("_ZWrite", 0);
            ghost.SetOverrideTag("RenderType", "Transparent");
            ghost.DisableKeyword("_ALPHATEST_ON");
            ghost.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            ghost.EnableKeyword("_ALPHABLEND_ON");
            ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ghost.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _ghostMaterials.Add(source, ghost);
            return ghost;
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

        public void SetModelScale(Vector3 scale)
        {
            if (_instance != null) _instance.transform.localScale = scale;
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
            Color tint = ghost != null ? ghost.color : valid ? Color.green : Color.red;
            foreach (var pair in _sourceColors)
            {
                Color color = Color.Lerp(pair.Value, tint, valid ? .12f : .3f);
                color.a = GhostOpacity;
                var material = pair.Key;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            }
            if (_footprintRenderer != null)
                _footprintRenderer.sharedMaterials = new[] { valid ? validFootprintMaterial : invalidFootprintMaterial, accessMaterial };
        }

        public void ShowPlacement(GridSystem grid, PlacementRequest request, PlacementResult result)
        {
            if (_instance == null || grid == null) return;
            SetVisible(true);
            SetValid(result.ok);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var accessTriangles = new List<int>();
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
            if (request.item.category == BuildCategory.Shelf)
            {
                int sides = request.item.twoSidedAccess ? 2 : 1;
                for (int side = 0; side < sides; side++)
                foreach (var cell in ShelfAccessArea.Cells(grid, request, side))
                {
                    Vector3 center = grid.CellToWorld(cell) + Vector3.up * .118f;
                    int facing = (request.facing + request.item.frontFacing + side * 2) % 4;
                    Vector3 outward = facing == 0 ? Vector3.forward : facing == 1 ? Vector3.right : facing == 2 ? Vector3.back : Vector3.left;
                    Vector3 lateral = Vector3.Cross(Vector3.up, outward);
                    float half = grid.cellSize * .4f;
                    int first = vertices.Count;
                    // Point towards the shelf, showing where customers collect stock.
                    vertices.Add(transform.InverseTransformPoint(center - outward * half));
                    vertices.Add(transform.InverseTransformPoint(center + outward * half + lateral * half));
                    vertices.Add(transform.InverseTransformPoint(center + outward * half - lateral * half));
                    accessTriangles.AddRange(new[] { first, first + 2, first + 1 });
                }
            }
            _footprintMesh.Clear();
            _footprintMesh.SetVertices(vertices);
            _footprintMesh.subMeshCount = 2;
            _footprintMesh.SetTriangles(triangles, 0);
            _footprintMesh.SetTriangles(accessTriangles, 1);
            _footprintMesh.RecalculateNormals();
            _footprintMesh.RecalculateBounds();
        }

        public static string FailureText(PlacementResult result)
        {
            switch (result.reason)
            {
                case PlaceFailReason.NotPurchased: return "За пределами доступной площади";
                case PlaceFailReason.Overlap: return "Место занято";
                case PlaceFailReason.NoAccess: return string.IsNullOrEmpty(result.message) ? "Оставьте проход перед стеллажом" : result.message;
                case PlaceFailReason.InsufficientInventory: return string.IsNullOrEmpty(result.message) ? "Нет предмета на складе" : result.message;
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
            _footprintRenderer.receiveShadows = false;
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
            foreach (var material in _ghostMaterials.Values) Release(material);
            _ghostMaterials.Clear();
            _sourceColors.Clear();
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
