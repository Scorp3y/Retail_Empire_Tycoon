using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>One pose calculation for preview, placement and loading. Does not resize models.</summary>
    public static class BuildPlacementPose
    {
        public static bool TryPointerAnchor(GridSystem grid, FloorPainter floors, BuildItemData item,
            Ray ray, int floor, bool ceiling, bool centerFootprint, bool rotated, out Vector3Int cell)
        {
            cell = default;
            var plane = new Plane(Vector3.up, Vector3.up * grid.FloorElevation(floor));
            if (!plane.Raycast(ray, out float distance) || distance > 2000f) return false;
            var hit = PointerSurface(grid, floors, ray, floor, ceiling);
            if (centerFootprint && item.alignModelToFootprint)
            {
                var offset = FootprintCenter(item.footprint, item.pivotOffset, rotated) * grid.cellSize;
                hit -= new Vector3(offset.x, 0, offset.y);
            }
            cell = grid.WorldToCell(hit);
            cell.y = floor;
            return true;
        }

        private static Vector3 PointerSurface(GridSystem grid, FloorPainter floors, Ray ray, int floor, bool ceiling)
        {
            Vector3 Hit(float elevation)
            {
                var plane = new Plane(Vector3.up, Vector3.up * elevation);
                return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin;
            }

            if (ceiling)
            {
                var upper = Hit(grid.FloorElevation(floor + 1) + FloorPainter.UpperSurfaceLift);
                var cell = grid.WorldToCell(upper);
                cell.y = floor + 1;
                if (floors != null && floors.HasSurface(cell) && !floors.IsAsphalt(cell)) return upper;
            }

            var level = grid.FloorElevation(floor);
            if (floors == null) return Hit(level);
            // Check each possible visible elevation before the base plane. The ray can cross different
            // grid cells at the raised floor and ground heights when the camera is tilted.
            var raised = Hit(level + floors.PaintedSurfaceLift(new Vector3Int(0, floor, 0), true));
            if (floor == 0)
            {
                var indoor = Hit(level + floors.surfaceHeight);
                var indoorCell = grid.WorldToCell(indoor);
                indoorCell.y = floor;
                if (Mathf.Approximately(floors.SurfaceElevation(indoorCell), indoor.y)) return indoor;
            }
            var raisedCell = grid.WorldToCell(raised);
            raisedCell.y = floor;
            return Mathf.Approximately(floors.SurfaceElevation(raisedCell), raised.y) ? raised : Hit(level);
        }

        private static Vector2 FootprintCenter(Vector2Int footprint, Vector2Int pivotOffset, bool rotated)
        {
            var size = rotated ? new Vector2Int(footprint.y, footprint.x) : footprint;
            var offset = rotated ? new Vector2Int(pivotOffset.y, pivotOffset.x) : pivotOffset;
            return (Vector2)offset + ((Vector2)size - Vector2.one) * .5f;
        }

        public static Vector3 Position(GridSystem grid, BuildItemData item, Vector3Int anchor, bool rotated, int facing)
        {
            Vector3 position = grid.CellToWorld(anchor);
            if (!item.alignModelToFootprint) return position;
            return AlignedPosition(grid, anchor, rotated, facing, item.footprint, item.pivotOffset, item.placementBounds)
                + Quaternion.Euler(0f, facing * 90f, 0f) * item.placementAlignmentOffset
                // Parking paint must sit above the editable asphalt (0.015 m), not inside it.
                + (item.isParkingSpace ? Vector3.up * .01f : Vector3.zero);
        }

        public static Vector3 AlignedPosition(GridSystem grid, Vector3Int anchor, bool rotated, int facing,
            Vector2Int footprint, Vector2Int pivotOffset, Bounds bounds)
        {
            Vector3 position = grid.CellToWorld(anchor);
            var offset = FootprintCenter(footprint, pivotOffset, rotated);
            position += new Vector3(offset.x, 0f, offset.y) * grid.cellSize;
            Vector3 center = bounds.center;
            center.y = 0f;
            return position - Quaternion.Euler(0f, facing * 90f, 0f) * center;
        }
    }
}
