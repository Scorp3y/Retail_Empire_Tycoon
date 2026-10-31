using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using UnityEditor;
using UnityEngine;

/// <summary>Installs approved food models into existing purchasing, stocking and save identities.</summary>
public static class FoodExpansionIntegration
{
    public const string Art = "Assets/Art/FoodExpansion";
    public const string Data = "Assets/Resources/StoreAssortment/FoodExpansion";
    public const string Report = "Library/FoodExpansionIntegration";

    [Serializable] public sealed class Manifest { public Entry[] products; }
    [Serializable] public sealed class Entry { public string key,folder,sha256,storage; public int triangles; }

    public static Entry[] Entries => JsonUtility.FromJson<Manifest>(File.ReadAllText(Art + "/Manifest.json")).products;

    [MenuItem("Retail Empire/Products/Install Food Expansion")]
    public static void Build()
    {
        Directory.CreateDirectory(Report);
        try
        {
            var entries = Entries;
            Require(entries.Length == 96 && entries.Select(e => e.key).Distinct().Count() == 96, "Expected 96 distinct approved products.");
            Wave2Integration.Build(Art, Data);
            var products = StoreAssortmentPack.Load().Products.ToDictionary(p => p.Id);
            foreach (var fixture in StoreAssortmentPack.Load().Fixtures.Where(f => f.id.StartsWith("shelf_fixture_")))
            {
                int number = int.Parse(fixture.id.Split('_')[2]);
                var additions = entries.Select(e => products["product_" + e.key]).Where(p => FitsFixture(number, p.StorageType)).ToArray();
                if (additions.Length == 0) continue;
                string path = AssetDatabase.GetAssetPath(fixture.prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var serialized = new SerializedObject(root.GetComponent<PlacedShelfStock>());
                    var allowed = serialized.FindProperty("extraAllowedProducts");
                    var values = Enumerable.Range(0, allowed.arraySize)
                        .Select(i => allowed.GetArrayElementAtIndex(i).objectReferenceValue as ProductItemData)
                        .Where(p => p != null).Concat(additions).Distinct().ToArray();
                    allowed.arraySize = values.Length;
                    for (int i = 0; i < values.Length; i++) allowed.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Validate();
        }
        catch (Exception error)
        {
            File.WriteAllText(Report + "/build.txt", "FAIL\n" + error);
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    public static bool FitsFixture(int number, ProductStorageType type)
    {
        bool ambient = type == ProductStorageType.Groceries || type == ProductStorageType.Oil
            || type == ProductStorageType.Drinks || type == ProductStorageType.Preserves || type == ProductStorageType.Snacks;
        switch (number)
        {
            case 1: case 2: case 3: return ambient;
            case 4: return ambient || type == ProductStorageType.Produce || type == ProductStorageType.Bakery;
            case 5: return type == ProductStorageType.Bakery;
            case 6: return type == ProductStorageType.Produce;
            case 7: return type == ProductStorageType.Dairy || type == ProductStorageType.Meat || type == ProductStorageType.ReadyMeals;
            default: return false;
        }
    }

    public static void Validate()
    {
        var pack = StoreAssortmentPack.Load();
        Require(pack.Products.Select(p => p.Id).Distinct().Count() == pack.Products.Count, "Duplicate catalogue identities.");
        var trace = new List<string>();
        foreach (var entry in Entries)
        {
            var product = pack.Products.Single(p => p.Id == "product_" + entry.key);
            Require(product.Icon != null && product.ShelfDisplayPrefab != null, "Missing model/icon: " + entry.key);
            Require(product.StorageType.ToString() == entry.storage, "Wrong storage category: " + entry.key);
            Require(product.BuyPrice > 0 && product.SellPrice * product.BoxAmount > product.BuyPrice, "Invalid purchase economics: " + entry.key);
            Require(product.UnitWeightKg > 0 && product.UnitVolumeM3 > 0, "Missing cargo measurements: " + entry.key);
            var mesh = product.ShelfDisplayPrefab.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh.triangles.Length / 3 == entry.triangles, "Imported geometry changed: " + entry.key);
            Require(Mathf.Abs(mesh.bounds.min.y) < .00001f, "Product not seated at Y=0: " + entry.key);
            var fixtures = pack.Fixtures.Where(f => f.id.StartsWith("shelf_fixture_")
                && f.prefab.GetComponent<PlacedShelfStock>().IsProductAllowed(product)).ToArray();
            Require(fixtures.Length > 0, "No fitted equipment: " + entry.key);
            foreach (var fixture in fixtures)
            {
                int number = int.Parse(fixture.id.Split('_')[2]);
                Require(FitsFixture(number, product.StorageType), "Wrong equipment: " + fixture.id + "/" + product.Id);
                Require(fixture.prefab.GetComponent<ShelfProductDisplay>().CompatibleCapacity(product) > 0, "No usable display slots.");
            }
            trace.Add("PASS " + product.Id + "; " + product.StorageType + "; " + entry.triangles + " triangles; "
                + string.Join(",", fixtures.Select(f => f.id)));
        }
        File.WriteAllLines(Report + "/build.txt", new[] { "PASS 96 products; " + pack.Products.Count + " total pack products; " + pack.Fixtures.Count + " fixtures." }.Concat(trace));
        Debug.Log("Food Expansion installed and validated: 96 products.");
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
