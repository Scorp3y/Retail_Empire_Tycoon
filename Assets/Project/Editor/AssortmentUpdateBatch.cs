using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports only the approved additional goods; never rebuilds the player's scenes or starter stock.</summary>
public static class AssortmentUpdateBatch
{
    public const string Art = "Assets/Art/Assortment";
    public const string Data = "Assets/Resources/StoreAssortment";
    public const string Report = "Library/AssortmentQA";
    [Serializable] public sealed class Definitions { public ProductDefinition[] products; public FixtureDefinition[] fixtures; }
    [Serializable] public sealed class ProductDefinition
    {
        public string key, name, storage, shape, color;
        public int cost, sale, index;
        public float weight, volume;
    }
    [Serializable] public sealed class FixtureDefinition
    {
        public string key, name, shelfType, description;
        public string[] accepts;
        public int price, footprintX, footprintZ, capacity, columns, rows, tiers;
        public float weight, volume, width, depth, height, slotY, slotZ, spacingZ, slotWidth, slotDepth, slotHeight;
    }

    public static void BuildAndPlay()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Report);
        try
        {
            foreach (var folder in new[] { Art + "/Products", Art + "/Fixtures", Data + "/Products", Data + "/Fixtures" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var definitions = JsonUtility.FromJson<Definitions>(File.ReadAllText(Art + "/Definitions.json"));
            Require(definitions.products.Length == 48 && definitions.fixtures.Length == 4, "Expected the approved 48 + 4 designs.");
            var atlas = (TextureImporter)AssetImporter.GetAtPath(Art + "/AssortmentAtlas.png");
            atlas.mipmapEnabled = true; atlas.maxTextureSize = 2048;
            atlas.textureCompression = TextureImporterCompression.Uncompressed; atlas.filterMode = FilterMode.Bilinear;
            atlas.wrapMode = TextureWrapMode.Clamp; atlas.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Assortment.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Art + "/Assortment.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/AssortmentAtlas.png"));
            material.SetFloat("_Smoothness", .1f); EditorUtility.SetDirty(material);
            var goods = definitions.products.Select(p => MakeProduct(p, material)).ToArray();
            var fixtures = definitions.fixtures.Select(f => MakeFixture(f, material)).ToArray();
            var pack = AssetDatabase.LoadAssetAtPath<StoreAssortmentPack>(Data + "/Expansion.asset");
            if (pack == null) { pack = ScriptableObject.CreateInstance<StoreAssortmentPack>(); AssetDatabase.CreateAsset(pack, Data + "/Expansion.asset"); }
            // Rebuilding this wave must retain later additions and their stable catalogue order.
            var mergedGoods = pack.Products.Concat(goods).Where(p => p != null).GroupBy(p => p.Id).Select(g => g.First()).ToArray();
            var mergedFixtures = pack.Fixtures.Concat(fixtures).Where(f => f != null).GroupBy(f => f.id).Select(g => g.First()).ToArray();
            var packData = new SerializedObject(pack); Assign(packData.FindProperty("products"), mergedGoods); Assign(packData.FindProperty("fixtures"), mergedFixtures);
            packData.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            foreach (var product in goods)
            {
                var serialized = new SerializedObject(product);
                serialized.FindProperty("icon").objectReferenceValue = ShopUiPreviews.Render(product.ShelfDisplayPrefab, product.Id, 25);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var fixture in fixtures)
            {
                fixture.icon = ShopUiPreviews.Render(fixture.prefab, fixture.id, 25); EditorUtility.SetDirty(fixture);
            }
            AssetDatabase.SaveAssets(); AssortmentUpdateReview.Capture();
            File.WriteAllText(Report + "/build.txt", "PASS 48 Blender products, four stockable fixtures, packaging atlas, 52 real-model icons and resource catalogue. No scene changes.\n");
            File.WriteAllLines(Report + "/outputs.txt", Directory.GetFiles(Art, "*", SearchOption.AllDirectories).Append(Art + ".meta")
                .Concat(Directory.GetFiles(Data, "*", SearchOption.AllDirectories)).Append(Data + ".meta")
                .Concat(goods.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.Icon), AssetDatabase.GetAssetPath(p.Icon) + ".meta" }))
                .Concat(fixtures.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.icon), AssetDatabase.GetAssetPath(p.icon) + ".meta" }))
                .Concat(ChangedScripts().SelectMany(p => new[] { p, p + ".meta" })).Distinct().OrderBy(p => p).Select(p => p.Replace('\\', '/')));
            AssortmentUpdatePlay.Run();
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText(Report + "/build.txt", "FAIL " + error); EditorApplication.Exit(1); }
    }

    public static ProductItemData MakeProduct(ProductDefinition definition, Material material, string artRoot = Art, string dataRoot = Data, GameObject visualOverride = null)
    {
        var visual = visualOverride != null ? visualOverride : MakeVisual(definition.key, artRoot + "/Products/" + definition.key + ".prefab", material, artRoot);
        // Reuse existing catalogue identity when an approved model replaces a legacy SKU.
        var existing = AssetDatabase.FindAssets("t:ProductItemData").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith(dataRoot + "/"))
            .Select(AssetDatabase.LoadAssetAtPath<ProductItemData>)
            .FirstOrDefault(p => p != null && p.Id == "product_" + definition.key);
        if (existing != null)
        {
            ShopUiSetup.Set(existing, "shelfDisplayPrefab", visual);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        string path = dataRoot + "/Products/" + definition.key + ".asset";
        var product = AssetDatabase.LoadAssetAtPath<ProductItemData>(path);
        if (product == null) { product = ScriptableObject.CreateInstance<ProductItemData>(); AssetDatabase.CreateAsset(product, path); }
        var data = new SerializedObject(product);
        data.FindProperty("id").stringValue = "product_" + definition.key;
        data.FindProperty("displayName").stringValue = definition.name;
        data.FindProperty("buyPrice").intValue = definition.cost * 10;
        data.FindProperty("sellPrice").intValue = definition.sale;
        data.FindProperty("boxAmount").intValue = 10;
        data.FindProperty("unitWeightKg").floatValue = definition.weight;
        data.FindProperty("unitVolumeM3").floatValue = definition.volume;
        data.FindProperty("storageType").intValue = (int)Enum.Parse(typeof(ProductStorageType), definition.storage);
        data.FindProperty("category").intValue = definition.storage == "Household" ? (int)ProductCategory.Household
            : definition.storage == "Snacks" ? (int)ProductCategory.Snacks
            : definition.storage == "Drinks" ? (int)ProductCategory.Drinks : (int)ProductCategory.Food;
        data.FindProperty("shelfDisplayPrefab").objectReferenceValue = visual;
        data.ApplyModifiedPropertiesWithoutUndo(); return product;
    }

    private static GameObject MakeVisual(string key, string output, Material material, string artRoot = Art)
    {
        string path = artRoot + "/Models/" + key + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.isReadable = true;
        importer.meshCompression = ModelImporterMeshCompression.Off; importer.SaveAndReimport();
        var root = new GameObject(key);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.transform.SetParent(root.transform, false);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                int submeshes = renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                renderer.sharedMaterials = submeshes > 1 ? new[] { material, GlassMaterial() } : new[] { material };
            }
            var bounds = Measure(root);
            Require(bounds.size.y > .02f && bounds.size.x > .02f, "Empty imported model " + key);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return PrefabUtility.SaveAsPrefabAsset(root, output);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Material GlassMaterial()
    {
        string path = Art + "/Glass.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", new Color(.67f, .83f, .78f, .12f));
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0); material.SetFloat("_Smoothness", .45f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000; EditorUtility.SetDirty(material); return material;
    }

    private static BuildItemData MakeFixture(FixtureDefinition definition, Material material)
    {
        string path = Art + "/Fixtures/" + definition.key + ".prefab";
        MakeVisual(definition.key, path, material);
        var template = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/Assortment/ProduceRack.asset");
        var root = PrefabUtility.LoadPrefabContents(path);
        Bounds bounds;
        try
        {
            root.layer = template.prefab.layer;
            var placed = root.AddComponent<PlacedObject>();
            var stock = root.AddComponent<PlacedShelfStock>();
            var display = root.AddComponent<ShelfProductDisplay>();
            var stockData = new SerializedObject(stock);
            stockData.FindProperty("shelfType").intValue = (int)Enum.Parse(typeof(ShelfStorageType), definition.shelfType);
            stockData.FindProperty("maxAmount").intValue = definition.capacity;
            var accepts = stockData.FindProperty("acceptedProductTypes"); accepts.arraySize = definition.accepts.Length;
            for (int i = 0; i < accepts.arraySize; i++) accepts.GetArrayElementAtIndex(i).intValue = (int)Enum.Parse(typeof(ProductStorageType), definition.accepts[i]);
            stockData.ApplyModifiedPropertiesWithoutUndo();
            var slots = new GameObject("ProductSlots").transform; slots.SetParent(root.transform, false);
            // Fill all visible tiers before adding the deeper rows.
            for (int row = 0; row < definition.rows; row++) for (int column = 0; column < definition.columns; column++) for (int tier = 0; tier < definition.tiers; tier++)
            {
                var slot = new GameObject("Slot_" + slots.childCount.ToString("D2")).transform; slot.SetParent(slots, false);
                float spacing = definition.slotWidth + .018f;
                slot.localPosition = new Vector3((column - (definition.columns - 1) * .5f) * spacing,
                    definition.slotY + tier * (definition.key == "snack_endcap" ? .27f : .31f), definition.slotZ + row * definition.spacingZ);
                slot.gameObject.AddComponent<ShelfProductSlot>().usableSize = new Vector3(definition.slotWidth, definition.slotHeight, definition.slotDepth);
            }
            Require(slots.childCount == definition.capacity, "Fixture has fewer visible slots than its stock capacity.");
            var displayData = new SerializedObject(display);
            displayData.FindProperty("shelfStock").objectReferenceValue = stock;
            displayData.FindProperty("slotsRoot").objectReferenceValue = slots;
            displayData.FindProperty("productScale").floatValue = 1;
            displayData.ApplyModifiedPropertiesWithoutUndo();
            bounds = Measure(root);
            var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
            ShopUiSetup.Set(root.AddComponent<ShelfHighlight>(), "boundsCollider", collider);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        string itemPath = Data + "/Fixtures/" + definition.key + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(itemPath);
        if (item == null) { item = ScriptableObject.CreateInstance<BuildItemData>(); AssetDatabase.CreateAsset(item, itemPath); }
        item.id = "shelf_" + definition.key + "_01"; item.displayName = definition.name;
        item.description = definition.description; item.price = definition.price;
        item.weightKg = definition.weight; item.cargoVolumeM3 = definition.volume;
        item.category = BuildCategory.Shelf; item.footprint = new Vector2Int(definition.footprintX, definition.footprintZ);
        item.alignModelToFootprint = true; item.placementBounds = bounds;
        item.ruleFlags = template.ruleFlags; item.frontFacing = 0;
        item.previewValidMaterial = template.previewValidMaterial; item.previewInvalidMaterial = template.previewInvalidMaterial;
        item.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); EditorUtility.SetDirty(item); return item;
    }

    public static Bounds Measure(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        Require(renderers.Length > 0, "No geometry: " + root.name);
        var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
    }

    private static void Assign(SerializedProperty values, Object[] references)
    {
        values.arraySize = references.Length;
        for (int i = 0; i < references.Length; i++) values.GetArrayElementAtIndex(i).objectReferenceValue = references[i];
    }

    public static string[] ChangedScripts() => new[] {
        "Assets/Project/Scripts/Core/StoreAssortmentPack.cs",
        "Assets/Project/Scripts/ProductSystem/ProductPlacementTypes.cs", "Assets/Project/Scripts/ProductSystem/ProductCatalog.cs",
        "Assets/Project/Scripts/BuildSystem/BuildItemCatalog.cs", "Assets/Project/Scripts/UI/Shop/ShopWindow.cs",
        "Assets/Project/Scripts/UI/Shop/ShopText.cs", "Assets/Project/Editor/AssortmentUpdateBatch.cs",
        "Assets/Project/Editor/AssortmentUpdatePlay.cs", "Assets/Project/Editor/AssortmentUpdateReview.cs" };

    public static void Record()
    {
        var inputs = File.ReadAllLines(Report + "/outputs.txt")
            .Concat(Directory.GetFiles("Assets/Project/Scripts", "*.cs", SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[] { CityUpdateSetup.GamePath, CityUpdateSetup.CityPath }, true)
                .Where(p => p.StartsWith("Assets/") && File.Exists(p))).Distinct().OrderBy(p => p);
        using var hash = SHA256.Create();
        File.WriteAllLines(Report + "/inputs.sha256", inputs.Select(p => { using var input = File.OpenRead(p); return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "") + " " + p.Replace('\\', '/'); }));
    }

    public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
