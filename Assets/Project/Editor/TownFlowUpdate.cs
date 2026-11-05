using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Converts the starter street furniture into construction items and removes the retired workshop stop.</summary>
public static class TownFlowUpdate
{
    private const string Art = "Assets/Art/FrontageDecor";
    private const string Items = "Assets/Resources/FrontageDecor";
    private const string Report = "Docs/TownFlowUpdate";
    private const string Blueprint = "Assets/Prefabs/StarterStore/StarterStore.asset";

    public static void Install()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "/Geometry.txt", "");
        try
        {
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory(Items);
            AssetDatabase.Refresh();
            var town = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var places = town.GetComponent<TownWorldPlaces>();
                var rail = town.GetComponentInChildren<TownRailTraffic>();
                rail.serviceIntervalSeconds = TownRailTraffic.DefaultServiceInterval;
                if (town.transform.Find("Bus stops/05_BusStop") != null) RetireWorkshopStop(town);
                if (!HasDecorItems()) ConvertFrontage(town, places.layout);
                if (PrefabUtility.SaveAsPrefabAsset(town, TownWorldAssembler.PrefabPath) == null)
                    throw new InvalidOperationException("Could not save the updated town.");
            }
            finally { PrefabUtility.UnloadPrefabContents(town); }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report + "/Install.txt", "PASS\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Report + "/Install.txt", "FAIL\n" + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    public static void Review()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Report);
        try
        {
            var scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var places = Object.FindObjectOfType<TownWorldPlaces>();
            var rail = places.GetComponentInChildren<TownRailTraffic>();
            float platform = rail.layout.stationDistance;
            rail.Advance((platform - rail.TravelDistance) / rail.metresPerSecond - .2f);
            var before = rail.cars[0].localPosition;
            rail.Advance(.4f);
            if (rail.TravelDistance <= platform || Vector3.Distance(before, rail.cars[0].localPosition) < 1)
                throw new InvalidOperationException("The train paused at the station.");
            var head = rail.cars[0].position;
            TownFloristBatch.Render(scene, Report + "/TrainAtStation.png", head + new Vector3(23, 12, -25), head + new Vector3(-14, 1, 0));
            rail.Advance((float)(359 - rail.ServiceClock));
            if (rail.IsInService) throw new InvalidOperationException("Train still visible before its next slot.");
            rail.Advance(1.1f);
            if (!rail.IsInService || Math.Abs(rail.NextDeparture - 720) > .01)
                throw new InvalidOperationException("Train did not depart at six minutes.");
            rail.SetCrossingHold(true);
            float held = rail.TravelDistance;
            rail.Advance(1);
            if (rail.TravelDistance != held) throw new InvalidOperationException("Railway safety hold was lost.");
            rail.SetCrossingHold(false);
            rail.Advance((float)(720.1 - rail.ServiceClock));
            if (!rail.IsInService || Math.Abs(rail.NextDeparture - 1080) > .01)
                throw new InvalidOperationException("Second departure missed its slot.");

            if (places.transform.Find("Bus stops/05_BusStop") != null) throw new InvalidOperationException("Retired bus stop still installed.");
            var fence = places.transform.Find(TownSpecialPlan.WorkshopRoot + "/AutoWorks/Fence/West rear");
            var barrier = fence.GetComponentInChildren<BoxCollider>();
            if (barrier.bounds.min.x > 79.15f || barrier.bounds.max.x < 111.9f)
                throw new InvalidOperationException("The rear fence does not cover the former bus stop.");
            TownFloristBatch.Render(scene, Report + "/WorkshopFence.png", new Vector3(67, 14, -135), new Vector3(102, 1.2f, -157));
            File.WriteAllText(Report + "/RailAndFence.txt", "PASS: continuous station passage, 360 s and 720 s departures, crossing hold, removed stop and extended physical fence.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Report + "/RailAndFence.txt", "FAIL\n" + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static bool HasDecorItems() => new SerializedObject(AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(Blueprint))
        .FindProperty("frontageDecorations").arraySize > 0;

    private static void ConvertFrontage(GameObject town, TownWorldLayout layout)
    {
        var origin = layout.cityShopOrigin;
        var rectangle = new Rect(origin.x, origin.z - 5, 30, 6);
        var surfaces = ReadGeometry(town, rectangle, filter => filter.name.StartsWith("TownChunk_", StringComparison.Ordinal));
        var material = AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder + "/TownPalette.mat");
        var lamp = MakeItem("StreetLamp", "street_lamp", "Street lamp", "decor_lamp", 2.75f, 80, material, layout.shopToCityScale);
        var bench = MakeItem("Bench", "bench", "Outdoor bench", "decor_bench", .82f, 90, material, layout.shopToCityScale);
        var bin = MakeItem("LitterBin", "bin", "Outdoor litter bin", "decor_bin", .68f, 35, material, layout.shopToCityScale);
        var planter = MakeItem("FlowerPlanter", "planter", "Flower planter", "decor_planter", .53f, 45, material, layout.shopToCityScale);

        foreach (float offset in new[] {2f, 28f})
        {
            RemoveProp("StreetLamp", offset, -2, 0, 1 / 1.3f);
            RemoveProp("FlowerPlanter", offset, -3, 0, .6f);
        }
        RemoveProp("Bench", 6, -3, 180, .6f);
        RemoveProp("LitterBin", 10, -3, 0, .6f);
        SaveCuts(surfaces, "StarterDecorCut");

        var blueprint = AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(Blueprint);
        var serialized = new SerializedObject(blueprint);
        var decorations = serialized.FindProperty("frontageDecorations");
        var slots = new[] {(lamp, 5), (planter, 8), (bench, 14), (bin, 22), (planter, 35), (lamp, 39)};
        decorations.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            var entry = decorations.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("itemId").stringValue = slots[i].Item1.id;
            entry.FindPropertyRelative("x").intValue = slots[i].Item2;
            entry.FindPropertyRelative("z").intValue = -16;
            entry.FindPropertyRelative("facing").intValue = slots[i].Item1 == bench ? 2 : 0;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        void RemoveProp(string source, float x, float z, float yaw, float scale)
        {
            var mesh = NativeProp(source);
            var pose = Matrix4x4.TRS(origin + new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0), Vector3.one * scale);
            int removed = surfaces.MatchAndRemove(mesh, pose, town.transform, allowMissing: true);
            if (removed == 0) throw new InvalidOperationException("Could not locate baked " + source + " at " + pose.GetColumn(3));
            File.AppendAllText(Report + "/Geometry.txt", $"{source} at {pose.GetColumn(3)}: {removed} of {mesh.triangles.Length / 3} native triangles.\n");
        }
    }

    private static Mesh NativeProp(string name) =>
        AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/TownKit/Meshes/Street/" + name + ".asset");

    private static BuildItemData MakeItem(string native, string suffix, string name, string iconSource, float worldHeight,
        int price, Material material, float shopToCityScale)
    {
        var id = "frontage_" + suffix;
        var existing = AssetDatabase.LoadAssetAtPath<BuildItemData>(Items + "/" + id + ".asset");
        if (existing != null) return existing;
        var model = new GameObject(name);
        try
        {
            var mesh = NativeProp(native);
            float scale = worldHeight / mesh.bounds.size.y / shopToCityScale;
            var body = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(model.transform, false);
            body.transform.localScale = Vector3.one * scale;
            var bounds = mesh.bounds;
            body.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
            var size = bounds.size * scale;
            var collider = model.AddComponent<BoxCollider>();
            collider.center = Vector3.up * size.y * .5f;
            collider.size = native == "StreetLamp" ? new Vector3(.12f, size.y, .12f) : size;
            var asset = ScriptableObject.CreateInstance<BuildItemData>();
            asset.id = id;
            asset.displayName = name;
            asset.category = BuildCategory.Decoration;
            asset.subCategory = BuildSubCategory.Decorative;
            asset.price = price;
            asset.beautyPoints = 2;
            asset.weightKg = 8;
            asset.cargoVolumeM3 = .12f;
            asset.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap;
            asset.footprint = new Vector2Int(Mathf.CeilToInt(size.x / ShopProportions.CellSize),
                Mathf.CeilToInt(size.z / ShopProportions.CellSize));
            asset.alignModelToFootprint = true;
            asset.placementBounds = new Bounds(Vector3.up * size.y * .5f, size);
            asset.icon = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/StoreUpgrade/" + iconSource + ".asset").icon;
            asset.prefab = PrefabUtility.SaveAsPrefabAsset(model, Art + "/" + id + ".prefab");
            AssetDatabase.CreateAsset(asset, Items + "/" + id + ".asset");
            return asset;
        }
        finally { Object.DestroyImmediate(model); }
    }

    private static void RetireWorkshopStop(GameObject town)
    {
        var boundary = Rect.MinMaxRect(82.9f, -156, 97.1f, -147f);
        var visual = ReadGeometry(town, boundary, filter => filter.name.StartsWith("TownChunk_", StringComparison.Ordinal) ||
            filter.name == "Connected bus platforms");
        bool Inside(Vector3 point) => boundary.Contains(new Vector2(point.x, point.z));
        int removed = visual.Remove((view, offset) =>
        {
            var points = TownCityCorrectionsGeometry.Points(view, offset);
            if (!points.All(Inside)) return false;
            return view.Filter.name == "Connected bus platforms" || points.Max(p => p.y) > .089f && points.Max(p => p.y) < 4.5f;
        });
        if (removed < 50) throw new InvalidOperationException("Retired stop geometry did not match its saved location.");
        SaveCuts(visual, "WorkshopStopCut");
        Object.DestroyImmediate(town.transform.Find("Bus stops/05_BusStop").gameObject);
        var pad = town.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Repair88.ApprovedStopFloor");
        Object.DestroyImmediate(pad.gameObject);
        File.AppendAllText(Report + "/Geometry.txt", $"Retired workshop stop: {removed} scoped triangles and its physical shelter/pad.\n");
    }

    private static TownCityCorrectionsGeometry ReadGeometry(GameObject town, Rect rect, Func<MeshFilter, bool> allowed) =>
        new(town.GetComponentsInChildren<MeshFilter>().Where(filter => filter.sharedMesh != null && allowed(filter) &&
            filter.TryGetComponent<MeshRenderer>(out var renderer) && rect.Overlaps(Rect.MinMaxRect(
                renderer.bounds.min.x, renderer.bounds.min.z, renderer.bounds.max.x, renderer.bounds.max.z))));

    private static void SaveCuts(TownCityCorrectionsGeometry geometry, string prefix)
    {
        Directory.CreateDirectory(Art + "/Meshes");
        AssetDatabase.Refresh();
        int index = 0;
        foreach (var view in geometry.Views.Where(view => view.Removed.Count > 0))
        {
            var collider = view.Filter.GetComponent<MeshCollider>();
            bool shareGeometry = collider != null && collider.sharedMesh == view.Source;
            view.Save(prefix + "_" + index++, Art);
            if (shareGeometry) collider.sharedMesh = view.Filter.sharedMesh;
        }
    }
}
