using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class FloorCollectionBatch
{
    public const string Root = "Assets/Art/FloorCollection";
    public const string Catalog = "Assets/Resources/FloorCollection";
    public const string ScenePath = Root + "/FloorCollectionShowcase.unity";
    public const string Pictures = "Docs/FloorCollection";
    private const string ManifestPath = "Tools/FloorCollection/catalog.json";
    private const float Cell = ShopProportions.CellSize;
    private const float Thickness = .035f;

    [Serializable]
    private sealed class Manifest { public Style[] styles; }

    [Serializable]
    private sealed class Style
    {
        public int number, price, columns, rows;
        public string key, title, ru, description, ruDescription, family;
        public float width, depth, smoothness, metallic;
        public string Id => $"Floor_{number:00}_{key}";
        public bool IsStripe => family == "safety";
    }

    [MenuItem("Retail Empire/Art/Build floor collection")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Generate the floor collection in Edit Mode.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var styles = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)).styles;
        Require(styles.Select(style => style.number).SequenceEqual(Enumerable.Range(1, 28).Append(30)),
            "The floor manifest must contain styles 01–28 and 30 in order.");
        foreach (string path in new[] { Catalog, Root + "/Materials", Root + "/Icons", Root + "/Prefabs", Root + "/Meshes", Pictures })
            Directory.CreateDirectory(path);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var flatCell = SaveMesh(Patch(1, 1, false), "FloorCell");
        var stripeCell = SaveMesh(Patch(1, 1, true), "FloorCellSafety");
        var swatch = SaveMesh(Patch(6, 6, false), "FloorSwatch");
        var stripeSwatch = SaveMesh(Patch(6, 6, true), "FloorSwatchSafety");
        var detail = SaveMesh(Patch(3, 3, false), "FloorDetail");
        var items = new List<BuildItemData>();
        foreach (var style in styles)
        {
            var material = MakeMaterial(style);
            var model = Surface(style.Id, style.IsStripe ? stripeCell : flatCell, material);
            string path = Root + "/Prefabs/" + style.Id + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(model, path);
            Object.DestroyImmediate(model);
            Require(prefab != null, "Could not save " + path);
            bool close = style.family == "hex" || style.family == "diamond" || style.family == "rubber" ||
                style.family == "endgrain" || style.family == "terrazzo";
            var icon = ShopUiPreviews.Render(prefab, style.Id, 16, outputFolder: Root + "/Icons",
                prepare: preview => preview.GetComponent<MeshFilter>().sharedMesh = style.IsStripe ? stripeSwatch : close ? detail : swatch);
            items.Add(CatalogItem(style, material, prefab, icon));
        }
        AssetDatabase.SaveAssets();
        CreateShowcase(styles, items, swatch, stripeSwatch, detail);
        Debug.Log("Floor Collection: 29 catalog entries, prefabs, materials and rendered icons.");
    }

    private static Texture2D ImportTexture(Style style, bool normal)
    {
        string path = $"{Root}/Textures/{style.Id}_{(normal ? "Normal" : "Albedo")}.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Require(importer != null, "Generate the texture first: " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.convertToNormalmap = false;
        importer.sRGBTexture = !normal;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.wrapMode = style.IsStripe ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material MakeMaterial(Style style)
    {
        string path = Root + "/Materials/" + style.Id + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        var repeat = style.IsStripe ? Vector2.one :
            new Vector2(4 * Cell / (style.width * style.columns), 4 * Cell / (style.depth * style.rows));
        material.enableInstancing = true;
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", ImportTexture(style, false));
        material.SetTextureScale("_BaseMap", repeat);
        material.SetTexture("_BumpMap", ImportTexture(style, true));
        material.SetTextureScale("_BumpMap", repeat);
        material.SetFloat("_BumpScale", 1);
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_Metallic", style.metallic);
        material.SetFloat("_Smoothness", style.smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static BuildItemData CatalogItem(Style style, Material material, GameObject prefab, Sprite icon)
    {
        string path = $"{Catalog}/{style.Id}.asset";
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<BuildItemData>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.id = style.Id;
        item.displayName = style.title;
        item.russianName = style.ru;
        item.description = style.description;
        item.russianDescription = style.ruDescription;
        item.price = style.price;
        item.weightKg = 2;
        item.cargoVolumeM3 = .01f;
        item.unlockedByDefault = true;
        item.placementKind = PlacementKind.Floor;
        item.category = BuildCategory.Structures;
        item.footprint = Vector2Int.one;
        item.allowRotation = true;
        item.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap;
        item.floorMaterial = material;
        item.usesMetricFloorPattern = true;
        item.hasFloorEdgeStripe = style.IsStripe;
        item.floorFinishGroup = style.number switch
        {
            <= 5 or 8 => FloorFinishGroup.Tile,
            6 or 7 or 9 or 10 or 16 or 17 => FloorFinishGroup.Stone,
            >= 11 and <= 15 => FloorFinishGroup.Wood,
            >= 18 and <= 20 => FloorFinishGroup.Industrial,
            >= 21 and <= 25 => FloorFinishGroup.Resilient,
            _ => FloorFinishGroup.Outdoor
        };
        item.prefab = prefab;
        item.icon = icon;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static Mesh Patch(int width, int depth, bool stripe)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        var offsets = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        var center = new Vector2(width, depth) * .5f;
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
        {
            int first = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            uvs.AddRange(new[] { uv1, uv2, uv3, uv4 });
            indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }

        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            var cell = new Vector2Int(x, z);
            var points = new Vector3[4];
            var tileUVs = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                var point = (Vector2)cell + offsets[i];
                points[i] = new Vector3((point.x - center.x) * Cell, Thickness, (point.y - center.y) * Cell);
                tileUVs[i] = stripe ? FloorPattern.StripeUV(cell, offsets[i], 0, z == depth - 1) : point * .25f;
            }
            Quad(points[0], points[1], points[2], points[3], tileUVs[0], tileUVs[1], tileUVs[2], tileUVs[3]);
        }

        var corners = offsets.Select(offset => new Vector3((offset.x * width - center.x) * Cell, Thickness,
            (offset.y * depth - center.y) * Cell)).ToArray();
        for (int i = 0; i < 4; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 4];
            var sample = stripe ? FloorPattern.StripeUV(Vector2Int.one, Vector2.one * .5f, 0, false) : new Vector2(.125f, .125f);
            Quad(a, a - Vector3.up * Thickness, b - Vector3.up * Thickness, b,
                sample, sample + new Vector2(0, .001f), sample + Vector2.one * .001f, sample + new Vector2(.001f, 0));
        }
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{Root}/Meshes/{name}.asset";
        mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    private static GameObject Surface(string name, Mesh mesh, Material material)
    {
        var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        root.GetComponent<MeshRenderer>().sharedMaterial = material;
        root.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
        return root;
    }

    private static void CreateShowcase(Style[] styles, List<BuildItemData> items, Mesh swatch, Mesh stripe, Mesh detail)
    {
        var previous = EditorSceneManager.GetActiveScene();
        bool additive = previous.IsValid() && previous.isLoaded && !string.IsNullOrEmpty(previous.path);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
        EditorSceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.63f, .59f, .53f);
            AddLight("Warm key", new Vector3(50, -35, 0), .96f, true);
            AddLight("Fill", new Vector3(40, 150, 0), .38f, false);
            var collection = new GameObject("Floor collection samples");
            for (int i = 0; i < items.Count; i++)
            {
                var style = styles[i];
                var sample = Surface(items[i].id, style.IsStripe ? stripe : swatch, items[i].floorMaterial);
                sample.transform.SetParent(collection.transform);
                sample.transform.position = new Vector3((i % 5 - 2) * 2.13f, 0, (2.5f - i / 5) * 2.05f);
                Label(sample.transform, $"{style.number:00}  {style.title}", new Vector3(0, .035f, -1.02f), .018f);
            }
            Label(collection.transform, "RETAIL EMPIRE TYCOON  /  FLOOR COLLECTION", new Vector3(0, .035f, 6.35f), .034f);
            var details = new GameObject("Floor details").transform;
            details.position = new Vector3(15, 0, 0);
            var detailNumbers = new[] { 5, 7, 14, 20, 21, 27 };
            for (int i = 0; i < detailNumbers.Length; i++)
            {
                var item = items[detailNumbers[i] - 1];
                var sample = Surface(item.id, detail, item.floorMaterial);
                sample.transform.SetParent(details, false);
                sample.transform.localPosition = new Vector3((i % 3 - 1) * 1.15f, 0, (.5f - i / 3) * 1.12f);
                Label(sample.transform, item.displayName, new Vector3(0, .035f, -.52f), .012f);
            }
            var junctions = AddJunctions(items, swatch, stripe);
            var camera = new GameObject("Collection camera", typeof(Camera)).GetComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.94f, .90f, .82f);
            camera.farClipPlane = 80;
            Pose(camera, new Vector3(0, 0, .24f), 6.32f, new Vector3(0, 16, -4.8f));
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save floor showcase.");
            Capture(camera, "FloorCollection", 1550, 1770);
            Pose(camera, details.position, 1.24f, new Vector3(0, 12, -5));
            Capture(camera, "FloorDetails", 1500, 900);
            Pose(camera, junctions.position + new Vector3(0, .27f, -.55f), 2.87f, new Vector3(3, 10, -6));
            Capture(camera, "FloorJunctions", 1536, 940);
        }
        finally
        {
            if (additive)
            {
                EditorSceneManager.CloseScene(scene, true);
                EditorSceneManager.SetActiveScene(previous);
            }
        }
    }

    private static Transform AddJunctions(List<BuildItemData> items, Mesh swatch, Mesh stripe)
    {
        var root = new GameObject("Seams — wall, entrance, garage and paving").transform;
        root.position = new Vector3(25, 0, 0);
        float size = 6 * Cell;
        var samples = new[] { 1, 14, 19, 28, 26, 30 };
        for (int i = 0; i < samples.Length; i++)
        {
            var item = items.Single(floor => floor.id.StartsWith($"Floor_{samples[i]:00}_", StringComparison.Ordinal));
            var sample = Surface(item.id, item.hasFloorEdgeStripe ? stripe : swatch, item.floorMaterial);
            sample.transform.SetParent(root, false);
            sample.transform.localPosition = new Vector3((i % 3 - 1) * size, 0, (.5f - i / 3) * size);
        }
        var architecture = new[]
        {
            "Assets/Resources/WallCollection/wall_finish_01.asset",
            "Assets/Prefabs/Structures/Door.asset",
            "Assets/Prefabs/City/unloading_gate_01.asset"
        };
        for (int i = 0; i < architecture.Length; i++)
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(architecture[i]);
            if (item?.prefab == null) continue;
            var fixture = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab, root);
            fixture.transform.localPosition = new Vector3((i - 1) * size, Thickness, 0);
        }
        var road = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/City/Asphalt.mat");
        var sidewalk = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/City/Sidewalk.mat");
        var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/City/Grass.mat");
        ContextBlock(root, "Road", new Vector3(0, Thickness * .5f, -size - .90f), new Vector3(3 * size, Thickness, .90f), road);
        ContextBlock(root, "Driveway", new Vector3(0, Thickness * .5f, -size - .225f), new Vector3(size, Thickness, .45f), road);
        foreach (int side in new[] { -1, 1 })
        {
            ContextBlock(root, "Sidewalk curb", new Vector3(side * size, .035f, -size - .225f), new Vector3(size, .07f, .45f), sidewalk);
            ContextBlock(root, "Grass verge", new Vector3(side * size * 1.65f, 0, -size * .34f), new Vector3(size * .3f, .035f, size * 1.35f), grass);
        }
        return root;
    }

    private static void ContextBlock(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        Require(material != null, "Missing town material for " + name);
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = center;
        block.transform.localScale = size;
        block.GetComponent<MeshRenderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(block, StaticEditorFlags.BatchingStatic);
    }

    private static void Label(Transform parent, string text, Vector3 position, float size)
    {
        var label = new GameObject(text, typeof(TextMesh)).GetComponent<TextMesh>();
        label.transform.SetParent(parent, false);
        label.transform.localPosition = position;
        label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 50;
        label.characterSize = size;
        label.color = new Color(.12f, .23f, .17f);
    }

    private static void AddLight(string name, Vector3 rotation, float intensity, bool shadows)
    {
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.transform.rotation = Quaternion.Euler(rotation);
    }

    private static void Pose(Camera camera, Vector3 target, float size, Vector3 offset)
    {
        camera.transform.position = target + offset;
        camera.transform.LookAt(target);
        camera.orthographicSize = size;
    }

    private static void Capture(Camera camera, string filename, int width, int height)
    {
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes($"{Pictures}/{filename}.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
        }
    }

    internal static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
