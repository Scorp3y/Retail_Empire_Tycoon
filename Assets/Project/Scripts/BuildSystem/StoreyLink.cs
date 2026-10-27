using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>A placed transport owns its two landings, opening and continuous route between them.</summary>
    public sealed class StoreyLink
    {
        public readonly PlacedObject Owner;
        private readonly GridSystem grid;
        public Vector3Int Lower { get; }
        public Vector3Int Upper { get; }
        public StoreyLink(GridSystem grid, PlacedObject owner)
            : this(grid, owner.item, owner.anchorCell, owner.rotated, owner.facing)
        { Owner = owner; }
        public StoreyLink(GridSystem grid, BuildItemData item, Vector3Int anchor, bool rotated, int facing)
        {
            this.grid = grid;
            var cells = grid.GetFootprintCells(anchor, item.footprint, rotated, item.pivotOffset).ToArray();
            var direction = Direction(facing);
            int min = cells.Min(c => c.x*direction.x+c.z*direction.z);
            int max = cells.Max(c => c.x*direction.x+c.z*direction.z);
            var lowEdge = cells.Where(c => c.x*direction.x+c.z*direction.z == min).ToArray();
            var highEdge = cells.Where(c => c.x*direction.x+c.z*direction.z == max).ToArray();
            Lower = lowEdge[lowEdge.Length / 2] - direction;
            Upper = (item.storeyConnection == StoreyConnectionKind.Elevator ? Lower : highEdge[highEdge.Length / 2] + direction) + Vector3Int.up;
        }
        public bool IsUsable(FloorPainter floors) => Owner != null && Owner.gameObject.activeInHierarchy
            && floors != null && floors.HasSurface(Upper);
        public List<Vector3> Route(bool upward)
        {
            Vector3 lower = grid.CellToWorld(Lower) + Vector3.up * RetailEmpireTycoon.StoreOperations.StoreNavigation.FloorOffset(Lower.y);
            Vector3 upper = grid.CellToWorld(Upper) + Vector3.up * RetailEmpireTycoon.StoreOperations.StoreNavigation.FloorOffset(Upper.y);
            var direction = (Vector3)Direction(Owner.facing);
            var route = new List<Vector3> { lower, lower + direction * grid.cellSize };
            if (Owner.item.storeyConnection == StoreyConnectionKind.Elevator)
                route.Add(new Vector3(route[1].x,upper.y,route[1].z));
            else route.Add(upper - direction * grid.cellSize);
            route.Add(upper);
            if (!upward) route.Reverse();
            return route;
        }
        public bool Contains(Vector3 point)
        {
            var route = Route(true);
            for (int i = 1; i < route.Count; i++)
            {
                var start = route[i-1]; var delta = route[i] - start;
                float t = Mathf.Clamp01(Vector3.Dot(point-start, delta) / Mathf.Max(.00001f, delta.sqrMagnitude));
                if (Vector3.Distance(point, start+delta*t) <= grid.cellSize*.48f) return true;
            }
            return false;
        }
        public static Vector3Int Direction(int facing) => ((facing % 4 + 4) % 4) switch
        { 0 => Vector3Int.forward, 1 => Vector3Int.right, 2 => Vector3Int.back, _ => Vector3Int.left };
        public static IEnumerable<Vector3Int> OpeningCells(GridSystem grid, PlacedObject owner)
            => grid.GetFootprintCells(owner.anchorCell+Vector3Int.up, owner.item.footprint, owner.rotated, owner.item.pivotOffset);
        public static IEnumerable<Vector3Int> LandingCells(GridSystem grid, PlacedObject owner)
        {
            var link = new StoreyLink(grid, owner); yield return link.Lower; yield return link.Upper;
        }
        public static IEnumerable<Vector3Int> OccupiedCells(GridSystem grid, BuildItemData item, Vector3Int anchor, bool rotated)
        {
            foreach (var cell in grid.GetFootprintCells(anchor,item.footprint,rotated,item.pivotOffset))
            {
                yield return cell;
                if (item.storeyConnection != StoreyConnectionKind.None) yield return cell + Vector3Int.up;
            }
        }
    }
}
