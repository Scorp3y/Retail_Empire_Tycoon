using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.BuildSystem
{
    [MovedFrom(false, "MyShopGame.BuildSystem", null, "Rule_Accessibility")]
    public sealed class Rule_Accessibility : IPlacementRule
    {
        private readonly IGridOccupancy _occupancy;
        private readonly GridSystem _grid;

        public Rule_Accessibility(IGridOccupancy occupancy, GridSystem grid)
        {
            _occupancy = occupancy;
            _grid = grid;
        }

        public bool EnabledFor(BuildItemData item)
        {
            return item != null && item.ruleFlags.HasFlag(PlacementRuleFlags.RequireAccessibility);
        }

        public PlacementResult Evaluate(PlacementRequest req)
        {
            int sides = req.item.twoSidedAccess ? 2 : 1;
            for (int side = 0; side < sides; side++)
            {
                bool accessible = false;
                foreach (var cell in GetAccessCells(req, side))
                {
                    if (_occupancy.IsOccupied(cell) && (req.ignoredObject == null || !req.ignoredObject.occupiedCells.Contains(cell))) continue;
                    accessible = true;
                    break;
                }
                if (!accessible) return PlacementResult.Fail(PlaceFailReason.NoAccess, "No access");
            }
            return PlacementResult.Success();
        }

        private IEnumerable<Vector3Int> GetAccessCells(PlacementRequest req, int side)
        {
            var size = req.item.footprint;
            var rotated = req.rotated;

            var w = rotated ? size.y : size.x;
            var h = rotated ? size.x : size.y;

            Vector2Int pivot = rotated ? new Vector2Int(req.item.pivotOffset.y, req.item.pivotOffset.x) : req.item.pivotOffset;
            var min = req.anchorCell + new Vector3Int(pivot.x, 0, pivot.y);
            var max = new Vector3Int(min.x + w - 1, min.y, min.z + h - 1);

            var dir = FacingToDir(req.facing + req.item.frontFacing + side * 2);

            var frontStart = dir.x != 0
                ? new Vector3Int(dir.x > 0 ? max.x + 1 : min.x - 1, min.y, min.z)
                : new Vector3Int(min.x, min.y, dir.z > 0 ? max.z + 1 : min.z - 1);

            var count = dir.x != 0 ? h : w;

            for (var i = 0; i < count; i++)
            {
                var offset = dir.x != 0
                    ? new Vector3Int(0, 0, i)
                    : new Vector3Int(i, 0, 0);

                yield return frontStart + offset;
            }
        }

        private static Vector3Int FacingToDir(int facing)
        {
            var f = ((facing % 4) + 4) % 4;
            return f switch
            {
                0 => new Vector3Int(0, 0, 1),
                1 => new Vector3Int(1, 0, 0),
                2 => new Vector3Int(0, 0, -1),
                _ => new Vector3Int(-1, 0, 0),
            };
        }
    }
}
