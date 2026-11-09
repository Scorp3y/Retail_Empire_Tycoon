using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Reserves the space actually swept by a door when a door or adjacent fixture is placed.</summary>
    public sealed class Rule_DoorClearance : IPlacementRule
    {
        private readonly GridSystem grid;
        private readonly FloorPainter floors;
        public Rule_DoorClearance(GridSystem grid, FloorPainter floors) { this.grid = grid; this.floors = floors; }
        public bool EnabledFor(BuildItemData item) => item != null && item.placementKind != PlacementKind.Floor;

        public PlacementResult Evaluate(PlacementRequest request)
        {
            var item = request.item;
            var origin = BuildPlacementPose.Position(grid, item, request.anchorCell, request.rotated, request.facing);
            var rotation = Quaternion.Euler(0, request.facing * 90, 0);
            var bounds = WorldBounds(item.placementBounds, origin, rotation);
            var door = item.prefab != null ? item.prefab.GetComponent<CollectionDoor>() : null;
            if (door != null && door.AllowsVehicles && request.anchorCell.y != 0)
                return PlacementResult.Fail(PlaceFailReason.NoAccess, "Vehicle gates must be placed on the ground floor.");
            var newSweeps = door == null ? System.Array.Empty<Bounds>() :
                door.MotionClearances.Select(sweep => WorldBounds(sweep, origin, rotation)).ToArray();
            if (floors != null && newSweeps.Any(sweep => IntersectsUpperFloor(sweep, floors)))
                return Blocked();

            foreach (var placed in Object.FindObjectsOfType<PlacedObject>())
            {
                if (placed == request.ignoredObject || placed.item == null) continue;
                var placementBounds = placed.item.HasVersionedWallGeometry
                    ? placed.item.WallBounds(placed.wallModuleVersion) : placed.item.placementBounds;
                var other = WorldBounds(placementBounds, placed.transform.position, placed.transform.rotation);
                if (newSweeps.Any(sweep => Overlaps(sweep, other))) return Blocked();
                if (!placed.TryGetComponent<CollectionDoor>(out var existing)) continue;
                foreach (var sweep in Sweeps(existing))
                    if (Overlaps(sweep, bounds) || newSweeps.Any(candidate => Overlaps(sweep, candidate))) return Blocked();
            }
            return PlacementResult.Success();
        }

        public static IEnumerable<Bounds> Sweeps(CollectionDoor door) => door.MotionClearances.Select(sweep =>
            WorldBounds(sweep, door.transform.position, door.transform.rotation));

        public static bool CoversCell(Bounds sweep, GridSystem grid, Vector3Int cell)
        {
            var center = grid.CellToWorld(cell) + Vector3.up * FloorPainter.UpperSurfaceLift;
            return Overlaps(sweep, new Bounds(center, new Vector3(grid.cellSize, .035f, grid.cellSize)));
        }

        private bool IntersectsUpperFloor(Bounds sweep, FloorPainter floor)
        {
            var min = grid.WorldToCell(sweep.min);
            var max = grid.WorldToCell(sweep.max);
            for (int layer = 1; layer <= GridSystem.TopFloor + 1; layer++)
            for (int x = min.x; x <= max.x; x++)
            for (int z = min.z; z <= max.z; z++)
            {
                var cell = new Vector3Int(x, layer, z);
                if (floor.HasSurface(cell) && CoversCell(sweep, grid, cell)) return true;
            }
            return false;
        }

        private static bool Overlaps(Bounds a, Bounds b)
        {
            var overlap = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
            return overlap.x > .012f && overlap.y > .008f && overlap.z > .012f;
        }

        private static Bounds WorldBounds(Bounds local, Vector3 origin, Quaternion rotation)
        {
            var size = local.extents;
            Vector3 Abs(Vector3 vector) => new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
            var extent = Abs(rotation * new Vector3(size.x, 0, 0)) + Abs(rotation * new Vector3(0, size.y, 0)) +
                Abs(rotation * new Vector3(0, 0, size.z));
            return new Bounds(origin + rotation * local.center, extent * 2);
        }

        private static PlacementResult Blocked() =>
            PlacementResult.Fail(PlaceFailReason.Overlap, "Keep the door's moving parts and tracks clear.");
    }
}
