using RetailEmpireTycoon.Core;
using UnityEngine;
using System.Linq;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Rejects a wall end that meets the back/side of a neighbour rather than its matching end.</summary>
    public sealed class Rule_WallJoint : IPlacementRule
    {
        public bool EnabledFor(BuildItemData item) => item != null && item.isWall;

        private static bool ConnectsTowards(BuildItemData item, int facing, Vector3Int direction)
        {
            var local = Quaternion.Euler(0, -facing * 90, 0) * (Vector3)direction;
            if (item.id == "wallcorner_01") return local.x > .9f || local.z > .9f;
            return Mathf.Abs(local.x) > .9f;
        }

        public PlacementResult Evaluate(PlacementRequest request)
        {
            var cells=FootprintCells(request.item,request.anchorCell,request.rotated);
            foreach (var neighbour in Object.FindObjectsOfType<PlacedObject>())
            {
                if (neighbour == request.ignoredObject || neighbour.item == null || !neighbour.item.isWall
                    || neighbour.wallModuleVersion < BuildItemData.CurrentWallModuleVersion) continue;
                if (request.item.id != "wallcorner_01" && neighbour.item.id != "wallcorner_01") continue;
                foreach(var direction in new[]{Vector3Int.left,Vector3Int.right,Vector3Int.forward,Vector3Int.back})
                {
                    if(!cells.Any(cell=>neighbour.occupiedCells.Contains(cell+direction)))continue;
                    bool candidateConnects = ConnectsTowards(request.item, request.facing, direction);
                    bool neighbourConnects = ConnectsTowards(neighbour.item, neighbour.facing, -direction);
                    if (candidateConnects != neighbourConnects)
                        return PlacementResult.Fail(PlaceFailReason.NoAccess, "Поверните стену: её торец должен совпадать с торцом соседней секции.");
                }
            }
            return PlacementResult.Success();
        }
        private static System.Collections.Generic.List<Vector3Int> FootprintCells(BuildItemData item,Vector3Int anchor,bool rotated)
        {
            int width=rotated?item.footprint.y:item.footprint.x,depth=rotated?item.footprint.x:item.footprint.y;
            var pivot=rotated?new Vector2Int(item.pivotOffset.y,item.pivotOffset.x):item.pivotOffset;
            return Enumerable.Range(0,width).SelectMany(x=>Enumerable.Range(0,depth).Select(z=>anchor+new Vector3Int(pivot.x+x,0,pivot.y+z))).ToList();
        }
    }
}
