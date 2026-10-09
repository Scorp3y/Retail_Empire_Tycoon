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
            return AlignedPosition(grid, anchor, rotated, facing, item.footprint, item.pivotOffset, item.placementBounds)
                + Quaternion.Euler(0f, facing * 90f, 0f) * item.placementAlignmentOffset;
        }

        public static Vector3 AlignedPosition(GridSystem grid, Vector3Int anchor, bool rotated, int facing,
            Vector2Int footprint, Vector2Int pivotOffset, Bounds bounds)
        {
            Vector3 position = grid.CellToWorld(anchor);
            int width = rotated ? footprint.y : footprint.x;
            int depth = rotated ? footprint.x : footprint.y;
            Vector2Int offset = rotated ? new Vector2Int(pivotOffset.y, pivotOffset.x) : pivotOffset;
            position += new Vector3(offset.x + (width - 1) * 0.5f, 0f, offset.y + (depth - 1) * 0.5f) * grid.cellSize;
            Vector3 center = bounds.center;
            center.y = 0f;
            return position - Quaternion.Euler(0f, facing * 90f, 0f) * center;
        }
    }
}
