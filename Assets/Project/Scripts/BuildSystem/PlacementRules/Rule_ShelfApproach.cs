using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class Rule_ShelfApproach : IPlacementRule
    {
        private readonly GridSystem grid;
        private readonly TerritoryManager territory;
        public Rule_ShelfApproach(GridSystem grid,TerritoryManager territory){this.grid=grid;this.territory=territory;}
        public bool EnabledFor(BuildItemData item)=>item!=null&&item.placementKind!=PlacementKind.Floor;
        public PlacementResult Evaluate(PlacementRequest request)
        {
            var footprint=grid.GetFootprintCells(request.anchorCell,request.item.footprint,request.rotated,request.item.pivotOffset).ToHashSet();
            foreach(var placed in Object.FindObjectsOfType<PlacedObject>())
            {
                if(placed==request.ignoredObject||placed.item==null||placed.item.category!=BuildCategory.Shelf||placed.GetComponentInParent<BuildPreview>()!=null)continue;
                var existing=new PlacementRequest(placed.item,placed.anchorCell,placed.rotated,placed.facing);
                for(int side=0;side<(placed.item.twoSidedAccess?2:1);side++)
                    if(ShelfAccessArea.Cells(grid,existing,side).Any(footprint.Contains))
                        return PlacementResult.Fail(PlaceFailReason.NoAccess,"Этот предмет перекроет подход покупателей к стеллажу.");
            }
            if(request.item.category!=BuildCategory.Shelf)return PlacementResult.Success();
            var wallBounds=Object.FindObjectsOfType<StoreWallOccluder>().Where(w=>request.ignoredObject==null||w.GetComponentInParent<PlacedObject>()!=request.ignoredObject).Select(w=>w.WorldBounds).ToArray();
            for(int side=0;side<(request.item.twoSidedAccess?2:1);side++)
                foreach(var cell in ShelfAccessArea.Cells(grid,request,side))
                    if(grid.IsOccupied(cell)&&(request.ignoredObject==null||!request.ignoredObject.occupiedCells.Contains(cell))||territory!=null&&!territory.IsCellPurchased(cell)||WallBlocks(cell,wallBounds))
                        return PlacementResult.Fail(PlaceFailReason.NoAccess,"Перед стеллажом нужен свободный проход глубиной 2 клетки.");
            return PlacementResult.Success();
        }
        private bool WallBlocks(Vector3Int cell,Bounds[] walls)
        {
            var cellBounds=new Bounds(grid.CellToWorld(cell)+Vector3.up*.4f,new Vector3(grid.cellSize*.8f,.7f,grid.cellSize*.8f));
            return walls.Any(w=>w.Intersects(cellBounds));
        }
    }
}
