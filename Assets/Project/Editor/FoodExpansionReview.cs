using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEngine;

/// <summary>Render stocked examples using actual display prefabs and slot seating.</summary>
public static class FoodExpansionReview
{
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(FoodExpansionIntegration.Report);
        Capture(6, "pumpkin");
        Capture(5, "apple_pie");
        Capture(7, "trout");
        Capture(1, "quinoa");
        Debug.Log("Food Expansion stocked display previews saved.");
    }

    private static void Capture(int number, string key)
    {
        var pack = StoreAssortmentPack.Load();
        var fixture = pack.Fixtures.Single(f => f.id.StartsWith("shelf_fixture_" + number.ToString("D2") + "_"));
        var product = pack.Products.Single(p => p.Id == "product_" + key);
        var sprite = ShopUiPreviews.Render(fixture.prefab, "food_review_" + key, 205, product);
        File.Copy(AssetDatabase.GetAssetPath(sprite), FoodExpansionIntegration.Report + "/display_" + key + ".png", true);
    }
}
