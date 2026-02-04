using System.Collections.Generic;
using MyShopGame.Core;

namespace MyShopGame.BuildSystem
{
    public sealed class Rule_NoOverlap : IPlacementRule
    {
        private readonly IGridOccupancy _occupancy;
        private readonly GridSystem _grid;

        public Rule_NoOverlap(IGridOccupancy occupancy, GridSystem grid)
        {
            _occupancy = occupancy;
            _grid = grid;
        }

        public bool EnabledFor(BuildItemData item)
        {
            return item != null && item.ruleFlags.HasFlag(PlacementRuleFlags.NoOverlap);
        }

        public PlacementResult Evaluate(PlacementRequest req)
        {
            var cells = _grid.GetFootprintCells(req.anchorCell, req.item.footprint, req.rotated);
            foreach (var c in cells)
            {
                if (!_occupancy.IsOccupied(c))
                    continue;

                return PlacementResult.Fail(PlaceFailReason.Overlap, "Cell occupied");
            }

            return PlacementResult.Success();
        }
    }
}
