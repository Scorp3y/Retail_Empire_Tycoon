using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Stage A: scale and clearance review of Auto Works, built in the isolated Unity project.</summary>
[InitializeOnLoad]
public static class AutoWorksProportions
{
    private const string AssetRoot = "Assets/Art/AutoWorksReview";
    private const string Report = "Docs/WorkshopConcept/StageA";
    private const string PrefabPath = AssetRoot + "/AutoWorks_Proportions.prefab";
    private const string TestActive = "AutoWorks.Proportions.Play";
    private const float Floor = .09f;
    private const float Facade = -167f;
    private const float Back = -153f;
    private const float ClearWidth = 5.4f;
    private const float ClearHeight = 4.7f;
    private const float Eaves = 5.55f;
    private const float Wall = .32f;
    private const float ParkedZ = -160.1f;
    private static readonly float[] Bays = {134f, 144f, 154f};
    private static readonly Vector3 SitePosition = new(120f, Floor, -168f);
    private static readonly Dictionary<string, Material> Materials = new();
    private static PickupDrive car;
    private static BoxCollider chassis;
    private static int run;
    private static DrivePhase phase;
    private static float settleUntil;
    private static DateTime deadline;
    private static float minSideClearance;
    private static float maxRoof;
    private static float startZ;
    private static string vehicleName;
    private enum DrivePhase { Prepare, Settle, Enter, Park, Reverse }

    static AutoWorksProportions() => EditorApplication.update += Tick;

    public static void Build()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report + "/build.txt", "RUNNING\n");
            Directory.CreateDirectory(AssetRoot + "/Materials");
            Directory.CreateDirectory(AssetRoot + "/Meshes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.Refresh();
            MakeMaterials();
            var root = BuildSite();
            Check(root.position == Vector3.zero && root.rotation == Quaternion.identity && root.localScale == Vector3.one,
                "Review prefab pivot or axes are not normalized.");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Check(prefab != null, "Review prefab could not be saved.");
            Object.DestroyImmediate(root.gameObject);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            AddReviewLighting();
            AddReviewCamera();
            Check(EditorSceneManager.SaveScene(scene, AssetRoot + "/AutoWorks_Proportions.unity"), "Review scene could not be saved.");
            AssetDatabase.SaveAssets();

            var heavy = HeavyTruck();
            Check(ClearWidth >= heavy.width + 1 && ClearHeight >= heavy.height + .8f, "The largest vehicle exceeds the required door margin.");
            Check(Back - Facade - Wall > heavy.length + 2, "The common hall is too shallow.");
            var bounds = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentInChildren<BoxCollider>();
            Check(bounds != null, "Physical review model has no colliders.");
            File.WriteAllText(Report + "/build.txt", FormattableString.Invariant(
                $"PASS. Site 84 x 36 m, hall 35 x 14 m, openings {ClearWidth:F2} x {ClearHeight:F2} m. Heavy box truck {heavy.length:F2} x {heavy.width:F2} x {heavy.height:F2} m.\n"));
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Finish("build", error); }
    }

    private static Transform BuildSite()
    {
        var root = new GameObject("STO_Root").transform;
        var building = Group(root, "Building");
        var interior = Group(root, "Interior");
        var canopy = Group(root, "Canopy");
        var yard = Group(root, "Yard");
        var fence = Group(root, "Fence");
        var doors = Group(root, "Doors");
        Group(root, "Greenery");
        Group(root, "Lights");
        Group(root, "Props");

        // A single physical slab supports the apron and hall. The notch meets the existing lowered driveway.
        Box(yard, "Continuous slab", 78, -183.9f, 84, 33.9f, -.15f, 0, "Asphalt");
        Box(yard, "West edge of apron", 78, -186, 62.9f, 2.1f, -.15f, 0, "Asphalt");
        Box(yard, "East edge of apron", 147.1f, -186, 14.9f, 2.1f, -.15f, 0, "Asphalt");
        Box(interior, "Concrete finish", 126.5f, Facade, 35, Back - Facade, .001f, .004f, "Concrete", false);
        Box(interior, "Rear wall", 126.5f, Back - Wall, 35, Wall, 0, Eaves, "Wall");
        Box(interior, "West wall", 126.5f, Facade, Wall, Back - Facade, 0, Eaves, "Wall");
        Box(interior, "East wall", 161.5f - Wall, Facade, Wall, Back - Facade, 0, Eaves, "Wall");

        float left = 126.5f;
        foreach (float center in Bays)
        {
            float openingLeft = center - ClearWidth / 2;
            Box(building, "Front pier", left, Facade, openingLeft - left, Wall, 0, Eaves, "Pier");
            Box(building, "Header", openingLeft, Facade, ClearWidth, Wall, ClearHeight, Eaves, "Wall");
            var opening = Group(doors, "Opening_" + (Array.IndexOf(Bays, center) + 1));
            opening.localPosition = Local(center, 0, Facade);
            left = center + ClearWidth / 2;
        }
        Box(building, "East front pier", left, Facade, 161.5f - left, Wall, 0, Eaves, "Pier");
        Gable(building, 144, -160, 35.45f, 14.42f, Eaves, .9f);
        Box(building, "Roof lantern mass", 134, -161.2f, 20, 2.4f, 6.15f, 7.05f, "Olive", false);
        Box(building, "Roof lantern cap", 133.8f, -161.4f, 20.4f, 2.8f, 7.05f, 7.22f, "Roof", false);

        Box(building, "Service block mass", 96.5f, -163.5f, 10, 9.5f, 0, 3.5f, "Wall");
        Box(building, "Reception mass", 106.5f, -164, 17, 10, 0, 4.1f, "Wall");
        Box(building, "Service roof", 96.3f, -163.7f, 10.4f, 9.9f, 3.5f, 3.72f, "Roof", false);
        Box(building, "Reception roof", 106.3f, -164.2f, 17.4f, 10.4f, 4.1f, 4.32f, "Roof", false);
        Box(building, "Reception glazing extent", 108, -164.04f, 14, .04f, .3f, 2.65f, "Glass", false);
        Box(building, "Reception awning extent", 107.7f, -165.05f, 14.6f, 1.05f, 2.82f, 3f, "Olive", false);

        foreach (float x in new[] {81.4f, 95.3f})
            foreach (float z in new[] {-164.6f, -157.6f})
                Box(canopy, "Canopy post", x, z, .24f, .24f, 0, 3.35f, "Pier");
        Box(canopy, "Canopy roof mass", 81, -165, 15, 8, 3.35f, 3.65f, "Olive", false);
        Box(yard, "Reception walk", 81.8f, -167.05f, 41.8f, 1.8f, .005f, .026f, "Concrete", false);
        Box(yard, "Rear pedestrian walk", 112, -154, 2, 4, .005f, .026f, "Concrete", false);
        for (int i = 0; i <= 3; i++)
            Box(yard, "Plain parking line", 82 + 3.1f * i, -173.1f, .09f, 6, .005f, .008f, "Mark", false);
        Box(yard, "Parking end line", 82, -167.19f, 9.39f, .09f, .005f, .008f, "Mark", false);

        Box(fence, "Front fence west", 79, -185.6f, 61.5f, .20f, 0, 1.5f, "Fence");
        Box(fence, "Front fence east", 147.5f, -185.6f, 13.5f, .20f, 0, 1.5f, "Fence");
        foreach (float x in new[] {79f, 140.05f, 147.5f, 160.55f})
            Box(fence, "Fence pier", x, -185.74f, .45f, .48f, 0, 1.64f, "Pier");
        Box(fence, "West side fence", 79, -185.4f, .15f, 29.4f, 0, 1.5f, "Fence");
        Box(fence, "East side fence", 161.8f, -185.4f, .15f, 32.8f, 0, 1.5f, "Fence");
        Box(fence, "Rear fence west", 96, -152, 16, .15f, 0, 1.5f, "Fence");
        Box(fence, "Rear fence east", 114, -152, 47.9f, .15f, 0, 1.5f, "Fence");
        return root;
    }

    public static void Capture()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            var scene = EditorSceneManager.OpenScene(AssetRoot + "/AutoWorks_Proportions.unity");
            ApplyPreviewColors();
            TownFloristBatch.Render(scene, Report + "/Greybox-Overview.png", new Vector3(-25, 64, 64), new Vector3(0, 1, 0), 30);
            TownFloristBatch.Render(scene, Report + "/Greybox-Entrance.png", new Vector3(-24, 2.6f, 23), new Vector3(-24, 2.3f, 1));
            var building = scene.GetRootGameObjects().Select(root => root.transform.Find("Building")).First(group => group != null);
            foreach (string name in new[] {"Garage roof mass", "Roof lantern mass", "Roof lantern cap"})
                building.Find(name).gameObject.SetActive(false);
            TownFloristBatch.Render(scene, Report + "/Greybox-Hall.png", new Vector3(-34, 27, 24), new Vector3(-24, .3f, -7), 14);
            scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var town = Object.FindObjectOfType<TownWorldPlaces>();
            var onSite = PlaceReviewOnSite(town);
            ApplyPreviewColors(onSite.GetComponentsInChildren<MeshRenderer>());
            RenderSettings.fog = false;
            TownFloristBatch.Render(scene, Report + "/Greybox-City-Context.png", new Vector3(155, 55, -224), new Vector3(120, 0, -168), 35);
            File.WriteAllText(Report + "/capture.txt", "PASS. Isolated review scene and unsaved city context captured.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Finish("capture", error); }
    }

    public static void Play()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report + "/play.txt", "RUNNING\n");
            File.WriteAllText(Report + "/drives.txt", string.Empty);
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            SessionState.SetBool(TestActive, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish("play", error); }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(TestActive, false) || Time.frameCount < 15) return;
        try
        {
            if (car == null) SetUpDriveScene();
            if (run == 6)
            {
                SessionState.SetBool(TestActive, false);
                File.WriteAllText(Report + "/play.txt", "PASS. Six real PickupDrive/WheelCollider runs through the three open greybox portals. The two center runs started on the real city roadway and crossed the retained lowered driveway. All three west parking stalls occupied by temporary clearance envelopes.\n");
                EditorApplication.Exit(0);
                return;
            }
            AdvanceDrive();
        }
        catch (Exception error) { Finish("play", error); }
    }

    private static void SetUpDriveScene()
    {
        var city = Object.FindObjectOfType<CityWorld>();
        Check(city != null && city.pickup != null && city.town != null, "City pickup or town is missing.");
        car = city.pickup;
        city.enabled = false;
        foreach (var traffic in Object.FindObjectsOfType<TownRoadTraffic>()) traffic.enabled = false;
        foreach (var vehicle in Object.FindObjectsOfType<TrafficVehicle>()) vehicle.gameObject.SetActive(false);
        var stage = PlaceReviewOnSite(city.town);
        var apron = city.town.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c => c.name == "Workshop lowered driveway");
        Check(apron != null && apron.enabled && apron.gameObject.activeInHierarchy, "Existing lowered driveway is not active.");
        var parking = Group(stage.transform, "QA occupied parking envelopes");
        for (int i = 0; i < 3; i++)
        {
            var blocker = Group(parking, "Parking envelope");
            blocker.localPosition = Local(83.55f + i * 3.1f, .85f, -170.1f);
            blocker.gameObject.AddComponent<BoxCollider>().size = new Vector3(2.45f, 1.7f, 5.2f);
        }
        car.ReadKeyboard = false;
        car.InputEnabled = true;
        chassis = car.GetComponent<BoxCollider>();
        Time.timeScale = 2.5f;
        Physics.SyncTransforms();
    }

    private static GameObject PlaceReviewOnSite(TownWorldPlaces town)
    {
        Check(town != null, "City town is missing.");
        var old = town.transform.Find(TownSpecialPlan.WorkshopRoot);
        Check(old != null, "Cannot isolate the exact existing city workshop.");
        old.gameObject.SetActive(false);
        var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        stage.transform.SetPositionAndRotation(SitePosition, Quaternion.Euler(0, 180, 0));
        return stage;
    }

    private static void AdvanceDrive()
    {
        int bay = run % Bays.Length;
        float center = Bays[bay];
        if (phase == DrivePhase.Prepare)
        {
            var definition = run < 3 ? null : HeavyTruck();
            car.SetVehicle(definition);
            vehicleName = definition == null ? "Delivery pickup" : definition.displayName;
            startZ = bay == 1 ? -193f : -179.5f;
            car.Stop();
            car.Body.position = new Vector3(center, .28f, startZ);
            car.Body.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            minSideClearance = float.PositiveInfinity;
            maxRoof = 0;
            settleUntil = Time.fixedTime + .65f;
            deadline = DateTime.UtcNow.AddSeconds(35);
            phase = DrivePhase.Settle;
            return;
        }

        var position = car.Body.position;
        Check(DateTime.UtcNow < deadline, $"{vehicleName}, bay {bay + 1}: {phase} timed out at {position}, speed {car.Speed:F2}.");
        Check(position.y > -.12f && position.y < .40f, $"{vehicleName} lost the intended floor at {position}.");
        if (phase == DrivePhase.Settle && Time.fixedTime < settleUntil) return;
        if (phase == DrivePhase.Settle) phase = DrivePhase.Enter;
        var bounds = chassis.bounds;
        if (bounds.max.z >= Facade && bounds.min.z <= Facade + Wall)
        {
            float gap = Mathf.Min(bounds.min.x - (center - ClearWidth / 2), center + ClearWidth / 2 - bounds.max.x);
            minSideClearance = Mathf.Min(minSideClearance, gap);
            maxRoof = Mathf.Max(maxRoof, bounds.max.y - Floor);
            Check(gap > .10f && maxRoof < ClearHeight - .10f, $"{vehicleName} lacks clearance at bay {bay + 1}.");
        }
        float steering = Mathf.Clamp((center - position.x) * .36f - Vector3.SignedAngle(Vector3.forward, car.transform.forward, Vector3.up) * .035f, -.36f, .36f);
        if (phase == DrivePhase.Enter)
        {
            if (position.z < ParkedZ)
            {
                car.SetInput(position.z < ParkedZ - 2.5f ? .26f : .12f, steering, false);
                return;
            }
            car.Stop();
            settleUntil = Time.fixedTime + .45f;
            phase = DrivePhase.Park;
            return;
        }
        if (phase == DrivePhase.Park)
        {
            if (Time.fixedTime < settleUntil) return;
            Check(bounds.min.z > Facade + .25f && bounds.max.z < Back - Wall - .25f, $"{vehicleName} is not fully parked inside the common hall.");
            Check(car.GroundedWheels >= 4 && car.Speed < .2f, $"{vehicleName} did not settle on the hall floor.");
            phase = DrivePhase.Reverse;
        }
        if (position.z > -178.5f)
        {
            float reverseCorrection = Mathf.Clamp(-(center - position.x) * .36f + Vector3.SignedAngle(Vector3.forward, car.transform.forward, Vector3.up) * .035f, -.36f, .36f);
            car.SetInput(-.48f, reverseCorrection, false);
            return;
        }
        Check(bounds.max.z < Facade - 1f && !float.IsInfinity(minSideClearance), "The physical vehicle did not clear the bay after reversing.");
        car.Stop();
        File.AppendAllText(Report + "/drives.txt", FormattableString.Invariant(
            $"PASS {vehicleName}, bay {bay + 1}: approach {startZ:F1} to {ParkedZ:F1}; fully stopped inside; reversed into yard; minimum side gap {minSideClearance:F2} m, maximum roof {maxRoof:F2} m above floor.\n"));
        run++;
        phase = DrivePhase.Prepare;
    }

    private static PlayerVehicleDefinition HeavyTruck()
    {
        var truck = AssetDatabase.LoadAssetAtPath<PlayerVehicleDefinition>("Assets/Resources/PlayerVehicles/heavy_box_truck.asset");
        Check(truck != null, "The actual heavy truck definition is missing.");
        return truck;
    }

    private static void MakeMaterials()
    {
        var palette = new Dictionary<string, string>
        {
            {"Asphalt", "#515658"}, {"Concrete", "#C5B99E"}, {"Wall", "#C9B18A"},
            {"Pier", "#8F6657"}, {"Roof", "#52595B"}, {"Olive", "#69725A"},
            {"Glass", "#799094"}, {"Fence", "#454C49"}, {"Mark", "#DFD5BB"}
        };
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Check(shader != null, "The project URP Lit shader is missing.");
        Materials.Clear();
        foreach (var pair in palette)
        {
            ColorUtility.TryParseHtmlString(pair.Value, out var color);
            var material = new Material(shader) { name = pair.Key };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .09f);
            string path = AssetRoot + "/Materials/" + pair.Key + ".mat";
            TownBakeryPlacement.SaveAsset(material, path);
            Materials.Add(pair.Key, AssetDatabase.LoadAssetAtPath<Material>(path));
        }
    }

    private static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static Vector3 Local(float x, float y, float z) => new(SitePosition.x - x, y, SitePosition.z - z);

    private static void Box(Transform parent, string name, float x, float z, float width, float depth,
        float bottom, float top, string material, bool collision = true)
    {
        Check(width > 0 && depth > 0 && top > bottom, "Invalid blockout dimensions: " + name);
        var size = new Vector3(width, top - bottom, depth);
        var block = Group(parent, name);
        block.localPosition = Local(x + width / 2, (bottom + top) / 2, z + depth / 2);
        string id = FormattableString.Invariant($"Box_{size.x:F3}_{size.y:F3}_{size.z:F3}");
        string path = AssetRoot + "/Meshes/" + id + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            var geometry = new TownMeshBuilder();
            geometry.Box(Vector3.zero, size, TownColor.White);
            AssetDatabase.CreateAsset(geometry.Build(id), path);
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        block.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        block.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Materials[material];
        if (collision) block.gameObject.AddComponent<BoxCollider>().size = size;
        block.gameObject.isStatic = true;
    }

    private static void Gable(Transform parent, float x, float z, float width, float depth, float eaves, float rise)
    {
        var roof = Group(parent, "Garage roof mass");
        roof.localPosition = Local(x, eaves, z);
        var geometry = new TownMeshBuilder();
        geometry.Gable(Vector3.zero, depth, width, rise, TownColor.White, TownColor.White);
        var mesh = geometry.Build("GarageRoof");
        var turn = Quaternion.Euler(0, 90, 0);
        mesh.vertices = mesh.vertices.Select(vertex => turn * vertex).ToArray();
        mesh.normals = mesh.normals.Select(normal => turn * normal).ToArray();
        mesh.RecalculateBounds();
        string path = AssetRoot + "/Meshes/GarageRoof.asset";
        TownBakeryPlacement.SaveAsset(mesh, path);
        roof.gameObject.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        roof.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Materials["Roof"];
        roof.gameObject.isStatic = true;
    }

    private static void AddReviewLighting()
    {
        var sun = new GameObject("Review daylight", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, .91f, .79f);
        sun.intensity = 1.2f;
        sun.transform.rotation = Quaternion.Euler(52, -32, 0);
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.7f, .72f, .73f);
        RenderSettings.fog = false;
    }

    private static void AddReviewCamera()
    {
        var camera = new GameObject("Review camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(-25, 43, 61);
        camera.transform.LookAt(new Vector3(0, 1, 0));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.68f, .72f, .73f);
        camera.orthographic = true;
        camera.orthographicSize = 30;
    }

    private static void ApplyPreviewColors() => ApplyPreviewColors(Object.FindObjectsOfType<MeshRenderer>());

    private static void ApplyPreviewColors(IEnumerable<MeshRenderer> renderers)
    {
        var block = new MaterialPropertyBlock();
        foreach (var renderer in renderers)
        {
            block.SetColor("_BaseColor", renderer.sharedMaterial.GetColor("_BaseColor"));
            renderer.SetPropertyBlock(block);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Finish(string stage, Exception error)
    {
        SessionState.SetBool(TestActive, false);
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "/" + stage + ".txt", "FAILED\n" + error + "\n");
        Debug.LogException(error);
        EditorApplication.Exit(1);
    }
}
