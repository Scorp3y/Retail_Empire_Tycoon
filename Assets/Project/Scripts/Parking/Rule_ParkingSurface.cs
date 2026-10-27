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
                var reserved=ApproachCells(grid,placed);
                if(footprint.Intersect(reserved).Any()) return PlacementResult.Fail(PlaceFailReason.NoAccess,"Keep the parking approach clear.");
            }
            if(!request.item.isParkingSpace) return PlacementResult.Success();
            foreach(var door in Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null && placed.item.isDoorway && placed.GetComponentInParent<BuildPreview>()==null))
                if(footprint.Intersect(DoorAccessCells(grid,door.item,door.anchorCell,door.rotated,door.facing)).Any())
                    return PlacementResult.Fail(PlaceFailReason.NoAccess,"Keep the customer entrance clear.");
            var parkingTraffic=Object.FindObjectOfType<ParkingTraffic>();
            if(parkingTraffic!=null && footprint.Any(cell=>parkingTraffic.BlocksStreetEntrance(grid.CellToWorld(cell),grid.cellSize*.5f)))
                return PlacementResult.Fail(PlaceFailReason.NoAccess,"Keep the driveway from the public road clear.");
            if(request.ignoredObject!=null&&parkingTraffic!=null&&!parkingTraffic.CanEdit(request.ignoredObject.GetComponent<ParkingBay>()))
                return PlacementResult.Fail(PlaceFailReason.NoAccess,"Wait until the vehicle leaves this bay.");
            if(floor==null || footprint.Any(c=>!floor.IsAsphalt(c)))
                return PlacementResult.Fail(PlaceFailReason.RuleFailed,"Parking bays must be placed on asphalt.");
            Vector3Int forward = request.facing%4==0 ? Vector3Int.forward : request.facing%4==1 ? Vector3Int.right
                : request.facing%4==2 ? Vector3Int.back : Vector3Int.left;
            int maximum=footprint.Max(c=>c.x*forward.x+c.z*forward.z);
            foreach(var edge in footprint.Where(c=>c.x*forward.x+c.z*forward.z==maximum))
                for(int step=1;step<=4;step++)
                {
                    var cell=edge+forward*step;
                    if(!floor.IsAsphalt(cell)||grid.IsOccupied(cell)&&(request.ignoredObject==null||!request.ignoredObject.occupiedCells.Contains(cell)))
                        return PlacementResult.Fail(PlaceFailReason.NoAccess,"Leave a clear four-cell asphalt approach in front of the parking bay.");
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
        public static System.Collections.Generic.IEnumerable<Vector3Int> ApproachCells(GridSystem grid,PlacedObject placed)
        {
            var cells=placed.occupiedCells.ToArray();
            if(cells.Length==0)return System.Array.Empty<Vector3Int>();
            var direction=StoreyLink.Direction(placed.facing);
            int maximum=cells.Max(c=>c.x*direction.x+c.z*direction.z);
            return cells.Where(c=>c.x*direction.x+c.z*direction.z==maximum).SelectMany(c=>Enumerable.Range(1,4).Select(step=>c+direction*step));
        }
        public static System.Collections.Generic.IEnumerable<Vector3Int> DoorAccessCells(GridSystem grid,BuildItemData item,Vector3Int anchor,bool rotated,int facing)
        {
            var cells=grid.GetFootprintCells(anchor,item.footprint,rotated,item.pivotOffset).ToArray();
            var direction=StoreyLink.Direction(facing);
            foreach(int sign in new[]{-1,1})
            {
                var forward=direction*sign;int edge=cells.Max(cell=>cell.x*forward.x+cell.z*forward.z);
                foreach(var cell in cells.Where(cell=>cell.x*forward.x+cell.z*forward.z==edge))
                    for(int step=1;step<=4;step++)yield return cell+forward*step;
            }
        }
    }
}
