using System.Collections.Generic;
using RetailEmpireTycoon.Territory;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class BuildGridOverlay : MonoBehaviour
    {
        [Header("Refs")]
        public GridSystem grid;
        public TerritoryManager territory;

        [Header("Material")]
        public Material overlayMaterialTemplate;

        [Header("Visual")]
        // The existing shop mesh has its floor at 0.096; the logical grid remains at zero.
        [Range(0.001f, 0.2f)] public float yOffset = 0.11f;
        [Range(0.001f, 0.2f)] public float lineWidth = 0.03f;
        public Color gridColor = new Color(0f, 0f, 0f, 0.75f);
        public Color fillColor = Color.clear;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _runtimeMaterial;

        private static readonly int GridColorId = Shader.PropertyToID("_GridColor");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int WorldOriginId = Shader.PropertyToID("_WorldOrigin");

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (overlayMaterialTemplate != null)
            {
                _runtimeMaterial = new Material(overlayMaterialTemplate);
                _meshRenderer.sharedMaterial = _runtimeMaterial;
            }

            gameObject.SetActive(false);
        }

        public void Show()
        {
            if (grid == null || territory == null || territory.PurchasedRects == null || territory.PurchasedRects.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            RebuildMesh();

            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.SetColor(GridColorId, gridColor);
                _runtimeMaterial.SetColor(FillColorId, fillColor);
                _runtimeMaterial.SetFloat(CellSizeId, grid.cellSize);
                _runtimeMaterial.SetFloat(LineWidthId, lineWidth);
                _runtimeMaterial.SetVector(WorldOriginId, new Vector4(grid.origin.x, 0f, grid.origin.z, 0f));
            }
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RebuildMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh();
                _mesh.name = "BuildGridOverlayMesh";
            }
            else
            {
                _mesh.Clear();
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            foreach (var rect in territory.PurchasedRects)
            {
                if (rect == null)
                    continue;

                Vector3 minWorld = new Vector3(
                    grid.origin.x + rect.min.x * grid.cellSize,
                    grid.origin.y + yOffset,
                    grid.origin.z + rect.min.z * grid.cellSize
                );

                Vector3 maxWorld = new Vector3(
                    grid.origin.x + (rect.max.x + 1) * grid.cellSize,
                    grid.origin.y + yOffset,
                    grid.origin.z + (rect.max.z + 1) * grid.cellSize
                );

                int index = vertices.Count;

                vertices.Add(transform.InverseTransformPoint(new Vector3(minWorld.x, minWorld.y, minWorld.z)));
                vertices.Add(transform.InverseTransformPoint(new Vector3(minWorld.x, minWorld.y, maxWorld.z)));
                vertices.Add(transform.InverseTransformPoint(new Vector3(maxWorld.x, maxWorld.y, maxWorld.z)));
                vertices.Add(transform.InverseTransformPoint(new Vector3(maxWorld.x, maxWorld.y, minWorld.z)));

                triangles.Add(index + 0);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
                triangles.Add(index + 0);
                triangles.Add(index + 2);
                triangles.Add(index + 3);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
            }

            _mesh.SetVertices(vertices);
            _mesh.SetTriangles(triangles, 0);
            _mesh.SetUVs(0, uvs);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
