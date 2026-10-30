using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEngine;

/// <summary>Installs the six approved bakery models into the existing shared catalogue without replacing its other waves.</summary>
public static class BakeryExpansionBatch
{
    public const string Art = "Assets/Art/Bakery";
    public const string Data = "Assets/Resources/StoreAssortment/Bakery";
    public const string PackPath = "Assets/Resources/StoreAssortment/Expansion.asset";
    public const string Report = "Library/BakeryQA";
    public static readonly string[] Keys = { "croissant", "doughnut", "muffin", "pretzel", "cupcake", "cake_slice" };

    public static void BuildAndPlay()
    {
        TownWorldSafety.RequireIsolatedEditor(); Directory.CreateDirectory(Report);
        try
        {
            Directory.CreateDirectory(Art + "/Products"); Directory.CreateDirectory(Data + "/Products"); AssetDatabase.Refresh();
            var definitions = JsonUtility.FromJson<AssortmentUpdateBatch.Definitions>(File.ReadAllText(Art + "/Definitions.json"));
            Require(definitions.products.Select(p => p.key).SequenceEqual(Keys), "Unexpected bakery designs or order.");
            var palette = (TextureImporter)AssetImporter.GetAtPath(Art + "/BakeryPalette.png");
            palette.maxTextureSize = 256; palette.mipmapEnabled = true; palette.filterMode = FilterMode.Bilinear;
            palette.textureCompression = TextureImporterCompression.Uncompressed; palette.wrapMode = TextureWrapMode.Clamp; palette.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Bakery.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Art + "/Bakery.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/BakeryPalette.png"));
            material.SetFloat("_Smoothness", 0); material.SetFloat("_Metallic", 0); material.SetFloat("_SpecularHighlights", 0); EditorUtility.SetDirty(material);
            var products = definitions.products.Select(p => AssortmentUpdateBatch.MakeProduct(p, material, Art, Data)).ToArray();
            foreach (var product in products)
            {
                Require(product.StorageType == ProductStorageType.Bakery, "New bakery product has wrong storage.");
                var mesh = product.ShelfDisplayPrefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                Require(mesh.subMeshCount == 1 && mesh.triangles.Length / 3 < 3000, "Product exceeds the one-material low-poly budget.");
                var data = new SerializedObject(product);
                data.FindProperty("icon").objectReferenceValue = ShopUiPreviews.Render(product.ShelfDisplayPrefab, product.Id, 25);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var pack = AssetDatabase.LoadAssetAtPath<StoreAssortmentPack>(PackPath);
            Require(pack != null && pack.Products.Count >= 48 && pack.Fixtures.Count >= 4, "Existing assortment is missing.");
            var oldIds = pack.Products.Select(p => p.Id).ToArray();
            var merged = pack.Products.Concat(products).GroupBy(p => p.Id).Select(group => group.First()).ToArray();
            var serialized = new SerializedObject(pack); var values = serialized.FindProperty("products"); values.arraySize = merged.Length;
            for (int i = 0; i < merged.Length; i++) values.GetArrayElementAtIndex(i).objectReferenceValue = merged[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(oldIds.SequenceEqual(pack.Products.Take(oldIds.Length).Select(p => p.Id)), "Existing product identities/order changed.");
            AssetDatabase.SaveAssets(); BakeryExpansionReview.Capture();
            File.WriteAllLines(Report + "/outputs.txt", Directory.GetFiles(Art, "*", SearchOption.AllDirectories).Append(Art + ".meta")
                .Concat(Directory.GetFiles(Data, "*", SearchOption.AllDirectories)).Append(Data + ".meta")
                .Append(PackPath).Append(PackPath + ".meta")
                .Concat(products.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.Icon), AssetDatabase.GetAssetPath(p.Icon) + ".meta" }))
                .Concat(Scripts().SelectMany(p => new[] { p, p + ".meta" })).Distinct().OrderBy(p => p).Select(p => p.Replace('\\', '/')));
            File.WriteAllText(Report + "/build.txt", "PASS six Blender bakery props, individual source files, one material each, small geometric bevels, native preview icons, existing bakery shelf and appended catalogue.\n");
            BakeryExpansionPlay.Run();
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText(Report + "/build.txt", "FAIL " + error); EditorApplication.Exit(1); }
    }

    public static string[] Scripts() => new[] { "Assets/Project/Editor/BakeryExpansionBatch.cs", "Assets/Project/Editor/BakeryExpansionPlay.cs",
        "Assets/Project/Editor/BakeryExpansionReview.cs", "Assets/Project/Editor/AssortmentUpdateBatch.cs", "Assets/Project/Editor/AssortmentUpdatePlay.cs",
        "Assets/Project/Editor/ShopUiPreviews.cs" };

    public static void Record()
    {
        var dependencies = AssetDatabase.GetDependencies(new[] { CityUpdateSetup.GamePath, CityUpdateSetup.CityPath, PackPath }, true)
            .Where(p => p.StartsWith("Assets/") && File.Exists(p));
        var inputs = File.ReadAllLines(Report + "/outputs.txt").Concat(dependencies)
            .Concat(Directory.GetFiles("Assets/Project/Scripts", "*.cs", SearchOption.AllDirectories)).Distinct().OrderBy(p => p);
        using var hash = SHA256.Create();
        File.WriteAllLines(Report + "/inputs.sha256", inputs.Select(p => { using var file = File.OpenRead(p); return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "") + " " + p.Replace('\\', '/'); }));
    }

    public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
