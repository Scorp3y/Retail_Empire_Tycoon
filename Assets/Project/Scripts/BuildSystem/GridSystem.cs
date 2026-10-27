using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.BuildSystem
{
    public interface IGridOccupancy
    {
        bool IsOccupied(Vector3Int cell);
        void Occupy(IEnumerable<Vector3Int> cells);
        void Release(IEnumerable<Vector3Int> cells);
    }

    [DisallowMultipleComponent]
    [MovedFrom(false, "MyShopGame.BuildSystem", null, "GridSystem")]
    public sealed class GridSystem : MonoBehaviour, IGridOccupancy
    {
        [Header("Grid")]
        [Min(0.1f)]
        public float cellSize = 1f;
        public Vector3 origin;
        // Logical Y is a full storey, not a grid-cell height; all construction uses the same proportion contract.
        public const float StoreyHeight = ShopProportions.Scale;
        public const int TopFloor = 1;
        public float FloorElevation(int floor) => origin.y + floor * StoreyHeight;

        private readonly HashSet<Vector3Int> _occupied = new HashSet<Vector3Int>();

        public Vector3Int WorldToCell(Vector3 world)
        {
            var local = world - origin;
            var x = Mathf.FloorToInt(local.x / cellSize);
            var y = Mathf.RoundToInt(local.y / StoreyHeight);
            var z = Mathf.FloorToInt(local.z / cellSize);
            return new Vector3Int(x, y, z);
        }

        public Vector3 CellToWorld(Vector3Int cell)
        {
            return origin + new Vector3(
                (cell.x + 0.5f) * cellSize,
                cell.y * StoreyHeight,
                (cell.z + 0.5f) * cellSize
            );
        }

        public IEnumerable<Vector3Int> GetFootprintCells(Vector3Int anchorCell, Vector2Int size, bool rotated, Vector2Int pivotOffset)
        {
            var w = rotated ? size.y : size.x;
            var h = rotated ? size.x : size.y;

            var ox = rotated ? pivotOffset.y : pivotOffset.x;
            var oz = rotated ? pivotOffset.x : pivotOffset.y;

            for (var dx = 0; dx < w; dx++)
            {
                for (var dz = 0; dz < h; dz++)
                {
                    yield return new Vector3Int(
                        anchorCell.x + ox + dx,
                        anchorCell.y,
                        anchorCell.z + oz + dz
                    );
                }
            }
        }

        public IEnumerable<Vector3Int> GetFootprintCells(Vector3Int anchorCell, Vector2Int size, bool rotated)
        {
            return GetFootprintCells(anchorCell, size, rotated, Vector2Int.zero);
        }

        public bool IsOccupied(Vector3Int cell) => _occupied.Contains(cell);

        public void Occupy(IEnumerable<Vector3Int> cells)
        {
            foreach (var c in cells)
                _occupied.Add(c);
        }

        public void Release(IEnumerable<Vector3Int> cells)
        {
            foreach (var c in cells)
                _occupied.Remove(c);
        }

        public void ClearAll() => _occupied.Clear();
    }
}
