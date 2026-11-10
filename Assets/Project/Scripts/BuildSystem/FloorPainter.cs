using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class FloorPainter : MonoBehaviour
    {
        public const float UpperSurfaceLift=.005f;
        public const float GroundSurfaceLift=.015f;
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
        private readonly Dictionary<Vector3Int, int> _facings = new();
        private readonly Dictionary<Vector3Int, FloorSurfaceChunk> _chunks = new();
        public event System.Action Changed;
        private int visibleThroughLayer = int.MaxValue;
        private RectInt _raisedFloorFootprint;

        // Cell data, not rendered objects, is authoritative for painting, parking and saves.
        public IEnumerable<MeshFilter> AsphaltSurfaces => _chunks.Values.SelectMany(chunk => chunk.AsphaltSurfaces);
        public IEnumerable<MeshFilter> Surfaces => _chunks.Values.SelectMany(chunk=>chunk.Surfaces);
        private GameObject _previewSurface;
        private Mesh _previewMesh;
        private readonly List<Vector3Int> _previewCells = new();
        private readonly List<Vector3> _previewVertices = new();
        private readonly List<int> _previewTriangles = new();
        private readonly List<Vector2> _previewUVs = new();
        private int _previewFacing = -1;
        private bool _previewAsphalt;

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
                    result.Add(new Vector3Int(x, a.y, z));
                }
            }

            return result;
        }

        public bool IsAsphalt(Vector3Int cell) => _tiles.TryGetValue(cell, out var item) && item != null && item.isAsphalt;
        public bool HasSurface(Vector3Int cell) => _tiles.ContainsKey(cell);
        public int OpeningTileCount(PlacementRequest request) => request.item.storeyConnection==RetailEmpireTycoon.Core.StoreyConnectionKind.None
            ? 0 : StoreyLink.OpeningCells(grid,request).Count(HasSurface);

        /// <summary>Called only after placement validation. Preview never removes tiles; each removed material returns to inventory once.</summary>
        public bool TryOpenTransport(PlacementRequest request,BuildInventory inventory)
        {
            if(request.item.storeyConnection==RetailEmpireTycoon.Core.StoreyConnectionKind.None)return true;
            var tiles=StoreyLink.OpeningCells(grid,request).Where(HasSurface).ToList();
            return tiles.Count==0 || RemoveCells(tiles,inventory);
        }
        public void SetRaisedFloorFootprint(RectInt footprint)
        {
            if (_raisedFloorFootprint.Equals(footprint)) return;
            _raisedFloorFootprint = footprint;
            _previewCells.Clear();
            if (_chunks.Count > 0) RebuildChunks(_chunks.Keys.Where(chunk => chunk.y == 0).ToHashSet());
        }

        internal bool IsRaisedFloor(Vector3Int cell) =>
            cell.y == 0 && _raisedFloorFootprint.Contains(new Vector2Int(cell.x, cell.z));

        internal float PaintedSurfaceLift(Vector3Int cell, bool asphalt)
        {
            if (cell.y > 0) return UpperSurfaceLift;
            return !asphalt && IsRaisedFloor(cell) ? surfaceHeight : GroundSurfaceLift;
        }

        public float SurfaceElevation(Vector3Int cell) => grid.FloorElevation(cell.y) +
            (HasSurface(cell) ? PaintedSurfaceLift(cell, IsAsphalt(cell)) : cell.y > 0 ? 0 : GroundSurfaceLift);
        public BuildItemData FirstSurface => _tiles.Values.FirstOrDefault(item=>item!=null);

        public void SetVisibleThroughLayer(int layer)
        {
            visibleThroughLayer = layer;
            foreach (var pair in _chunks) pair.Value.SetVisible(pair.Key.y <= layer);
        }

        public bool AreCellsValid(List<Vector3Int> cells, BuildItemData surface = null)
        {
            if (cells == null || cells.Count == 0)
                return false;
            var parkingCells = new HashSet<Vector3Int>();
            if (surface != null && !surface.isAsphalt)
                foreach (var parking in FindObjectsOfType<PlacedObject>().Where(p=>p.item!=null&&p.item.isParkingSpace))
                {
                    parkingCells.UnionWith(parking.occupiedCells);
                    parkingCells.UnionWith(RetailEmpireTycoon.Parking.Rule_ParkingSurface.ApproachCells(grid,parking));
                }

            var openings = FindObjectsOfType<PlacedObject>().Where(p => p.item != null && p.item.storeyConnection != StoreyConnectionKind.None)
                .SelectMany(p => StoreyLink.OpeningCells(grid,p)).ToHashSet();
            var doorSweeps = surface != null && cells[0].y > 0
                ? FindObjectsOfType<CollectionDoor>().Where(door => door.GetComponent<PlacedObject>() != null)
                    .SelectMany(Rule_DoorClearance.Sweeps).ToArray() : System.Array.Empty<Bounds>();
            if(surface!=null && surface.isAsphalt)
            {
                var affected=cells.ToHashSet();
                if(FindObjectsOfType<PlacedObject>().Any(p=>p.item!=null &&
                    (p.decorMount==DecorMount.Floor && p.occupiedCells.Any(affected.Contains) || DecorPlacement.CeilingSupports(p,affected,grid))))
                    return false;
            }
            foreach (var cell in cells)
            {
                if (cell.y < 0 || cell.y > GridSystem.TopFloor + 1) return false;
                if (territory != null && !(surface != null && surface.isAsphalt || surface==null && IsAsphalt(cell)
                    ? territory.IsParkingCellAllowed(cell) : territory.IsCellPurchased(cell)))
                    return false;
                if (parkingCells.Contains(cell)) return false;
                if (openings.Contains(cell)) return false;
                if (doorSweeps.Any(sweep => Rule_DoorClearance.CoversCell(sweep, grid, cell))) return false;
            }

            return true;
        }

        public void ShowPreview(List<Vector3Int> cells, bool valid, int stripeFacing = -1, bool asphalt = false)
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
            if (_previewFacing == stripeFacing && _previewAsphalt == asphalt && _previewCells.SequenceEqual(cells)) return;
            _previewFacing = stripeFacing;
            _previewAsphalt = asphalt;
            _previewCells.Clear();_previewCells.AddRange(cells);
            _previewVertices.Clear();_previewTriangles.Clear();_previewUVs.Clear();
            float half=grid.cellSize*.48f;
            var stroke = stripeFacing >= 0 ? cells.ToHashSet() : null;
            var edge = FloorPattern.Edge(stripeFacing);
            foreach(var cell in cells)
            {
                Vector3 center = grid.CellToWorld(cell) + Vector3.up * (PaintedSurfaceLift(cell, asphalt) + .005f);
                var neighbor = cell + edge;
                _facings.TryGetValue(neighbor, out int neighborFacing);
                bool showEdge = stroke != null && !stroke.Contains(neighbor) &&
                    !(_tiles.TryGetValue(neighbor, out var item) && item.hasFloorEdgeStripe && neighborFacing == stripeFacing);
                if (showEdge)
                {
                    float x = Mathf.Abs(edge.x);
                    float z = Mathf.Abs(edge.z);
                    AddPreviewQuad(center - (Vector3)edge * (.18f * half), half * (1 - .18f * x), half * (1 - .18f * z));
                    AddPreviewQuad(center + (Vector3)edge * (.88f * half), half * (1 - .88f * x), half * (1 - .88f * z));
                }
                else AddPreviewQuad(center, half, half);
            }
            _previewMesh.Clear();_previewMesh.SetVertices(_previewVertices);_previewMesh.SetUVs(0,_previewUVs);
            _previewMesh.SetTriangles(_previewTriangles,0);_previewMesh.RecalculateNormals();_previewMesh.RecalculateBounds();
        }

        private void AddPreviewQuad(Vector3 center, float halfX, float halfZ)
        {
            int first = _previewVertices.Count;
            _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center + new Vector3(-halfX, 0, -halfZ)));
            _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center + new Vector3(-halfX, 0, halfZ)));
            _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center + new Vector3(halfX, 0, halfZ)));
            _previewVertices.Add(_previewSurface.transform.InverseTransformPoint(center + new Vector3(halfX, 0, -halfZ)));
            _previewTriangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            _previewUVs.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
        }

        private void OnDisable() { ClearPreview(); }
        private void OnDestroy()
        {
            foreach (var chunk in _chunks.Values) chunk.Dispose();
            if (_previewSurface != null) Destroy(_previewSurface);
            if (_previewMesh != null) Destroy(_previewMesh);
        }

        public int TilesRequired(List<Vector3Int> cells, BuildItemData item)
        {
            if (!item.usesMetricFloorPattern) return cells.Count;
            return cells.Distinct().Count(cell => !_tiles.TryGetValue(cell, out var existing) || existing != item);
        }

        public void PaintCells(List<Vector3Int> cells, BuildItemData item, int facing = 0)
        {
            if (grid == null || item == null || item.floorMaterial == null)
                return;
            if (!AreCellsValid(cells, item)) return;

            var dirtyChunks = new HashSet<Vector3Int>();
            foreach (var cell in cells) SetCell(cell, item, facing, dirtyChunks);
            RebuildChunks(dirtyChunks);
        }

        private void SetCell(Vector3Int cell, BuildItemData item, int facing, HashSet<Vector3Int> dirtyChunks)
        {
            facing = item.usesMetricFloorPattern ? FloorPattern.Facing(facing) : 0;
            _tiles.TryGetValue(cell, out var previous);
            _facings.TryGetValue(cell, out int previousFacing);
            if (previous == item && previousFacing == facing) return;
            _tiles[cell] = item;
            if (facing == 0) _facings.Remove(cell);
            else _facings[cell] = facing;
            MarkChanged(cell, dirtyChunks);
        }

        private void MarkChanged(Vector3Int cell, HashSet<Vector3Int> dirtyChunks)
        {
            dirtyChunks.Add(FloorSurfaceChunk.Coordinates(cell));
            // The edge on a neighboring stripe can change across a chunk boundary.
            for (int facing = 0; facing < 4; facing++)
            {
                var neighbor = cell - FloorPattern.Edge(facing);
                _facings.TryGetValue(neighbor, out int neighborFacing);
                if (neighborFacing == facing && _tiles.TryGetValue(neighbor, out var item) && item.hasFloorEdgeStripe)
                    dirtyChunks.Add(FloorSurfaceChunk.Coordinates(neighbor));
            }
        }

        private void RebuildChunks(HashSet<Vector3Int> dirtyChunks)
        {
            foreach (var coordinates in dirtyChunks)
            {
                if (!_chunks.TryGetValue(coordinates, out var chunk))
                {
                    chunk = new FloorSurfaceChunk(coordinates, floorRoot != null ? floorRoot : transform);
                    _chunks.Add(coordinates, chunk);
                }
                chunk.Rebuild(_tiles, _facings, grid, PaintedSurfaceLift);
                chunk.SetVisible(coordinates.y <= visibleThroughLayer);
            }
            Changed?.Invoke();
        }

        public bool RemoveCells(List<Vector3Int> cells, BuildInventory inventory)
        {
            if (!AreCellsValid(cells) || inventory == null) return false;
            var removed = cells.Where(_tiles.ContainsKey).ToHashSet();
            var objects = FindObjectsOfType<PlacedObject>().Where(p => p.item != null).ToArray();
            if (objects.Any(p => p.occupiedCells.Any(removed.Contains))) return false;
            if (objects.Any(p => DecorPlacement.CeilingSupports(p,removed,grid))) return false;
            if(objects.Where(p=>p.item.isParkingSpace).Any(p=>RetailEmpireTycoon.Parking.Rule_ParkingSurface.ApproachCells(grid,p).Any(removed.Contains)))return false;
            if (objects.Where(p => p.item.storeyConnection != StoreyConnectionKind.None)
                .Any(p => StoreyLink.LandingCells(grid, p).Any(removed.Contains))) return false;
            var dirtyChunks = new HashSet<Vector3Int>();
            foreach (var cell in removed)
            {
                inventory.Add(_tiles[cell], 1);
                _tiles.Remove(cell);
                _facings.Remove(cell);
                MarkChanged(cell, dirtyChunks);
            }
            RebuildChunks(dirtyChunks);
            return true;
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
                    floor = cell.y,
                    z = cell.z,
                    facing = _facings.TryGetValue(cell, out int facing) ? facing : 0
                });
            }

            return result;
        }

        public void ApplySaveData(List<FloorTileSaveData> data, BuildItemCatalog catalog)
        {
            ClearPlacedFloors();

            if (data == null || catalog == null || grid == null)
                return;

            var dirtyChunks = new HashSet<Vector3Int>();
            foreach (var group in data.GroupBy(d => d.itemId))
            {
                var item = catalog.GetById(group.Key);
                if (item == null || item.floorMaterial == null)
                    continue;
                // Restoring authored/saved ground is not a request to build. Public starter
                // paving and older custom floors must survive reduced construction rights,
                // just as saved furniture does. Interactive PaintCells still checks ownership.
                foreach (var tile in group)
                    if (tile.floor >= 0 && tile.floor <= GridSystem.TopFloor + 1)
                        SetCell(new Vector3Int(tile.x, tile.floor, tile.z), item, tile.facing, dirtyChunks);
            }
            RebuildChunks(dirtyChunks);
        }

        private void ClearPlacedFloors()
        {
            foreach (var chunk in _chunks.Values) chunk.Dispose();
            _chunks.Clear();
            _tiles.Clear();
            _facings.Clear();

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
