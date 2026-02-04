using System.Collections.Generic;
using UnityEngine;
using MyShopGame.Core;

namespace MyShopGame.BuildSystem
{
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
            var accessCells = GetAccessCells(req);
            foreach (var a in accessCells)
            {
                if (_occupancy.IsOccupied(a))
                    continue;

                return PlacementResult.Success();
            }

            return PlacementResult.Fail(PlaceFailReason.NoAccess, "No access");
        }

        private IEnumerable<Vector3Int> GetAccessCells(PlacementRequest req)
        {
            var size = req.item.footprint;
            var rotated = req.rotated;

            var w = rotated ? size.y : size.x;
            var h = rotated ? size.x : size.y;

            var min = req.anchorCell;
            var max = new Vector3Int(min.x + w - 1, min.y, min.z + h - 1);

            var dir = FacingToDir(req.facing);

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
