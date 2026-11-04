using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class AutoWorksAuthoring
{
    internal const string Report = "Docs/WorkshopConcept/Final";
    private static readonly Rect Parcel = new(78, -186, 84, 36);

    [MenuItem("Tools/Retail Empire Tycoon/Auto Works/Rebuild model")]
    public static void RebuildModel()
    {
        var model = AutoWorksModel.Save();
        AssetDatabase.SaveAssets();
        Selection.activeObject = model;
    }

    public static void BuildAndInstall() => Run("build", () =>
    {
        var model = AutoWorksModel.Save();
        ValidatePrefab(model);
        Install(model);
    });

    private static void Install(GameObject model)
    {
        string path = TownWorldAssembler.PrefabPath;
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = PrefabUtility.LoadPrefabContents(path);
        var changedRecords = new HashSet<string>();
        var oldModelPaths = new HashSet<string>();
        var oldFiles = AssetDatabase.GetDependencies(path, true).Where(p => p.StartsWith("Assets/") && p != path && !p.StartsWith(AutoWorksModel.Root))
            .Concat(new[] {CityUpdateSetup.CityPath, CityUpdateSetup.GamePath})
            .Distinct().ToDictionary(p => p, TownForgottenYardBatch.Hash);
        var before = Records(path);
        var notes = new List<string>();
        try
        {
            var site = root.transform.Find(TownSpecialPlan.WorkshopRoot);
            Require(site != null, "Cannot find the existing workshop anchor.");
            var originalSite = original.transform.Find(TownSpecialPlan.WorkshopRoot);
            var route = site.Find("Routes");
            Require(route != null && route.childCount == 7, "Expected seven workshop route markers.");
            var existing = site.Find("AutoWorks");
            if (existing == null)
            {
                string[] legacy = {"Props", "Surfaces", "Site geometry"};
                Require(site.childCount == 4 && legacy.All(name => site.Find(name) != null), "Workshop contains unexpected children.");
                foreach (string name in legacy)
                {
                    var source = originalSite.Find(name);
                    foreach (var item in source.GetComponentsInChildren<Component>(true))
                    {
                        Require(item is Transform || item is MeshFilter || item is MeshRenderer || item is Collider,
                            "An old workshop behavior needs migration: " + item);
                        Mark(item, changedRecords);
                        Mark(item.gameObject, changedRecords);
                        if (item is MeshFilter filter) oldModelPaths.Add(AssetDatabase.GetAssetPath(filter.sharedMesh));
                    }
                    Object.DestroyImmediate(site.Find(name).gameObject);
                }
                var shell = (GameObject)PrefabUtility.InstantiatePrefab(model, site);
                shell.name = "AutoWorks";
                shell.transform.SetPositionAndRotation(AutoWorksModel.TownPosition, AutoWorksModel.TownRotation);
                Require(shell.transform.localScale == Vector3.one, "Workshop scale was changed by its parent.");
                UpdateRoutes(route, originalSite.Find("Routes"), changedRecords);
                Mark(originalSite, changedRecords);
                ClipSharedSurfaces(root.transform, original.transform, changedRecords, notes);
                Require(root.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh == null ||
                    !oldModelPaths.Contains(AssetDatabase.GetAssetPath(f.sharedMesh))), "The town still references the old STO model mesh.");
                Require(PrefabUtility.SaveAsPrefabAsset(root, path) != null, "Town prefab could not be saved.");
                AssetDatabase.SaveAssets();

                var after = Records(path);
                foreach (var pair in before)
                    if (!changedRecords.Contains(pair.Key))
                        Require(after.TryGetValue(pair.Key, out string record) && record == pair.Value,
                            "Unrelated town record changed: " + pair.Key);
                var removed = before.Keys.Where(key => !after.ContainsKey(key)).ToHashSet();
                var dangling = Regex.Matches(string.Join("\n", after.Values), @"\{fileID: (-?\d+)\}")
                    .Cast<Match>().Select(m => m.Groups[1].Value).Where(removed.Contains).Distinct().ToArray();
                Require(dangling.Length == 0, "Town contains dangling references to replaced objects: " + string.Join(",", dangling));
                notes.Add($"Unrelated serialized town records identical: {before.Count - changedRecords.Count}. Old model records removed: {removed.Count}.");
                foreach (var pair in oldFiles)
                    Require(TownForgottenYardBatch.Hash(pair.Key) == pair.Value, "An original shared asset changed: " + pair.Key);
                notes.Add($"Original scene files and other existing dependencies SHA-256 identical: {oldFiles.Count}.");
                notes.Add("Retained the Workshop anchor and all seven route marker objects with their file IDs. No old behaviors, checkpoints or NavMesh links were attached.");
            }
            else
            {
                Require(existing.position == AutoWorksModel.TownPosition, "Installed workshop moved since approval.");
                notes.Add("Town already has the new prefab. The standalone model has been regenerated in place.");
            }
            notes.Add("Lowered driveway and public pedestrian crossing retained.");
            File.WriteAllLines(Report + "/Installation.txt", notes);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ClipSharedSurfaces(Transform town, Transform original, HashSet<string> changed, List<string> notes)
    {
        // Explicitly exclude public junction equipment, bus platforms and the retained lowered driveway.
        foreach (var (layerName, maxY) in new[] {("Render chunks — 48 metre cells", 8f),
                     ("Seam repairs stage 6", .26f), ("Site cleanup and access revision", .26f)})
        {
            var layer = town.Find(layerName);
            var source = original.Find(layerName);
            Require(layer != null && source != null, "Shared geometry layer missing: " + layerName);
            for (int i = 0; i < layer.childCount; i++)
            {
                var filter = layer.GetChild(i).GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                var b = renderer.bounds;
                if (b.min.x >= Parcel.xMax || b.max.x <= Parcel.xMin || b.min.z >= Parcel.yMax || b.max.z <= Parcel.yMin) continue;
                var geometry = new TownCityCorrectionsGeometry(new[] {filter});
                int count = geometry.ClipGround(Parcel, maxY);
                if (count == 0) continue;
                Mark(source.GetChild(i).GetComponent<MeshFilter>(), changed);
                string id = "WorkshopCut_" + layer.GetSiblingIndex() + "_" + i;
                geometry.Views[0].Save(id, AutoWorksModel.Root + "/TownCuts");
                notes.Add($"{filter.name}: clipped {count} source triangles against the 84 × 36 m parcel, retaining exterior fragments.");
            }
        }
    }

    private static void UpdateRoutes(Transform routes, Transform original, HashSet<string> changed)
    {
        var positions = new (string Id, Vector2 From, Vector2 To)[]
        {
            ("VehicleEntry", new(144,-192), new(144,-173)),
            ("WorkingLane", new(144,-173), new(98,-173)),
            ("PedEntry", new(113,-149.5f), new(113,-153.1f)),
            ("HallApproach", new(121,-166.2f), new(134,-170)),
            ("HallDoor", new(134,-170), new(134,-165)),
            ("OfficeDoor", new(113,-151.8f), new(115,-153.96f)),
            ("StopLink", new(90,-149.5f), new(113,-149.5f))
        };
        foreach (var (id, from, to) in positions)
        {
            var marker = routes.Find("Workshop." + id);
            var old = original.Find("Workshop." + id);
            foreach (var (end, point) in new[] {("From", from), ("To", to)})
            {
                var node = marker.Find(end);
                var p = node.position;
                var target = new Vector3(point.x, p.y, point.y);
                if (p == target) continue;
                Mark(old.Find(end), changed);
                node.position = target;
            }
        }
    }

    private static void ValidatePrefab(GameObject model)
    {
        Require(model.transform.localScale == Vector3.one && model.transform.rotation == Quaternion.identity &&
            model.transform.position == Vector3.zero, "The standalone model pivot or axes are incorrect.");
        var doors = model.GetComponentsInChildren<GarageDoor>();
        Require(doors.Length == 3 && doors.All(door => !door.gameObject.isStatic), "Three non-static door assemblies required.");
        Require(model.GetComponentsInChildren<MeshCollider>().Length == 0, "The standalone workshop must use simple colliders.");
        var all = model.GetComponentsInChildren<MeshFilter>();
        var greenery = model.transform.Find("Greenery");
        int green = all.Where(f => f.transform.IsChildOf(greenery)).Sum(f => TriangleCount(f.sharedMesh));
        int total = all.Sum(f => TriangleCount(f.sharedMesh));
        int materials = model.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct().Count();
        Require(total - green <= 30000 && green <= 5000 && materials <= 15, $"Art budget exceeded: {total-green}/{green} triangles, {materials} materials.");
        for (int i = 0; i <= 128; i++)
        {
            float travel = GarageDoor.OpenTravel * i / 128f;
            for (int j = 0; j < GarageDoor.SectionCount; j++)
            {
                var a = GarageDoor.Track(j * GarageDoor.SectionHeight + travel);
                var b = GarageDoor.Track((j + 1) * GarageDoor.SectionHeight + travel);
                float furthestForward = Mathf.Max(a.z, b.z) - 1.56f + .10f;
                Require(furthestForward < -1.32f && Mathf.Max(a.y, b.y) + .10f < 5.34f, "Panel envelope crosses the facade or lower ceiling equipment.");
                if (i == 128) Require(Mathf.Min(a.y, b.y) - .10f > GarageDoor.ClearHeight, "An open panel reduces the approved clearance.");
            }
        }
        File.WriteAllText(Report + "/Model.txt", $"{total-green} building, interior and lot triangles. {green} vegetation triangles. {materials} materials including editable text font.\n" +
            "Three moving doors, five sections each. Panel clearance checked at 129 positions through the full travel.\n");
    }

    private static int TriangleCount(Mesh mesh)
    {
        int count = 0;
        for (int sub = 0; sub < mesh.subMeshCount; sub++) count += (int)mesh.GetIndexCount(sub) / 3;
        return count;
    }

    private static Dictionary<string, string> Records(string path) =>
        Regex.Matches(File.ReadAllText(path), @"(?ms)^--- !u!\d+ &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)")
            .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Value.Replace("\r\n", "\n"));

    private static void Mark(Object obj, HashSet<string> records)
    {
        Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string _, out long id), "No persistent ID for " + obj);
        records.Add(id.ToString(CultureInfo.InvariantCulture));
    }

    public static void Capture() => Run("capture", () =>
    {
        var scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town = Object.FindObjectOfType<TownWorldPlaces>();
        var model = town.transform.Find(TownSpecialPlan.WorkshopRoot + "/AutoWorks");
        Require(model != null, "City is missing the installed Auto Works prefab.");
        var ramp = town.transform.Find(TownCityCorrectionsPlacement.SiteName + "/Workshop lowered driveway");
        var road = ramp.GetComponent<MeshRenderer>().sharedMaterial;
        Require(model.Find("Yard/Asphalt").GetComponent<MeshRenderer>().sharedMaterial == road,
            "Workshop paving differs from the retained city ramp material.");
        var cameraLights = model.GetComponent<AutoWorksLighting>();
        cameraLights.SetLights(true);
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.71f, .73f, .76f);
        if (RenderSettings.sun != null)
        {
            RenderSettings.sun.transform.rotation = Quaternion.Euler(43, 30, 0);
            RenderSettings.sun.color = new Color(1, .89f, .75f);
        }
        void Shot(string name, Vector3 p, Vector3 target, float ortho = 0, int width = 1600, int height = 1000) => TownFloristBatch.Render(scene, Report + "/" + name + ".png",
            model.TransformPoint(p), model.TransformPoint(target), ortho, width, height);
        Shot("01-Overview", new Vector3(-23, 60, 77), new Vector3(0, 1, 0), 32);
        Shot("02-Facade", new Vector3(1, 13, 21), new Vector3(-1, 2.9f, -2), 12.5f, 1800, 520);
        Shot("03-Doors-Closed", new Vector3(-30, 10.4f, 21), new Vector3(-24, 2.6f, -1), 9.6f, 1600, 730);
        SetPreviewDoors(model, true);
        Shot("04-Doors-Open", new Vector3(-30, 10.4f, 21), new Vector3(-24, 2.6f, -1), 9.6f, 1600, 730);
        Shot("05-Interior", new Vector3(-23.8f, 2.38f, -.15f), new Vector3(-26.3f, 2.6f, -13));
        Shot("07-Tools", new Vector3(-15, 2.64f, -3.2f), new Vector3(-7.35f, 1.6f, -8.5f));
        Shot("09-Workshop-Details", new Vector3(-16.8f, 2.8f, -8.6f), new Vector3(-23.8f, 1.75f, -14.5f));
        Shot("06-Canopy", new Vector3(27, 2.7f, 7.3f), new Vector3(31.3f, 1.6f, -9));
        // The editor's one-shot camera can reuse the wrong material constants after loading the font atlas.
        bool batching = GraphicsSettings.useScriptableRenderPipelineBatching;
        try
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = false;
            Shot("08-Reception", new Vector3(7.1f, 3.95f, 5.35f), new Vector3(5.3f, 2.0f, -3.75f));
        }
        finally { GraphicsSettings.useScriptableRenderPipelineBatching = batching; }
    });

    private static void SetPreviewDoors(Transform model, bool open)
    {
        foreach (var door in model.GetComponentsInChildren<GarageDoor>())
            for (int i = 0; i < GarageDoor.SectionCount; i++)
                GarageDoor.SetSectionPose(door.transform.Find("Section_" + (i + 1)), i, open ? GarageDoor.OpenTravel : 0);
    }

    internal static void Require(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException(reason);
    }

    private static void Run(string stage, Action task)
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Report);
        try
        {
            File.WriteAllText(Report + "/" + stage + ".txt", "RUNNING\n");
            task();
            File.WriteAllText(Report + "/" + stage + ".txt", "PASS\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Report + "/" + stage + ".txt", "FAILED\n" + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}


