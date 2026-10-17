using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Imports the individually approved Blender sources without reauthoring the town or roads.</summary>
public static class IndividualTrafficBatch
{
    private const string Root = TrafficFleetBatch.Root;
    private const string Manifest = "ArtSource/IndividualTraffic/fleet.json";
    private const string Evidence = "Library/IndividualTrafficQA";

    [Serializable] private sealed class Fleet { public Design[] vehicles; }
    [Serializable] private sealed class Design
    {
        public string id, source_sha256;
        public float width, length, height, wheelbase, radius, centerZ;
        public Wheel[] wheels;
        public int[] lodTriangles;
    }
    [Serializable] private sealed class Wheel { public string name; public float[] position; public bool front; }

    public static void Build() => Run(true);
    public static void Review() => Run(false);

    private static void Run(bool build)
    {
        string stage = build ? "build" : "review";
        Directory.CreateDirectory(Evidence);
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            File.WriteAllText(Evidence + "/" + stage + ".txt", "RUNNING\n");
            var protectedFiles = new[] { TownWorldAssembler.PrefabPath, CityUpdateSetup.CityPath,
                CityUpdateSetup.GamePath, Root + "/Data/RoadNetwork.asset" };
            var before = protectedFiles.ToDictionary(path => path, TrafficFleetBatch.Hash);
            var fleet = JsonUtility.FromJson<Fleet>(File.ReadAllText(Manifest));
            Require(fleet.vehicles.Length == 20, "Twenty individual sources are required.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.Refresh();
            if (build)
            {
                var material = CreateMaterial();
                File.WriteAllText(Evidence + "/geometry.txt", string.Empty);
                foreach (var design in fleet.vehicles) Bake(design, material);
                AssetDatabase.SaveAssets();
            }
            Validate(fleet);
            Directory.CreateDirectory(TrafficFleetBatch.Pictures);
            TrafficFleetReferenceReview.Capture();
            CaptureDistricts();
            foreach (var entry in before)
                Require(TrafficFleetBatch.Hash(entry.Key) == entry.Value, "Protected scene/network changed: " + entry.Key);
            TrafficFleetBatch.Receipt("individual-" + stage);
            File.WriteAllText(Evidence + "/" + stage + ".txt",
                "PASS: 20 individual sources; source IDs retained; 3 LODs, 4 wheel pivots, shared textured opaque material; City/Game fleet bindings; unchanged town/scenes/network.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Evidence + "/" + stage + ".txt", "FAILED\n" + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static Material CreateMaterial()
    {
        Texture2D Texture(string name, bool normal, bool srgb)
        {
            string path = Root + "/Textures/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Require(importer != null, "Missing exported atlas: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { name = "Individual traffic / opaque textured atlas", enableInstancing = true };
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", Texture("FleetBase", false, true));
        material.SetTexture("_MetallicGlossMap", Texture("FleetMetalSmooth", false, false));
        material.SetTexture("_BumpMap", Texture("FleetNormal", true, false));
        material.SetFloat("_Metallic", 1);
        material.SetFloat("_Smoothness", 1);
        material.SetFloat("_BumpScale", 1);
        material.SetFloat("_Surface", 0);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetOverrideTag("RenderType", "Opaque");
        TrafficFleetBatch.Save(material, Root + "/Materials/IndividualFleet.mat");
        return AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/IndividualFleet.mat");
    }

    private static void Bake(Design design, Material material)
    {
        string path = Root + "/Models/" + design.id + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        Require(importer != null, "Missing FBX: " + path);
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.isReadable = true;
        importer.SaveAndReimport();
        string prefabPath = Root + "/Prefabs/" + design.id + ".prefab";
        var previous = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Require(previous != null, "The installed traffic prefab must exist: " + design.id);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(previous.GetComponent<TrafficVehicle>(), out string oldGuid, out long oldFileId);
        var holder = new GameObject("Individual FBX bake");
        var output = new GameObject(design.id);
        try
        {
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), holder.transform, false);
            var sources = holder.GetComponentsInChildren<MeshFilter>();
            Require(sources.Length == 15, "Expected five meshes per LOD: " + design.id);
            var lodRenderers = new List<Renderer>[] { new(), new(), new() };
            var bindings = new List<TrafficVehicle.Wheel>();
            Part("Body", Vector3.zero);
            foreach (var wheel in design.wheels)
            {
                var pivot = new Vector3(wheel.position[0], wheel.position[1], wheel.position[2]);
                var transform = Part("Wheel" + wheel.name, pivot);
                bindings.Add(new TrafficVehicle.Wheel { visual = transform, front = wheel.front });
            }
            var group = output.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.16f, lodRenderers[0].ToArray()),
                new LOD(.06f, lodRenderers[1].ToArray()), new LOD(.008f, lodRenderers[2].ToArray()) });
            group.RecalculateBounds();
            var rigidbody = output.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var collider = output.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, (design.height + .2f) * .5f, design.centerZ);
            collider.size = new Vector3(design.width, design.height - .2f, design.length);
            output.AddComponent<TrafficVehicle>().Configure(design.id, design.length,
                design.width, design.wheelbase, design.radius, bindings.ToArray());
            var saved = PrefabUtility.SaveAsPrefabAsset(output, prefabPath);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(saved.GetComponent<TrafficVehicle>(), out string guid, out long fileId);
            Require(guid == oldGuid && fileId == oldFileId, "Saved town fleet reference would break: " + design.id);
            File.AppendAllText(Evidence + "/geometry.txt", design.id + ": " +
                string.Join(" / ", design.lodTriangles) + " triangles; source " + design.source_sha256 + "\n");

            Transform Part(string name, Vector3 pivot)
            {
                var parent = new GameObject(name).transform;
                parent.SetParent(output.transform, false);
                parent.localPosition = pivot;
                for (int level = 0; level < 3; level++)
                {
                    string sourceName = "LOD" + level + "_" + name;
                    var filter = sources.Single(f => f.name == sourceName);
                    var mesh = Object.Instantiate(filter.sharedMesh);
                    var matrix = holder.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var normalMatrix = matrix.inverse.transpose;
                    mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v) - pivot).ToArray();
                    mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
                    mesh.name = design.id + "_" + name + (level == 0 ? "" : "_LOD" + level);
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    string meshPath = Root + "/Meshes/" + mesh.name + ".asset";
                    TrafficFleetBatch.Save(mesh, meshPath);
                    var part = new GameObject("LOD" + level, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(parent, false);
                    part.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    var renderer = part.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = level == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    lodRenderers[level].Add(renderer);
                }
                return parent;
            }
        }
        finally { Object.DestroyImmediate(holder); Object.DestroyImmediate(output); }
    }

    private static void Validate(Fleet fleet)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/IndividualFleet.mat");
        Require(material != null && material.GetFloat("_Surface") == 0 && material.renderQueue < 3000,
            "Traffic glass must be opaque.");
        Require(material.GetTexture("_BumpMap") != null && material.GetTexture("_MetallicGlossMap") != null,
            "Surface maps were lost during transfer.");
        foreach (var design in fleet.vehicles)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + design.id + ".prefab");
            var group = prefab.GetComponent<LODGroup>();
            Require(group != null && group.lodCount == 3, "LOD group missing: " + design.id);
            var lods = group.GetLODs();
            int referenceGlassTriangles = -1;
            for (int level = 0; level < 3; level++)
            {
                Require(lods[level].renderers.Length == 5, "Exactly five renderers per LOD required.");
                int count = 0;
                foreach (var renderer in lods[level].renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    count += mesh.triangles.Length / 3;
                    Require(renderer.sharedMaterial == material, "Material atlas missing.");
                    Require(mesh.uv.Length == mesh.vertexCount && mesh.normals.Length == mesh.vertexCount,
                        "Lost UVs/normals: " + renderer.name);
                }
                Require(count == design.lodTriangles[level], "Imported triangle count differs: " + design.id);
                var glazing = prefab.transform.Find("Body/LOD" + level).GetComponent<MeshFilter>().sharedMesh;
                var textureCoordinates = glazing.uv;
                var indices = glazing.triangles;
                int glassTriangles = 0;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var uv = textureCoordinates[indices[i]];
                    if (Mathf.FloorToInt(uv.x * 8) == 0 && Mathf.FloorToInt(uv.y * 8) == 5) glassTriangles++;
                }
                if (level == 0) referenceGlassTriangles = glassTriangles;
                Require(glassTriangles >= 24 && glassTriangles == referenceGlassTriangles,
                    "Distance simplification deleted window overlays: " + design.id);
            }
            Require(design.lodTriangles[1] < design.lodTriangles[0] * .5f &&
                design.lodTriangles[2] < design.lodTriangles[0] * .20f, "Distance meshes not simplified.");
            var body = prefab.transform.Find("Body/LOD0").GetComponent<MeshFilter>().sharedMesh;
            Require(body.bounds.size.z > 2.7f && body.bounds.size.z < 7.1f && body.bounds.size.x < 2.9f &&
                body.bounds.min.y > -.02f, "Wrong FBX scale/axes: " + design.id);
            var lightVertices = Enumerable.Range(0, body.vertexCount).Where(i =>
                Mathf.FloorToInt(body.uv[i].x * 8) == 6 && Mathf.FloorToInt(body.uv[i].y * 8) == 5).ToArray();
            Require(lightVertices.Length > 0 && lightVertices.Average(i => body.vertices[i].z) > design.length * .30f,
                "Headlamps must face Unity +Z: " + design.id);
            foreach (var wheel in design.wheels)
            {
                var pivot = prefab.transform.Find("Wheel" + wheel.name);
                Require(pivot != null && pivot.childCount == 3, "Lost independent wheel: " + design.id);
                var mesh = pivot.Find("LOD0").GetComponent<MeshFilter>().sharedMesh;
                Require(mesh.bounds.size.x < .6f && mesh.bounds.center.magnitude < .08f &&
                    Mathf.Abs(pivot.localPosition.y - design.radius) < .001f, "Wheel axis/pivot changed.");
            }
        }
        foreach (string scenePath in new[] { CityUpdateSetup.CityPath, CityUpdateSetup.GamePath })
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var systems = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TownRoadTraffic>(true)).ToArray();
            Require(systems.Length == 1 && systems[0].Network != null, "Missing traffic in saved scene.");
            var bindings = new SerializedObject(systems[0]).FindProperty("fleet");
            Require(bindings.arraySize == 20, "Incomplete saved-scene fleet.");
            for (int i = 0; i < 20; i++)
            {
                var vehicle = bindings.GetArrayElementAtIndex(i).objectReferenceValue as TrafficVehicle;
                Require(vehicle != null && vehicle.DesignId == fleet.vehicles[i].id && vehicle.GetComponent<LODGroup>() != null,
                    "Saved scene still uses an old or missing model: " + scenePath);
            }
        }
    }

    private static void CaptureDistricts()
    {
        var scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var traffic = Object.FindObjectOfType<TownRoadTraffic>();
        traffic.enabled = false;
        var areas = new[] { new Vector3(-115, 0, 40), new Vector3(-24, 0, 350), new Vector3(550, 0, 278) };
        for (int i = 0; i < areas.Length; i++)
        {
            traffic.SetObserver(null);
            traffic.Simulate(.02f, 0, traffic.transform.TransformPoint(areas[i]), false);
            Require(traffic.ActiveCars.Count > 0, "No traffic in review district.");
            TownFloristBatch.Render(scene, TrafficFleetBatch.Pictures + "/IndividualDistrict" + i + ".png",
                areas[i] + new Vector3(-48, 24, -52), areas[i] + Vector3.up);
        }
        scene = EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        traffic = Object.FindObjectOfType<TownRoadTraffic>();
        traffic.enabled = false;
        traffic.Simulate(.02f, 0, traffic.transform.TransformPoint(areas[0]), false);
        Require(traffic.ActiveCars.Count > 0, "Scaled Game scene has no traffic.");
        TownFloristBatch.Render(scene, TrafficFleetBatch.Pictures + "/IndividualGame.png",
            traffic.transform.TransformPoint(areas[0] + new Vector3(-48, 24, -52)),
            traffic.transform.TransformPoint(areas[0] + Vector3.up));
    }

    private static void Require(bool condition, string message) => TrafficFleetBatch.Require(condition, message);
}
