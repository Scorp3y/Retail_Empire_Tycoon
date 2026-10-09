using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit catalog migration. Keeps existing IDs/GUIDs, appends new entries, never opens a player save.</summary>
public static class ShopAssortmentSetup
{
    private const string Farm = "Assets/Models/lowpoly models/Pandazole Farm Ranch Pack/Prefabs/";
    private const string Kitchen = "Assets/Models/kitchen food/Pandazole Kitchen Food/Prefabs/";
    private const string Food = "Assets/Models/Food/Food Pack - Low Poly Assets/Prefabs/";
    private const string ProductFolder = "Assets/Prefabs/Product/Assortment";
    private const string RackFolder = "Assets/Prefabs/Shelf/Assortment";

    private sealed class ProductDefinition
    {
        public readonly string Key, Name, Model;
        public readonly ProductStorageType Storage;
        public readonly int Buy, Sell;
        public readonly float ModelSize;
        public ProductDefinition(string key, string name, string model, ProductStorageType storage, int buy, int sell, float modelSize)
        { Key = key; Name = name; Model = model; Storage = storage; Buy = buy; Sell = sell; ModelSize = modelSize; }
    }

    private static ProductDefinition[] Definitions => new[]
    {
        Produce("apple", "Apples", "Apple", 3, 5), Produce("banana", "Bananas", "Banana", 3, 5),
        Produce("orange", "Oranges", "Orange", 4, 7), Produce("pear", "Pears", "Pear", 4, 7),
        Produce("lemon", "Lemons", "Lemon", 3, 5), Produce("grape", "Grapes", "Grape", 5, 8),
        Produce("carrot", "Carrots", "Carrot", 2, 4), Produce("cucumber", "Cucumbers", "Cucumber", 3, 5),
        Produce("tomato", "Tomatoes", "Tomato", 3, 5), Produce("broccoli", "Broccoli", "Broccoli", 4, 7),
        Produce("potato", "Potatoes", "Potato", 2, 4), Produce("pepper", "Sweet peppers", "Pepper", 4, 7),
        new ProductDefinition("cheese", "Cheese", Kitchen + "Food_Cheese.prefab", ProductStorageType.Dairy, 9, 15, .15f),
        new ProductDefinition("eggs", "Eggs", Kitchen + "Food_Egg.prefab", ProductStorageType.Dairy, 3, 5, .14f),
        new ProductDefinition("sausage", "Sausage", Kitchen + "Food_Susage.prefab", ProductStorageType.Meat, 8, 13, .30f),
        new ProductDefinition("chicken", "Chicken fillet", Kitchen + "Food_Chicken Breast.prefab", ProductStorageType.Meat, 10, 17, .30f),
        new ProductDefinition("ketchup", "Ketchup", Food + "Ketchup.prefab", ProductStorageType.Groceries, 6, 10, .32f),
        new ProductDefinition("mustard", "Mustard", Food + "Mustard.prefab", ProductStorageType.Groceries, 5, 8, .32f),
        new ProductDefinition("juice", "Juice", Food + "SodaBottleVar2.prefab", ProductStorageType.Drinks, 5, 8, .32f),
        new ProductDefinition("icecream", "Ice cream", Food + "IceCream04Var1.prefab", ProductStorageType.Frozen, 6, 10, .30f)
    };

    private static ProductDefinition Produce(string key, string name, string model, int buy, int sell) =>
        new ProductDefinition(key, name, Farm + "food_" + model + ".prefab", ProductStorageType.Produce, buy, sell, .1f);

    [MenuItem("Retail Empire/UI/Apply current UI and assortment %#&F1")]
    public static void ApplyAll() { Apply(); ShopUiSetup.Apply(); }

    [MenuItem("Retail Empire/Products/Apply expanded assortment")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != "Assets/Project/Scenes/Game.unity")
            throw new InvalidOperationException("Requires the saved Game scene in Edit Mode.");
        // Validate source assets before changing any catalog entries.
        foreach (var definition in Definitions)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.Model) == null)
                throw new InvalidOperationException("Missing existing product model: " + definition.Model);
        Directory.CreateDirectory(ProductFolder); Directory.CreateDirectory(RackFolder); AssetDatabase.Refresh();
        var products = Definitions.Select(CreateProduct).ToArray();
        ShopBakeryAssortment.ApplyModels();
        var breadItem = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/FreshMarketShelf_BuildItem.asset");
        RebuildRack(breadItem, false);
        breadItem.displayName = "Bakery rack"; breadItem.description = "For bread and bakery products";
        EditorUtility.SetDirty(breadItem);
        var produceItem = AssetDatabase.LoadAssetAtPath<BuildItemData>(RackFolder + "/ProduceRack.asset");
        if (produceItem == null)
        {
            produceItem = ScriptableObject.CreateInstance<BuildItemData>();
            produceItem.id = "shelf_produce_01"; produceItem.displayName = "Produce rack";
            produceItem.description = "For fresh fruit and vegetables"; produceItem.price = 140;
            produceItem.category = BuildCategory.Shelf; produceItem.footprint = breadItem.footprint;
            produceItem.alignModelToFootprint = true; produceItem.placementBounds = breadItem.placementBounds;
            produceItem.ruleFlags = breadItem.ruleFlags; produceItem.frontFacing = breadItem.frontFacing;
            produceItem.previewValidMaterial = breadItem.previewValidMaterial; produceItem.previewInvalidMaterial = breadItem.previewInvalidMaterial;
            AssetDatabase.CreateAsset(produceItem, RackFolder + "/ProduceRack.asset");
        }
        RebuildRack(produceItem, true);
        ConfigureStorage("Wall Goods Shelf", ProductStorageType.Oil, ProductStorageType.Drinks, ProductStorageType.Groceries);
        ConfigureStorage("Double-Sided Shelf", ProductStorageType.Oil, ProductStorageType.Drinks, ProductStorageType.Groceries);
        ConfigureStorage("Refrigerated Display", ProductStorageType.Dairy);
        ConfigureStorage("Cold Pantry", ProductStorageType.Meat, ProductStorageType.Frozen);
        foreach (var window in Object.FindObjectsOfType<ShopWindow>(true))
        {
            Append(window, "buildCatalog", produceItem);
            foreach (var product in products) Append(window, "productCatalog", product);
        }
        foreach (var catalog in Object.FindObjectsOfType<BuildItemCatalog>(true)) Append(catalog, "items", produceItem);
        foreach (var catalog in Object.FindObjectsOfType<ProductCatalog>(true))
            foreach (var product in products) Append(catalog, "products", product);
        AssetDatabase.SaveAssets(); ShopUiPreviews.Generate();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Expanded assortment applied: 20 new products, bakery rack rebuilt, separate produce rack. Player save untouched.");
    }

    private static ProductItemData CreateProduct(ProductDefinition definition)
    {
        string prefabPath = ProductFolder + "/" + definition.Key + ".prefab";
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (visual == null)
        {
            var root = new GameObject(definition.Name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(definition.Model));
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(root.transform, false); model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(CompatibleMaterial).ToArray();
                Bounds bounds = Measure(root);
                float maximum = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (maximum <= .0001f) throw new InvalidOperationException("Empty product model: " + definition.Model);
                model.transform.localScale *= definition.ModelSize / maximum;
                bounds = Measure(root);
                // Every shelf model has a centered ground-level pivot, regardless of its imported FBX pose.
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                visual = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        string dataPath = ProductFolder + "/" + definition.Key + "_Product.asset";
        var item = AssetDatabase.LoadAssetAtPath<ProductItemData>(dataPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ProductItemData>();
            AssetDatabase.CreateAsset(item, dataPath);
            var data = new SerializedObject(item);
            data.FindProperty("id").stringValue = "product_" + definition.Key;
            data.FindProperty("displayName").stringValue = definition.Name;
            data.FindProperty("storageType").enumValueIndex = (int)definition.Storage;
            data.FindProperty("buyPrice").intValue = definition.Buy * 10;
            data.FindProperty("sellPrice").intValue = definition.Sell;
            data.FindProperty("boxAmount").intValue = 10;
            data.FindProperty("shelfDisplayPrefab").objectReferenceValue = visual;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        return item;
    }

    private static Material CompatibleMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("Product model has a missing material.");
        if (source.shader.name.StartsWith("Universal Render Pipeline/")) return source;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        // Name + GUID identifies the original material without modifying third-party model packs.
        string path = ProductFolder + "/Material_" + guid + "_" + source.name.Replace('/', '_') + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name + " Shop" };
        material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
        if (source.HasProperty("_MainTex")) material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
        material.SetFloat("_Smoothness", .15f); AssetDatabase.CreateAsset(material, path); return material;
    }

    private static void RebuildRack(BuildItemData item, bool produce)
    {
        string path = produce ? RackFolder + "/ProduceRack.prefab" : "Assets/Prefabs/Shelf/Fresh Market Shelf.prefab";
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Produce rack");
        try
        {
            root.transform.localScale = Vector3.one * .75f;
            root.transform.localPosition = Vector3.zero;
            if (produce)
                root.layer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shelf/Fresh Market Shelf.prefab").layer;
            var stock = root.GetComponent<PlacedShelfStock>();
            if (stock == null) stock = root.AddComponent<PlacedShelfStock>();
            var display = root.GetComponent<ShelfProductDisplay>();
            if (display == null) display = root.AddComponent<ShelfProductDisplay>();
            if (root.GetComponent<PlacedObject>() == null) root.AddComponent<PlacedObject>();
            Transform oldModel = root.transform.Find("display-bread");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);
            Transform rack = root.transform.Find("Rack model");
            if (rack != null) Object.DestroyImmediate(rack.gameObject);
            rack = new GameObject("Rack model").transform; rack.SetParent(root.transform, false);
            Material wood = RackMaterial("Wood", new Color(.46f, .29f, .15f));
            Material frame = RackMaterial("Frame", new Color(.22f, .28f, .26f));
            Material lightWood = RackMaterial("LightWood", new Color(.68f, .47f, .25f));
            Material green = RackMaterial("Green", new Color(.18f, .37f, .25f));
            foreach (float x in new[] { -.32f, .32f })
                foreach (float z in new[] { -.24f, .24f }) Cube(rack, "Frame post", new Vector3(x, .36f, z), new Vector3(.035f, .72f, .035f), frame);
            Cube(rack, "Rear panel", new Vector3(0, .36f, -.255f), new Vector3(.62f, .66f, .025f), wood);
            for (int tier = 0; tier < 3; tier++)
            {
                float height = .16f + tier * .20f;
                Cube(rack, "Wood tray " + tier, new Vector3(0, height, 0), new Vector3(.61f, .025f, .50f), lightWood);
                Cube(rack, "Front lip " + tier, new Vector3(0, height + .035f, .25f), new Vector3(.61f, .055f, .025f), wood);
                foreach (float x in new[] { -.3f, .3f }) Cube(rack, "Tray side", new Vector3(x, height + .045f, 0), new Vector3(.025f, .08f, .50f), wood);
                if (produce) foreach (float x in new[] { -.10f, .10f }) Cube(rack, "Crate divider", new Vector3(x, height + .035f, 0), new Vector3(.013f, .055f, .48f), wood);
            }
            Cube(rack, "Market header", new Vector3(0, .725f, -.25f), new Vector3(.67f, .09f, .03f), green);
            Transform slots = root.transform.Find("ProductSlots");
            if (slots == null) { slots = new GameObject("ProductSlots").transform; slots.SetParent(root.transform, false); }
            slots.localPosition = Vector3.zero; slots.localRotation = Quaternion.identity; slots.localScale = Vector3.one;
            for (int i = slots.childCount - 1; i >= 0; i--) Object.DestroyImmediate(slots.GetChild(i).gameObject);
            int columns = 6;
            // Interleave tiers so a partly stocked rack is readable, rather than hiding all units on its bottom tray.
            for (int depth = 0; depth < (produce?2:3); depth++) for (int column = 0; column < columns; column++) for (int tier = 0; tier < 3; tier++)
            {
                Transform slot = new GameObject("Slot_" + slots.childCount.ToString("D2")).transform;
                slot.SetParent(slots, false);
                float x = produce ? -.20f + (column / 2) * .20f + (column % 2 == 0 ? -.04f : .04f)
                    : -.25f + column * .10f;
                slot.localPosition = new Vector3(x, .173f + tier * .20f, produce?-.12f + depth * .24f:-.16f+depth*.16f);
                slot.gameObject.AddComponent<ShelfProductSlot>().usableSize = produce
                    ? new Vector3(.075f, .13f, .18f) : new Vector3(.09f, .14f, .145f);
            }
            var stockData = new SerializedObject(stock);
            stockData.FindProperty("shelfType").enumValueIndex = produce ? (int)ShelfStorageType.Produce : (int)ShelfStorageType.Fresh;
            stockData.FindProperty("acceptedProductTypes").arraySize = 1;
            stockData.FindProperty("acceptedProductTypes").GetArrayElementAtIndex(0).enumValueIndex = produce ? (int)ProductStorageType.Produce : (int)ProductStorageType.Bakery;
            // Existing stored quantities must not be clamped during this visual migration.
            stockData.FindProperty("maxAmount").intValue = produce ? 30 : 54; stockData.ApplyModifiedPropertiesWithoutUndo();
            var displayData = new SerializedObject(display);
            displayData.FindProperty("shelfStock").objectReferenceValue = stock;
            displayData.FindProperty("slotsRoot").objectReferenceValue = slots;
            displayData.FindProperty("slots").arraySize = 0;
            displayData.FindProperty("productScale").floatValue = 1;
            displayData.FindProperty("fillMode").enumValueIndex = 0; displayData.ApplyModifiedPropertiesWithoutUndo();
            Bounds bounds = Measure(rack.gameObject);
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center); collider.size = bounds.size / .75f;
            var highlight = root.GetComponent<ShelfHighlight>();
            if (highlight == null) highlight = root.AddComponent<ShelfHighlight>();
            ShopUiSetup.Set(highlight, "boundsCollider", collider);
            item.prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            // Preserve the old occupied X/Z cells; only the rack's vertical extent changes.
            bounds.center -= root.transform.position; item.placementBounds = bounds;
            EditorUtility.SetDirty(item);
        }
        finally { if (existing) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
        Object.DestroyImmediate(cube.GetComponent<Collider>()); cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position; cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }
    private static Material RackMaterial(string name, Color color)
    {
        string path = RackFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Market rack " + name };
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .1f);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Bounds Measure(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("No mesh in " + root.name);
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    private static void ConfigureStorage(string name, params ProductStorageType[] types)
    {
        string path = "Assets/Prefabs/Shelf/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var data = new SerializedObject(root.GetComponent<PlacedShelfStock>());
            var values = data.FindProperty("acceptedProductTypes"); values.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++) values.GetArrayElementAtIndex(i).enumValueIndex = (int)types[i];
            data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Append(Object target, string field, Object item)
    {
        var data = new SerializedObject(target); var values = data.FindProperty(field);
        for (int i = 0; i < values.arraySize; i++) if (values.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
        values.arraySize++; values.GetArrayElementAtIndex(values.arraySize - 1).objectReferenceValue = item;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
