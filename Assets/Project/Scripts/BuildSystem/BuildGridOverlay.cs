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
        public FloorPainter floors;

        [Header("Material")]
        public Material overlayMaterialTemplate;

        [Header("Visual")]
        // A tiny depth bias prevents flicker; surface elevation comes from the actual floor data.
        [Range(0.001f, 0.2f)] public float yOffset = 0.002f;
        [Range(0.001f, 0.2f)] public float lineWidth = 0.03f;
        public Color gridColor = new Color(0f, 0f, 0f, 0.75f);
        public Color fillColor = Color.clear;
        public int ActiveFloor { get; set; }

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
            if(floors!=null)floors.Changed+=RefreshVisibleMesh;

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
        private void RefreshVisibleMesh(){if(isActiveAndEnabled)RebuildMesh();}

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
                for(int x=rect.min.x;x<=rect.max.x;x++)
                for(int z=rect.min.z;z<=rect.max.z;z++)
                {
                    var cell=new Vector3Int(x,ActiveFloor,z);
                    float height=(floors!=null?floors.SurfaceElevation(cell):grid.FloorElevation(ActiveFloor))+yOffset;

                    Vector3 minWorld = new Vector3(
                        grid.origin.x + x * grid.cellSize,
                        height,
                        grid.origin.z + z * grid.cellSize
                    );

                    Vector3 maxWorld = new Vector3(
                        grid.origin.x + (x + 1) * grid.cellSize,
                        height,
                        grid.origin.z + (z + 1) * grid.cellSize
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
            if(floors!=null)floors.Changed-=RefreshVisibleMesh;
            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
