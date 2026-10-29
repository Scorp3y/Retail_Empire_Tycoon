using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class Rule_StoreySupport : IPlacementRule
    {
        private readonly GridSystem grid;
        private readonly FloorPainter floors;
        public Rule_StoreySupport(GridSystem grid, FloorPainter floors) { this.grid=grid; this.floors=floors; }
        public bool EnabledFor(BuildItemData item) => item != null && item.placementKind != PlacementKind.Floor;
        public PlacementResult Evaluate(PlacementRequest request)
        {
            int level = request.anchorCell.y;
            if (level < 0 || level > GridSystem.TopFloor) return Fail("Floors 1 and 2 are available.");
            if (level != 0 && (request.item.isParkingSpace || request.item.isUnloadingGate)) return Fail("Parking and unloading must be on the ground floor.");
            var cells = grid.GetFootprintCells(request.anchorCell, request.item.footprint, request.rotated, request.item.pivotOffset).ToArray();
            if (level > 0 && (floors == null || cells.Any(c => !floors.HasSurface(c)))) return Fail("Build a floor beneath the entire item first.");
            var placedObjects = Object.FindObjectsOfType<PlacedObject>().Where(p => p != request.ignoredObject && p.item != null && p.GetComponentInParent<BuildPreview>() == null).ToArray();
            if (placedObjects.Where(p => p.item.storeyConnection != StoreyConnectionKind.None)
                .Any(p => StoreyLink.LandingCells(grid,p).Intersect(cells).Any())) return Fail("Keep the stairs and elevator landings clear.");
            if (request.item.storeyConnection == StoreyConnectionKind.None) return PlacementResult.Success();
            if (level != 0) return Fail("This connection links floors 1 and 2 and must be placed on floor 1.");
            if (floors == null) return Fail("The floor system is unavailable.");
            if (request.ignoredObject!=null && new StoreyLink(grid,request.ignoredObject).HasPassenger)
                return Fail("Wait until everyone has left this connection before moving it.");
            if(cells.Any(cell=>!floors.HasSurface(cell)))return Fail("Build a floor beneath the entire connection first.");
            foreach (var cell in cells)
                if (grid.IsOccupied(cell+Vector3Int.up) && (request.ignoredObject == null || !request.ignoredObject.occupiedCells.Contains(cell+Vector3Int.up)))
                    return Fail("Move the upstairs furniture out of the opening first. Ceiling tiles are returned automatically.");
            var link = new StoreyLink(grid,request.item,request.anchorCell,request.rotated,request.facing);
            foreach (var landing in new[] { link.Lower, link.Upper })
                if (floors.territory != null && !floors.territory.IsCellPurchased(landing)
                    || landing.y==0 && !floors.HasSurface(landing) || grid.IsOccupied(landing) && (request.ignoredObject == null || !request.ignoredObject.occupiedCells.Contains(landing)))
                    return Fail("Keep both landings clear and build a floor at the lower entrance.");
            return PlacementResult.Success();
        }
        private static PlacementResult Fail(string message) => PlacementResult.Fail(PlaceFailReason.NoAccess, message);
    }
}
