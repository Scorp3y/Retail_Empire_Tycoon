using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Grid occupancy alone does not detect intersections with the prebuilt store shell.</summary>
    public sealed class Rule_VisualOverlap : IPlacementRule
    {
        private readonly GridSystem grid;
        public Rule_VisualOverlap(GridSystem grid){this.grid=grid;}
        public bool EnabledFor(BuildItemData item)=>item!=null&&item.alignModelToFootprint&&item.placementKind!=PlacementKind.Floor;
        public PlacementResult Evaluate(PlacementRequest request)
        {
            var rotation=Quaternion.Euler(0,request.facing*90,0);
            var center=BuildPlacementPose.Position(grid,request.item,request.anchorCell,request.rotated,request.facing)+rotation*request.item.placementBounds.center;
            var size=request.item.placementBounds.size;
            if(request.facing%2==1)size=new Vector3(size.z,size.y,size.x);
            var bounds=new Bounds(center,size);
            foreach(var wall in Object.FindObjectsOfType<StoreWallOccluder>())
            {
                if(wall.GetComponentInParent<BuildPreview>()!=null)continue;
                if(request.ignoredObject!=null&&wall.GetComponentInParent<PlacedObject>()==request.ignoredObject)continue;
                var other=wall.WorldBounds;
                var overlap=Vector3.Min(bounds.max,other.max)-Vector3.Max(bounds.min,other.min);
                if(overlap.x>.012f&&overlap.z>.012f&&overlap.y>.1f)
                    return PlacementResult.Fail(PlaceFailReason.Overlap,"Предмет пересекает существующую стену.");
            }
            return PlacementResult.Success();
        }
    }
}
