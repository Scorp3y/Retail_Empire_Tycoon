using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class FloorPainter : MonoBehaviour
    {
        public GridSystem grid;
        public TerritoryManager territory;

        [Header("Floor Tile Visual")]
        public GameObject floorTilePrefab;
        public Transform floorRoot;
        public float surfaceHeight = .096f;

        [Header("Preview")]
        public Transform previewRoot;
        public Material previewValidMaterial;
        public Material previewInvalidMaterial;

        private readonly Dictionary<Vector3Int, BuildItemData> _tiles = new();
        private readonly Dictionary<Vector2Int, FloorSurfaceChunk> _chunks = new();

        // Cell data, not rendered objects, is authoritative for painting, parking and saves.
        public IEnumerable<MeshFilter> AsphaltSurfaces => _chunks.Values.SelectMany(chunk => chunk.AsphaltSurfaces);
        private GameObject _previewSurface;
        private Mesh _previewMesh;
        private readonly List<Vector3Int> _previewCells = new();
        private readonly List<Vector3> _previewVertices = new();
        private readonly List<int> _previewTriangles = new();
        private readonly List<Vector2> _previewUVs = new();

        public void ClearPreview()
        {
            if (_previewSurface != null) _previewSurface.SetActive(false);
        }

        public List<Vector3Int> GetRectCells(Vector3Int a, Vector3Int b)
        {
            var result = new List<Vector3Int>();

            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minZ = Mathf.Min(a.z, b.z);
            int maxZ = Mathf.Max(a.z, b.z);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    result.Add(new Vector3Int(x, 0, z));
                }
            }

            return result;
        }

        public bool IsAsphalt(Vector3Int cell) => _tiles.TryGetValue(cell, out var item) && item != null && item.isAsphalt;

        public bool AreCellsValid(List<Vector3Int> cells, BuildItemData surface = null)
        {
            if (cells == null || cells.Count == 0)
                return false;
            var parkingCells = new HashSet<Vector3Int>();
            if (surface != null && !surface.isAsphalt)
                foreach (var parking in FindObjectsOfType<PlacedObject>().Where(p=>p.item!=null&&p.item.isParkingSpace))
                {
                    parkingCells.UnionWith(parking.occupiedCells);
                    parkingCells.UnionWith(RetailEmpireTycoon.Parking.Rule_ParkingSurface.ApproachCells(grid,parking.item,parking.anchorCell,parking.rotated,parking.facing));
                }

            foreach (var cell in cells)
            {
                if (territory != null && !territory.IsCellPurchased(cell))
                    return false;
                if (parkingCells.Contains(cell)) return false;
            }

            return true;
        }

        public void ShowPreview(List<Vector3Int> cells, bool valid)
        {
            if (grid == null || cells == null || cells.Count == 0) { ClearPreview(); return; }
            if (_previewSurface == null)
            {
                _previewCells.Clear();
                _previewSurface = new GameObject("Floor brush preview",typeof(MeshFilter),typeof(MeshRenderer));
                _previewSurface.transform.SetParent(previewRoot != null ? previewRoot : transform,false);
                _previewMesh = new Mesh {name="FloorBrushPreview",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                _previewSurface.GetComponent<MeshFilter>().sharedMesh=_previewMesh;
                var renderer=_previewSurface.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=false;
            }
            _previewSurface.SetActive(true);
            _previewSurface.GetComponent<MeshRenderer>().sharedMaterial=valid?previewValidMaterial:previewInvalidMaterial;
            if (_previewCells.SequenceEqual(cells)) return;
            _previewCells.Clear();_previewCells.AddRange(cells);
            _previewVertices.Clear();_previewTriangles.Clear();_previewUVs.Clear();
            float half=grid.cellSize*.48f;
            foreach(var cell in cells)
            {
                // Above the imported store floor at 0.096, independent of its authored prefab origin.
                Vector3 center=grid.CellToWorld(cell)+Vector3.up*.125f;
                int first=_previewVertices.Count;
                _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center+new Vector3(-half,0,-half)));
                _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center+new Vector3(-half,0,half)));
                _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center+new Vector3(half,0,half)));
                _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center+new Vector3(half,0,-half)));
                _previewTriangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                _previewUVs.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});
            }
            _previewMesh.Clear();_previewMesh.SetVertices(_previewVertices);_previewMesh.SetUVs(0,_previewUVs);
            _previewMesh.SetTriangles(_previewTriangles,0);_previewMesh.RecalculateNormals();_previewMesh.RecalculateBounds();
        }

        private void OnDisable() { ClearPreview(); }
        private void OnDestroy()
        {
            foreach (var chunk in _chunks.Values) chunk.Dispose();
            if (_previewSurface != null) Destroy(_previewSurface);
            if (_previewMesh != null) Destroy(_previewMesh);
        }

        public void PaintCells(List<Vector3Int> cells, BuildItemData item)
        {
            if (grid == null || item == null || item.floorMaterial == null)
                return;
            if (!AreCellsValid(cells, item)) return;

            var dirtyChunks = new HashSet<Vector2Int>();
            foreach (var cell in cells)
            {
                if (_tiles.TryGetValue(cell, out var previous) && previous == item) continue;
                _tiles[cell] = item;
                dirtyChunks.Add(FloorSurfaceChunk.Coordinates(cell));
            }
            foreach (var coordinates in dirtyChunks)
            {
                if (!_chunks.TryGetValue(coordinates, out var chunk))
                {
                    chunk = new FloorSurfaceChunk(coordinates, floorRoot != null ? floorRoot : transform);
                    _chunks.Add(coordinates, chunk);
                }
                chunk.Rebuild(_tiles, grid, surfaceHeight);
            }
        }

        public List<FloorTileSaveData> BuildSaveData()
        {
            var result = new List<FloorTileSaveData>();

            foreach (var pair in _tiles)
            {
                var cell = pair.Key;
                var item = pair.Value;
                if (item == null) continue;

                result.Add(new FloorTileSaveData
                {
                    itemId = item.id,
                    x = cell.x,
                    z = cell.z
                });
            }

            return result;
        }

        public void ApplySaveData(List<FloorTileSaveData> data, BuildItemCatalog catalog)
        {
            ClearPlacedFloors();

            if (data == null || catalog == null)
                return;

            foreach (var group in data.GroupBy(d => d.itemId))
            {
                var item = catalog.GetById(group.Key);
                if (item == null)
                    continue;
                var cells = group.Select(d => new Vector3Int(d.x, 0, d.z)).ToList();
                if (AreCellsValid(cells, item)) PaintCells(cells, item);
                else foreach (var cell in cells) PaintCells(new List<Vector3Int> { cell }, item);
            }
        }

        private void ClearPlacedFloors()
        {
            foreach (var chunk in _chunks.Values) chunk.Dispose();
            _chunks.Clear();
            _tiles.Clear();

            if (floorRoot != null)
            {
                for (int i = floorRoot.childCount - 1; i >= 0; i--)
                {
                    floorRoot.GetChild(i).gameObject.SetActive(false);
                    Destroy(floorRoot.GetChild(i).gameObject);
                }
            }
        }

    }


}
