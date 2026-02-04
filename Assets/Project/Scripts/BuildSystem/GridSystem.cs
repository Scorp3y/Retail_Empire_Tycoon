using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyShopGame.BuildSystem
{
    public interface IGridOccupancy
    {
        bool IsOccupied(Vector3Int cell);
        void Occupy(IEnumerable<Vector3Int> cells);
        void Release(IEnumerable<Vector3Int> cells);
    }

    [DisallowMultipleComponent]
    public sealed class GridSystem : MonoBehaviour, IGridOccupancy
    {
        [Header("Grid")]
        [Min(0.1f)]
        public float cellSize = 1f;
        public Vector3 origin;

        private readonly HashSet<Vector3Int> _occupied = new HashSet<Vector3Int>();

        public Vector3Int WorldToCell(Vector3 world)
        {
            var local = world - origin;
            var x = Mathf.FloorToInt(local.x / cellSize);
            var y = Mathf.FloorToInt(local.y / cellSize);
            var z = Mathf.FloorToInt(local.z / cellSize);
            return new Vector3Int(x, y, z);
        }

        public Vector3 CellToWorld(Vector3Int cell)
        {
            return origin + new Vector3(
                (cell.x + 0.5f) * cellSize,
                (cell.y + 0.0f) * cellSize,
                (cell.z + 0.5f) * cellSize
            );
        }

        public IEnumerable<Vector3Int> GetFootprintCells(Vector3Int anchorCell, Vector2Int size, bool rotated)
        {
            var w = rotated ? size.y : size.x;
            var h = rotated ? size.x : size.y;

            for (var dx = 0; dx < w; dx++)
            for (var dz = 0; dz < h; dz++)
                yield return new Vector3Int(anchorCell.x + dx, anchorCell.y, anchorCell.z + dz);
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
