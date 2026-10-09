using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Rejects a wall end that meets the back/side of a neighbour rather than its matching end.</summary>
    public sealed class Rule_WallJoint : IPlacementRule
    {
        public bool EnabledFor(BuildItemData item) => item != null && item.isWall && item.footprint == Vector2Int.one;

        private static bool ConnectsTowards(BuildItemData item, int facing, Vector3Int direction)
        {
            var local = Quaternion.Euler(0, -facing * 90, 0) * (Vector3)direction;
            if (item.id == "wallcorner_01") return local.x > .9f || local.z > .9f;
            return Mathf.Abs(local.x) > .9f;
        }

        public PlacementResult Evaluate(PlacementRequest request)
        {
            foreach (var neighbour in Object.FindObjectsOfType<PlacedObject>())
            {
                if (neighbour == request.ignoredObject || neighbour.item == null || !neighbour.item.isWall
                    || neighbour.wallModuleVersion < BuildItemData.CurrentWallModuleVersion || neighbour.occupiedCells.Count != 1) continue;
                if (request.item.id != "wallcorner_01" && neighbour.item.id != "wallcorner_01") continue;
                var direction = neighbour.anchorCell - request.anchorCell;
                if (Mathf.Abs(direction.x) + Mathf.Abs(direction.z) != 1) continue;
                bool candidateConnects = ConnectsTowards(request.item, request.facing, direction);
                bool neighbourConnects = ConnectsTowards(neighbour.item, neighbour.facing, -direction);
                if (candidateConnects != neighbourConnects)
                    return PlacementResult.Fail(PlaceFailReason.NoAccess, "Поверните стену: её торец должен совпадать с торцом соседней секции.");
            }
            return PlacementResult.Success();
        }
    }
}
