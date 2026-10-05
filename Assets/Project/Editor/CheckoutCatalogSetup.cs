using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds the existing register model to the build/shop/save catalogs without replacing their entries.</summary>
public static class CheckoutCatalogSetup
{
    private const string ItemPath = "Assets/Prefabs/Structures/CashRegister.asset";
    private const string PrefabPath = "Assets/Prefabs/Structures/CashRegister.prefab";

    [MenuItem("Retail Empire/Shop/Configure checkout catalog %#F5")]
    public static void Configure()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != "Assets/Project/Scenes/Game.unity")
            throw new InvalidOperationException("Requires saved Game scene in Edit Mode.");
        var building = UnityEngine.Object.FindObjectOfType<BuildController>(true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Market kit/Models/FBX format/cash-register.fbx");
            if (model == null) throw new InvalidOperationException("Existing cash-register model is missing.");
            var root = new GameObject("Cash register");
            try
            {
                var visual = UnityEngine.Object.Instantiate(model, root.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * .45f;
                var bounds = Measure(root);
                visual.transform.localPosition -= Vector3.up * bounds.min.y;
                bounds = Measure(root);
                var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                root.AddComponent<PlacedObject>();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(ItemPath);
        if (item == null)
        {
            var previewPath = "Assets/Models/Market kit/Previews/cash-register.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(previewPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            item = ScriptableObject.CreateInstance<BuildItemData>();
            item.id = "cash_register_01"; item.displayName = "Cash Register";
            item.description = "For customer checkout"; item.price = 300;
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(previewPath);
            item.prefab = prefab; item.category = BuildCategory.Structures; item.isCheckout = true;
            item.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap | PlacementRuleFlags.RequireAccessibility;
            item.alignModelToFootprint = true; item.placementBounds = Measure(prefab);
            item.footprint = new Vector2Int(Mathf.CeilToInt(item.placementBounds.size.x / building.grid.cellSize),
                Mathf.CeilToInt(item.placementBounds.size.z / building.grid.cellSize));
            var template = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Wall.asset");
            item.previewValidMaterial = template.previewValidMaterial; item.previewInvalidMaterial = template.previewInvalidMaterial;
            AssetDatabase.CreateAsset(item, ItemPath);
        }
        foreach (var window in UnityEngine.Object.FindObjectsOfType<ShopWindow>(true)) Append(window, "buildCatalog", item);
        foreach (var catalog in UnityEngine.Object.FindObjectsOfType<BuildItemCatalog>(true)) Append(catalog, "items", item);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Checkout catalog configured: Structures, $300, " + item.footprint + ". Existing entries and player save preserved.");
    }

    private static Bounds Measure(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Register has no renderer.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static void Append(UnityEngine.Object target, string field, BuildItemData item)
    {
        var serialized = new SerializedObject(target); var entries = serialized.FindProperty(field);
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
        entries.arraySize++;
        entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = item;
        serialized.ApplyModifiedProperties();
    }

    [MenuItem("Retail Empire/Shop/Validate checkout catalog in sandbox %#F4")]
    public static void Validate()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != ShopGameplaySandbox.Path)
            throw new InvalidOperationException("Requires sandbox Play Mode.");
        ShopGameplaySandbox.PrepareActivities();
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(ItemPath);
        var shop = UnityEngine.Object.FindObjectOfType<StoreOperations>();
        var building = UnityEngine.Object.FindObjectOfType<BuildController>();
        var money = UnityEngine.Object.FindObjectOfType<MoneyController>();
        var catalog = UnityEngine.Object.FindObjectOfType<BuildItemCatalog>();
        var window = UnityEngine.Object.FindObjectOfType<ShopWindow>(true);
        var wallet = money.Money; var stock = building.inventory.BuildSaveData();
        PlacedObject placed = null;
        try
        {
            Require(item != null && item.isCheckout && item.category == BuildCategory.Structures && item.icon != null, "Checkout asset missing data.");
            Require(catalog.GetById(item.id) == item, "Checkout missing from save catalog.");
            Require(item.footprint.x * building.grid.cellSize >= item.placementBounds.size.x
                && item.footprint.y * building.grid.cellSize >= item.placementBounds.size.z, "Checkout exceeds its grid footprint.");
            window.gameObject.SetActive(true); window.OpenCategory_Structures();
            Require(window.CategoryTabsHost.GetComponentsInChildren<Button>(true).Length == 2, "Staff still exists in build categories.");
            var card = window.GetComponentsInChildren<ShopItemCard>().FirstOrDefault(c => c.nameText.text == ShopText.Item(item));
            Require(card != null && card.icon.sprite == item.icon, "Checkout shop card is missing.");
            int amount = building.inventory.GetCount(item);
            money.SetMoney(item.price - 1); card.buyButton.onClick.Invoke();
            Require(building.inventory.GetCount(item) == amount && money.Money == item.price - 1, "Unaffordable purchase changed stock/money.");
            money.SetMoney(item.price); card.buyButton.onClick.Invoke();
            Require(building.inventory.GetCount(item) == amount + 1 && money.Money == 0, "Checkout purchase did not debit once and add inventory.");
            var saved = building.inventory.BuildSaveData(); building.inventory.ApplySaveData(saved, catalog);
            Require(building.inventory.GetCount(item) == amount + 1, "Checkout inventory save round trip failed.");
            window.Close(); shop.Advance(.1f); building.EnterBuildMode(item); shop.Advance(.1f);
            bool installed = false;
            foreach (var rect in building.territory.PurchasedRects)
            {
                for (int x = rect.min.x + 2; x < rect.max.x - item.footprint.x - 2 && !installed; x++)
                    for (int z = rect.min.z + 2; z < rect.max.z - item.footprint.y - 2 && !installed; z++)
                    {
                        var cell = new Vector3Int(x, 0, z);
                        if (!building.CanPlaceAt(cell).ok) continue;
                        Vector3 pose = BuildPlacementPose.Position(building.grid, item, cell, false, 0);
                        Vector3 front = pose + new Vector3(item.placementBounds.center.x, 0, item.placementBounds.max.z + building.grid.cellSize * 2);
                        if (!shop.Navigation.TryNearest(front, out var point) || (point - shop.EntrancePosition).magnitude < 1.05f
                            || !shop.Navigation.TryPath(shop.EntrancePosition, point, out _)) continue;
                        installed = building.TryPlaceAt(cell);
                    }
                if (installed) break;
            }
            Require(installed, "No accessible checkout placement in sandbox.");
            placed = UnityEngine.Object.FindObjectsOfType<PlacedObject>().First(p => p.item == item);
            Physics.SyncTransforms(); building.ExitBuildMode(); shop.Advance(.1f); shop.Advance(.1f);
            Require(shop.CheckoutObject == placed, "Customers did not switch to purchased checkout.");
            Require(shop.Navigation.TryPath(shop.ArrivalPosition, shop.CheckoutPosition, out _, true), "Placed checkout unreachable from street.");
            Require(building.BuildPlacedSaveData().Any(p => p.itemId == item.id), "Placed checkout missing from save data.");
            window.gameObject.SetActive(true); window.ShowMainTabs();
            var staff = window.MainTabsHost.GetComponentsInChildren<Button>().FirstOrDefault(b =>
                b.GetComponentsInChildren<ShopIconGraphic>().Any(icon => icon.Icon == ShopIcon.Staff));
            Require(staff != null, "Main store lost staff button.");
            staff.onClick.Invoke(); Require(window.IsStaffView, "Main staff button does not open staff."); window.Close();
            Directory.CreateDirectory("Library/ShopGameplayQA");
            File.WriteAllText("Library/ShopGameplayQA/checkout.txt", "PASS Structures card/icon, two build tabs, main staff action, unaffordable/paid purchase, inventory save round trip, grid placement, footprint, active checkout, outdoor route and placed save entry.");
            Debug.Log("Checkout catalog validation passed. Player save untouched.");
        }
        finally
        {
            window.Close();
            if (placed != null) { building.grid.Release(placed.occupiedCells); UnityEngine.Object.DestroyImmediate(placed.gameObject); }
            Physics.SyncTransforms(); building.EnterBuildMode(item); shop.Advance(.1f); building.ExitBuildMode(); shop.Advance(.1f); shop.Advance(.1f);
            building.inventory.ApplySaveData(stock, catalog); money.SetMoney(wallet);
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
