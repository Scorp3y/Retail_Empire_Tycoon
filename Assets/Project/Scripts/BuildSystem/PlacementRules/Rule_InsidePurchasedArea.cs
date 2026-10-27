using System.Collections.Generic;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.BuildSystem
{
    [MovedFrom(false, "MyShopGame.BuildSystem", null, "Rule_InsidePurchasedArea")]
    public sealed class Rule_InsidePurchasedArea : IPlacementRule
    {
        private readonly ITerritoryQuery _territory;
        private readonly GridSystem _grid;

        public Rule_InsidePurchasedArea(ITerritoryQuery territory, GridSystem grid)
        {
            _territory = territory;
            _grid = grid;
        }

        public bool EnabledFor(BuildItemData item)
        {
            return item != null && item.ruleFlags.HasFlag(PlacementRuleFlags.InsidePurchasedArea);
        }

        public PlacementResult Evaluate(PlacementRequest req)
        {
            var cells = _grid.GetFootprintCells(req.anchorCell, req.item.footprint, req.rotated, req.item.pivotOffset);
            foreach (var c in cells)
            {
                if (_territory.IsCellPurchased(c) || req.item.isParkingSpace && _territory is TerritoryManager land && land.IsParkingCellAllowed(c))
                    continue;

                return PlacementResult.Fail(PlaceFailReason.NotPurchased, "Not purchased");
            }

            return PlacementResult.Success();
        }
    }
}
