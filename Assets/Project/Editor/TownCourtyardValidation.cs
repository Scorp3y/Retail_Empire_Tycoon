using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;

/// <summary>Independent acceptance of saved courtyard geometry and the residential access it must preserve.</summary>
internal static class TownCourtyardValidation
{
    private const string ReportFolder = "Library/TownCourtyardQA";
    private const float Tolerance = .005f;

    public static void ValidateSaved(TownCourtyardPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        TownWorldSafety.RequireIsolatedEditor();
        ValidateGeometryContracts();
        TownCourtyardModels.ValidateCatalogue(plan.Models);
        var log = new StringBuilder();
        Directory.CreateDirectory(ReportFolder);
        var town = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        var meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        bool passed = false;
        try
        {
            var hierarchy = town.transform.Find(TownCourtyardPlan.HierarchyRoot);
            Require(hierarchy != null && hierarchy.childCount == plan.Blocks.Count,
                "Saved courtyard hierarchy is missing or contains undeclared blocks.");
            var original = TownResidentialPlan.Create();
            TownResidentialRoutes.ReadSaved(original, town.transform);
            TownResidentialValidation.ValidateSaved(original);
            var snapshot = CaptureResidentialSnapshot(town);
            Require(snapshot.BuildingCount == 62 && snapshot.WalkwayCount == 299,
                "The accepted 62 residential buildings and 299 original access paths are not intact.");
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            var obstacles = ReadSolids(town.transform).ToArray();
            // Old render chunks can contain triangles spanning a parcel edge; stage-three trimming deliberately
            // preserves them. Audit the actual complete town ground once, not only the new block's own meshes.
            var groundPalette = AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder + "/TownPalette.mat");
            Require(groundPalette != null && groundPalette.mainTexture != null, "The legacy ground atlas is missing.");
            var worldFloor = ReadFloor(town.transform, groundPalette);
            var floorIndex = new FloorIndex(worldFloor);
            int zones = 0, props = 0, paths = 0, trees = 0, slots = 0;
            foreach (var block in plan.Blocks)
            {
                Require(identifiers.Add(block.Id), "Duplicate courtyard block: " + block.Id);
                var saved = hierarchy.Find(block.Id);
                Require(saved != null && saved.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                    block.Id + ": saved courtyard is missing or introduces gameplay behaviours.");
                var geometry = saved.Find("Courtyard geometry");
                Require(geometry != null && geometry.GetComponentsInChildren<MeshFilter>(true).Length == 1,
                    block.Id + ": one real combined courtyard mesh is required.");
                var renderer = geometry.GetComponent<MeshRenderer>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder + "/TownPalette.mat");
                Require(renderer != null && renderer.enabled && material != null && renderer.sharedMaterials.Length == 1
                    && renderer.sharedMaterial == material, block.Id + ": combined furniture lost the existing shared palette.");
                var rendered = ReadRenderedTriangles(geometry);
                var savedMesh = geometry.GetComponent<MeshFilter>().sharedMesh;
                Require(savedMesh.uv2.Length == savedMesh.vertexCount, block.Id + ": combined geometry lost procedural native-metre coordinates.");
                var nativeVertices = savedMesh.vertices;
                var nativeUv = savedMesh.uv2;
                for (int vertex = 0; vertex < nativeVertices.Length; vertex++)
                    Require((nativeUv[vertex] - new Vector2(nativeVertices[vertex].x, nativeVertices[vertex].z)).sqrMagnitude < .000001f,
                        block.Id + ": procedural material scale no longer follows native world positions.");
                var floor = ReadFloor(geometry);
                var savedZones = saved.Find("Zones");
                Require(savedZones != null && savedZones.childCount == block.Zones.Count
                    && block.Zones.Any(zone => zone.Kind == TownCourtyardKind.Quiet),
                    block.Id + ": a connected quiet garden is required, not scattered individual objects.");
                var expectedSolids = new HashSet<Collider>();
                foreach (var zone in block.Zones)
                {
                    Require(identifiers.Add(zone.Id) && zone.BlockId == block.Id && Contains(block.Residential.Bounds, zone.Bounds),
                        zone.Id + ": duplicate zone, wrong parcel, or area outside the accepted residential ground.");
                    var marker = savedZones.Find(zone.Id);
                    Require(marker != null, zone.Id + ": assigned zone metadata is missing.");
                    var surface = marker.Find("Surface");
                    RequireMarker(surface, new Vector3(zone.Bounds.center.x, .12f, zone.Bounds.center.y), 0, Vector3.one, zone.Id + "/Surface");
                    var surfaceBox = surface.GetComponent<BoxCollider>();
                    Require(surfaceBox != null && surfaceBox.enabled && !surfaceBox.isTrigger && surfaceBox.center == Vector3.zero
                        && (surfaceBox.size - new Vector3(zone.Bounds.width, .08f, zone.Bounds.height)).sqrMagnitude < .000001f,
                        zone.Id + ": floor support must be a thin 0.08–0.16m slab, never an enclosing activity-zone box.");
                    expectedSolids.Add(surfaceBox);
                    RequireMarker(marker.Find("Entrance"), zone.Entrance, 0, Vector3.one, zone.Id + "/Entrance");
                    CheckSurface(floor, zone.Bounds, zone.SurfaceKind, zone.SurfaceY, zone.Id);
                    ValidateZoneProfile(zone, floorIndex, block.Paths.Concat(block.Residential.Walkways).ToArray());
                    if (zone.Kind != TownCourtyardKind.Parking)
                        CheckSavedPassage(new TownResidentialWalkway(zone.Id + ".Gateway", zone.Entrance - zone.Outward * 1.2f,
                            zone.Entrance + zone.Outward * 1.2f), obstacles, .28f);
                    foreach (var prop in zone.Props)
                    {
                        Require(identifiers.Add(prop.Id), "Duplicate courtyard object: " + prop.Id);
                        var authored = marker.Find("Props/" + prop.Id);
                        RequireMarker(authored, prop.Position, prop.Yaw, prop.Scale, prop.Id);
                        Require(prop.Model.Role == TownCourtyardRole.Fence || prop.Scale == Vector3.one,
                            prop.Id + ": native furniture cannot be arbitrarily scaled; only fence length adapts to its exact perimeter.");
                        Require(authored.Find(prop.ModelId) != null && authored.GetComponentsInChildren<Renderer>(true).Length == 0,
                            prop.Id + ": furniture markers must refer to their native model and not add duplicate renderers.");
                        var colliders = authored.GetComponentsInChildren<Collider>(true);
                        Require(colliders.Length == prop.Model.Solids.Count, prop.Id + ": real per-part collision proxies are missing.");
                        for (int index = 0; index < prop.Model.Solids.Count; index++)
                        {
                            var definition = prop.Model.Solids[index];
                            var solid = authored.Find("Solid" + index);
                            Require(solid != null && (solid.localPosition - definition.Center).sqrMagnitude < Tolerance * Tolerance
                                && Quaternion.Angle(solid.localRotation, definition.Rotation) < Tolerance
                                && solid.localScale == Vector3.one, prop.Id + ": physical part pose is not native.");
                            var box = solid.GetComponent<BoxCollider>();
                            Require(box != null && box.enabled && !box.isTrigger && box.center == Vector3.zero
                                && (box.size - definition.Size).sqrMagnitude < Tolerance * Tolerance,
                                prop.Id + ": saved physical part differs from its real post/leg/beam.");
                            expectedSolids.Add(box);
                        }
                        Require(Mathf.Abs(prop.VisualBounds.min.y - zone.SurfaceY) < Tolerance,
                            prop.Id + ": furniture floats or sinks relative to the real shared ground.");
                        Require(Contains(TownCourtyardGeometry.Expand(zone.Bounds, .4f), TownCourtyardGeometry.Rect(prop.VisualBounds)),
                            prop.Id + ": visual object leaves its functional zone.");
                        if (!meshes.TryGetValue(prop.ModelId, out var mesh)) meshes.Add(prop.ModelId, mesh = prop.Model.BuildMesh());
                        RequireRenderedModel(rendered, mesh, prop.Pose, prop.Id);
                        CheckModelIntersections(prop, authored, mesh, obstacles);
                        props++;
                    }
                    ValidateParking(zone, marker, floorIndex, obstacles);
                    slots += zone.Slots.Count;
                    zones++;
                }
                var routeRoot = saved.Find("Walkways");
                Require(routeRoot != null && routeRoot.childCount == block.Paths.Count,
                    block.Id + ": saved functional-zone access paths differ from the plan.");
                foreach (var path in block.Paths)
                {
                    var marker = routeRoot.Find(path.Id);
                    Require(marker != null, path.Id + ": saved route marker is missing.");
                    RequireMarker(marker.Find("From"), path.From, 0, Vector3.one, path.Id + "/From");
                    RequireMarker(marker.Find("To"), path.To, 0, Vector3.one, path.Id + "/To");
                    CheckSavedPassage(path, obstacles, .28f);
                    CheckPathSurface(path, floorIndex);
                    paths++;
                }
                ValidateNetwork(block);
                var treeRoot = saved.Find("Trees");
                Require(treeRoot != null && treeRoot.childCount == block.Trees.Count, block.Id + ": saved tree groups differ from the plan.");
                foreach (var tree in block.Trees)
                {
                    var marker = treeRoot.Find(tree.Id);
                    RequireMarker(marker, tree.Position, tree.Yaw, Vector3.one * tree.Scale, tree.Id);
                    var box = marker.GetComponent<BoxCollider>();
                    Require(box != null && box.enabled && !box.isTrigger && (box.center - Vector3.up * 1.05f).sqrMagnitude < .000001f
                        && (box.size - new Vector3(.44f, 2.1f, .44f)).sqrMagnitude < .000001f,
                        tree.Id + ": real tree trunk collision is missing or encloses its crown.");
                    expectedSolids.Add(box);
                    Require(Mathf.Abs(tree.Position.y - .07f) < Tolerance, tree.Id + ": tree does not stand on the preserved grass.");
                    var sourceTree = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.FindAssets("TreeRound t:Prefab",
                        new[] { "Assets/Art/TownKit/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath)
                        .Single(path => Path.GetFileNameWithoutExtension(path) == "TreeRound"));
                    Require(sourceTree != null && sourceTree.GetComponent<MeshFilter>().sharedMesh != null,
                        tree.Id + ": native grouped-tree source is missing.");
                    RequireRenderedModel(rendered, sourceTree.GetComponent<MeshFilter>().sharedMesh, tree.Pose, tree.Id);
                    trees++;
                }
                Require(saved.GetComponentsInChildren<Collider>(true).All(expectedSolids.Contains),
                    block.Id + ": undeclared collider fills a zone, canopy or access corridor.");
                CheckNativeArchitectureDetails(town.transform.Find(TownResidentialPlan.HierarchyRoot + "/" + block.Id), expectedSolids);
                foreach (var pair in block.Zones.SelectMany((zone, index) => block.Zones.Skip(index + 1).Select(other => (zone, other))))
                    Require(!pair.zone.Bounds.Overlaps(pair.other.Bounds), block.Id + ": assigned functional areas overlap.");
                foreach (var refuse in block.Zones.Where(zone => zone.Kind == TownCourtyardKind.Refuse))
                    foreach (var peaceful in block.Zones.Where(zone => zone.Kind == TownCourtyardKind.Children || zone.Kind == TownCourtyardKind.Quiet))
                        Require(!TownCourtyardGeometry.Expand(peaceful.Bounds, 4.99f).Overlaps(refuse.Bounds),
                            refuse.Id + ": refuse enclosure is not separated from children and quiet recreation by five metres.");
                bool Meaningful(Vector3 point) => block.Zones.Any(zone => Inside(zone.Bounds, point)
                    && (zone.Kind == TownCourtyardKind.Parking || zone.Props.Where(prop => prop.Model.Role != TownCourtyardRole.Fence
                        && prop.Model.Role != TownCourtyardRole.Lighting).Any(prop =>
                            Vector2.Distance(new Vector2(point.x, point.z), new Vector2(prop.Position.x, prop.Position.z)) <= 6)));
                float blank = LargestBlankPavedSquare(block.Residential.Bounds, floorIndex, Meaningful,
                    block.Residential.Buildings.SelectMany(building => building.WorldBodies).ToArray());
                Require(blank < 12, block.Id + ": an actual unused paved square of " + blank.ToString("F1", CultureInfo.InvariantCulture)
                    + " m remains; unrelated unbuilt grass is not part of this measurement.");
                log.AppendLine(block.Id + ": " + string.Join(", ", block.Zones.Select(zone => zone.Kind.ToString()))
                    + "; " + block.Paths.Count + " new access paths; " + block.Trees.Count + " grouped trees; largest unassigned hardscape square "
                    + blank.ToString("F1", CultureInfo.InvariantCulture) + " m (0.5m sampling).");
                int legacySamples = 0;
                for (float z = block.Residential.Bounds.yMin + .25f; z < block.Residential.Bounds.yMax; z += .5f)
                    for (float x = block.Residential.Bounds.xMin + .25f; x < block.Residential.Bounds.xMax; x += .5f)
                        if (floorIndex.IsVisibleLegacyPaving(new Vector3(x, .16f, z))) legacySamples++;
                log.AppendLine(block.Id + ": " + legacySamples + " visible legacy hardscape samples above 0.07m audited with all original render chunks.");
                log.AppendLine(block.Id + ": native triangle comparison used " + rendered.FallbackCount + " spatial fallback matches; maximum actual XYZ delta "
                    + rendered.MaximumDelta.ToString("R", CultureInfo.InvariantCulture) + " m (limit 0.0001m, UV limit 0.000001).");
            }
            Require(plan.Blocks.SelectMany(block => block.Zones).Any(zone => zone.Kind == TownCourtyardKind.Children), "No real shared play area was authored.");
            Require(plan.Blocks.SelectMany(block => block.Zones).Any(zone => zone.Kind == TownCourtyardKind.Refuse), "No separate refuse enclosure was authored.");
            var models = plan.Blocks.SelectMany(block => block.Zones).SelectMany(zone => zone.Props).Select(prop => prop.Model.Role).ToArray();
            Require(models.Contains(TownCourtyardRole.FitnessEquipment) && models.Contains(TownCourtyardRole.BasketballHoop)
                && models.Contains(TownCourtyardRole.Pergola), "The overall courtyard set lost workout, mini-court or sheltered recreation.");
            int renderers = town.GetComponentsInChildren<Renderer>(true).Length;
            int triangles = town.GetComponentsInChildren<MeshFilter>(true).Sum(filter => filter.sharedMesh != null ? filter.sharedMesh.triangles.Length / 3 : 0);
            Require(renderers <= 480 && triangles <= 1400000, "Courtyard town geometry exceeds 480 renderers / 1.4M triangles.");
            Require(town.GetComponentsInChildren<TownAmbientWalker>(true).Length == 0, "Decorative townspeople returned during courtyard authoring.");
            log.AppendLine($"PASS {zones} real functional zones / {props} rendered native objects / {paths} new paths / {trees} trees / {slots} parking bays.");
            log.AppendLine($"PASS original {snapshot.BuildingCount} buildings / {snapshot.WalkwayCount} full-width routes; actual mesh/solid/surface/gate acceptance.");
            log.AppendLine($"PASS town budget {renderers}/480 renderers / {triangles}/1400000 triangles. This is not a frame-time measurement.");
            passed = true;
        }
        finally
        {
            foreach (var mesh in meshes.Values) UnityEngine.Object.DestroyImmediate(mesh);
            PrefabUtility.UnloadPrefabContents(town);
            File.WriteAllText(ReportFolder + "/assets.txt", (passed ? "PASS\n" : "FAILED\n") + log);
        }
    }

    internal sealed class ResidentialSnapshot
    {
        public string Hash { get; }
        public int BuildingCount { get; }
        public int WalkwayCount { get; }

        public ResidentialSnapshot(string hash, int buildings, int walkways)
        {
            Hash = hash;
            BuildingCount = buildings;
            WalkwayCount = walkways;
        }

        public void RequireUnchanged(ResidentialSnapshot current)
        {
            Require(current != null && Hash == current.Hash && BuildingCount == current.BuildingCount
                && WalkwayCount == current.WalkwayCount,
                "Courtyard authoring changed protected residential architecture, original ground, or saved entrance paths.");
        }

        public override string ToString() => Hash + " / " + BuildingCount + " buildings / " + WalkwayCount + " paths";
    }

    /// <summary>
    /// Includes actual native mesh/material/texture bytes, saved poses, individual wing boxes, original ground,
    /// and route marker transforms. Only the replaceable stage-three tree meshes and trunks are omitted.
    /// </summary>
    public static ResidentialSnapshot CaptureResidentialSnapshot(GameObject town)
    {
        if (town == null) throw new ArgumentNullException(nameof(town));
        var residential = town.transform.Find(TownResidentialPlan.HierarchyRoot);
        Require(residential != null, "Missing protected stage-three residential hierarchy.");
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        int buildings = 0, paths = 0;
        using (var bytes = new MemoryStream())
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
        {
            WriteTransform(writer, town.transform);
            WriteTransform(writer, residential);
            foreach (var block in TownResidentialPlan.Create().Blocks)
            {
                var savedBlock = residential.Find(block.Id);
                Require(savedBlock != null, block.Id + ": protected residential block is missing.");
                writer.Write(block.Id);
                WriteTransform(writer, savedBlock);
                foreach (var building in block.Buildings.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    var saved = savedBlock.Find(building.Id);
                    Require(saved != null, building.Id + ": protected native building is missing.");
                    writer.Write(building.Id);
                    WriteProtectedGeometry(writer, saved, dependencies);
                    buildings++;
                }
                var ground = savedBlock.Find("Residential ground and walkways");
                Require(ground != null, block.Id + ": protected residential ground is missing.");
                writer.Write("Original ground");
                WriteProtectedGeometry(writer, ground, dependencies);
                var walkways = savedBlock.Find("Walkways");
                Require(walkways != null, block.Id + ": protected saved route markers are missing.");
                WriteTransform(writer, walkways);
                foreach (Transform route in walkways.Cast<Transform>().OrderBy(item => item.name, StringComparer.Ordinal))
                {
                    writer.Write(route.name);
                    WriteTransform(writer, route);
                    var from = route.Find("From");
                    var to = route.Find("To");
                    Require(from != null && to != null && route.childCount == 2,
                        block.Id + "/" + route.name + ": incomplete protected walkway metadata.");
                    WriteTransform(writer, from);
                    WriteTransform(writer, to);
                    paths++;
                }
            }
            foreach (string path in dependencies.OrderBy(item => item, StringComparer.Ordinal))
            {
                Require(File.Exists(path), "Protected native dependency does not exist: " + path);
                writer.Write(path);
                writer.Write(FileDigest(path));
                if (File.Exists(path + ".meta")) writer.Write(FileDigest(path + ".meta"));
            }
            writer.Flush();
            using (var hash = SHA256.Create())
                return new ResidentialSnapshot(Hex(hash.ComputeHash(bytes.ToArray())), buildings, paths);
        }
    }

    private static void WriteProtectedGeometry(BinaryWriter writer, Transform root, HashSet<string> dependencies)
    {
        var transforms = root.GetComponentsInChildren<Transform>(true)
            .OrderBy(item => RelativePath(root, item), StringComparer.Ordinal).ToArray();
        writer.Write(transforms.Length);
        foreach (var transform in transforms)
        {
            writer.Write(RelativePath(root, transform));
            WriteTransform(writer, transform);
            var filters = transform.GetComponents<MeshFilter>();
            writer.Write(filters.Length);
            foreach (var filter in filters)
            {
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                Require(filter.sharedMesh != null && !string.IsNullOrEmpty(path),
                    root.name + ": protected building or ground has no native mesh asset.");
                writer.Write(path);
                AddDependencies(path, dependencies);
            }
            var renderers = transform.GetComponents<Renderer>();
            writer.Write(renderers.Length);
            foreach (var renderer in renderers)
            {
                writer.Write(renderer.enabled);
                writer.Write((int)renderer.shadowCastingMode);
                writer.Write(renderer.receiveShadows);
                writer.Write(renderer.sharedMaterials.Length);
                foreach (var material in renderer.sharedMaterials)
                {
                    string path = AssetDatabase.GetAssetPath(material);
                    Require(material != null && !string.IsNullOrEmpty(path), root.name + ": protected material is missing.");
                    writer.Write(path);
                    AddDependencies(path, dependencies);
                }
            }
            var colliders = transform.GetComponents<Collider>();
            writer.Write(colliders.Length);
            foreach (var collider in colliders)
            {
                Require(collider is BoxCollider, root.name + ": protected native wing is no longer an individual box.");
                var box = (BoxCollider)collider;
                writer.Write(collider.GetType().Name);
                writer.Write(collider.enabled);
                writer.Write(collider.isTrigger);
                WriteVector(writer, box.center);
                WriteVector(writer, box.size);
                writer.Write(AssetDatabase.GetAssetPath(collider.sharedMaterial));
            }
            writer.Write(transform.GetComponents<MonoBehaviour>().Length);
        }
    }

    private static void WriteTransform(BinaryWriter writer, Transform transform)
    {
        writer.Write(transform.name);
        writer.Write(transform.gameObject.activeSelf);
        WriteVector(writer, transform.localPosition);
        writer.Write(transform.localRotation.x);
        writer.Write(transform.localRotation.y);
        writer.Write(transform.localRotation.z);
        writer.Write(transform.localRotation.w);
        WriteVector(writer, transform.localScale);
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    private static void AddDependencies(string path, HashSet<string> dependencies)
    {
        foreach (string dependency in AssetDatabase.GetDependencies(path, true))
            if (!AssetDatabase.IsValidFolder(dependency)) dependencies.Add(dependency);
    }

    private static string RelativePath(Transform root, Transform child)
    {
        if (child == root) return string.Empty;
        var parts = new List<string>();
        while (child != root)
        {
            Require(child != null, "Protected geometry hierarchy is not a descendant of its declared building.");
            parts.Add(child.name);
            child = child.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string FileDigest(string path)
    {
        using (var source = File.OpenRead(path))
        using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(source));
    }

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

    private readonly struct OrientedSolid
    {
        public readonly string Name;
        public readonly Vector3 Center;
        public readonly Vector3 Extents;
        public readonly Quaternion Rotation;
        public readonly Collider Collider;
        public Vector3[] Axes => new[] { Rotation * Vector3.right, Rotation * Vector3.up, Rotation * Vector3.forward };

        public OrientedSolid(string name, Vector3 center, Vector3 extents, Quaternion rotation, Collider collider = null)
        {
            Name = name;
            Center = center;
            Extents = extents;
            Rotation = rotation;
            Collider = collider;
        }

        public static OrientedSolid FromBox(BoxCollider box)
        {
            Vector3 scale = box.transform.lossyScale;
            return new OrientedSolid(HierarchyPath(box.transform), box.transform.TransformPoint(box.center),
                Vector3.Scale(box.size / 2, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                box.transform.rotation, box);
        }
    }

    // Separating-axis comparison uses each real post/leg/wing box. A pergola reservation is never a solid.
    private static bool Intersects(OrientedSolid first, OrientedSolid second, float tolerance = .003f)
    {
        var firstAxes = first.Axes;
        var secondAxes = second.Axes;
        var directions = firstAxes.Concat(secondAxes)
            .Concat(firstAxes.SelectMany(a => secondAxes.Select(b => Vector3.Cross(a, b))));
        foreach (Vector3 rawAxis in directions)
        {
            if (rawAxis.sqrMagnitude < .000001f) continue;
            Vector3 axis = rawAxis.normalized;
            float firstRadius = Radius(first, axis, firstAxes);
            float secondRadius = Radius(second, axis, secondAxes);
            if (Mathf.Abs(Vector3.Dot(second.Center - first.Center, axis)) >= firstRadius + secondRadius - tolerance)
                return false;
        }
        return true;
    }

    private static float Radius(OrientedSolid solid, Vector3 axis, Vector3[] basis)
        => Mathf.Abs(Vector3.Dot(axis, basis[0])) * solid.Extents.x
            + Mathf.Abs(Vector3.Dot(axis, basis[1])) * solid.Extents.y
            + Mathf.Abs(Vector3.Dot(axis, basis[2])) * solid.Extents.z;

    private static bool TriangleIntersects(Vector3 first, Vector3 second, Vector3 third, OrientedSolid solid)
    {
        Quaternion inverse = Quaternion.Inverse(solid.Rotation);
        var points = new[] { inverse * (first - solid.Center), inverse * (second - solid.Center), inverse * (third - solid.Center) };
        var edges = new[] { points[1] - points[0], points[2] - points[1], points[0] - points[2] };
        var basis = new[] { Vector3.right, Vector3.up, Vector3.forward };
        var axes = basis.Concat(new[] { Vector3.Cross(edges[0], edges[1]) })
            .Concat(edges.SelectMany(edge => basis.Select(axis => Vector3.Cross(edge, axis))));
        foreach (Vector3 rawAxis in axes)
        {
            if (rawAxis.sqrMagnitude < .000001f) continue;
            Vector3 axis = rawAxis.normalized;
            float minimum = points.Min(point => Vector3.Dot(point, axis));
            float maximum = points.Max(point => Vector3.Dot(point, axis));
            float radius = Mathf.Abs(axis.x) * solid.Extents.x + Mathf.Abs(axis.y) * solid.Extents.y + Mathf.Abs(axis.z) * solid.Extents.z;
            if (minimum >= radius - .003f || maximum <= -radius + .003f) return false;
        }
        return true;
    }

    private readonly struct FloorTriangle
    {
        public readonly Vector3 First, Second, Third;
        public readonly Vector2 Uv;
        public readonly Rect Bounds;
        public readonly bool IsLegacy;

        public FloorTriangle(Vector3 first, Vector3 second, Vector3 third, Vector2 uv, bool legacy = false)
        {
            First = first;
            Second = second;
            Third = third;
            Uv = uv;
            IsLegacy = legacy;
            Bounds = Rect.MinMaxRect(Mathf.Min(first.x, second.x, third.x), Mathf.Min(first.z, second.z, third.z),
                Mathf.Max(first.x, second.x, third.x), Mathf.Max(first.z, second.z, third.z));
        }

        public bool Covers(Vector3 point, Vector2 uv, float height)
            => (Uv - uv).sqrMagnitude < .000001f && Mathf.Abs(First.y - height) < .025f && CoversXZ(point);

        public bool CoversXZ(Vector3 point)
        {
            if (point.x < Bounds.xMin - .001f || point.x > Bounds.xMax + .001f
                || point.z < Bounds.yMin - .001f || point.z > Bounds.yMax + .001f) return false;
            float a = Side(First, Second, point), b = Side(Second, Third, point), c = Side(Third, First, point);
            return !(a < -.001f || b < -.001f || c < -.001f) || !(a > .001f || b > .001f || c > .001f);
        }

        private static float Side(Vector3 a, Vector3 b, Vector3 point)
            => (b.x - a.x) * (point.z - a.z) - (b.z - a.z) * (point.x - a.x);
    }

    private static List<FloorTriangle> ReadFloor(Transform geometry, Material groundPalette = null)
    {
        var result = new List<FloorTriangle>();
        foreach (var filter in geometry.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<Renderer>();
            if (!filter.gameObject.activeInHierarchy || renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
            // UV coordinates only have meaning together with their atlas. Surface-material variants sharing the
            // legacy texture are valid; the separate 32x3 architecture atlas is not a ground colour classifier.
            if (groundPalette != null && (renderer.sharedMaterials.Length == 0
                || renderer.sharedMaterials.Any(material => material == null || material.mainTexture != groundPalette.mainTexture))) continue;
            var mesh = filter.sharedMesh;
            var positions = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            var uv = mesh.uv;
            Require(uv.Length == positions.Length, filter.name + ": real floor mesh has missing atlas coordinates.");
            var indices = mesh.triangles;
            bool legacy = true;
            for (Transform parent = filter.transform; parent != null; parent = parent.parent)
                if (parent.name == TownResidentialPlan.HierarchyRoot || parent.name == TownCourtyardPlan.HierarchyRoot)
                { legacy = false; break; }
            for (int triangle = 0; triangle < indices.Length; triangle += 3)
            {
                int a = indices[triangle], b = indices[triangle + 1], c = indices[triangle + 2];
                if (positions[a].y < .05f || positions[a].y > .27f || Mathf.Abs(positions[a].y - positions[b].y) > .001f
                    || Mathf.Abs(positions[a].y - positions[c].y) > .001f || (uv[a] - uv[b]).sqrMagnitude > .000001f
                    || (uv[a] - uv[c]).sqrMagnitude > .000001f) continue;
                if (Mathf.Abs(Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).normalized.y) < .99f) continue;
                result.Add(new FloorTriangle(positions[a], positions[b], positions[c], uv[a], legacy));
            }
        }
        return result;
    }

    private static void CheckSurface(List<FloorTriangle> floor, Rect bounds, TownColor color, float height, string name)
    {
        Require(bounds.width > 0 && bounds.height > 0, name + ": surface has invalid real metre dimensions.");
        int countX = Mathf.Max(1, Mathf.CeilToInt(bounds.width / .5f));
        int countZ = Mathf.Max(1, Mathf.CeilToInt(bounds.height / .5f));
        Vector2 uv = TownPalette.UV(color);
        for (int z = 0; z <= countZ; z++)
            for (int x = 0; x <= countX; x++)
            {
                var point = new Vector3(Mathf.Lerp(bounds.xMin + .01f, bounds.xMax - .01f, (float)x / countX), height,
                    Mathf.Lerp(bounds.yMin + .01f, bounds.yMax - .01f, (float)z / countZ));
                Require(floor.Any(triangle => triangle.Covers(point, uv, height)), name + ": real shared surface is missing at " + point + ".");
            }
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        return Vector3.Distance(point, from + direction * Mathf.Clamp01(Vector3.Dot(point - from, direction) / direction.sqrMagnitude));
    }

    private static bool Inside(Rect rectangle, Vector3 point, float inset = 0)
        => point.x >= rectangle.xMin + inset && point.x <= rectangle.xMax - inset
            && point.z >= rectangle.yMin + inset && point.z <= rectangle.yMax - inset;

    private static bool Contains(Rect outer, Rect inner)
        => inner.xMin >= outer.xMin - Tolerance && inner.xMax <= outer.xMax + Tolerance
            && inner.yMin >= outer.yMin - Tolerance && inner.yMax <= outer.yMax + Tolerance;

    private static IEnumerable<OrientedSolid> ReadSolids(Transform root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)
            .Where(item => item.enabled && !item.isTrigger && item.gameObject.activeInHierarchy))
        {
            if (collider is BoxCollider box) yield return OrientedSolid.FromBox(box);
            else
            {
                var bounds = TownResidentialPlacement.ColliderBounds(collider);
                yield return new OrientedSolid(HierarchyPath(collider.transform), bounds.center, bounds.extents, Quaternion.identity, collider);
            }
        }
    }

    private static void RequireMarker(Transform marker, Vector3 position, float yaw, Vector3 scale, string name)
    {
        Require(marker != null && marker.gameObject.activeInHierarchy && (marker.position - position).sqrMagnitude < Tolerance * Tolerance
            && Quaternion.Angle(marker.rotation, Quaternion.Euler(0, yaw, 0)) < Tolerance && (marker.lossyScale - scale).sqrMagnitude < .000001f,
            name + ": saved pose, native scale or active state differs from the authored plan.");
    }

    private static string HierarchyPath(Transform transform)
    {
        var root = transform.root;
        return root == transform ? root.name : root.name + "/" + RelativePath(root, transform);
    }

    private static void ValidateZoneProfile(TownCourtyardZone zone, FloorIndex floor, IReadOnlyList<TownResidentialWalkway> network)
    {
        Require(Mathf.Abs(zone.Entrance.y) < Tolerance && Mathf.Abs(zone.Outward.y) < Tolerance
            && zone.Outward.sqrMagnitude > .99f && Inside(TownCourtyardGeometry.Expand(zone.Bounds, .01f), zone.Entrance),
            zone.Id + ": functional-zone gate is not a native ground entrance.");
        var roles = zone.Props.Select(prop => prop.Model.Role).ToArray();
        if (zone.Kind == TownCourtyardKind.Children)
        {
            Require(zone.Bounds.width >= 12 && zone.Bounds.height >= 12
                && (zone.SurfaceKind == TownColor.Blue || zone.SurfaceKind == TownColor.Rubber)
                && zone.Props.Where(prop => prop.Model.Role == TownCourtyardRole.PlayEquipment).Select(prop => prop.ModelId).Distinct().Count() >= 3
                && roles.Count(role => role == TownCourtyardRole.Fence) >= 8,
                zone.Id + ": play equipment must share a real fenced ground area, not remain scattered on grass.");
            ValidateCurbAndFence(zone, floor);
        }
        else if (zone.Kind == TownCourtyardKind.Sport)
        {
            Require(zone.Bounds.width * zone.Bounds.height >= 56 && zone.SurfaceKind == TownColor.GreenStripe
                && (roles.Contains(TownCourtyardRole.FitnessEquipment) || roles.Contains(TownCourtyardRole.BasketballHoop)),
                zone.Id + ": a gym or mini-court needs its own continuous sports surface.");
            if (roles.Contains(TownCourtyardRole.BasketballHoop))
            {
                foreach (var point in new[] {
                    new Vector3(zone.Bounds.xMin+.8f,0,zone.Bounds.center.y),new Vector3(zone.Bounds.xMax-.8f,0,zone.Bounds.center.y),
                    new Vector3(zone.Bounds.center.x,0,zone.Bounds.yMin+.8f),new Vector3(zone.Bounds.center.x+1.8f,0,zone.Bounds.center.y),
                    new Vector3(zone.Bounds.center.x-1.8f,0,zone.Bounds.center.y) })
                    Require(floor.HasSurface(point, TownColor.White, .18f), zone.Id + ": real mini-court lines or circle are missing.");
            }
        }
        else if (zone.Kind == TownCourtyardKind.Quiet)
            Require(zone.Bounds.width * zone.Bounds.height >= 35 && roles.Contains(TownCourtyardRole.Seating)
                && roles.Contains(TownCourtyardRole.Planting), zone.Id + ": quiet recreation needs grouped seating and planting.");
        else if (zone.Kind == TownCourtyardKind.Refuse)
            Require(zone.Props.Any(prop => prop.ModelId == "BinEnclosure") && zone.SurfaceKind == TownColor.Pavement,
                zone.Id + ": containers need a separate actual enclosure and service ground.");
        if (zone.Kind != TownCourtyardKind.Parking)
            Require(network.Any(path => DistanceToSegment(zone.Entrance, path.From, path.To) < .05f),
                zone.Id + ": entrance does not connect to an actual authored public path.");
    }

    private static void ValidateCurbAndFence(TownCourtyardZone zone, FloorIndex floor)
    {
        int curbChecks = 0, curbMatches = 0, fenceChecks = 0, fenceMatches = 0;
        var corners = new[] { new Vector3(zone.Bounds.xMin,0,zone.Bounds.yMin),new Vector3(zone.Bounds.xMax,0,zone.Bounds.yMin),
            new Vector3(zone.Bounds.xMax,0,zone.Bounds.yMax),new Vector3(zone.Bounds.xMin,0,zone.Bounds.yMax) };
        for (int side = 0; side < 4; side++)
        {
            var from = corners[side]; var to = corners[(side + 1) % 4];
            int samples = Mathf.CeilToInt(Vector3.Distance(from, to) / .5f);
            for (int index = 1; index < samples; index++)
            {
                var point = Vector3.Lerp(from, to, (float)index / samples);
                bool gateEdge = Mathf.Abs(Vector3.Dot(point - zone.Entrance, zone.Outward)) < .1f;
                if (!gateEdge || Vector3.Distance(point, zone.Entrance) > 1.7f)
                {
                    curbChecks++;
                    if (floor.HasSurface(point, TownColor.Stone, .21f)) curbMatches++;
                }
                if (!gateEdge || Vector3.Distance(point, zone.Entrance) > 3.5f)
                {
                    fenceChecks++;
                    if (zone.Props.Where(prop => prop.Model.Role == TownCourtyardRole.Fence)
                        .Any(prop => Inside(TownCourtyardGeometry.Expand(TownCourtyardGeometry.Rect(prop.VisualBounds), .08f), point))) fenceMatches++;
                }
            }
        }
        Require(curbChecks > 0 && (float)curbMatches / curbChecks >= .85f,
            zone.Id + ": a genuine stone border, with a gateway gap, is missing from the saved surface.");
        Require(fenceChecks > 0 && (float)fenceMatches / fenceChecks >= .75f,
            zone.Id + ": physical fence modules do not define a coherent perimeter around the shared play surface.");
    }

    private static void CheckModelIntersections(TownCourtyardProp prop, Transform authored, Mesh mesh, OrientedSolid[] obstacles)
    {
        var relevant = obstacles.Where(solid => !solid.Collider.transform.IsChildOf(authored)
            && solid.Collider.transform != authored && BoundsOf(solid).Intersects(prop.VisualBounds)).ToArray();
        if (relevant.Length == 0) return;
        var positions = mesh.vertices.Select(prop.Pose.MultiplyPoint3x4).ToArray();
        var triangles = mesh.triangles;
        for (int index = 0; index < triangles.Length; index += 3)
            foreach (var other in relevant)
                Require(!TriangleIntersects(positions[triangles[index]], positions[triangles[index + 1]], positions[triangles[index + 2]], other),
                    prop.Id + ": native visual geometry penetrates a foreign physical part " + other.Name + ".");
    }

    private static void CheckNativeArchitectureDetails(Transform residential, HashSet<Collider> newColliders)
    {
        var solids = newColliders.OfType<BoxCollider>().Where(box => box.transform.name != "Surface")
            .Select(OrientedSolid.FromBox).ToArray();
        foreach (var building in residential.Cast<Transform>().Where(child => child.name.StartsWith(residential.name + ".", StringComparison.Ordinal)))
            foreach (var filter in building.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var envelope = TownCourtyardGeometry.Transform(mesh.bounds, filter.transform.localToWorldMatrix);
                var candidates = solids.Where(solid => BoundsOf(solid).Intersects(envelope)).ToArray();
                if (candidates.Length == 0) continue;
                var positions = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                var indices = mesh.triangles;
                for (int triangle = 0; triangle < indices.Length; triangle += 3)
                {
                    Vector3 a = positions[indices[triangle]], b = positions[indices[triangle + 1]], c = positions[indices[triangle + 2]];
                    var bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                    foreach (var solid in candidates.Where(candidate => BoundsOf(candidate).Intersects(bounds)))
                        Require(!TriangleIntersects(a, b, c, solid),
                            building.name + ": a real balcony/canopy/facade triangle penetrates new courtyard physical part " + solid.Name + ".");
                }
            }
    }

    private static Bounds BoundsOf(OrientedSolid solid)
    {
        var bounds = new Bounds(solid.Center, Vector3.zero);
        for (int index = 0; index < 8; index++) bounds.Encapsulate(solid.Center + solid.Rotation * Vector3.Scale(solid.Extents,
            new Vector3((index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1)));
        return bounds;
    }

    private static void CheckSavedPassage(TownResidentialWalkway route, OrientedSolid[] obstacles, float longitudinal)
    {
        Require(route.Width >= 2.2f && Vector3.Distance(route.From, route.To) > .001f
            && Mathf.Abs(route.From.y) < Tolerance && Mathf.Abs(route.To.y) < Tolerance, route.Id + ": invalid native-width ground route.");
        int samples = Mathf.CeilToInt(Vector3.Distance(route.From, route.To) / .5f);
        Quaternion rotation = Quaternion.LookRotation(route.To - route.From);
        for (int sample = 0; sample <= samples; sample++)
        {
            var passage = new OrientedSolid(route.Id, Vector3.Lerp(route.From, route.To, (float)sample / samples) + Vector3.up,
                new Vector3(route.Width / 2, .7f, longitudinal), rotation);
            foreach (var obstacle in obstacles)
                Require(!Intersects(passage, obstacle), route.Id + ": real full-width corridor is blocked by " + obstacle.Name + ".");
        }
    }

    private static void CheckPathSurface(TownResidentialWalkway route, FloorIndex floor)
    {
        Vector3 side = Vector3.Cross(Vector3.up, (route.To - route.From).normalized);
        int samples = Mathf.CeilToInt(Vector3.Distance(route.From, route.To) / .5f);
        foreach (float offset in new[] { -1f, -.5f, 0f, .5f, 1f })
            for (int index = 0; index <= samples; index++)
            {
                var point = Vector3.Lerp(route.From, route.To, (float)index / samples) + side * offset * (route.Width / 2 - .01f);
                Require(floor.HasSurface(point, TownColor.Pavement, .16f),
                    route.Id + ": actual paving does not cover the full 2.2m route at " + point + ".");
            }
    }

    private static void ValidateNetwork(TownCourtyardBlock block)
    {
        var remaining = block.Paths.ToList();
        var connected = block.Residential.Walkways.ToList();
        bool Adjacent(TownResidentialWalkway first, TownResidentialWalkway second)
            => DistanceToSegment(first.From, second.From, second.To) < .05f || DistanceToSegment(first.To, second.From, second.To) < .05f
                || DistanceToSegment(second.From, first.From, first.To) < .05f || DistanceToSegment(second.To, first.From, first.To) < .05f;
        bool progress = true;
        while (progress && remaining.Count > 0)
        {
            progress = false;
            for (int index = remaining.Count - 1; index >= 0; index--)
                if (connected.Any(path => Adjacent(path, remaining[index])))
                { connected.Add(remaining[index]); remaining.RemoveAt(index); progress = true; }
        }
        Require(remaining.Count == 0, block.Id + ": a new zone walkway is disconnected from the original public door network.");
    }

    private static void ValidateParking(TownCourtyardZone zone, Transform marker, FloorIndex floor, OrientedSolid[] obstacles)
    {
        if (zone.Kind != TownCourtyardKind.Parking)
        {
            Require(zone.Slots.Count == 0 && zone.Approaches.Count == 0, zone.Id + ": non-parking zone declares vehicle data.");
            return;
        }
        Require(zone.Slots.Count >= 2 && zone.Approaches.Count > 0 && zone.SurfaceKind == TownColor.Asphalt,
            zone.Id + ": parking requires real marked bays and a physical street approach.");
        var savedSlots = marker.Find("Slots");
        var savedApproaches = marker.Find("Approaches");
        Require(savedSlots != null && savedSlots.childCount == zone.Slots.Count && savedApproaches != null
            && savedApproaches.childCount == zone.Approaches.Count, zone.Id + ": persisted parking geometry metadata is missing.");
        var layout = AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownWorldSetup.LayoutPath);
        Require(layout != null, zone.Id + ": actual public street layout is missing.");
        foreach (var slot in zone.Slots)
        {
            Require(Contains(zone.Bounds, slot.Bounds) && slot.Size.x >= 3 && slot.Size.y >= 6, slot.Id + ": bay is too small or outside the real parking apron.");
            RequireMarker(savedSlots.Find(slot.Id), slot.Position, slot.Yaw, Vector3.one, slot.Id);
            var vehicle = new OrientedSolid(slot.Id, slot.Position + Vector3.up * 1.1f,
                new Vector3(1.25f, .65f, 2.3f), Quaternion.Euler(0, slot.Yaw, 0));
            foreach (var obstacle in obstacles) Require(!Intersects(vehicle, obstacle), slot.Id + ": native pickup envelope is obstructed by " + obstacle.Name + ".");
            Quaternion yaw = Quaternion.Euler(0, slot.Yaw, 0);
            foreach (float side in new[] { -1f, 1f })
                foreach (float longitudinal in new[] { -2.7f, 0f, 2.7f })
                {
                    var point = slot.Position + yaw * new Vector3(side * slot.Size.x / 2, 0, longitudinal);
                    Require(floor.HasSurface(point, TownColor.White, .17f), slot.Id + ": actual bay edge markings are missing.");
                }
        }
        foreach (var approach in zone.Approaches)
        {
            var saved = savedApproaches.Find(approach.Id);
            Require(saved != null, approach.Id + ": saved approach marker is missing.");
            RequireMarker(saved.Find("From"), approach.From, 0, Vector3.one, approach.Id + "/From");
            RequireMarker(saved.Find("To"), approach.To, 0, Vector3.one, approach.Id + "/To");
            Require(approach.Width >= 3.8f && Inside(zone.Bounds, approach.To)
                && layout.roads.Any(road => Vector3.Distance(road.Position(layout.roadTileSize), approach.From) < .01f),
                approach.Id + ": driveway does not physically connect the existing street to the parking apron.");
            CheckSavedPassage(approach, obstacles, 2.3f);
        }
    }

    /// <summary>Checks live physics for added access and native-car reach; the separate residential Check drives protected streets.</summary>
    public static IEnumerator Check(CityWorld city, TownCourtyardPlan plan)
    {
        TownWorldSafety.RequireIsolatedEditor();
        Require(Application.isPlaying && city != null && city.town != null && city.pickup != null,
            "Courtyard physics acceptance requires the live City scene.");
        Directory.CreateDirectory(ReportFolder);
        var log = new StringBuilder();
        bool passed = false;
        File.WriteAllText(ReportFolder + "/runtime.txt", "RUNNING\n");
        try
        {
            Physics.SyncTransforms();
            int paths = 0, gates = 0, bays = 0;
            foreach (var block in plan.Blocks)
            {
                foreach (var path in block.Paths)
                { CheckLiveRoute(city, path, new Vector3(path.Width / 2, .7f, .28f), 1); paths++; }
                foreach (var zone in block.Zones)
                {
                    if (zone.Kind != TownCourtyardKind.Parking)
                    {
                        var gate = new TownResidentialWalkway(zone.Id + ".Gateway", zone.Entrance - zone.Outward * 1.2f,
                            zone.Entrance + zone.Outward * 1.2f);
                        CheckLiveRoute(city, gate, new Vector3(1.1f, .7f, .28f), 1); gates++;
                    }
                    foreach (var approach in zone.Approaches)
                        CheckLiveRoute(city, approach, new Vector3(1.25f, .65f, 2.3f), 1.1f);
                    foreach (var slot in zone.Slots)
                    {
                        RequireLiveClear(city, slot.Position + Vector3.up * 1.1f, new Vector3(1.25f, .65f, 2.3f),
                            Quaternion.Euler(0, slot.Yaw, 0), slot.Id);
                        bays++;
                    }
                }
                yield return null;
            }
            log.AppendLine($"PASS {paths} added full-width live paths / {gates} real 2.2m gateway traversals / {bays} native-car bay envelopes.");
            log.AppendLine("PASS parking approaches connect public roads and are tested with the complete 2.5x4.6m car envelope every 0.5m.");
            log.AppendLine("Actual steering/parking manoeuvres in each new bay and frame time are not claimed by this collision-envelope audit.");
            passed = true;
        }
        finally { File.WriteAllText(ReportFolder + "/runtime.txt", (passed ? "PASS\n" : "FAILED\n") + log); }
    }

    private static void CheckLiveRoute(CityWorld city, TownResidentialWalkway route, Vector3 extents, float height)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route.From, route.To) / .5f));
        Quaternion rotation = Quaternion.LookRotation(route.To - route.From);
        for (int sample = 0; sample <= count; sample++)
            RequireLiveClear(city, Vector3.Lerp(route.From, route.To, (float)sample / count) + Vector3.up * height,
                extents, rotation, route.Id);
    }

    private static void RequireLiveClear(CityWorld city, Vector3 centre, Vector3 extents, Quaternion rotation, string name)
    {
        var obstacles = Physics.OverlapBox(centre, extents, rotation, ~0, QueryTriggerInteraction.Ignore)
            .Where(collider => collider.gameObject.scene == city.gameObject.scene && !collider.transform.IsChildOf(city.pickup.transform)).ToArray();
        Require(obstacles.Length == 0, name + ": full live passage is blocked at " + centre + " by "
            + string.Join(", ", obstacles.Take(6).Select(collider => collider.name)) + ".");
    }

    private sealed class FloorIndex
    {
        private const float CellSize = 2;
        private readonly Dictionary<long, List<FloorTriangle>> buckets = new Dictionary<long, List<FloorTriangle>>();

        public FloorIndex(IEnumerable<FloorTriangle> triangles)
        {
            foreach (var triangle in triangles)
                for (int z = Mathf.FloorToInt(triangle.Bounds.yMin / CellSize); z <= Mathf.FloorToInt(triangle.Bounds.yMax / CellSize); z++)
                    for (int x = Mathf.FloorToInt(triangle.Bounds.xMin / CellSize); x <= Mathf.FloorToInt(triangle.Bounds.xMax / CellSize); x++)
                    {
                        long key = Key(x, z);
                        if (!buckets.TryGetValue(key, out var bucket)) buckets.Add(key, bucket = new List<FloorTriangle>());
                        bucket.Add(triangle);
                    }
        }

        public bool HasSurface(Vector3 point, TownColor color, float height)
            => At(point).Any(triangle => triangle.Covers(point, TownPalette.UV(color), height));

        public bool IsPaved(Vector3 point)
        {
            var covering = At(point).Where(triangle => triangle.CoversXZ(point)).ToArray();
            if (covering.Length == 0) return false;
            float top = covering.Max(triangle => triangle.First.y);
            // Paving hidden under the accepted grass or a new coloured play surface is not visible blank hardscape.
            return covering.Any(triangle => triangle.First.y >= top - .001f && PavedUv(triangle.Uv));
        }

        public bool IsVisibleLegacyPaving(Vector3 point)
        {
            var covering = At(point).Where(triangle => triangle.CoversXZ(point)).ToArray();
            if (covering.Length == 0) return false;
            float top = covering.Max(triangle => triangle.First.y);
            return covering.Any(triangle => triangle.IsLegacy && triangle.First.y > .0701f
                && triangle.First.y >= top - .001f && PavedUv(triangle.Uv));
        }

        private static bool PavedUv(Vector2 uv)
            => new[] { TownPalette.UV(TownColor.Pavement), TownPalette.UV(TownColor.Asphalt), TownPalette.UV(TownColor.Stone) }
                .Any(paved => (uv - paved).sqrMagnitude < .000001f);

        private IEnumerable<FloorTriangle> At(Vector3 point)
        {
            long key = Key(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.z / CellSize));
            return buckets.TryGetValue(key, out var bucket) ? bucket : Enumerable.Empty<FloorTriangle>();
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }

    /// <summary>
    /// A half-metre measurement of uninterrupted unassigned hardscape. Grass and enclosed building bodies
    /// are not empty paving. Assignment is accepted only near the real features of a separately validated zone.
    /// </summary>
    private static float LargestBlankPavedSquare(Rect parcel, FloorIndex floor, Func<Vector3, bool> meaningful,
        IReadOnlyList<Bounds> buildings)
    {
        const float sample = .5f;
        int width = Mathf.FloorToInt(parcel.width / sample), height = Mathf.FloorToInt(parcel.height / sample);
        var previous = new int[width];
        int largest = 0;
        for (int z = 0; z < height; z++)
        {
            var current = new int[width];
            for (int x = 0; x < width; x++)
            {
                var point = new Vector3(parcel.xMin + (x + .5f) * sample, .16f, parcel.yMin + (z + .5f) * sample);
                bool enclosed = buildings.Any(body => point.x >= body.min.x && point.x <= body.max.x
                    && point.z >= body.min.z && point.z <= body.max.z);
                if (enclosed || meaningful(point) || !floor.IsPaved(point)) continue;
                current[x] = 1 + Mathf.Min(previous[x], x > 0 ? current[x - 1] : 0, x > 0 ? previous[x - 1] : 0);
                largest = Mathf.Max(largest, current[x]);
            }
            previous = current;
        }
        return largest * sample;
    }

    private sealed class RenderedGeometry
    {
        private const float PositionEpsilon = .0001f;
        private const float UvEpsilon = .000001f;
        private const float CellSize = .001f;
        private readonly Dictionary<string, List<RenderedTriangle>> exact = new Dictionary<string, List<RenderedTriangle>>(StringComparer.Ordinal);
        private readonly Dictionary<Vector3Int, List<RenderedTriangle>> buckets = new Dictionary<Vector3Int, List<RenderedTriangle>>();
        private readonly List<RenderedTriangle> triangles = new List<RenderedTriangle>();
        public int FallbackCount { get; private set; }
        public float MaximumDelta { get; private set; }

        public void Add(Vector3 a, Vector2 aUv, Vector3 b, Vector2 bUv, Vector3 c, Vector2 cUv)
        {
            var triangle = new RenderedTriangle(new[] { a, b, c }, new[] { aUv, bUv, cUv });
            triangles.Add(triangle);
            string key = TriangleKey(a, aUv, b, bUv, c, cUv);
            if (!exact.TryGetValue(key, out var sameKey)) exact.Add(key, sameKey = new List<RenderedTriangle>());
            sameKey.Add(triangle);
            var cell = Cell((a + b + c) / 3);
            if (!buckets.TryGetValue(cell, out var bucket)) buckets.Add(cell, bucket = new List<RenderedTriangle>());
            bucket.Add(triangle);
        }

        public bool Consume(Vector3[] positions, Vector2[] uv)
        {
            string key = TriangleKey(positions[0], uv[0], positions[1], uv[1], positions[2], uv[2]);
            if (exact.TryGetValue(key, out var sameKey))
                foreach (var candidate in sameKey)
                    if (TryConsume(candidate, positions, uv)) return true;
            // Native CombineMeshes and managed MultiplyPoint3x4 may differ by one float ULP. Rounding is only
            // an index, never acceptance: neighbouring buckets still require all three real XYZ/UV matches.
            Vector3Int centre = Cell((positions[0] + positions[1] + positions[2]) / 3);
            for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        if (!buckets.TryGetValue(centre + new Vector3Int(x, y, z), out var candidates)) continue;
                        foreach (var candidate in candidates)
                            if (TryConsume(candidate, positions, uv)) { FallbackCount++; return true; }
                    }
            return false;
        }

        private bool TryConsume(RenderedTriangle triangle, Vector3[] positions, Vector2[] uv)
        {
            if (triangle.Consumed) return false;
            foreach (int first in new[] { 0, 1, 2 })
                foreach (int second in new[] { 0, 1, 2 })
                {
                    if (first == second) continue;
                    var order = new[] { first, second, 3 - first - second };
                    float maximum = 0;
                    bool matched = true;
                    for (int vertex = 0; vertex < 3; vertex++)
                    {
                        float squared = (positions[vertex] - triangle.Positions[order[vertex]]).sqrMagnitude;
                        if (squared > PositionEpsilon * PositionEpsilon
                            || (uv[vertex] - triangle.Uv[order[vertex]]).sqrMagnitude > UvEpsilon * UvEpsilon)
                        { matched = false; break; }
                        maximum = Mathf.Max(maximum, Mathf.Sqrt(squared));
                    }
                    if (!matched) continue;
                    triangle.Consumed = true;
                    MaximumDelta = Mathf.Max(MaximumDelta, maximum);
                    return true;
                }
            return false;
        }

        public string Diagnose(string name, int index, Vector3[] positions, Vector2[] uv, Matrix4x4 pose)
        {
            var log = new StringBuilder(name + ": declared native model triangle " + index + " is absent from the real saved courtyard geometry.\n");
            log.AppendLine("Expected pose X/Z rows: " + string.Join(", ", new[] { pose.m00,pose.m02,pose.m03,pose.m20,pose.m22,pose.m23 }
                .Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
            for (int vertex = 0; vertex < 3; vertex++)
            {
                log.AppendLine("Expected vertex " + vertex + ": " + Format(positions[vertex], uv[vertex]));
                var nearby = triangles.SelectMany(triangle => Enumerable.Range(0, 3)
                    .Select(item => new { Position = triangle.Positions[item], Uv = triangle.Uv[item], triangle.Consumed }))
                    .OrderBy(item => (item.Position - positions[vertex]).sqrMagnitude).Take(10);
                foreach (var actual in nearby)
                    log.AppendLine("  Actual " + Format(actual.Position, actual.Uv) + "; delta "
                        + Vector3.Distance(actual.Position, positions[vertex]).ToString("R", CultureInfo.InvariantCulture)
                        + " m; consumed " + actual.Consumed);
            }
            return log.ToString();
        }

        private static Vector3Int Cell(Vector3 point) => new Vector3Int(Mathf.FloorToInt(point.x / CellSize),
            Mathf.FloorToInt(point.y / CellSize), Mathf.FloorToInt(point.z / CellSize));
        private static string Format(Vector3 position, Vector2 uv)
            => "XYZ(" + string.Join(",", new[] { position.x,position.y,position.z }.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))
                + ") UV(" + uv.x.ToString("R", CultureInfo.InvariantCulture) + "," + uv.y.ToString("R", CultureInfo.InvariantCulture) + ")";
    }

    private sealed class RenderedTriangle
    {
        public Vector3[] Positions { get; }
        public Vector2[] Uv { get; }
        public bool Consumed { get; set; }
        public RenderedTriangle(Vector3[] positions, Vector2[] uv) { Positions = positions; Uv = uv; }
    }

    private static RenderedGeometry ReadRenderedTriangles(Transform geometry)
    {
        var result = new RenderedGeometry();
        foreach (var filter in geometry.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<Renderer>();
            Require(filter.sharedMesh != null && renderer != null && renderer.enabled && filter.gameObject.activeInHierarchy,
                filter.name + ": courtyard geometry must be actually rendered, not a hidden marker mesh.");
            var mesh = filter.sharedMesh;
            var positions = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            var uv = mesh.uv;
            Require(uv.Length == positions.Length, filter.name + ": rendered model has incomplete UV coordinates.");
            var triangles = mesh.triangles;
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                int a = triangles[triangle], b = triangles[triangle + 1], c = triangles[triangle + 2];
                result.Add(positions[a], uv[a], positions[b], uv[b], positions[c], uv[c]);
            }
        }
        return result;
    }

    private static void RequireRenderedModel(RenderedGeometry rendered, Mesh mesh, Matrix4x4 pose, string name)
    {
        var positions = mesh.vertices.Select(pose.MultiplyPoint3x4).ToArray();
        var uv = mesh.uv;
        var triangles = mesh.triangles;
        Require(uv.Length == positions.Length, name + ": native model has incomplete UV coordinates.");
        for (int triangle = 0; triangle < triangles.Length; triangle += 3)
        {
            int a = triangles[triangle], b = triangles[triangle + 1], c = triangles[triangle + 2];
            var points = new[] { positions[a], positions[b], positions[c] };
            var atlas = new[] { uv[a], uv[b], uv[c] };
            if (!rendered.Consume(points, atlas))
                throw new InvalidOperationException(rendered.Diagnose(name, triangle / 3, points, atlas, pose));
        }
    }

    private static string TriangleKey(Vector3 a, Vector2 aUv, Vector3 b, Vector2 bUv, Vector3 c, Vector2 cUv)
    {
        // Quantisation tolerates float round trips through CombineMeshes, but not meaningful pose/shape edits.
        string Vertex(Vector3 point, Vector2 uv) => Mathf.RoundToInt(point.x * 10000) + ","
            + Mathf.RoundToInt(point.y * 10000) + "," + Mathf.RoundToInt(point.z * 10000) + ","
            + Mathf.RoundToInt(uv.x * 1000000) + "," + Mathf.RoundToInt(uv.y * 1000000);
        return string.Join(";", new[] { Vertex(a, aUv), Vertex(b, bUv), Vertex(c, cUv) }.OrderBy(item => item, StringComparer.Ordinal));
    }

    private static void ValidateGeometryContracts()
    {
        ValidateTriangleMatchingContracts();
        var diagonal = new OrientedSolid("rotated thin beam", new Vector3(0, 1, 0),
            new Vector3(.1f, 1, 4), Quaternion.Euler(0, 45, 0));
        var outside = new OrientedSolid("empty AABB corner", new Vector3(2.7f, 1, -2.7f),
            new Vector3(.1f, .1f, .1f), Quaternion.identity);
        Require(!Intersects(diagonal, outside), "OBB contract failed: an empty envelope corner became a real rotated solid.");
        var inside = new OrientedSolid("real beam overlap", new Vector3(1, 1, 1),
            new Vector3(.2f, .2f, .2f), Quaternion.identity);
        Require(Intersects(diagonal, inside), "OBB contract failed: a real physical crossing was accepted.");
        var supported = new OrientedSolid("supported prop", new Vector3(0, .66f, 0),
            Vector3.one * .5f, Quaternion.identity);
        Require(!TriangleIntersects(new Vector3(-1, .16f, -1), new Vector3(1, .16f, -1), new Vector3(0, .16f, 1), supported),
            "Mesh contract failed: intended floor support became a penetration.");
        Require(TriangleIntersects(new Vector3(-1, .6f, -1), new Vector3(1, .6f, -1), new Vector3(0, .6f, 1), supported),
            "Mesh contract failed: triangle crossing a foreign solid was accepted.");
        var pavingUv = TownPalette.UV(TownColor.Pavement);
        var grassUv = TownPalette.UV(TownColor.Grass);
        var paved = new FloorIndex(new[] {
            new FloorTriangle(new Vector3(0,.16f,0),new Vector3(12,.16f,0),new Vector3(12,.16f,12),pavingUv),
            new FloorTriangle(new Vector3(0,.16f,0),new Vector3(12,.16f,12),new Vector3(0,.16f,12),pavingUv)
        });
        var grass = new FloorIndex(new[] {
            new FloorTriangle(new Vector3(0,.16f,0),new Vector3(12,.16f,0),new Vector3(12,.16f,12),grassUv),
            new FloorTriangle(new Vector3(0,.16f,0),new Vector3(12,.16f,12),new Vector3(0,.16f,12),grassUv)
        });
        Require(LargestBlankPavedSquare(new Rect(0, 0, 12, 12), paved, point => false, Array.Empty<Bounds>()) == 12,
            "Blank paving contract failed: a real empty 12x12 paved square was not measured.");
        Require(LargestBlankPavedSquare(new Rect(0, 0, 12, 12), grass, point => false, Array.Empty<Bounds>()) == 0,
            "Blank paving contract failed: an intentional green court was mistaken for blank hardscape.");
        Require(LargestBlankPavedSquare(new Rect(0, 0, 12, 12), paved, point => true, Array.Empty<Bounds>()) == 0,
            "Blank paving contract failed: a separately validated meaningful zone was mistaken for unused paving.");
        var coveredPaving = new FloorIndex(new[] {
            new FloorTriangle(new Vector3(0,.07f,0),new Vector3(12,.07f,0),new Vector3(12,.07f,12),pavingUv,true),
            new FloorTriangle(new Vector3(0,.07f,0),new Vector3(12,.07f,12),new Vector3(0,.07f,12),pavingUv,true),
            new FloorTriangle(new Vector3(0,.16f,0),new Vector3(12,.16f,0),new Vector3(12,.16f,12),grassUv),
            new FloorTriangle(new Vector3(0,.16f,0),new Vector3(12,.16f,12),new Vector3(0,.16f,12),grassUv)
        });
        Require(LargestBlankPavedSquare(new Rect(0, 0, 12, 12), coveredPaving, point => false, Array.Empty<Bounds>()) == 0,
            "Visible-floor contract failed: paving hidden under real grass became an empty hardscape square.");
    }

    private static void ValidateTriangleMatchingContracts()
    {
        var source = new[] { new Vector3(1.000051f,0,0),new Vector3(1.000051f,0,1),new Vector3(2.000051f,0,0) };
        var actual = new[] { new Vector3(1.000049f,0,0),new Vector3(1.000049f,0,1),new Vector3(2.000049f,0,0) };
        var uv = Enumerable.Repeat(TownPalette.UV(TownColor.Frame), 3).ToArray();
        RenderedGeometry Geometry(Vector3[] points, Vector2[] atlas)
        {
            var geometry = new RenderedGeometry();
            geometry.Add(points[0],atlas[0],points[1],atlas[1],points[2],atlas[2]);
            return geometry;
        }
        var boundary = Geometry(actual, uv);
        Require(boundary.Consume(source, uv) && boundary.FallbackCount == 1 && boundary.MaximumDelta < .0001f,
            "Triangle contract failed: a float rounding-bin edge rejected genuinely equal native geometry.");
        Require(!boundary.Consume(source, uv), "Triangle contract failed: one real triangle satisfied two declared model instances.");
        Require(!Geometry(actual, uv).Consume(source.Select(point => point + Vector3.right * .001f).ToArray(), uv),
            "Triangle contract failed: a real one-millimetre shape/pose change was accepted as rounding.");
        var alteredUv = (Vector2[])uv.Clone(); alteredUv[0] += Vector2.right * .0001f;
        Require(!Geometry(actual, uv).Consume(source, alteredUv), "Triangle contract failed: altered native atlas coordinates were accepted.");
        Require(!new RenderedGeometry().Consume(source, uv), "Triangle contract failed: a missing actual triangle was accepted.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
