using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    public sealed class Rule_ParkingSurface : IPlacementRule
    {
        private readonly GridSystem grid;
        private readonly FloorPainter floor;
        public Rule_ParkingSurface(GridSystem grid,FloorPainter floor) { this.grid=grid; this.floor=floor; }
        public bool EnabledFor(BuildItemData item) => item!=null && item.placementKind!=PlacementKind.Floor;
        public PlacementResult Evaluate(PlacementRequest request)
        {
            var footprint=grid.GetFootprintCells(request.anchorCell,request.item.footprint,request.rotated,request.item.pivotOffset).ToArray();
            foreach(var placed in Object.FindObjectsOfType<PlacedObject>())
            {
                if(placed==request.ignoredObject||placed.item==null||!placed.item.isParkingSpace||placed.GetComponentInParent<BuildPreview>()!=null) continue;
                var reserved=ApproachCells(grid,placed.item,placed.anchorCell,placed.rotated,placed.facing);
                if(footprint.Intersect(reserved).Any()) return PlacementResult.Fail(PlaceFailReason.NoAccess,"Нельзя перекрывать подъезд к парковке.");
            }
            if(!request.item.isParkingSpace) return PlacementResult.Success();
            if(floor==null || footprint.Any(c=>!floor.IsAsphalt(c)))
                return PlacementResult.Fail(PlaceFailReason.RuleFailed,"Парковку можно строить только на асфальте.");
            Vector3Int forward = request.facing%4==0 ? Vector3Int.forward : request.facing%4==1 ? Vector3Int.right
                : request.facing%4==2 ? Vector3Int.back : Vector3Int.left;
            int maximum=footprint.Max(c=>c.x*forward.x+c.z*forward.z);
            foreach(var edge in footprint.Where(c=>c.x*forward.x+c.z*forward.z==maximum))
                for(int step=1;step<=4;step++)
                {
                    var cell=edge+forward*step;
                    if(!floor.IsAsphalt(cell)||grid.IsOccupied(cell)&&(request.ignoredObject==null||!request.ignoredObject.occupiedCells.Contains(cell)))
                        return PlacementResult.Fail(PlaceFailReason.NoAccess,"Перед парковкой нужен свободный асфальтовый подъезд шириной 4 клетки.");
                }
            return PlacementResult.Success();
        }
        public static System.Collections.Generic.IEnumerable<Vector3Int> ApproachCells(GridSystem grid,BuildItemData item,Vector3Int anchor,bool rotated,int facing)
        {
            var cells=grid.GetFootprintCells(anchor,item.footprint,rotated,item.pivotOffset).ToArray();
            Vector3Int forward=facing%4==0?Vector3Int.forward:facing%4==1?Vector3Int.right:facing%4==2?Vector3Int.back:Vector3Int.left;
            int maximum=cells.Max(c=>c.x*forward.x+c.z*forward.z);
            foreach(var edge in cells.Where(c=>c.x*forward.x+c.z*forward.z==maximum))
                for(int step=1;step<=4;step++) yield return edge+forward*step;
        }
    }
}
