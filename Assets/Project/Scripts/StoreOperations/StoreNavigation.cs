using System.Collections.Generic;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Territory;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Walks purchased floor cells and checks real colliders, including newly placed furniture.</summary>
    public sealed class StoreNavigation
    {
        private readonly GridSystem _grid;
        private readonly TerritoryManager _territory;
        private readonly Dictionary<Vector3Int, PlacedObject> _doorways = new Dictionary<Vector3Int, PlacedObject>();
        private readonly List<StoreDoorway> _doors = new List<StoreDoorway>();
        private static readonly Vector3Int[] Directions = { Vector3Int.left, Vector3Int.right, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
        public const float FloorHeight = 0.105f;
        public const float BodyRadius = .075f;
        private static readonly Vector3 BodyBottom = Vector3.up * (FloorHeight + .1f + BodyRadius);
        private static readonly Vector3 BodyTop = Vector3.up * (FloorHeight + .48f - BodyRadius);
        public StoreNavigation(GridSystem grid, TerritoryManager territory) { _grid = grid; _territory = territory; }
        public void RefreshDoorways(IEnumerable<PlacedObject> objects)
        {
            _doorways.Clear();
            _doors.Clear();
            foreach (var placed in objects)
            {
                if (placed == null || placed.item == null || !placed.item.isDoorway) continue;
                var door = placed.GetComponent<StoreDoorway>() ?? placed.gameObject.AddComponent<StoreDoorway>();
                _doors.Add(door);
                foreach (var cell in placed.occupiedCells) _doorways[cell] = placed;
            }
        }

        public bool IsWalkable(Vector3Int cell, bool allowOutside = false)
        {
            if (!allowOutside && !_territory.IsCellPurchased(cell)) return false;
            if (_grid.IsOccupied(cell))
            {
                if (!_doorways.TryGetValue(cell, out var doorway) || doorway == null) return false;
                Vector3 local = Quaternion.Inverse(doorway.transform.rotation) * (_grid.CellToWorld(cell) - doorway.transform.position);
                // Keep one cell's clearance from the solid side posts; only the central opening is walkable.
                float halfOpening = doorway.item.placementBounds.extents.x - _grid.cellSize * 0.75f;
                // Center the route rather than grazing the posts or animated leaves.
                if (Mathf.Abs(local.x - doorway.item.placementBounds.center.x) >= Mathf.Min(halfOpening, _grid.cellSize * .9f)) return false;
            }
            return IsBodyClear(_grid.CellToWorld(cell));
        }
        public bool IsWalkable(Vector3 position, bool allowOutside = false) => IsWalkable(_grid.WorldToCell(position), allowOutside);

        public bool CanTraverse(Vector3 start, Vector3 end)
        {
            if (!IsBodyClear(start) || !IsBodyClear(end)) return false;
            Vector3 from = start; from.y = 0;
            Vector3 delta = end - start; delta.y = 0;
            if (delta.sqrMagnitude < .000001f) return true;
            foreach (var hit in Physics.CapsuleCastAll(from + BodyBottom, from + BodyTop, BodyRadius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsActor(hit.collider)) continue;
                var placed = hit.collider.GetComponentInParent<PlacedObject>();
                if (placed != null && placed.item != null && placed.item.isDoorway)
                {
                    Vector3 localStart = Quaternion.Inverse(placed.transform.rotation) * (start - placed.transform.position);
                    Vector3 localEnd = Quaternion.Inverse(placed.transform.rotation) * (end - placed.transform.position);
                    float clearance = placed.item.placementBounds.extents.x - _grid.cellSize * .75f;
                    float center = placed.item.placementBounds.center.x;
                    if (Mathf.Abs(localStart.x - center) < clearance && Mathf.Abs(localEnd.x - center) < clearance) continue;
                }
                return false;
            }
            return true;
        }
        private bool IsBodyClear(Vector3 position)
        {
            position.y = 0;
            foreach (var collider in Physics.OverlapCapsule(position + BodyBottom, position + BodyTop, BodyRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsActor(collider)) continue;
                var placed = collider.GetComponentInParent<PlacedObject>();
                if (placed != null && placed.item != null && placed.item.isDoorway)
                {
                    Vector3 local = Quaternion.Inverse(placed.transform.rotation) * (position - placed.transform.position);
                    float clearance = placed.item.placementBounds.extents.x - _grid.cellSize * .75f;
                    if (Mathf.Abs(local.x - placed.item.placementBounds.center.x) < clearance) continue;
                }
                return false;
            }
            return true;
        }
        private static bool IsActor(Collider collider) => collider.GetComponentInParent<ShopCharacter>() != null || collider.GetComponentInParent<Avatar>() != null;
        public void NotifyMovement(Vector3 position)
        {
            foreach (var door in _doors)
                if (door != null && (door.transform.position - position).sqrMagnitude < 1.2f * 1.2f)
                    door.RequestPassage();
        }

        public bool TryNearest(Vector3 point, out Vector3 position, bool allowOutside = false, bool requireConnection = false)
        {
            var center = _grid.WorldToCell(point);
            int radius = Mathf.CeilToInt(0.8f / _grid.cellSize);
            Vector3Int best = default; float distance = float.MaxValue; bool found = false;
            for (int x = -radius; x <= radius; x++)
                for (int z = -radius; z <= radius; z++)
                {
                    var cell = center + new Vector3Int(x, 0, z);
                    if (!IsWalkable(cell, allowOutside)) continue;
                    if (requireConnection && !CanTraverse(point, _grid.CellToWorld(cell))) continue;
                    float d = (_grid.CellToWorld(cell) - point).sqrMagnitude;
                    if (d >= distance) continue;
                    best = cell; distance = d; found = true;
                }
            position = _grid.CellToWorld(best); position.y = FloorHeight;
            return found;
        }

        public bool TryPath(Vector3 start, Vector3 end, out List<Vector3> path, bool allowOutside = false)
        {
            path = new List<Vector3>();
            if (!TryNearest(start, out var first, allowOutside, true) || !TryNearest(end, out var last, allowOutside)) return false;
            if (!CanTraverse(start, first)) return false;
            var from = _grid.WorldToCell(first); var to = _grid.WorldToCell(last);
            var comparer = Comparer<(int Cost, int Order, Vector3Int Cell)>.Create((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : a.Order.CompareTo(b.Order));
            var queue = new SortedSet<(int Cost, int Order, Vector3Int Cell)>(comparer); var previous = new Dictionary<Vector3Int, Vector3Int>();
            var costs = new Dictionary<Vector3Int, int>(); int order = 0;
            var walkable = new Dictionary<Vector3Int, bool>();
            queue.Add((0, order++, from)); previous.Add(from, from); costs.Add(from, 0);
            while (queue.Count > 0 && previous.Count <= 20000)
            {
                var candidate = queue.Min; queue.Remove(candidate); var current = candidate.Cell;
                if (current == to)
                {
                    for (var cell = to; cell != from; cell = previous[cell])
                    {
                        var point = _grid.CellToWorld(cell); point.y = FloorHeight; path.Add(point);
                    }
                    path.Add(first); path.Reverse(); return true;
                }
                foreach (var offset in Directions)
                {
                    var next = current + offset;
                    int cost = costs[current] + 1;
                    if (costs.TryGetValue(next, out int known) && known <= cost) continue;
                    if (!walkable.TryGetValue(next, out bool open)) { open = IsWalkable(next, allowOutside); walkable.Add(next, open); }
                    if (!open || !CanTraverse(_grid.CellToWorld(current), _grid.CellToWorld(next))) continue;
                    previous[next] = current; costs[next] = cost;
                    int estimate = Mathf.Abs(next.x - to.x) + Mathf.Abs(next.z - to.z);
                    queue.Add((cost + estimate, order++, next));
                }
            }
            return false;
        }
    }
}
