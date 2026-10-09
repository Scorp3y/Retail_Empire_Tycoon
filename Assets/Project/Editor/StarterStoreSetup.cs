using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.Territory;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Authors the new-game snapshot, not the player's live layout or save.</summary>
public static class StarterStoreSetup
{
    public const string Folder = "Assets/Prefabs/StarterStore";
    public const string BlueprintPath = Folder + "/StarterStore.asset";
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != CityUpdateSetup.GamePath)
            throw new InvalidOperationException("Open the saved Game scene in Edit Mode.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        PrepareFloorPatterns();
        var build = Object.FindObjectOfType<BuildController>(true);
        var land = Object.FindObjectOfType<TerritoryPlotLayout>(true);
        var blueprint = AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(BlueprintPath);
        if (blueprint == null) { blueprint = ScriptableObject.CreateInstance<StarterStoreBlueprint>(); AssetDatabase.CreateAsset(blueprint, BlueprintPath); }
        var start = build.grid.WorldToCell(new Vector3(land.initialBounds.xMin + .001f, 0, land.initialBounds.yMin + .001f));
        var state = new GameData { usesModularStore = true };
        void Add(string id, int x, int z, int facing = 0, string product = null, int amount = 0, bool playerParking = false)
        {
            var entry = new PlacedBuildSaveData { itemId = id, x = start.x + x, z = start.z + z, facing = facing, rotated = facing % 2 != 0, wallModuleVersion = BuildItemData.CurrentWallModuleVersion, playerParking = playerParking };
            state.placedObjects.Add(entry);
            if (product != null) state.shelfStocks.Add(new ShelfStockSaveEntry { buildItemId = id, anchorX = entry.x, anchorZ = entry.z, facing = facing, rotated = entry.rotated, productId = product, amount = amount });
        }
        for (int x = 0; x < 90; x++) for (int z = 0; z < 72; z++)
        {
            string style = x < 20 ? "floor_concrete" : "floor_cream";
            if (x >= 24 && x < 49 && z >= 12 && z < 23) style = "floor_wood";
            if (x < 2 || x >= 88 || z < 2 || z >= 70) style = "floor_graphite";
            state.floorTiles.Add(new FloorTileSaveData { itemId = style, x = start.x + x, z = start.z + z });
        }
        for (int x = -24; x < 0; x++) for (int z = 0; z < 24; z++)
            state.floorTiles.Add(new FloorTileSaveData { itemId = "asphalt_01", x = start.x + x, z = start.z + z });
        for (int x = 1; x < 89; x++)
        {
            if (x < 54 || x >= 60) Add("wall_01", x, 0);
            Add("wall_01", x, 71);
        }
        for (int z = 1; z < 71; z++)
        {
            if (z < 56 || z >= 62) Add("wall_01", 0, z, 1);
            Add("wall_01", 89, z, 1);
            if (z < 12 || z >= 18) Add("wall_01", 18, z, 1);
        }
        Add("wallcorner_01", 0, 0); Add("wallcorner_01", 89, 0, 3);
        Add("wallcorner_01", 89, 71, 2); Add("wallcorner_01", 0, 71, 1);
        Add("door_01", 56, 0); // Main entrance, blue edge in the reference.
        Add("door_01", 18, 14, 1); // Staff doorway into the separated warehouse.
        Add("unloading_gate_01", 0, 56, 1); // Mandatory dock on the outer warehouse wall.
        foreach (int z in new[] { 24, 40, 56 }) Add("storage_rack_01", 6, z);
        Add("parking_01", -16, 8, 3, playerParking: true);
        Add("cash_register_01", 64, 6, 2);
        Add("shelf_fresh_01", 26, 14, 2, "product_bread", 54);
        Add("shelf_produce_01", 42, 14, 2, "product_apple", 30);
        Add("shelf_doublesided_01", 32, 32, 0, "product_sodacola", 40);
        Add("shelf_doublesided_01", 52, 32, 0, "product_juice", 30);
        Add("shelf_refrigerated_01", 76, 22, 3, "product_milk", 20);
        Add("shelf_wallgoods_01", 78, 50, 3, "product_oil", 30);
        Add("shelf_cold_01", 58, 52, 2, "product_steak", 20);
        Add("decor_plant", 24, 5); Add("decor_plant", 84, 5);
        Add("decor_bench", 80, 62); Add("decor_bin", 82, 8);
        blueprint.initialState = state;
        blueprint.serviceYard = new Rect(land.initialBounds.xMin - 24 * build.grid.cellSize, land.initialBounds.yMin, 24 * build.grid.cellSize, 24 * build.grid.cellSize);
        blueprint.pickupPosition = build.grid.CellToWorld(start + new Vector3Int(-6, 0, 10));
        blueprint.pickupEuler = new Vector3(0, 270, 0);
        blueprint.cameraFocus = new Vector3(land.initialBounds.center.x, 0, land.initialBounds.center.y);
        EditorUtility.SetDirty(blueprint);
        var service = GameObject.Find("City and delivery services");
        var initializer = service.GetComponent<StarterStoreInitialization>() ?? service.AddComponent<StarterStoreInitialization>();
        initializer.blueprint = blueprint; initializer.land = land; initializer.cityTrip = service.GetComponent<CityTrip>();
        initializer.building = build; initializer.floors = Object.FindObjectOfType<FloorPainter>(true); initializer.catalog = Object.FindObjectOfType<BuildItemCatalog>(true);
        initializer.cameraInput = Object.FindObjectOfType<MainCamera>(true);
        ShopUiSetup.Set(Object.FindObjectOfType<SaveManager>(true), "starterStore", initializer);
        var editor = build.GetComponent<BuildEditingController>() ?? build.gameObject.AddComponent<BuildEditingController>();
        editor.building = build; editor.controls = Object.FindObjectOfType<GameplayControls>(true); editor.deliveries = service.GetComponent<DeliveryOrders>();
        editor.productAssignment = Object.FindObjectOfType<RetailEmpireTycoon.Shelves.ProductAssignMode>(true); editor.work = Object.FindObjectOfType<WorkMinigame>(true);
        var hud = service.GetComponent<BuildEditorHud>() ?? service.AddComponent<BuildEditorHud>();
        hud.editor = editor; hud.uiRoot = initializer.cityTrip.uiRoot;
        var versionLabel = service.GetComponent<GameVersionLabel>() ?? service.AddComponent<GameVersionLabel>();
        versionLabel.uiRoot = initializer.cityTrip.uiRoot;
        var inventory = Object.FindObjectOfType<RetailEmpireTycoon.UI.Windows.BuildInventoryWindow>(true);
        hud.warehouseWindow = inventory.transform;
        var content = ((GameObject)ShopUiSetup.Read(inventory, "mainCategoriesPanel")).GetComponent<RectTransform>();
        content.offsetMin = new Vector2(content.offsetMin.x, 96);
        CreateLandscapeStages(Object.FindObjectOfType<StorePrefabSpawner>(true));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Starter store and build editor configured without touching player saves.");
    }

    private static void CreateLandscapeStages(StorePrefabSpawner spawner)
    {
        var serialized = new SerializedObject(spawner);
        var original = serialized.FindProperty("prefabs");
        var modular = serialized.FindProperty("modularPrefabs");
        modular.arraySize = original.arraySize;
        for (int i = 0; i < original.arraySize; i++)
        {
            var entry = original.GetArrayElementAtIndex(i);
            var source = (GameObject)entry.FindPropertyRelative("prefab").objectReferenceValue;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var area = instance.GetComponentInChildren<StoreBuildArea>(true);
                foreach (Transform child in instance.transform.Cast<Transform>().ToArray())
                {
                    bool holdsArea = area != null && area.areaRects.Any(c => c != null && (c.transform == child || c.transform.IsChildOf(child)));
                    if (child.name != "Trees - Unpurchased territory" && !holdsArea) Object.DestroyImmediate(child.gameObject);
                }
                instance.name = "Modular store landscape " + i;
                var target = modular.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("level").enumValueIndex = entry.FindPropertyRelative("level").enumValueIndex;
                target.FindPropertyRelative("prefab").objectReferenceValue = PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/Landscape" + i + ".prefab");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PrepareFloorPatterns()
    {
        foreach (string id in new[] { "floor_cream", "floor_graphite", "floor_checker", "floor_wood", "floor_concrete", "floor_terrazzo" })
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreUpgradeSetup.Folder + "/" + id + "_Pattern.asset");
            if (texture == null) throw new InvalidOperationException("Missing floor pattern: " + id);
            if (texture.mipmapCount == 1)
            {
                var mipmapped = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, true) { name = texture.name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat };
                mipmapped.SetPixels32(texture.GetPixels32()); mipmapped.Apply(true);
                EditorUtility.CopySerialized(mipmapped, texture); Object.DestroyImmediate(mipmapped);
            }
            texture.filterMode = FilterMode.Trilinear;
            EditorUtility.SetDirty(texture);
        }
    }
}
