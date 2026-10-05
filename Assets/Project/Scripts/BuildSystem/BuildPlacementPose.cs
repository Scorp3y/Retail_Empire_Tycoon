using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>One pose calculation for preview, placement and loading. Does not resize models.</summary>
    public static class BuildPlacementPose
    {
        public static Vector3 Position(GridSystem grid, BuildItemData item, Vector3Int anchor, bool rotated, int facing)
        {
            Vector3 position = grid.CellToWorld(anchor);
            if (!item.alignModelToFootprint) return position;
            int width = rotated ? item.footprint.y : item.footprint.x;
            int depth = rotated ? item.footprint.x : item.footprint.y;
            Vector2Int offset = rotated ? new Vector2Int(item.pivotOffset.y, item.pivotOffset.x) : item.pivotOffset;
            position += new Vector3(offset.x + (width - 1) * 0.5f, 0f, offset.y + (depth - 1) * 0.5f) * grid.cellSize;
            Vector3 center = item.placementBounds.center;
            center.y = 0f;
            return position - Quaternion.Euler(0f, facing * 90f, 0f) * center;
        }
    }
}
