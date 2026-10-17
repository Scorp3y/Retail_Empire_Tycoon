using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEngine;

/// <summary>Packs meaningful zones into genuinely free ground, preserving the accepted door network.</summary>
internal sealed class TownCourtyardPlanner
{
    private readonly Dictionary<string, TownCourtyardModel> models;
    private readonly Rect[] preservedSolids;
    private readonly TownWorldLayout layout;
    public TownCourtyardPlanner(GameObject town, IReadOnlyList<TownCourtyardModel> catalogue)
    {
        models = catalogue.ToDictionary(m => m.Id);
        layout = AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownWorldSetup.LayoutPath);
        var courtyard = town.transform.Find(TownCourtyardPlan.HierarchyRoot);
        preservedSolids = town.GetComponentsInChildren<Collider>(true)
            .Where(c => c.enabled && !c.isTrigger && !c.name.StartsWith("Residential tree trunk", StringComparison.Ordinal) &&
                (courtyard == null || !c.transform.IsChildOf(courtyard)))
            .Select(TownResidentialPlacement.ColliderBounds).Where(b => b.max.y > .3f && b.min.y < 2)
            .Select(TownCourtyardGeometry.Rect).ToArray();
    }

    public TownCourtyardBlock Build(TownResidentialBlock block)
    {
        var obstacles = preservedSolids.Where(r => r.Overlaps(TownCourtyardGeometry.Expand(block.Bounds, 20)))
            .Select(r => TownCourtyardGeometry.Expand(r, .65f)).ToList();
        var originalPaths = block.Walkways.Select(p => TownCourtyardGeometry.Path(p, .3f)).ToArray();
        var zones = new List<TownCourtyardZone>();
        var connections = new List<TownResidentialWalkway>();
        var network = block.Walkways.ToList();

        bool Reserve(TownCourtyardKind kind, float width, float depth, bool small = false)
        {
            // A later zone must preserve connections already accepted for earlier zones,
            // not only the stage-three door paths.
            var reservedPaths = network.Select(p => TownCourtyardGeometry.Path(p, .3f)).ToArray();
            var candidates = new List<Rect>();
            for (float z = block.Bounds.yMin + 3; z + depth <= block.Bounds.yMax - 3; z += 1)
                for (float x = block.Bounds.xMin + 3; x + width <= block.Bounds.xMax - 3; x += 1)
                {
                    var area = new Rect(x, z, width, depth);
                    if (obstacles.Any(r => r.Overlaps(area)) || reservedPaths.Any(r => r.Overlaps(area)) ||
                        zones.Any(zon => TownCourtyardGeometry.Expand(zon.Bounds, 2.5f).Overlaps(area))) continue;
                    if (kind == TownCourtyardKind.Refuse && zones.Where(t => t.Kind == TownCourtyardKind.Children || t.Kind == TownCourtyardKind.Quiet)
                        .Any(t => TownCourtyardGeometry.Expand(t.Bounds, 5).Overlaps(area))) continue;
                    candidates.Add(area);
                }
            // Start at the middle of a real courtyard rather than placing arbitrary isolated objects at a corner.
            foreach (var area in candidates.OrderBy(r => (r.center - block.Bounds.center).sqrMagnitude))
            {
                foreach (var gate in Gates(area).OrderBy(g => network.Min(p => Distance(g.point, p))))
                {
                    var zone = Compose(block.Id, zones.Count, kind, area, gate.point, gate.outward, small);
                    var gatePassage = TownCourtyardGeometry.Path(new TownResidentialWalkway("gate", gate.point - gate.outward * 1.2f,
                        gate.point + gate.outward * 1.2f), .08f);
                    if (zone.Props.SelectMany(p => p.WorldSolids).Where(b => b.min.y < 2 && b.max.y > .3f)
                        .Select(TownCourtyardGeometry.Rect).Any(r => r.Overlaps(gatePassage))) continue;
                    var solids = obstacles.Concat(zones.Select(zon => TownCourtyardGeometry.Expand(zon.Bounds, .1f)))
                        .Concat(zone.Props.SelectMany(p => p.WorldSolids).Where(b => b.min.y < 2).Select(TownCourtyardGeometry.Rect)).ToArray();
                    var path = Connect(zone.Id, gate.point, gate.outward, network, solids, block.Bounds);
                    if (path == null) continue;
                    zones.Add(zone); connections.AddRange(path); network.AddRange(path); return true;
                }
            }
            return false;
        }

        // Only frontage pockets with a verified physical approach may become parking.
        var parking = TryParking(block, obstacles, originalPaths);
        if (parking != null) zones.Add(parking);
        bool compact = block.Bounds.height < 45;
        bool sportFirst = block.Id == "NorthWest" || block.Id == "NorthEast" || block.Id == "WestNorth";
        if (!compact && sportFirst) Reserve(TownCourtyardKind.Sport, 8, 12);
        if (!compact) Reserve(TownCourtyardKind.Children, 14, 16);
        if (!Reserve(TownCourtyardKind.Quiet, 8, 10) && !Reserve(TownCourtyardKind.Quiet, 5, 7, true))
            throw new InvalidOperationException(block.Id + ": no connected quiet garden fits the accepted buildings/passages.");
        if (!sportFirst || compact) Reserve(TownCourtyardKind.Sport, 7, 8);
        Reserve(TownCourtyardKind.Refuse, 6, 5);
        var trees = GreenGroups(block, zones, network, obstacles);
        return new TownCourtyardBlock(block, zones, connections, trees);
    }

    private TownCourtyardZone Compose(string blockId, int number, TownCourtyardKind kind, Rect area,
        Vector3 entrance, Vector3 outward, bool small)
    {
        string id = blockId + "." + kind + number;
        var props = new List<TownCourtyardProp>();
        void Place(string model, float x, float z, float yaw = 0)
            => props.Add(new TownCourtyardProp(id + "." + props.Count, models[model],
                new Vector3(area.center.x + x, .16f, area.center.y + z), yaw));
        TownColor surface;
        switch (kind)
        {
            case TownCourtyardKind.Children:
                surface = TownColor.Blue;
                Place("PlaySlide", -4, -2.5f); Place("SwingSet", 3, -1); Place("Sandpit", 1.5f, 5);
                Fence(area, entrance, outward, id, props); break;
            case TownCourtyardKind.Sport:
                surface = TownColor.GreenStripe;
                if (area.height >= 12) Place("MiniCourt", 0, 4.8f, 180);
                else Place("OutdoorGym", 0, 0);
                break;
            case TownCourtyardKind.Quiet:
                surface = TownColor.Soil;
                if (small)
                {
                    Place("Bench", 0, 1.7f); Place("FlowerBed", 0, -2);
                    Place("HedgeModule", -1.9f, 0, 90); Place("LitterBin", 1.85f, 2.6f);
                }
                else
                {
                    Place("QuietPergola", 0, 1.2f); Place("Bench", 1.8f, -3.3f, 180);
                    Place("FlowerBed", -2.2f, -3.6f); Place("CourtyardLamp", 3.5f, 4);
                    Place("HedgeModule", 3.35f, 1, 90);
                    Place("LitterBin", -3.5f, 4);
                }
                break;
            case TownCourtyardKind.Refuse:
                surface = TownColor.Pavement; Place("BinEnclosure", 0, -.4f); break;
            default: throw new InvalidOperationException("Parking has a separate road-connected recipe.");
        }
        return new TownCourtyardZone(id, blockId, kind, area, entrance, outward, surface, props);
    }

    private void Fence(Rect area, Vector3 gate, Vector3 outward, string id, List<TownCourtyardProp> props)
    {
        void Side(Vector3 from, Vector3 to, float yaw)
        {
            var direction = (to - from).normalized;
            from += direction * .08f;
            to -= direction * .08f;
            float length = Vector3.Distance(from, to);
            void Run(float start, float end)
            {
                int pieces = Mathf.CeilToInt((end - start) / 3);
                if (pieces <= 0) return;
                float piece = (end - start) / pieces;
                for (int i = 0; i < pieces; i++)
                {
                    var point = Vector3.Lerp(from, to, (start + (i + .5f) * piece) / length);
                    // Length is a fence parameter; height, rail thickness and support grounding remain native.
                    props.Add(new TownCourtyardProp(id + ".Fence" + props.Count, models["FenceModule"],
                        point + Vector3.up * .16f, yaw, new Vector3(piece / models["FenceModule"].VisualBounds.size.x, 1, 1)));
                }
            }
            if (Mathf.Abs(Vector3.Dot((from + to) * .5f - gate, outward)) < .2f)
            { Run(0, length / 2 - 1.6f); Run(length / 2 + 1.6f, length); }
            else Run(0, length);
        }
        Side(new Vector3(area.xMin, 0, area.yMin), new Vector3(area.xMax, 0, area.yMin), 0);
        Side(new Vector3(area.xMin, 0, area.yMax), new Vector3(area.xMax, 0, area.yMax), 0);
        Side(new Vector3(area.xMin, 0, area.yMin), new Vector3(area.xMin, 0, area.yMax), 90);
        Side(new Vector3(area.xMax, 0, area.yMin), new Vector3(area.xMax, 0, area.yMax), 90);
    }

    private TownCourtyardZone TryParking(TownResidentialBlock block, List<Rect> solids, Rect[] paths)
    {
        var roadPoints = layout.roads.Where(r => r.connections == 5 || r.connections == 10)
            .Select(r => r.Position(layout.roadTileSize)).ToArray();
        foreach (int side in new[] { 0, 1, 2, 3 })
        {
            float yaw = side * 90;
            Vector3 inward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float width = side % 2 == 0 ? 10 : 14, depth = side % 2 == 0 ? 14 : 10;
            for (float offset = 8; offset < (side % 2 == 0 ? block.Bounds.width : block.Bounds.height) - 8; offset += 2)
            {
                Vector3 centre = side == 0 ? new Vector3(block.Bounds.xMin + offset, 0, block.Bounds.yMin + 10) :
                    side == 2 ? new Vector3(block.Bounds.xMin + offset, 0, block.Bounds.yMax - 10) :
                    side == 1 ? new Vector3(block.Bounds.xMin + 10, 0, block.Bounds.yMin + offset) :
                        new Vector3(block.Bounds.xMax - 10, 0, block.Bounds.yMin + offset);
                var area = new Rect(centre.x - width / 2, centre.z - depth / 2, width, depth);
                if (solids.Any(r => r.Overlaps(area)) || paths.Any(r => r.Overlaps(area))) continue;
                Vector3 entrance = centre - inward * 7;
                var road = roadPoints.Where(p => Vector3.Dot(p - entrance, inward) < 0 &&
                        Mathf.Abs(Vector3.Cross(p - entrance, inward).y) < .2f)
                    .OrderBy(p => Vector3.Distance(p, entrance)).FirstOrDefault();
                if (road == Vector3.zero || Vector3.Distance(road, entrance) > 22) continue;
                var approach = new TownResidentialWalkway(block.Id + ".ParkingApproach", road, centre + inward * 4, 3.8f);
                if (solids.Any(r => r.Overlaps(TownCourtyardGeometry.Path(approach, .1f)))) continue;
                var rotation = Quaternion.Euler(0, yaw, 0);
                string id = block.Id + ".Parking";
                var slots = new[] { -3.1f, 3.1f }.Select((x, i) => new TownCourtyardSlot(id + ".Slot" + i,
                    centre + rotation * new Vector3(x, 0, -1), yaw)).ToArray();
                return new TownCourtyardZone(id, block.Id, TownCourtyardKind.Parking, area, entrance, -inward,
                    TownColor.Asphalt, Array.Empty<TownCourtyardProp>(), slots, new[] { approach });
            }
        }
        return null;
    }

    private static IEnumerable<(Vector3 point, Vector3 outward)> Gates(Rect area)
    {
        yield return (new Vector3(area.xMin, 0, area.center.y), Vector3.left);
        yield return (new Vector3(area.xMax, 0, area.center.y), Vector3.right);
        yield return (new Vector3(area.center.x, 0, area.yMin), Vector3.back);
        yield return (new Vector3(area.center.x, 0, area.yMax), Vector3.forward);
    }

    private static float Distance(Vector3 point, TownResidentialWalkway path)
    {
        var delta = path.To - path.From;
        return Vector3.Distance(point, path.From + delta * Mathf.Clamp01(Vector3.Dot(point - path.From, delta) / delta.sqrMagnitude));
    }

    private static List<TownResidentialWalkway> Connect(string id, Vector3 gate, Vector3 outward,
        List<TownResidentialWalkway> network, Rect[] obstacles, Rect plot)
    {
        bool Clear(Vector3 a, Vector3 b) => !obstacles.Any(r => r.Overlaps(TownCourtyardGeometry.Path(new TownResidentialWalkway("probe", a, b), .08f)));
        var start = gate + outward * 1.35f;
        if (!Clear(gate, start)) return null;
        foreach (var target in network.OrderBy(p => Distance(start, p)))
        {
            var delta = target.To - target.From;
            var end = target.From + delta * Mathf.Clamp01(Vector3.Dot(start - target.From, delta) / delta.sqrMagnitude);
            foreach (var bend in new[] { new Vector3(end.x, 0, start.z), new Vector3(start.x, 0, end.z) })
            {
                if (!Clear(start, bend) || !Clear(bend, end)) continue;
                var points = new[] { gate, start, bend, end };
                var result = new List<TownResidentialWalkway>();
                for (int i = 1; i < points.Length; i++) if (Vector3.Distance(points[i - 1], points[i]) > .02f)
                    result.Add(new TownResidentialWalkway(id + ".Link" + i, points[i - 1], points[i]));
                if (result.All(p => TownCourtyardGeometry.Contains(TownCourtyardGeometry.Expand(plot, 1.8f), TownCourtyardGeometry.Path(p)))) return result;
            }
        }
        return null;
    }

    private static List<TownCourtyardTree> GreenGroups(TownResidentialBlock block,
        List<TownCourtyardZone> zones, List<TownResidentialWalkway> paths, List<Rect> solids)
    {
        var result = new List<TownCourtyardTree>();
        var garden = zones.First(z => z.Kind == TownCourtyardKind.Quiet).Bounds.center;
        var blocked = solids.Concat(zones.Select(z => z.Bounds)).ToArray();
        bool compact = block.Bounds.height < 45;
        // Smaller trees retain greenery in the tight townhouse courts without forcing crowns through buildings.
        float crownRadius = compact ? 1.9f : 2.7f;
        // Loose clusters follow the garden and walking spine; no repeated lattice of identical saplings.
        var candidates = new List<Vector3>();
        for (float z = block.Bounds.yMin + 5; z < block.Bounds.yMax - 5; z += 4)
            for (float x = block.Bounds.xMin + 5; x < block.Bounds.xMax - 5; x += 4)
            {
                var point = new Vector3(x + Mathf.Sin(z * .43f) * 1.1f, .07f, z + Mathf.Cos(x * .37f));
                if (blocked.Any(r => r.Overlaps(new Rect(point.x - crownRadius, point.z - crownRadius, crownRadius * 2, crownRadius * 2))) ||
                    paths.Any(p => Distance(point, p) < 1.85f) ||
                    zones.SelectMany(zon => zon.Approaches).Any(p => Distance(point, p) < 3.2f)) continue;
                candidates.Add(point);
            }
        foreach (var point in candidates.OrderBy(p => (new Vector2(p.x, p.z) - garden).sqrMagnitude))
        {
            if (result.Any(t => Vector3.Distance(point, t.Position) < (compact ? 4.5f : 6))) continue;
            int i = result.Count;
            float scale = compact ? .6f + i % 3 * .04f : .8f + i % 3 * .08f;
            result.Add(new TownCourtyardTree(block.Id + ".Tree" + i, point, scale, i * 73));
            if (result.Count >= (compact ? 4 : 12)) break;
        }
        return result;
    }
}
