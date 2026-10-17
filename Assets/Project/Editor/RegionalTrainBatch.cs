using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Imports Blender modules, bakes one palette draw per car, replaces only train visuals.</summary>
[InitializeOnLoad]
public static class RegionalTrainBatch
{
    private const string Root = "Assets/Art/RegionalTrain";
    private const string Report = "Library/RegionalTrainQA";
    private const string Pictures = "Docs/TrainBlenderReview";
    private const string Playing = "RegionalTrain.Play";
    private static int firstFrame;
    private static float firstDistance;
    private static float firstTime;
    private static DateTime playDeadline;
    private static readonly string[] ColorNames = { "Cream", "Green", "Red", "Roof", "Glass", "Dark", "Steel", "Lamp" };
    private static readonly Color[] Colors = {
        new(.85f,.82f,.69f), new(.09f,.32f,.29f), new(.65f,.21f,.15f), new(.34f,.38f,.41f),
        new(.28f,.45f,.54f), new(.13f,.17f,.18f), new(.43f,.49f,.48f), new(1,.91f,.62f)
    };

    static RegionalTrainBatch() { EditorApplication.update += Tick; }
    public static void Build() => Run(true);
    public static void Review() => Run(false);

    private static void Prepare(string stage)
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Report);
        Directory.CreateDirectory(Pictures);
        File.WriteAllText(Report + "/" + stage + ".txt", "RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();
    }

    private static void Run(bool build)
    {
        string stage = build ? "build" : "review";
        try
        {
            Prepare(stage);
            if (build)
            {
                foreach (string folder in new[] { "Meshes", "Materials", "Prefabs" })
                    Directory.CreateDirectory(Root + "/" + folder);
                AssetDatabase.Refresh();
                var material = Palette();
                BakeModule("RegionalCab", material);
                BakeModule("RegionalCoach", material);
                var town = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
                try
                {
                    var train = town.GetComponentInChildren<TownRailTraffic>(true);
                    Require(train != null && train.cars.Length == 4, "Expected existing four-car service.");
                    string outside = OutsideSignature(town, train.transform);
                    string service = EditorJsonUtility.ToJson(train);
                    for (int i = 0; i < train.cars.Length; i++)
                    {
                        var car = train.cars[i];
                        Require(car.childCount == 0, "Unrecognized car children; refusing to remove custom work.");
                        Object.DestroyImmediate(car.GetComponent<MeshRenderer>());
                        Object.DestroyImmediate(car.GetComponent<MeshFilter>());
                        string module = i == 0 || i == 3 ? "RegionalCab" : "RegionalCoach";
                        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + module + ".prefab"));
                        visual.transform.SetParent(car, false);
                        if (i == 3) visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    }
                    Require(EditorJsonUtility.ToJson(train) == service, "Train route/timetable/roots changed.");
                    Require(OutsideSignature(town, train.transform) == outside, "Unrelated town content changed.");
                    PrefabUtility.SaveAsPrefabAsset(town, TownWorldAssembler.PrefabPath);
                    File.WriteAllText(Report + "/protection.txt", "PASS: every transform and component outside the train service unchanged in the loaded prefab; existing four car roots, route, spacing, station wait, speed and seven-minute timetable unchanged.\n");
                }
                finally { PrefabUtility.UnloadPrefabContents(town); }
                AssetDatabase.SaveAssets();
            }
            else
            {
                ValidateAndCapture();
            }
            Receipt(stage);
            File.WriteAllText(Report + "/" + stage + ".txt", "PASS: " + (build ? "Blender FBX imported; train-only visual replacement saved." : "Imported native geometry, four-car assembly, schedule and rendered town views validated.") + "\n");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Fail(stage, e); }
    }

    private static Material Palette()
    {
        var texture = new Texture2D(8, 1, TextureFormat.RGBA32, false) {
            name = "RegionalTrainPalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(Colors);
        texture.Apply();
        TownBakeryPlacement.SaveAsset(texture, Root + "/Materials/Palette.asset");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Require(shader != null, "Project URP Lit shader not available.");
        var material = new Material(shader) { name = "RegionalTrain", enableInstancing = true };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Materials/Palette.asset"));
        material.SetFloat("_Smoothness", 0);
        material.SetFloat("_Metallic", 0);
        TownBakeryPlacement.SaveAsset(material, Root + "/Materials/Train.mat");
        return AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Train.mat");
    }

    private static void BakeModule(string id, Material material)
    {
        string path = Root + "/Models/" + id + ".fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        Require(importer != null, "Missing Blender export: " + path);
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.isReadable = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.SaveAndReimport();

        var holder = new GameObject("FBX baking space");
        try
        {
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), holder.transform, false);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            foreach (var filter in holder.GetComponentsInChildren<MeshFilter>())
            {
                var source = filter.sharedMesh;
                var points = source.vertices;
                var directions = source.normals;
                var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                var matrix = holder.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    string name = materials[sub].name.Split('.')[0];
                    int color = Array.IndexOf(ColorNames, name);
                    Require(color >= 0, "Unknown source material: " + name);
                    foreach (int index in source.GetTriangles(sub))
                    {
                        vertices.Add(matrix.MultiplyPoint3x4(points[index]));
                        normals.Add(normalMatrix.MultiplyVector(directions[index]).normalized);
                        uv.Add(new Vector2((color + .5f) / 8, .5f));
                        indices.Add(indices.Count);
                    }
                }
            }
            var mesh = new Mesh { name = id, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            var size = mesh.bounds.size;
            Require(size.x > 2.5f && size.x < 3 && size.y > 3.3f && size.y < 3.8f && size.z > 10.5f && size.z < 12.2f,
                "FBX units/axis conversion incorrect: " + id + " " + size);
            Require(indices.Count / 3 < 2500 && vertices.All(TownBakeryQuarterPlacement.Finite), "Low-poly geometry budget/finite check failed.");
            if (id == "RegionalCab")
            {
                var windscreen = Enumerable.Range(0, vertices.Count).Where(i => Mathf.Abs(uv[i].x - 4.5f / 8) < .001f && Mathf.Abs(vertices[i].x) < 1.15f && vertices[i].z > 4.9f).ToArray();
                Require(windscreen.Length == 12 && windscreen.All(i => normals[i].z > .5f),
                    "Front glass must face outwards towards local +Z; found " + windscreen.Length + " windshield vertices; normals=" + string.Join(",", windscreen.Select(i => normals[i]).Distinct()));
            }
            TownBakeryPlacement.SaveAsset(mesh, Root + "/Meshes/" + id + ".asset");
            var prefab = new GameObject(id, typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                prefab.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Meshes/" + id + ".asset");
                prefab.GetComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/Prefabs/" + id + ".prefab");
            }
            finally { Object.DestroyImmediate(prefab); }
            File.AppendAllText(Report + "/geometry.txt", id + ": " + indices.Count / 3 + " triangles, bounds=" + size + "; one mesh, one palette material, no generated physics collider.\n");
        }
        finally { Object.DestroyImmediate(holder); }
    }

    private static string OutsideSignature(GameObject town, Transform train)
    {
        var lines = new StringBuilder();
        foreach (var component in town.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.transform.IsChildOf(train)) continue;
            lines.Append(component.GetType().FullName).Append(EditorJsonUtility.ToJson(component)).Append('\n');
        }
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(lines.ToString())));
    }

    private static void ValidateAndCapture()
    {
        var scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var train = Object.FindObjectOfType<TownRailTraffic>();
        Require(train.cars.Length == 4 && train.serviceIntervalSeconds == 420, "Existing service changed.");
        foreach (var car in train.cars)
        {
            var filters = car.GetComponentsInChildren<MeshFilter>();
            Require(filters.Length == 1 && AssetDatabase.GetAssetPath(filters[0].sharedMesh).StartsWith(Root + "/Meshes/"), "Old model retained or new model missing.");
            Require(car.GetComponentInChildren<MeshRenderer>().sharedMaterials.Length == 1, "Excessive material count.");
        }
        Require(Quaternion.Angle(train.cars[3].GetChild(0).localRotation, Quaternion.Euler(0, 180, 0)) < .01f, "Tail cab faces the wrong direction.");
        train.enabled = false;
        train.Advance((train.layout.stationDistance - train.TravelDistance) / train.metresPerSecond + .01f);
        Require(train.IsWaiting && Mathf.Abs(train.TravelDistance - train.layout.stationDistance) < .01f, "Station stop failed.");
        RenderSettings.fog = false;
        var head = train.cars[0].position;
        TownFloristBatch.Render(scene, Pictures + "/UnityTrain.png", head + new Vector3(18, 8, 22), head + new Vector3(-11, 1.5f, 0));
        TownFloristBatch.Render(scene, Pictures + "/UnityCab.png", head + new Vector3(8, 4, 7), head + new Vector3(1, 1.5f, 0));
        TownFloristBatch.Render(scene, Pictures + "/UnityStation.png", head + new Vector3(37, 28, -54), head + new Vector3(-10, 0, -13));
        train.Advance((float)(419 - train.ServiceClock));
        Require(!train.IsInService && train.cars.All(c => !c.gameObject.activeSelf), "Train not hidden between trips.");
        train.Advance(1.01f);
        Require(train.IsInService && Math.Abs(train.NextDeparture - 840) < .01, "420-second departure failed.");
        float position = train.TravelDistance;
        train.SetCrossingHold(true); train.Advance(5);
        Require(Mathf.Abs(position - train.TravelDistance) < .001f, "Occupied crossing hold failed.");
        train.SetCrossingHold(false); train.Advance((float)(840.01 - train.ServiceClock));
        Require(train.IsInService && Math.Abs(train.NextDeparture - 1260) < .01, "840-second departure failed.");
        scene = EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        var scaled = Object.FindObjectOfType<TownRailTraffic>();
        Require(scaled != null && scaled.cars.All(c => c.GetComponentsInChildren<MeshFilter>().Length == 1), "Store-view town lost train modules.");
        File.WriteAllText(Report + "/validation.txt", "PASS: actual imported FBX units/axes; four native one-draw modules; rear-facing tail cab; station stop; hidden idle; 420/840-second repeated departures; occupied crossing hold; shared model in City and reduced-scale Game views.\n");
    }

    public static void Play()
    {
        try { Prepare("play"); EditorSceneManager.OpenScene(CityUpdateSetup.CityPath); SessionState.SetBool(Playing, true); EditorApplication.EnterPlaymode(); }
        catch (Exception e) { Fail("play", e); }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Playing, false) || !EditorApplication.isPlaying || Time.frameCount < 20) return;
        try
        {
            var train = Object.FindObjectOfType<TownRailTraffic>();
            if (firstFrame == 0)
            {
                firstFrame = Time.frameCount; firstDistance = train.TravelDistance; firstTime = Time.time;
                playDeadline = DateTime.UtcNow.AddSeconds(60); return;
            }
            Require(DateTime.UtcNow < playDeadline, "Live service stalled: enabled=" + train.enabled + ", active=" + train.gameObject.activeInHierarchy + ", timeScale=" + Time.timeScale + ", held=" + train.IsHeldAtCrossing + ", distance=" + train.TravelDistance);
            // Frame count is not elapsed time in an uncapped batch player. A station wait is valid.
            if (Time.time - firstTime < 2 || train.TravelDistance < firstDistance + 1) return;
            for (int i = 0; i < train.cars.Length; i++)
            {
                var expected = TownRailTraffic.Sample(train.layout.railway.points, train.TravelDistance - i * train.carSpacing, out var direction);
                Require(Vector3.Distance(expected, train.cars[i].localPosition) < .01f, "Live car left the rail path.");
                Require(Vector3.Angle(train.cars[i].forward, train.transform.TransformDirection(direction)) < .1f, "Live car orientation failed.");
                Require(train.cars[i].GetComponentInChildren<MeshRenderer>().sharedMaterial.shader.isSupported, "Imported model shader unsupported.");
            }
            SessionState.SetBool(Playing, false);
            Receipt("play");
            File.WriteAllText(Report + "/play.txt", "PASS: actual Play Mode updates moved four Blender-authored modules along the existing railway with correct spacing/orientation and supported URP material. No save components loaded.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { SessionState.SetBool(Playing, false); Fail("play", e); }
    }

    private static void Receipt(string stage) => File.WriteAllLines(Report + "/" + stage + "-assets.sha256",
        Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Concat(new[] { Root + ".meta", TownWorldAssembler.PrefabPath })
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => TownForgottenYardBatch.Hash(p) + " " + p.Replace('\\', '/')));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Fail(string stage, Exception e) { Directory.CreateDirectory(Report); File.WriteAllText(Report + "/" + stage + ".txt", "FAILED\n" + e); Debug.LogException(e); EditorApplication.Exit(1); }
}
