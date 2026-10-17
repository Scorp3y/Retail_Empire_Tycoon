using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;

internal enum TownCourtyardKind { Children, Sport, Quiet, Parking, Refuse }

/// <summary>Stage-four functional places within the already accepted residential parcels.</summary>
internal sealed class TownCourtyardPlan
{
    public const string AssetRoot = "Assets/Art/TownCourtyards";
    public const string HierarchyRoot = "Courtyards stage 4";
    public IReadOnlyList<TownCourtyardBlock> Blocks { get; }
    public IReadOnlyList<Rect> AllowedAreas { get; }
    public IReadOnlyList<TownCourtyardModel> Models { get; }

    private TownCourtyardPlan(TownCourtyardBlock[] blocks, IReadOnlyList<TownCourtyardModel> models)
    {
        Blocks = Array.AsReadOnly(blocks);
        AllowedAreas = TownResidentialPlan.Create().AllowedAreas;
        var used = new HashSet<string>(blocks.SelectMany(b => b.Zones).SelectMany(z => z.Props).Select(p => p.ModelId));
        Models = Array.AsReadOnly(models.Where(m => used.Contains(m.Id)).ToArray());
    }

    public static TownCourtyardPlan Create(GameObject town)
    {
        var residential = TownResidentialPlan.Create();
        TownResidentialRoutes.ReadSaved(residential, town.transform);
        var models = TownCourtyardModels.Create();
        var planner = new TownCourtyardPlanner(town, models);
        return new TownCourtyardPlan(residential.Blocks.Select(planner.Build).ToArray(), models);
    }
}

internal sealed class TownCourtyardBlock
{
    public string Id => Residential.Id;
    public TownResidentialBlock Residential { get; }
    public IReadOnlyList<TownCourtyardZone> Zones { get; }
    public IReadOnlyList<TownResidentialWalkway> Paths { get; }
    public IReadOnlyList<TownCourtyardTree> Trees { get; }
    public TownCourtyardBlock(TownResidentialBlock residential, IEnumerable<TownCourtyardZone> zones,
        IEnumerable<TownResidentialWalkway> paths, IEnumerable<TownCourtyardTree> trees)
    {
        Residential = residential;
        Zones = Array.AsReadOnly(zones.ToArray());
        Paths = Array.AsReadOnly(paths.ToArray());
        Trees = Array.AsReadOnly(trees.ToArray());
    }
}

internal sealed class TownCourtyardZone
{
    public string Id { get; }
    public string BlockId { get; }
    public TownCourtyardKind Kind { get; }
    public Rect Bounds { get; }
    public Vector3 Entrance { get; }
    public Vector3 Outward { get; }
    public float SurfaceY => .16f;
    public TownColor SurfaceKind { get; }
    public IReadOnlyList<TownCourtyardProp> Props { get; }
    public IReadOnlyList<TownCourtyardSlot> Slots { get; }
    public IReadOnlyList<TownResidentialWalkway> Approaches { get; }
    public TownCourtyardZone(string id, string blockId, TownCourtyardKind kind, Rect bounds,
        Vector3 entrance, Vector3 outward, TownColor surface, IEnumerable<TownCourtyardProp> props,
        IEnumerable<TownCourtyardSlot> slots = null, IEnumerable<TownResidentialWalkway> approaches = null)
    {
        Id = id; BlockId = blockId; Kind = kind; Bounds = bounds; Entrance = entrance; Outward = outward;
        SurfaceKind = surface; Props = Array.AsReadOnly(props.ToArray());
        Slots = Array.AsReadOnly((slots ?? Array.Empty<TownCourtyardSlot>()).ToArray());
        Approaches = Array.AsReadOnly((approaches ?? Array.Empty<TownResidentialWalkway>()).ToArray());
    }
}

internal sealed class TownCourtyardProp
{
    public string Id { get; }
    public string ModelId => Model.Id;
    public TownCourtyardModel Model { get; }
    public Vector3 Position { get; }
    public float Yaw { get; }
    public Vector3 Scale { get; }
    public Matrix4x4 Pose => Matrix4x4.TRS(Position, Quaternion.Euler(0, Yaw, 0), Scale);
    public Bounds[] WorldSolids => Model.Solids.Select(s => TownCourtyardGeometry.Transform(s.Bounds, Pose)).ToArray();
    public Bounds VisualBounds => TownCourtyardGeometry.Transform(Model.VisualBounds, Pose);
    public TownCourtyardProp(string id, TownCourtyardModel model, Vector3 position, float yaw = 0, Vector3? scale = null)
    { Id = id; Model = model; Position = position; Yaw = yaw; Scale = scale ?? Vector3.one; }
}

internal sealed class TownCourtyardSlot
{
    public string Id { get; }
    public Vector3 Position { get; }
    public Vector3 Center => Position;
    public float Yaw { get; }
    public Rect Bounds { get; }
    public Vector2 Size => new Vector2(3.1f, 6);
    public TownCourtyardSlot(string id, Vector3 position, float yaw)
    {
        Id = id; Position = position; Yaw = yaw;
        Bounds = TownCourtyardGeometry.Rect(TownCourtyardGeometry.Transform(
            new Bounds(Vector3.zero, new Vector3(3.1f, 1, 6)), Matrix4x4.TRS(position, Quaternion.Euler(0, yaw, 0), Vector3.one)));
    }
}

internal sealed class TownCourtyardTree
{
    public string Id { get; }
    public Vector3 Position { get; }
    public float Scale { get; }
    public float Yaw { get; }
    public Matrix4x4 Pose => Matrix4x4.TRS(Position, Quaternion.Euler(0, Yaw, 0), Vector3.one * Scale);
    public Bounds Solid => TownCourtyardGeometry.Transform(new Bounds(Vector3.up * 1.05f,
        new Vector3(.44f, 2.1f, .44f)), Pose);
    public TownCourtyardTree(string id, Vector3 position, float scale, float yaw)
    { Id = id; Position = position; Scale = scale; Yaw = yaw; }
}

internal static class TownCourtyardGeometry
{
    public static Bounds Transform(Bounds local, Matrix4x4 pose)
    {
        var result = new Bounds(pose.MultiplyPoint3x4(local.min), Vector3.zero);
        for (int i = 1; i < 8; i++) result.Encapsulate(pose.MultiplyPoint3x4(new Vector3(
            (i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y,
            (i & 4) == 0 ? local.min.z : local.max.z)));
        return result;
    }
    public static Rect Rect(Bounds bounds) => UnityEngine.Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
    public static Rect Expand(Rect rect, float margin) => UnityEngine.Rect.MinMaxRect(
        rect.xMin - margin, rect.yMin - margin, rect.xMax + margin, rect.yMax + margin);
    public static Rect Path(TownResidentialWalkway path, float extra = 0)
        => Expand(UnityEngine.Rect.MinMaxRect(Mathf.Min(path.From.x, path.To.x), Mathf.Min(path.From.z, path.To.z),
            Mathf.Max(path.From.x, path.To.x), Mathf.Max(path.From.z, path.To.z)), path.Width / 2 + extra);
    public static bool Contains(Rect outer, Rect inner) => outer.xMin <= inner.xMin && outer.yMin <= inner.yMin &&
        outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;
}
