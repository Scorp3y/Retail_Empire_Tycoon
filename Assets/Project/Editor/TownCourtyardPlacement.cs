using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Writes only new courtyard assets and adds native geometry to the accepted town prefab.</summary>
internal sealed class TownCourtyardPlacement
{
    private readonly TownCourtyardPlan plan;
    private readonly GameObject town;
    private readonly Material material;
    private readonly Dictionary<string, Mesh> templates = new Dictionary<string, Mesh>();
    private readonly Mesh treeMesh;

    public TownCourtyardPlacement(TownCourtyardPlan plan, GameObject town)
    {
        this.plan = plan; this.town = town;
        material = AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder + "/TownPalette.mat");
        if (material == null) throw new InvalidOperationException("The shared existing courtyard palette is missing.");
        treeMesh = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.FindAssets("TreeRound t:Prefab",
            new[] { "Assets/Art/TownKit/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath)
            .Single(p => Path.GetFileNameWithoutExtension(p) == "TreeRound")).GetComponent<MeshFilter>().sharedMesh;
    }

    public void Build()
    {
        if (town.transform.Find(TownCourtyardPlan.HierarchyRoot) != null)
            throw new InvalidOperationException("Courtyard generation must start from the immutable stage-three town.");
        Directory.CreateDirectory(TownCourtyardPlan.AssetRoot + "/Meshes");
        Directory.CreateDirectory(TownCourtyardPlan.AssetRoot + "/Models");
        Directory.CreateDirectory(TownCourtyardPlan.AssetRoot + "/Prefabs");
        AssetDatabase.Refresh();
        TownCourtyardModels.ValidateCatalogue(plan.Models);
        foreach (var model in plan.Models)
        {
            Mesh mesh = SaveMesh(model.BuildMesh(), "Models/" + model.Id);
            templates.Add(model.Id, mesh);
            var prototype = new GameObject(model.Id, typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                prototype.GetComponent<MeshFilter>().sharedMesh = mesh;
                prototype.GetComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(prototype, TownCourtyardPlan.AssetRoot + "/Prefabs/" + model.Id + ".prefab");
            }
            finally { Object.DestroyImmediate(prototype); }
        }
        Directory.CreateDirectory("Library/TownCourtyardQA");
        File.WriteAllText("Library/TownCourtyardQA/templates.txt", "PASS: native models generated and validated BEFORE placement; existing 32-colour shared material.\n" +
            string.Join("\n", plan.Models.Select(m => m.Id + ": " + m.VisualBounds + ", " + m.Solids.Count + " physical parts")));
        var hierarchy = new GameObject(TownCourtyardPlan.HierarchyRoot).transform;
        hierarchy.SetParent(town.transform, false);
        foreach (var block in plan.Blocks) BuildBlock(block, hierarchy);
    }

    private void BuildBlock(TownCourtyardBlock block, Transform hierarchy)
    {
        var old = town.transform.Find(TownResidentialPlan.HierarchyRoot + "/" + block.Id);
        if (old == null) throw new InvalidOperationException("Accepted residential block is missing: " + block.Id);
        foreach (var child in old.Cast<Transform>().Where(c => c.name == "Residential green groups" ||
            c.name.StartsWith("Residential tree trunk ", StringComparison.Ordinal)).ToArray()) Object.DestroyImmediate(child.gameObject);
        var parent = new GameObject(block.Id).transform; parent.SetParent(hierarchy, false);
        var surfaces = new TownMeshBuilder();
        var instances = new List<CombineInstance>();
        var zones = new GameObject("Zones").transform; zones.SetParent(parent, false);
        foreach (var zone in block.Zones)
        {
            surfaces.Ground(zone.Bounds.center.x, zone.Bounds.center.y, zone.Bounds.width, zone.Bounds.height, zone.SurfaceY, zone.SurfaceKind);
            DrawCurbs(surfaces, zone);
            var marker = new GameObject(zone.Id).transform; marker.SetParent(zones, false);
            Point(marker, "Entrance", zone.Entrance);
            var floor = new GameObject("Surface").transform; floor.SetParent(marker, false);
            floor.localPosition = new Vector3(zone.Bounds.center.x, .12f, zone.Bounds.center.y);
            floor.gameObject.isStatic = true;
            floor.gameObject.AddComponent<BoxCollider>().size = new Vector3(zone.Bounds.width, .08f, zone.Bounds.height);
            var props = new GameObject("Props").transform; props.SetParent(marker, false);
            foreach (var prop in zone.Props)
            {
                instances.Add(new CombineInstance { mesh = templates[prop.ModelId], transform = prop.Pose });
                var item = new GameObject(prop.Id).transform; item.SetParent(props, false);
                item.localPosition = prop.Position; item.localRotation = Quaternion.Euler(0, prop.Yaw, 0); item.localScale = prop.Scale;
                new GameObject(prop.ModelId).transform.SetParent(item, false);
                for (int i = 0; i < prop.Model.Solids.Count; i++)
                {
                    var solid = prop.Model.Solids[i];
                    var part = new GameObject("Solid" + i).transform; part.SetParent(item, false);
                    part.localPosition = solid.Center; part.localRotation = solid.Rotation;
                    part.gameObject.isStatic = true;
                    part.gameObject.AddComponent<BoxCollider>().size = solid.Size;
                }
            }
            if (zone.Kind == TownCourtyardKind.Sport && zone.Props.Any(p => p.Model.Role == TownCourtyardRole.BasketballHoop)) DrawCourt(surfaces, zone.Bounds);
            var slots = new GameObject("Slots").transform; slots.SetParent(marker, false);
            foreach (var slot in zone.Slots)
            {
                Point(slots, slot.Id, slot.Position).localRotation = Quaternion.Euler(0, slot.Yaw, 0);
                DrawSlot(surfaces, slot);
            }
            MarkPaths(marker, "Approaches", zone.Approaches);
            foreach (var approach in zone.Approaches)
            {
                var area = TownCourtyardGeometry.Path(approach);
                var allowed = TownCourtyardGeometry.Expand(block.Residential.Bounds, 1.8f);
                area = Rect.MinMaxRect(Mathf.Max(area.xMin, allowed.xMin), Mathf.Max(area.yMin, allowed.yMin),
                    Mathf.Min(area.xMax, allowed.xMax), Mathf.Min(area.yMax, allowed.yMax));
                surfaces.Ground(area.center.x, area.center.y, area.width, area.height, .166f, TownColor.Asphalt);
                // Pedestrian priority across the driveway: the old continuous path is not physically obstructed.
                foreach (var path in block.Residential.Walkways.Where(p => TownCourtyardGeometry.Path(p).Overlaps(area)))
                {
                    var cross = TownCourtyardGeometry.Path(path);
                    float minX = Mathf.Max(cross.xMin, area.xMin), maxX = Mathf.Min(cross.xMax, area.xMax);
                    float minZ = Mathf.Max(cross.yMin, area.yMin), maxZ = Mathf.Min(cross.yMax, area.yMax);
                    for (float x = minX + .15f; x < maxX; x += .7f)
                        surfaces.Ground(x, (minZ + maxZ) / 2, .3f, maxZ - minZ, .174f, TownColor.White);
                }
            }
        }
        foreach (var path in block.Paths) DrawPath(surfaces, path);
        MarkPaths(parent, "Walkways", block.Paths);
        var trees = new GameObject("Trees").transform; trees.SetParent(parent, false);
        foreach (var tree in block.Trees)
        {
            instances.Add(new CombineInstance { mesh = treeMesh, transform = tree.Pose });
            var marker = Point(trees, tree.Id, tree.Position); marker.localScale = Vector3.one * tree.Scale;
            marker.localRotation = Quaternion.Euler(0, tree.Yaw, 0); marker.gameObject.isStatic = true;
            var trunk = marker.gameObject.AddComponent<BoxCollider>(); trunk.center = Vector3.up * 1.05f;
            trunk.size = new Vector3(.44f, 2.1f, .44f);
        }
        var surfaceMesh = surfaces.Build(block.Id + " surfaces");
        try
        {
            instances.Add(new CombineInstance { mesh = surfaceMesh, transform = Matrix4x4.identity });
            var combined = new Mesh { name = block.Id + " Courtyard", indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(instances.ToArray(), true, true);
            combined.uv2 = combined.vertices.Select(v => new Vector2(v.x, v.z)).ToArray();
            var geometry = new GameObject("Courtyard geometry", typeof(MeshFilter), typeof(MeshRenderer));
            geometry.transform.SetParent(parent, false); geometry.isStatic = true;
            geometry.GetComponent<MeshFilter>().sharedMesh = SaveMesh(combined, "Meshes/" + block.Id);
            geometry.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        finally { Object.DestroyImmediate(surfaceMesh); }
    }

    private static Transform Point(Transform parent, string name, Vector3 position)
    { var point = new GameObject(name).transform; point.SetParent(parent, false); point.localPosition = position; return point; }
    private static void MarkPaths(Transform parent, string name, IReadOnlyList<TownResidentialWalkway> paths)
    {
        var group = new GameObject(name).transform; group.SetParent(parent, false);
        foreach (var path in paths)
        { var marker = new GameObject(path.Id).transform; marker.SetParent(group, false); Point(marker, "From", path.From); Point(marker, "To", path.To); }
    }
    private static void DrawPath(TownMeshBuilder mesh, TownResidentialWalkway path)
    {
        var rect = TownCourtyardGeometry.Path(path);
        mesh.Ground(rect.center.x, rect.center.y, rect.width, rect.height, .17f, TownColor.Pavement);
    }
    private static void DrawCurbs(TownMeshBuilder mesh, TownCourtyardZone zone)
    {
        var r = zone.Bounds;
        void Side(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a; float length = delta.magnitude;
            bool gate = Mathf.Abs(Vector3.Dot((a + b) / 2 - zone.Entrance, zone.Outward)) < .1f;
            void Run(float from, float to)
            {
                var middle = a + delta.normalized * ((from + to) / 2); middle.y = .145f;
                mesh.Box(middle, new Vector3(Mathf.Abs(delta.x) > .1f ? to - from : .13f, .15f,
                    Mathf.Abs(delta.z) > .1f ? to - from : .13f), TownColor.Stone);
            }
            float opening = zone.Kind == TownCourtyardKind.Parking ? 2.1f : 1.6f;
            if (gate) { Run(0, length / 2 - opening); Run(length / 2 + opening, length); }
            else Run(0, length);
        }
        Side(new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin));
        Side(new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax));
        Side(new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMin, 0, r.yMax));
        Side(new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMax, 0, r.yMax));
    }
    private static void DrawSlot(TownMeshBuilder mesh, TownCourtyardSlot slot)
    {
        var pose = Matrix4x4.TRS(slot.Position + Vector3.up * .18f, Quaternion.Euler(0, slot.Yaw, 0), Vector3.one);
        foreach (float x in new[] { -1.55f, 1.55f })
            mesh.Box(pose.MultiplyPoint3x4(new Vector3(x, 0, 0)), new Vector3(.075f, .012f, 6), TownColor.White, Quaternion.Euler(0, slot.Yaw, 0));
        mesh.Box(pose.MultiplyPoint3x4(new Vector3(0, 0, 3)), new Vector3(3.1f, .012f, .075f), TownColor.White, Quaternion.Euler(0, slot.Yaw, 0));
    }
    private static void DrawCourt(TownMeshBuilder mesh, Rect rect)
    {
        var centre = new Vector3(rect.center.x, .18f, rect.center.y);
        mesh.Box(centre + Vector3.back * (rect.height / 2 - .8f), new Vector3(rect.width - 1.6f, .012f, .07f), TownColor.White);
        foreach (float side in new[] { -1f, 1f })
            mesh.Box(centre + Vector3.right * side * (rect.width / 2 - .8f), new Vector3(.07f, .012f, rect.height - 1.6f), TownColor.White);
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2 / 24, b = (i + 1) * Mathf.PI * 2 / 24;
            var p = centre + new Vector3(Mathf.Cos(a) * 1.8f, 0, Mathf.Sin(a) * 1.8f);
            var q = centre + new Vector3(Mathf.Cos(b) * 1.8f, 0, Mathf.Sin(b) * 1.8f);
            mesh.Box((p + q) / 2, new Vector3(.07f, .012f, Vector3.Distance(p, q)), TownColor.White, Quaternion.LookRotation(q - p));
        }
    }
    private static Mesh SaveMesh(Mesh mesh, string relative)
    {
        string path = TownCourtyardPlan.AssetRoot + "/" + relative + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); return existing;
    }
}
