using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports the original texture-free Blender meshes with their three material assignments intact.</summary>
public static class ProduceExpansionBatch
{
    public const string Art = "Assets/Art/FreshProduce", Data = "Assets/Resources/StoreAssortment/FreshProduce";
    public const string Report = "Library/ProduceQA", PackPath = BakeryExpansionBatch.PackPath;
    public static readonly string[] Keys = { "kiwi", "peaches", "plums", "mango", "cherries", "eggplant" };
    [Serializable] private sealed class Definitions { public ModelDesign[] products; public MaterialDesign[] materials; }
    [Serializable] private sealed class ModelDesign { public string key; public int triangles, limit; public float[] size; }
    [Serializable] private sealed class MaterialDesign { public string name; public float[] color; public float roughness, alpha; }

    public static void BuildAndPlay()
    {
        TownWorldSafety.RequireIsolatedEditor(); Directory.CreateDirectory(Report);
        try
        {
            File.WriteAllText(Report + "/geometry.txt", "");
            foreach (var folder in new[] { Art + "/Products", Art + "/Materials", Data + "/Products" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh(); string json = File.ReadAllText(Art + "/Definitions.json");
            var source = JsonUtility.FromJson<Definitions>(json);
            var economics = JsonUtility.FromJson<AssortmentUpdateBatch.Definitions>(json);
            Require(source.products.Select(p => p.key).SequenceEqual(Keys), "Unexpected produce models.");
            var materials = source.materials.ToDictionary(m => m.name, MakeMaterial);
            var products = new List<ProductItemData>();
            foreach (var design in source.products)
            {
                var visual = MakeVisual(design, materials);
                var definition = economics.products.Single(p => p.key == design.key);
                Require(definition.storage == "Produce", "Produce storage category missing.");
                var item = AssortmentUpdateBatch.MakeProduct(definition, null, Art, Data, visual);
                var data = new SerializedObject(item);
                data.FindProperty("icon").objectReferenceValue = ShopUiPreviews.Render(visual, item.Id, 25);
                data.ApplyModifiedPropertiesWithoutUndo(); products.Add(item);
            }
            var pack = AssetDatabase.LoadAssetAtPath<StoreAssortmentPack>(PackPath);
            Require(pack != null && pack.Products.Count >= 54 && pack.Fixtures.Count >= 4, "Earlier assortment missing.");
            var previous = pack.Products.Select(p => p.Id).ToArray();
            var merged = pack.Products.Concat(products).GroupBy(p => p.Id).Select(group => group.First()).ToArray();
            var serialized = new SerializedObject(pack); var array = serialized.FindProperty("products"); array.arraySize = merged.Length;
            for (int i = 0; i < merged.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = merged[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(previous.SequenceEqual(pack.Products.Take(previous.Length).Select(p => p.Id)), "Earlier catalogue order/identities changed.");
            AssetDatabase.SaveAssets();
            BakeryExpansionReview.Capture(Data, Keys, "FRESH PRODUCE", "ACTUAL UNITY MODELS  /  FARMERS MARKET  /  PRODUCE RACK", "Docs/ProduceExpansion/Models.png");
            File.WriteAllLines(Report + "/outputs.txt", Directory.GetFiles(Art, "*", SearchOption.AllDirectories).Append(Art + ".meta")
                .Concat(Directory.GetFiles(Data, "*", SearchOption.AllDirectories)).Append(Data + ".meta")
                .Append(PackPath).Append(PackPath + ".meta")
                .Concat(products.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.Icon), AssetDatabase.GetAssetPath(p.Icon) + ".meta" }))
                .Concat(Scripts().SelectMany(p => new[] { p, p + ".meta" })).Distinct().OrderBy(p => p).Select(p => p.Replace('\\', '/')));
            File.WriteAllText(Report + "/build.txt", "PASS six original Blender produce models, three simple materials each, transparent cherry box, measured units, triangle budgets, six UI icons and appended catalogue.\n");
            ProduceExpansionPlay.Run();
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText(Report + "/build.txt", "FAIL " + error); EditorApplication.Exit(1); }
    }

    private static Material MakeMaterial(MaterialDesign design)
    {
        string path = Art + "/Materials/" + design.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetTexture("_BaseMap", null);
        material.SetColor("_BaseColor", new Color(design.color[0], design.color[1], design.color[2], design.alpha));
        material.SetFloat("_Smoothness", 1 - design.roughness); material.SetFloat("_Metallic", 0);
        if (design.alpha < 1)
        {
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // The plastic already has inner/outer wall geometry. Culling avoids
            // blending both sides of each wall over the fruit a second time.
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
        }
        else
        {
            material.SetFloat("_SpecularHighlights", 0); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        EditorUtility.SetDirty(material); return material;
    }

    private static GameObject MakeVisual(ModelDesign design, IReadOnlyDictionary<string, Material> materials)
    {
        string path = Art + "/Models/" + design.key + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
        importer.importNormals = ModelImporterNormals.Import; importer.isReadable = true; importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.SaveAndReimport();
        var root = new GameObject(design.key);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); model.transform.SetParent(root.transform, false);
            var renderers = root.GetComponentsInChildren<MeshRenderer>(); Require(renderers.Length == 1, "Expected one product mesh.");
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                {
                    Require(m != null && materials.ContainsKey(m.name.Split('.')[0]), "Unknown Blender material " + m?.name);
                    return materials[m.name.Split('.')[0]];
                }).ToArray();
                Require(renderer.sharedMaterials.Length == 3 && renderer.sharedMaterials.Distinct().Count() == 3, "Three original materials were not retained.");
            }
            int triangles = root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
            // Unity may discard zero-area triangles created by a very thin bevel.
            // Preserve the geometry budget and check actual dimensions below.
            Require(triangles <= design.triangles && triangles >= design.triangles * .95f && triangles < design.limit,
                "Unexpected triangle count: " + design.key + " imported " + triangles + ", source " + design.triangles);
            var bounds = AssortmentUpdateBatch.Measure(root);
            var expected = new Vector3(design.size[0], design.size[2], design.size[1]);
            Require(Vector3.Distance(bounds.size, expected) < .00002f, "Wrong model size or orientation: " + design.key + " " + bounds.size);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            File.AppendAllText(Report + "/geometry.txt", design.key + ": " + triangles + " triangles, size " + bounds.size.ToString("F5") + ", three material slots, no image textures.\n");
            return PrefabUtility.SaveAsPrefabAsset(root, Art + "/Products/" + design.key + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static string[] Scripts() => new[] { "Assets/Project/Editor/ProduceExpansionBatch.cs", "Assets/Project/Editor/ProduceExpansionPlay.cs",
        "Assets/Project/Editor/AssortmentUpdateBatch.cs", "Assets/Project/Editor/BakeryExpansionPlay.cs", "Assets/Project/Editor/BakeryExpansionReview.cs" };
    public static void Record()
    {
        var dependencies = AssetDatabase.GetDependencies(new[] { CityUpdateSetup.GamePath, CityUpdateSetup.CityPath, PackPath }, true)
            .Where(p => p.StartsWith("Assets/") && File.Exists(p));
        var inputs = File.ReadAllLines(Report + "/outputs.txt").Concat(dependencies)
            .Concat(Directory.GetFiles("Assets/Project/Scripts", "*.cs", SearchOption.AllDirectories)).Distinct().OrderBy(p => p);
        using var hash = SHA256.Create();
        File.WriteAllLines(Report + "/inputs.sha256", inputs.Select(p => { using var file = File.OpenRead(p); return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "") + " " + p.Replace('\\', '/'); }));
    }
    private static void Require(bool condition, string message) => AssortmentUpdateBatch.Require(condition, message);
}
