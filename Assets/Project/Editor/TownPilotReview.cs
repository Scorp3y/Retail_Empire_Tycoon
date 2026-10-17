using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Captures the isolated City scene without saving it. Before is an immutable visual baseline;
/// After must use the same camera manifest. This is composition evidence, not a runtime FPS test.
/// </summary>
public static class TownPilotReview
{
    private const string ReviewRoot = "Library/TownPilotReview";
    private const string ManifestName = "camera-manifest.json";
    private const string CompletionName = "complete.txt";
    private const float GridSize = .5f;

    public static void CaptureBefore() => Run("Before");
    public static void CaptureVariantA() => Run("VariantA");
    public static void CaptureAfter() => Run("After");
    public static void CaptureArchitectureBefore() => Run("Before", "Library/TownArchitectureQA");
    public static void CaptureArchitectureAfter() => Run("After", "Library/TownArchitectureQA");
    public static void CaptureResidentialBefore() => Run("Before", "Library/TownResidentialQA");
    public static void CaptureResidentialAfter() => Run("After", "Library/TownResidentialQA");
    public static void CaptureCourtyardsBefore() => Run("Before", "Library/TownCourtyardQA");
    public static void CaptureCourtyardsAfter() => Run("After", "Library/TownCourtyardQA");
    public static void CaptureSpecialBefore() => Run("Before", "Library/TownSpecialQA");
    public static void CaptureSpecialAfter() => Run("After", "Library/TownSpecialQA");
    public static void CaptureSeamBefore() => Run("Before", "Library/TownSeamQA");
    public static void CaptureSeamAfter() => Run("After", "Library/TownSeamQA");

    private static void Run(string phase, string reviewRoot = ReviewRoot)
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Require(!EditorApplication.isPlaying, "Capture the saved authoring scene outside Play Mode.");
            string directory = Path.Combine(reviewRoot, phase);
            string baselineDirectory = Path.Combine(reviewRoot, "Before");
            bool courtyard = reviewRoot == "Library/TownCourtyardQA";
            bool special = reviewRoot == "Library/TownSpecialQA";
            bool seams = reviewRoot == "Library/TownSeamQA";
            bool residential=reviewRoot=="Library/TownResidentialQA" || courtyard || special;
            var definitions = seams ? SeamCameraDefinitions() : special ? SpecialCameraDefinitions() : courtyard ? CourtyardCameraDefinitions() : residential ? ResidentialCameraDefinitions() : CameraDefinitions();
            string manifest = JsonUtility.ToJson(new CameraManifest { cameras = definitions }, true);
            if (phase != "Before")
            {
                Require(File.Exists(Path.Combine(baselineDirectory, CompletionName)), "A successful Before capture is required.");
                Require(File.ReadAllText(Path.Combine(baselineDirectory, ManifestName)) == manifest,
                    "Camera definitions changed. Baseline and variant images would not be comparable.");
            }
            else
            {
                Require(!File.Exists(Path.Combine(directory, CompletionName)),
                    "Before baseline already exists. Archive that review directory explicitly before a new baseline.");
            }

            Directory.CreateDirectory(directory);
            // A failed retry must not retain the success marker of an earlier variant run.
            string completion = Path.Combine(directory, CompletionName);
            if (File.Exists(completion)) File.Delete(completion);
            File.WriteAllText(Path.Combine(directory, ManifestName), manifest);
            var scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath, OpenSceneMode.Single);
            var town = SceneComponents<TownWorldPlaces>(scene).Single();
            var report = Measure(scene, town.transform);
            if(residential)
            {
                var buildings=town.GetComponentsInChildren<BoxCollider>(true)
                    .Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&IsBuildingBody(c,town.transform))
                    .Select(c=>BodyFootprint.From(c,town.transform)).ToArray();
                report.plots=report.plots.Concat(TownResidentialPlan.Create().Blocks
                    .Select(block=>MeasurePlot(block.Id,block.Bounds,buildings))).ToArray();
            }
            report.phase = phase;
            report.scenePath = scene.path;
            report.capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            report.cameras = definitions.Select(definition => Capture(scene, definition, directory)).ToArray();
            File.WriteAllText(Path.Combine(directory, "metrics.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(directory, "review.txt"), Describe(report));
            // Bind the pictures to their saved scene/model inputs so later Apply cannot use stale evidence.
            var sourcePaths=AssetDatabase.GetDependencies(new[]{scene.path,TownWorldAssembler.PrefabPath},true)
                .Where(File.Exists).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            var hashes=new SourceHashes{files=sourcePaths.Select(HashSource).ToArray()};
            File.WriteAllText(Path.Combine(directory,"source-hashes.json"),JsonUtility.ToJson(hashes,true));
            Require(report.activeAmbientWalkers == 0 && report.activeBackgroundSpawners == 0,
                "Decorative city people are active. See the captured audit before continuing.");
            File.WriteAllText(completion, "PASS: fixed-camera scene capture and composition measurements.\nNo scene or asset was saved.\n");
            Debug.Log("Town pilot " + report.phase + " review captured: " + directory);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static SourceHash HashSource(string path)
    {
        using(var file=File.OpenRead(path))
        using(var hash=SHA256.Create())
            return new SourceHash{path=path,sha256=BitConverter.ToString(hash.ComputeHash(file)).Replace("-","")};
    }

    [Serializable] private sealed class SourceHashes { public SourceHash[] files; }
    [Serializable] private sealed class SourceHash { public string path; public string sha256; }

    private static CameraDefinition[] CameraDefinitions()
    {
        return new[]
        {
            new CameraDefinition("PilotTop", new Vector3(-168, 95, -168), new Vector3(-168, 0, -168), 29, true),
            new CameraDefinition("PilotStreet", new Vector3(-168, 8, -204), new Vector3(-168, 4, -164)),
            new CameraDefinition("PilotVehicleHeight", new Vector3(-208, 1.65f, -193), new Vector3(-153, 2, -171)),
            new CameraDefinition("TownhouseContext", new Vector3(-145, 52, -221), new Vector3(-168, 0, -168)),
            new CameraDefinition("WorkshopContext", new Vector3(147, 49, -219), new Vector3(120, 1, -168)),
            new CameraDefinition("RiversideContext", new Vector3(292, 90, -118), new Vector3(219, 3, -18)),
            new CameraDefinition("SmallOfficeContext", new Vector3(284, 47, 118), new Vector3(219, 2, 168)),
            new CameraDefinition("CityOverview", new Vector3(580, 930, -770), new Vector3(175, 0, 135), 470, true)
        };
    }

    private static CameraDefinition[] ResidentialCameraDefinitions()
    {
        var cameras=CameraDefinitions().ToList();
        foreach(var block in TownResidentialPlan.Create().Blocks)
        {
            // The immutable baseline already covers this parcel with SmallOfficeContext.
            if (block.Id == "OfficeNeighbour") continue;
            var focus=new Vector3(block.Bounds.center.x,0,block.Bounds.center.y);
            cameras.Add(new CameraDefinition(block.Id+"Top",focus+Vector3.up*130,focus,
                Mathf.Max(block.Bounds.height/2+6,block.Bounds.width*.35f),true));
        }
        cameras.Add(new CameraDefinition("NorthCourts",new Vector3(155,110,-25),new Vector3(20,8,95),0,true));
        cameras.Add(new CameraDefinition("WestStreet",new Vector3(-223,3,-112),new Vector3(-166,6,-93),0,true));
        cameras.Add(new CameraDefinition("RiverStreet",new Vector3(261,3,-77),new Vector3(215,6,7),0,true));
        return cameras.ToArray();
    }

    private static CameraDefinition[] CourtyardCameraDefinitions()
    {
        var cameras = ResidentialCameraDefinitions().ToList();
        cameras.Add(new CameraDefinition("GardenHeight", new Vector3(34, 3, 103), new Vector3(14, 2, 97), 0, true));
        cameras.Add(new CameraDefinition("WestCourtContext", new Vector3(-126, 44, -47), new Vector3(-166, 0, 4), 0, true));
        cameras.Add(new CameraDefinition("RiverCourtContext", new Vector3(251, 48, -107), new Vector3(219, 0, -11), 0, true));
        cameras.Add(new CameraDefinition("OfficeNeighbourTop", new Vector3(219, 130, 168), new Vector3(219, 0, 168), 29, true));
        return cameras.ToArray();
    }

    private static CaptureCounters Capture(Scene scene, CameraDefinition definition, string directory)
    {
        var root = new GameObject("Temporary pilot review camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(root, scene);
        var camera = root.GetComponent<Camera>();
        camera.enabled = false;
        camera.transform.SetPositionAndRotation(definition.position, definition.rotation);
        camera.nearClipPlane = definition.nearClip;
        camera.farClipPlane = definition.farClip;
        camera.fieldOfView = definition.fieldOfView;
        camera.orthographic = definition.orthographicSize > 0;
        camera.orthographicSize = Mathf.Max(1, definition.orthographicSize);
        camera.aspect = (float)definition.width / definition.height;
        camera.clearFlags = definition.disableFog ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
        camera.backgroundColor = new Color(.7f, .82f, .9f);
        var additional = camera.GetUniversalAdditionalCameraData();
        additional.renderPostProcessing = true;
        additional.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var texture = new RenderTexture(definition.width, definition.height, 24);
        var image = new Texture2D(definition.width, definition.height, TextureFormat.RGB24, false);
        var previousTexture = RenderTexture.active;
        bool previousFog = RenderSettings.fog;
        try
        {
            if (definition.disableFog) RenderSettings.fog = false;
            Require(texture.Create(), "Cannot create the review render texture.");
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, definition.width, definition.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(directory, definition.name + ".png"), image.EncodeToPNG());
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return new CaptureCounters
            {
                camera = definition.name,
                rendererBoundsInFrustum = SceneComponents<Renderer>(scene).Count(renderer => renderer.enabled
                    && renderer.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)),
                editorDrawCalls = UnityStats.drawCalls,
                editorBatches = UnityStats.batches,
                editorSetPassCalls = UnityStats.setPassCalls
            };
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previousTexture;
            RenderSettings.fog = previousFog;
            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(root);
        }
    }

    private static CameraDefinition[] SeamCameraDefinitions()
    {
        var cameras=CameraDefinitions().ToList();
        cameras.Add(new CameraDefinition("SlopedRoof",new Vector3(-58,19,-12),new Vector3(-50,14,-3),0,true));
        cameras.Add(new CameraDefinition("AerialSeat",new Vector3(-25,17,-9),new Vector3(-29.8f,14,-3),0,true));
        cameras.Add(new CameraDefinition("OfficeRoof",new Vector3(154,33,-129),new Vector3(143,28,-120),0,true));
        cameras.Add(new CameraDefinition("LowerOfficeRoof",new Vector3(155,20,-20),new Vector3(144,16,-10),0,true));
        cameras.Add(new CameraDefinition("OldEntrance",new Vector3(-56,3.1f,9),new Vector3(-52,1.8f,3),0,true));
        cameras.Add(new CameraDefinition("Downpipe",new Vector3(-61,4,-14),new Vector3(-56.7f,4,-8.16f),0,true));
        cameras.Add(new CameraDefinition("ShelterJoin",new Vector3(-87,2.2f,-144),new Vector3(-80,.7f,-152),0,true));
        cameras.Add(new CameraDefinition("ApprovedStopJoin",new Vector3(99,2.2f,-144),new Vector3(90,.7f,-152),0,true));
        return cameras.ToArray();
    }

    private static CameraDefinition[] SpecialCameraDefinitions()
    {
        var cameras=CameraDefinitions().ToList();
        cameras.Add(new CameraDefinition("WorkshopTop",new Vector3(120,130,-168),new Vector3(120,0,-168),30,true));
        cameras.Add(new CameraDefinition("ConstructionTop",new Vector3(146,130,-58),new Vector3(146,0,-58),36,true));
        cameras.Add(new CameraDefinition("WorkshopVehicleHeight",new Vector3(144,2.6f,-197),new Vector3(112,2.4f,-163),0,true));
        cameras.Add(new CameraDefinition("ConstructionVehicleHeight",new Vector3(118,2.5f,-60),new Vector3(144,4,-42),0,true));
        cameras.Add(new CameraDefinition("ConstructionContext",new Vector3(201,51,-111),new Vector3(145,4,-55),0,true));
        return cameras.ToArray();
    }

    private static ReviewReport Measure(Scene scene, Transform town)
    {
        var renderers = SceneComponents<Renderer>(scene);
        var walkers = SceneComponents<TownAmbientWalker>(scene);
        var spawners = SceneComponents<AvatarSpawn>(scene);
        var meshes = SceneComponents<MeshFilter>(scene).Where(filter => filter.sharedMesh != null).ToArray();
        var buildings = town.GetComponentsInChildren<BoxCollider>(true)
            .Where(collider => collider.enabled && collider.gameObject.activeInHierarchy && IsBuildingBody(collider, town))
            .Select(collider => BodyFootprint.From(collider, town)).ToArray();
        return new ReviewReport
        {
            gridMetres = GridSize,
            allSceneRenderers = renderers.Length,
            activeSceneRenderers = renderers.Count(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy),
            allTownRenderers = town.GetComponentsInChildren<Renderer>(true).Length,
            allSceneTriangles = meshes.Sum(filter => TriangleCount(filter.sharedMesh)),
            activeSceneTriangles = meshes.Where(filter => filter.gameObject.activeInHierarchy
                && filter.TryGetComponent<Renderer>(out var renderer) && renderer.enabled).Sum(filter => TriangleCount(filter.sharedMesh)),
            allAmbientWalkers = walkers.Length,
            activeAmbientWalkers = walkers.Count(walker => walker.isActiveAndEnabled),
            activeBackgroundSpawners = spawners.Count(spawner => spawner.isActiveAndEnabled),
            backgroundSpawners = spawners.Select(spawner => PathInScene(spawner.transform) + "; enabled=" + spawner.isActiveAndEnabled).ToArray(),
            debugRenderers = SceneComponents<LineRenderer>(scene).Select(renderer => DebugRenderer(renderer))
                .Concat(SceneComponents<TrailRenderer>(scene).Select(renderer => DebugRenderer(renderer))).ToArray(),
            plots = new[]
            {
                MeasurePlot("PilotTownhouses", new Rect(-210, -186, 84, 36), buildings),
                MeasurePlot("Workshop", new Rect(78, -186, 84, 36), buildings),
                MeasurePlot("RiversideBlock", new Rect(180, -78, 78, 120), buildings),
                MeasurePlot("SmallOffice", new Rect(180, 150, 78, 36), buildings)
            }
        };
    }

    private static bool IsBuildingBody(BoxCollider collider, Transform town)
    {
        // Visual chunk merging preserves these physics roots, but removes source renderers.
        // Restricting the sections avoids counting bins, canopies, fences and cars as houses.
        Transform section = collider.transform;
        while (section.parent != null && section.parent != town) section = section.parent;
        bool buildingSection = section.name == "Mixed low-rise streets" || section.name == "Urban buildings"
            || section.name==TownResidentialPlan.HierarchyRoot || section.name=="Special sites stage 5";
        return buildingSection && collider.size.x >= 3 && collider.size.z >= 3 && collider.size.y >= 2.5f;
    }

    private static PlotMetrics MeasurePlot(string name, Rect plot, BodyFootprint[] buildings)
    {
        var bodies = buildings.Where(building => building.area.Overlaps(plot)).ToArray();
        var clipped = bodies.Select(body => Intersection(plot, body.area)).ToArray();
        float builtArea = UnionArea(clipped);
        var unbuilt = LargestUnbuiltRectangle(plot, clipped);
        return new PlotMetrics
        {
            name = name,
            bounds = plot,
            areaSquareMetres = plot.width * plot.height,
            buildingBodyCount = bodies.Length,
            authoredBodyNames = bodies.Select(body => body.name).Distinct().OrderBy(bodyName => bodyName, StringComparer.Ordinal).ToArray(),
            distinctBodyHeightsMetres = bodies.Select(body => (float)Math.Round(body.height, 2)).Distinct().OrderBy(height => height).ToArray(),
            buildingFootprintSquareMetres = builtArea,
            buildingCoveragePercent = builtArea * 100 / (plot.width * plot.height),
            largestUnbuiltRectangle = unbuilt,
            largestUnbuiltSquareMetres = unbuilt.width * unbuilt.height,
            bodies = bodies
        };
    }

    private static float UnionArea(Rect[] areas)
    {
        var xs = areas.SelectMany(area => new[] { area.xMin, area.xMax }).Distinct().OrderBy(x => x).ToArray();
        float result = 0;
        for (int i = 0; i < xs.Length - 1; i++)
        {
            float middle = (xs[i] + xs[i + 1]) / 2;
            var intervals = areas.Where(area => area.xMin <= middle && area.xMax >= middle).OrderBy(area => area.yMin).ToArray();
            if (intervals.Length == 0) continue;
            float start = intervals[0].yMin, end = intervals[0].yMax, total = 0;
            foreach (var interval in intervals.Skip(1))
            {
                if (interval.yMin > end)
                {
                    total += end - start;
                    start = interval.yMin;
                }
                end = Mathf.Max(end, interval.yMax);
            }
            result += (xs[i + 1] - xs[i]) * (total + end - start);
        }
        return result;
    }

    private static Rect LargestUnbuiltRectangle(Rect plot, Rect[] buildings)
    {
        int columns = Mathf.FloorToInt(plot.width / GridSize), rows = Mathf.FloorToInt(plot.height / GridSize);
        var heights = new int[columns];
        var stack = new int[columns + 1];
        Rect largest = new Rect(plot.xMin, plot.yMin, 0, 0);
        int bestCells = 0;
        // Each histogram row finds its largest rectangle in linear time. Occupied cells are
        // conservative: touching any body footprint excludes the entire half-metre cell.
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var cell = new Rect(plot.xMin + column * GridSize, plot.yMin + row * GridSize, GridSize, GridSize);
                heights[column] = buildings.Any(building => building.Overlaps(cell)) ? 0 : heights[column] + 1;
            }
            int stackCount = 0;
            for (int column = 0; column <= columns; column++)
            {
                int currentHeight = column == columns ? 0 : heights[column];
                while (stackCount > 0 && heights[stack[stackCount - 1]] > currentHeight)
                {
                    int height = heights[stack[--stackCount]];
                    int left = stackCount == 0 ? 0 : stack[stackCount - 1] + 1;
                    int cells = height * (column - left);
                    if (cells <= bestCells) continue;
                    bestCells = cells;
                    largest = new Rect(plot.xMin + left * GridSize, plot.yMin + (row + 1 - height) * GridSize,
                        (column - left) * GridSize, height * GridSize);
                }
                if (column < columns) stack[stackCount++] = column;
            }
        }
        return largest;
    }

    private static Rect Intersection(Rect first, Rect second) => Rect.MinMaxRect(
        Mathf.Max(first.xMin, second.xMin), Mathf.Max(first.yMin, second.yMin),
        Mathf.Min(first.xMax, second.xMax), Mathf.Min(first.yMax, second.yMax));

    private static long TriangleCount(Mesh mesh)
    {
        long count = 0;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            if (mesh.GetTopology(submesh) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(submesh) / 3;
        return count;
    }

    private static T[] SceneComponents<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static string DebugRenderer(Renderer renderer) => renderer.GetType().Name + ": " + PathInScene(renderer.transform)
        + "; active=" + (renderer.enabled && renderer.gameObject.activeInHierarchy) + "; bounds=" + renderer.bounds;

    private static string PathInScene(Transform transform)
    {
        var names = new List<string>();
        for (var node = transform; node != null; node = node.parent) names.Add(node.name);
        names.Reverse();
        return string.Join("/", names);
    }

    private static string Describe(ReviewReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("PILOT COMPOSITION REVIEW — " + report.phase);
        text.AppendLine("Scene: " + report.scenePath + "; captured " + report.capturedUtc);
        text.AppendLine("Fixed 1280 x 800 Camera.Render images; no SceneView selections or gizmos; no scene/asset save.");
        foreach (var plot in report.plots)
        {
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} bodies; {2:F2} / {3:F2} m² = {4:F2}% building coverage; body names {5}; heights [{6}] m; largest UNBUILT rectangle {7:F1} x {8:F1} m = {9:F2} m².",
                plot.name, plot.buildingBodyCount, plot.buildingFootprintSquareMetres, plot.areaSquareMetres,
                plot.buildingCoveragePercent, string.Join(", ", plot.authoredBodyNames),
                string.Join(", ", plot.distinctBodyHeightsMetres.Select(height => height.ToString("F2", CultureInfo.InvariantCulture))),
                plot.largestUnbuiltRectangle.width, plot.largestUnbuiltRectangle.height, plot.largestUnbuiltSquareMetres));
        }
        text.AppendLine("Coverage is the exact union of clipped body-collider axis-aligned bounds in native town metres, not roof/canopy coverage.");
        text.AppendLine("Non-right-angle bodies would inflate these bounds. Height means collision-body height, not roof/apex height.");
        text.AppendLine("Authored body names identify model families, not proven distinct visual variants after chunk merging.");
        text.AppendLine("Largest UNBUILT rectangle uses a conservative 0.5 m grid. It may contain lawn, paths, parking or furniture; it is NOT an object-free or purposeless area.");
        text.AppendLine("The 12 x 12 m undecorated-paving criterion requires visual review; this measurement does not claim to validate it.");
        text.AppendLine($"Geometry including inactive future plots: {report.allSceneRenderers} scene renderers, {report.allTownRenderers} town renderers, {report.allSceneTriangles} scene triangles.");
        text.AppendLine($"Active geometry: {report.activeSceneRenderers} scene renderers, {report.activeSceneTriangles} triangles.");
        text.AppendLine($"Decorative people: {report.activeAmbientWalkers} active / {report.allAmbientWalkers} saved walkers; {report.activeBackgroundSpawners} active AvatarSpawn components.");
        text.AppendLine("Line/Trail renderer audit:");
        foreach (string renderer in report.debugRenderers) text.AppendLine(renderer);
        if (report.debugRenderers.Length == 0) text.AppendLine("No saved LineRenderer or TrailRenderer components in City.");
        text.AppendLine("Debug.Draw code is not inferred from this component inventory. SceneView-only lines are excluded from the images, not removed from source.");
        text.AppendLine("PERFORMANCE LIMIT: UnityStats below are editor-frame counters read after Camera.Render, informational only. They are not isolated runtime draw calls or FPS.");
        foreach (var capture in report.cameras)
            text.AppendLine($"{capture.camera}: {capture.rendererBoundsInFrustum} enabled renderer bounds in frustum; editor draw calls {capture.editorDrawCalls}, batches {capture.editorBatches}, SetPass {capture.editorSetPassCalls}.");
        return text.ToString();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class CameraManifest
    {
        public int schemaVersion = 1;
        public CameraDefinition[] cameras;
    }

    [Serializable]
    private sealed class CameraDefinition
    {
        public string name;
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView = 65;
        public float nearClip = .1f;
        public float farClip = 1600;
        public float orthographicSize;
        public bool disableFog;
        public int width = 1280;
        public int height = 800;

        public CameraDefinition(string name, Vector3 position, Vector3 focus, float orthographicSize = 0, bool disableFog = false)
        {
            this.name = name;
            this.position = position;
            this.orthographicSize = orthographicSize;
            this.disableFog = disableFog;
            var forward = (focus - position).normalized;
            rotation = Quaternion.LookRotation(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .99f ? Vector3.forward : Vector3.up);
        }
    }

    [Serializable]
    private sealed class BodyFootprint
    {
        public string name;
        public string hierarchy;
        public Rect area;
        public float height;

        public static BodyFootprint From(BoxCollider collider, Transform town)
        {
            var minimum = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var maximum = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = collider.center + Vector3.Scale(collider.size / 2, new Vector3(x, y, z));
                        var native = town.InverseTransformPoint(collider.transform.TransformPoint(corner));
                        minimum = Vector3.Min(minimum, native);
                        maximum = Vector3.Max(maximum, native);
                    }
            return new BodyFootprint
            {
                name = collider.gameObject.name,
                hierarchy = PathInScene(collider.transform),
                area = Rect.MinMaxRect(minimum.x, minimum.z, maximum.x, maximum.z),
                height = maximum.y - minimum.y
            };
        }
    }

    [Serializable]
    private sealed class PlotMetrics
    {
        public string name;
        public Rect bounds;
        public float areaSquareMetres;
        public int buildingBodyCount;
        public string[] authoredBodyNames;
        public float[] distinctBodyHeightsMetres;
        public float buildingFootprintSquareMetres;
        public float buildingCoveragePercent;
        public Rect largestUnbuiltRectangle;
        public float largestUnbuiltSquareMetres;
        public BodyFootprint[] bodies;
    }

    [Serializable]
    private sealed class CaptureCounters
    {
        public string camera;
        public int rendererBoundsInFrustum;
        public int editorDrawCalls;
        public int editorBatches;
        public int editorSetPassCalls;
    }

    [Serializable]
    private sealed class ReviewReport
    {
        public string phase;
        public string scenePath;
        public string capturedUtc;
        public float gridMetres;
        public int allSceneRenderers;
        public int activeSceneRenderers;
        public int allTownRenderers;
        public long allSceneTriangles;
        public long activeSceneTriangles;
        public int allAmbientWalkers;
        public int activeAmbientWalkers;
        public int activeBackgroundSpawners;
        public string[] backgroundSpawners;
        public string[] debugRenderers;
        public PlotMetrics[] plots;
        public CaptureCounters[] cameras;
    }
}
