using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    internal sealed class DecorPlacement
    {
        private const float MountHeight = 1.19f;
        private const float MinimumHeadroom = 1.04f;
        private const float FrontageRadius = 3.1f;
        private const float EntranceMatRadius = 1.15f;
        private const float MinimumGarland = .55f;
        private const float MaximumGarland = 2.5f;
        private const float MountGap = .012f;

        private readonly BuildController build;
        private StoreNavigation navigation;
        private List<(Vector3 Start, Vector3[] Targets, HashSet<Vector3Int> Route)> routes;
        private PlacementRequest routeRequest;
        private PlacementResult routeResult;
        private GridSystem Grid => build.grid;
        private FloorPainter Floors => build.floorPainter;

        internal readonly struct Pose
        {
            public readonly DecorMount Mount;
            public readonly Vector3 Position, Scale;
            public readonly Quaternion Rotation;
            public Pose(DecorMount mount, Vector3 position, Quaternion rotation, Vector3 scale)
            { Mount=mount; Position=position; Rotation=rotation; Scale=scale; }
        }

        internal DecorPlacement(BuildController build) => this.build = build;

        internal void InvalidateRoutes()
        {
            routes=null;
            routeRequest=default;
        }

        internal bool CanStartGarland(PlacementRequest request) => build.territory.IsCellPurchased(request.anchorCell) &&
            TryWallPoint(Objects(),request.anchorCell,request.facing,DecorMount.Wall,out _);

        internal PlacementResult ValidateRoutes(PlacementRequest request)
        {
            if (routeRequest.item==request.item && routeRequest.anchorCell==request.anchorCell &&
                routeRequest.facing==request.facing && routeRequest.ignoredObject==request.ignoredObject) return routeResult;
            routeRequest=request;
            if (routes==null) CollectRoutes();
            var blocked=Grid.GetFootprintCells(request.anchorCell,request.item.footprint,request.rotated,request.item.pivotOffset).ToHashSet();
            navigation.DecorObstruction=blocked;
            navigation.IgnoredFixture=request.ignoredObject;
            try
            {
                foreach(var route in routes)
                {
                    if(!route.Route.Overlaps(blocked)) continue;
                    if(!route.Targets.Any(target=>!blocked.Contains(Grid.WorldToCell(target)) && navigation.TryPath(route.Start,target,out _)))
                        return routeResult=Fail("This decoration would block access to a checkout or shelf.");
                }
                return routeResult=PlacementResult.Success();
            }
            finally { navigation.DecorObstruction=null; navigation.IgnoredFixture=null; }
        }

        private void CollectRoutes()
        {
            var objects=Objects();
            navigation=new StoreNavigation(Grid,build.territory);
            navigation.RefreshDoorways(objects);
            routes=new List<(Vector3,Vector3[],HashSet<Vector3Int>)>();
            bool Indoor(Vector3Int c)=>Floors.HasSurface(c)&&!Floors.IsAsphalt(c);
            var starts=objects.Where(p=>p.anchorCell.y==0 && p.item.isDoorway && !p.item.isUnloadingGate &&
                p.GetComponent<CollectionDoor>()?.AllowsVehicles!=true && IsExteriorEntrance(p,Indoor))
                .Select(p=>Grid.CellToWorld(new Vector3Int(Grid.WorldToCell(p.transform.position).x,0,Grid.WorldToCell(p.transform.position).z)))
                .ToArray();
            if(starts.Length==0) return;
            foreach(var fixture in objects.Where(p=>IsCounter(p.item)))
            {
                var pose=new PlacementRequest(fixture.item,fixture.anchorCell,fixture.rotated,fixture.facing);
                var targets=Enumerable.Range(0,fixture.item.twoSidedAccess?2:1).SelectMany(side=>ShelfAccessArea.Cells(Grid,pose,side))
                    .Where(c=>navigation.IsWalkable(c)).OrderBy(c=>(Grid.CellToWorld(c)-fixture.transform.position).sqrMagnitude)
                    .Select(Grid.CellToWorld).ToArray();
                foreach(var start in starts)
                foreach(var target in targets)
                    if(navigation.TryPath(start,target,out var path))
                    {
                        routes.Add((start,targets,path.Select(Grid.WorldToCell).ToHashSet()));
                        break;
                    }
            }
        }

        internal static PlacedObject[] Objects() => Object.FindObjectsOfType<PlacedObject>()
            .Where(p=>p.item!=null && !p.IsPendingRemoval && p.GetComponentInParent<BuildPreview>()==null).ToArray();

        internal static bool PickSurface(Ray ray, BuildItemData item, int level, GridSystem grid,
            out Vector3Int cell, out int facing)
        {
            cell=default; facing=-1;
            if (item == null || !DecorCatalog.IsMounted(item.decorMount)) return false;
            foreach (var hit in Physics.RaycastAll(ray, 1000, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit=>hit.distance))
            {
                var support=hit.collider.GetComponentInParent<PlacedObject>();
                if(support==null || support.item==null || support.anchorCell.y!=level || support.occupiedCells.Count==0) continue;
                bool wall=IsWallSupport(support.item,item.decorMount);
                bool counter=item.decorMount.HasFlag(DecorMount.Counter) && IsCounter(support.item);
                if (!wall && !counter) continue;
                var point=hit.point;
                cell=support.occupiedCells.OrderBy(c=>
                {
                    var delta=grid.CellToWorld(c)-point;
                    return delta.x*delta.x+delta.z*delta.z;
                }).First();
                if (wall)
                {
                    var normal=support.transform.forward;
                    if (Vector3.Dot(ray.direction,normal)>0) normal=-normal;
                    facing=(Mathf.RoundToInt(Mathf.Atan2(normal.x,normal.z)*Mathf.Rad2Deg/90)+4)%4;
                }
                return true;
            }
            return false;
        }

        internal PlacementResult Validate(PlacementRequest request)
        {
            var objects=Objects();
            if (!TryResolve(request,objects,out var pose,out var issue)) return Fail(issue);
            if (!DecorCatalog.UsesFloorSpace(request.item.decorMount) && pose.Mount != DecorMount.EntryMat)
            {
                var bounds=BoundsFor(request.item,pose);
                if(objects.Where(p=>p!=request.ignoredObject && p.decorMount!=DecorMount.None &&
                        !DecorCatalog.UsesFloorSpace(p.decorMount))
                    .Any(p=>Intersects(bounds,BoundsFor(p.item,new Pose(p.decorMount,p.transform.position,p.transform.rotation,p.decorScale)))))
                    return Fail("This mount already has a decoration in the way.");
                if (objects.Select(p=>p.GetComponent<CollectionDoor>()).Where(d=>d!=null)
                    .SelectMany(Rule_DoorClearance.Sweeps).Any(sweep=>Intersects(bounds,sweep)))
                    return Fail("Keep the door's moving panels clear.");
            }
            if (pose.Mount==DecorMount.EntryMat && objects.Any(p=>p!=request.ignoredObject &&
                p.decorMount==DecorMount.EntryMat && Intersects(BoundsFor(request.item,pose),
                    BoundsFor(p.item,new Pose(p.decorMount,p.transform.position,p.transform.rotation,Vector3.one)))))
                return Fail("There is already a mat here.");
            return PlacementResult.Success();
        }

        internal bool TryResolve(PlacementRequest request, out Pose pose, DecorMount preferred=DecorMount.None) =>
            TryResolve(request,Objects(),out pose,out _,preferred);

        private bool TryResolve(PlacementRequest request, PlacedObject[] objects, out Pose pose, out string issue,
            DecorMount preferred=DecorMount.None)
        {
            var item=request.item;
            var cell=request.anchorCell;
            var rotation=Quaternion.Euler(0,request.facing*90,0);
            var position=BuildPlacementPose.Position(Grid,item,cell,request.rotated,request.facing);
            pose=new Pose(DecorMount.None,position,rotation,Vector3.one);
            issue="This decoration needs a compatible surface.";
            if(Floors==null || cell.y<0 || cell.y>GridSystem.TopFloor) return false;
            var mounts=preferred!=DecorMount.None?preferred:item.decorMount;
            var occupied=Grid.GetFootprintCells(cell,item.footprint,request.rotated,item.pivotOffset).ToArray();
            bool Owned(Vector3Int c)=>build.territory.IsCellPurchased(c);
            bool Yard(Vector3Int c)=>build.territory.IsParkingCellAllowed(c);
            bool Indoor(Vector3Int c)=>Floors.HasSurface(c) && !Floors.IsAsphalt(c);
            var here=Grid.CellToWorld(cell);
            var top=Grid.FloorElevation(cell.y+1);
            if(mounts.HasFlag(DecorMount.Garland))
            {
                issue="Choose a wall, then a second wall or ceiling 0.55–2.5 m away.";
                if(!Owned(cell) || !TryWallPoint(objects,cell,request.facing,DecorMount.Wall,out var first)) return false;
                first.y=top-MountGap-.12f;
                if(!request.decorEnd.HasValue || request.decorEnd.Value.y!=cell.y || !Owned(request.decorEnd.Value)) return false;
                var end=request.decorEnd.Value;
                var last=Grid.CellToWorld(end);
                var endWall=objects.FirstOrDefault(p=>p.item.isWall && p.occupiedCells.Contains(end));
                if(endWall!=null)
                {
                    var endNormal=endWall.transform.forward;
                    if(Vector3.Dot(first-last,endNormal)<0) endNormal=-endNormal;
                    int endFacing=(Mathf.RoundToInt(Mathf.Atan2(endNormal.x,endNormal.z)*Mathf.Rad2Deg/90)+4)%4;
                    if(!TryWallPoint(objects,end,endFacing,DecorMount.Wall,out last)) return false;
                    last.y=first.y;
                }
                else
                {
                    if(!Indoor(end+Vector3Int.up)) return false;
                    last.y=top-MountGap;
                }
                Vector3 axis=last-first;
                float distance=axis.magnitude;
                if(distance<MinimumGarland || distance>MaximumGarland) return false;
                var right=axis.normalized;
                var forward=Vector3.Cross(right,Vector3.up).normalized;
                var turn=Quaternion.LookRotation(forward,Vector3.Cross(forward,right));
                var hang=(first+last)*.5f-turn*Vector3.up*(item.placementBounds.max.y-.014f);
                if(BoundsFor(item,new Pose(DecorMount.Garland,hang,turn,Vector3.one)).min.y <
                    Grid.FloorElevation(cell.y)+MinimumHeadroom) return false;
                pose=new Pose(DecorMount.Garland,hang,turn,new Vector3(distance/item.placementBounds.size.x,1,1));
                return true;
            }
            if(mounts.HasFlag(DecorMount.Counter))
            {
                issue="Choose a free display top on a counter or shelf.";
                var support=objects.FirstOrDefault(p=>p!=request.ignoredObject && IsCounter(p.item) && p.occupiedCells.Contains(cell));
                if(support==null || !Owned(cell)) return false;
                var bounds=SupportBounds(support);
                here.y=bounds.max.y+MountGap-item.placementBounds.min.y;
                var prop=BoundsFor(item,new Pose(DecorMount.Counter,here,rotation,Vector3.one));
                if(prop.min.x<bounds.min.x-.01f || prop.max.x>bounds.max.x+.01f ||
                    prop.min.z<bounds.min.z-.01f || prop.max.z>bounds.max.z+.01f) return false;
                pose=new Pose(DecorMount.Counter,here,rotation,Vector3.one);
                return true;
            }
            if((mounts & (DecorMount.Wall|DecorMount.Awning))!=0)
            {
                var kind=mounts.HasFlag(DecorMount.Awning)?DecorMount.Awning:DecorMount.Wall;
                issue=kind==DecorMount.Awning?"Fit the awning to the outside of a display window.":"Choose the face of an installed wall.";
                if(Owned(cell) && TryWallPoint(objects,cell,request.facing,kind,out var point))
                {
                    if(kind!=DecorMount.Awning || cell.y==0 && !Indoor(cell+StoreyLink.Direction(request.facing)))
                    {
                        point.y=kind==DecorMount.Awning?top-.07f:Grid.FloorElevation(cell.y)+MountHeight;
                        point.y-=item.placementBounds.max.y;
                        pose=new Pose(kind,point,rotation,Vector3.one);
                        return true;
                    }
                }
            }
            if(mounts.HasFlag(DecorMount.Ceiling))
            {
                issue="Build a ceiling over this spot first.";
                if(occupied.Any(c=>!Owned(c)||!Indoor(c+Vector3Int.up))) return false;
                position.y=top-MountGap-item.placementBounds.max.y;
                if(position.y+item.placementBounds.min.y < Grid.FloorElevation(cell.y)+MinimumHeadroom)
                {
                    issue="The ceiling is too low for this hanging decoration.";
                    return false;
                }
                pose=new Pose(DecorMount.Ceiling,position,rotation,Vector3.one);
                return true;
            }

            var entrances=objects.Where(p=>p.anchorCell.y==0 && p.item.isDoorway && !p.item.isUnloadingGate &&
                p.GetComponent<CollectionDoor>()?.AllowsVehicles!=true && IsExteriorEntrance(p,Indoor)).ToArray();
            bool NearDoor(float range)=>entrances.Any(p=>
                Vector2.Distance(new Vector2(position.x,position.z),new Vector2(p.transform.position.x,p.transform.position.z))<=range);
            if (mounts.HasFlag(DecorMount.EntryMat))
            {
                issue="Place the mat beside a ground-floor entrance.";
                if(cell.y!=0 || !NearDoor(EntranceMatRadius) || occupied.Any(c=>!Yard(c) ||
                    Grid.IsOccupied(c) && !objects.Any(p=>p.item.isDoorway && p.occupiedCells.Contains(c)))) return false;
                position.y=occupied.Max(Floors.SurfaceElevation)+.008f-item.placementBounds.min.y;
                pose=new Pose(DecorMount.EntryMat,position,rotation,Vector3.one);
                return true;
            }
            if(mounts.HasFlag(DecorMount.Floor) && occupied.All(c=>Owned(c)&&Indoor(c)))
            {
                position.y=occupied.Max(Floors.SurfaceElevation)-Mathf.Min(0,item.placementBounds.min.y);
                pose=new Pose(DecorMount.Floor,position,rotation,Vector3.one);
                return true;
            }
            if((mounts&(DecorMount.Frontage|DecorMount.Street))!=0)
            {
                bool frontage=mounts.HasFlag(DecorMount.Frontage) && NearDoor(FrontageRadius);
                issue=mounts.HasFlag(DecorMount.Frontage) && !mounts.HasFlag(DecorMount.Street)?
                    "Place this on the forecourt by the entrance.":"Place this on an outdoor paved surface.";
                if(cell.y!=0 || occupied.Any(c=>!Yard(c)||Indoor(c) || !frontage&&!Floors.IsAsphalt(c)) ||
                    !frontage&&!mounts.HasFlag(DecorMount.Street)) return false;
                if(NearDoor(EntranceMatRadius) || objects.Where(p=>p.item.isParkingSpace)
                    .SelectMany(p=>RetailEmpireTycoon.Parking.Rule_ParkingSurface.ApproachCells(Grid,p)).Intersect(occupied).Any())
                {
                    issue="Keep the entrance and parking approaches clear.";
                    return false;
                }
                position.y=occupied.Max(Floors.SurfaceElevation)-Mathf.Min(0,item.placementBounds.min.y);
                pose=new Pose(frontage?DecorMount.Frontage:DecorMount.Street,position,rotation,Vector3.one);
                return true;
            }
            issue="Place this decoration on an indoor floor.";
            return false;
        }

        private bool IsExteriorEntrance(PlacedObject placed, System.Func<Vector3Int,bool> indoor)
        {
            var normal=StoreyLink.Direction(placed.facing);
            var cells=placed.occupiedCells.ToHashSet();
            bool one=placed.occupiedCells.Any(c=>!cells.Contains(c+normal) && indoor(c+normal));
            bool other=placed.occupiedCells.Any(c=>!cells.Contains(c-normal) && indoor(c-normal));
            return one!=other;
        }

        private bool TryWallPoint(PlacedObject[] objects, Vector3Int cell, int facing, DecorMount kind, out Vector3 point)
        {
            point=Grid.CellToWorld(cell);
            var direction=StoreyLink.Direction(facing);
            var support=objects.FirstOrDefault(p=>IsWallSupport(p.item,kind) && p.occupiedCells.Contains(cell) &&
                Mathf.Abs(Vector3.Dot(p.transform.forward,direction))>.9f);
            if(support==null) return false;
            var bounds=SupportBounds(support);
            if(kind==DecorMount.Awning) point=bounds.center;
            if(direction.x!=0) point.x=(direction.x>0?bounds.max.x:bounds.min.x)+direction.x*MountGap;
            else point.z=(direction.z>0?bounds.max.z:bounds.min.z)+direction.z*MountGap;
            return true;
        }

        private static bool IsWallSupport(BuildItemData item, DecorMount kind) =>
            kind.HasFlag(DecorMount.Awning) ? item.id is "wall_finish_25" or "wall_finish_27" :
            item.isWall && !item.IsWallCorner;

        private static bool IsCounter(BuildItemData item) => item.category==BuildCategory.Shelf || item.isCheckout;

        private static Bounds SupportBounds(PlacedObject support)
        {
            var occluder=support.GetComponentInChildren<StoreWallOccluder>();
            if (occluder!=null) return occluder.WorldBounds;
            var bounds=support.item.placementBounds;
            if (bounds.size.sqrMagnitude>.001f)
                return BoundsFor(support.item,new Pose(DecorMount.None,support.transform.position,support.transform.rotation,Vector3.one));
            var renderers=support.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            return renderers.Length==0?new Bounds(support.transform.position,Vector3.zero):
                renderers.Skip(1).Aggregate(renderers[0].bounds,(current,r)=>{current.Encapsulate(r.bounds);return current;});
        }

        internal static bool SupportsDecoration(PlacedObject support) =>
            Objects().Any(p=>p!=support && (p.decorMount is DecorMount.Counter or DecorMount.Wall or DecorMount.Awning or DecorMount.Garland) &&
                (support.occupiedCells.Contains(p.anchorCell) || p.decorEnd.HasValue && support.occupiedCells.Contains(p.decorEnd.Value)));

        internal static bool CeilingSupports(PlacedObject decor, HashSet<Vector3Int> surfaces, GridSystem grid) =>
            decor.decorMount==DecorMount.Ceiling && grid.GetFootprintCells(decor.anchorCell+Vector3Int.up,
                decor.item.footprint,decor.rotated,decor.item.pivotOffset).Any(surfaces.Contains) ||
            decor.decorMount==DecorMount.Garland && decor.decorEnd.HasValue && surfaces.Contains(decor.decorEnd.Value+Vector3Int.up);

        private static Bounds BoundsFor(BuildItemData item, Pose pose)
        {
            var local=item.placementBounds;
            var size=Vector3.Scale(local.extents,pose.Scale);
            Vector3 Abs(Vector3 v)=>new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
            var worldExtents=Abs(pose.Rotation*Vector3.right)*size.x+Abs(pose.Rotation*Vector3.up)*size.y+
                Abs(pose.Rotation*Vector3.forward)*size.z;
            return new Bounds(pose.Position+pose.Rotation*Vector3.Scale(local.center,pose.Scale),worldExtents*2);
        }

        private static bool Intersects(Bounds a, Bounds b)
        {
            var overlap=Vector3.Min(a.max,b.max)-Vector3.Max(a.min,b.min);
            return overlap.x>.009f && overlap.y>.006f && overlap.z>.009f;
        }

        private static PlacementResult Fail(string message)=>PlacementResult.Fail(PlaceFailReason.NoAccess,message);
    }
}

