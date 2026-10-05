using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.HUD;
using RetailEmpireTycoon.UI.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ShopGameplaySetup
{
    [MenuItem("Retail Empire/Shop/Configure gameplay")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Requires a saved scene in Edit Mode.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Project/Scenes/Game.unity") throw new InvalidOperationException("Open Game first.");
        var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Shop gameplay") ?? new GameObject("Shop gameplay");
        var shop = Component<StoreOperations>(root); var work = Component<WorkMinigame>(root);
        var controls = Component<GameplayControls>(root); var hud = Component<ShopOperationsHud>(root);
        var building = UnityEngine.Object.FindObjectOfType<BuildController>(true);
        var cameraInput = UnityEngine.Object.FindObjectOfType<MainCamera>(true);
        var legacyHud = UnityEngine.Object.FindObjectOfType<HUDController>(true);
        var warehouse = UnityEngine.Object.FindObjectOfType<ProductInventory>(true);
        var settings = UnityEngine.Object.FindObjectOfType<ControlPanel>(true);
        var folder = "Assets/Prefabs/ShopGameplay";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "ShopGameplay");
        var balance = AssetDatabase.LoadAssetAtPath<ShopBalance>(folder + "/ShopBalance.asset");
        if (balance == null) { balance = ScriptableObject.CreateInstance<ShopBalance>(); AssetDatabase.CreateAsset(balance, folder + "/ShopBalance.asset"); }
        var canvas = root.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            var uiRoot = new GameObject("Shop gameplay UI", typeof(RectTransform)); uiRoot.transform.SetParent(root.transform, false);
            canvas = uiRoot.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
            var scaler = uiRoot.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f; uiRoot.AddComponent<GraphicRaycaster>();
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder + "/ShopRussianFont.asset");
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font Text/mini.ttf");
            font = TMP_FontAsset.CreateFontAsset(source); font.name = "ShopRussianFont";
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.TryAddCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz —•/:.$!?", out var missing);
            if (!string.IsNullOrEmpty(missing))
            {
                // Use the bundled font with Cyrillic rather than displaying missing-character squares.
                source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Plugins/TextMesh Pro/Fonts/LiberationSans.ttf");
                UnityEngine.Object.DestroyImmediate(font); font = TMP_FontAsset.CreateFontAsset(source); font.name = "ShopRussianFont";
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic; font.TryAddCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz —•/:.$!?", out missing);
            }
            AssetDatabase.CreateAsset(font, folder + "/ShopRussianFont.asset");
            AssetDatabase.AddObjectToAsset(font.material, font); foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
        }
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/2D Casual UI/Sprite/GUI.png").OfType<Sprite>().ToArray();
        Func<string, Sprite> art = name => sprites.First(s => s.name == name);
        var sprite = art("GUI_11");
        foreach (var target in new UnityEngine.Object[] { work, hud })
        { Set(target, "font", font); Set(target, "panelSprite", null); Set(target, "buttonSprite", sprite); Set(target, "canvas", canvas); }
        Set(hud, "activeStar", art("GUI_24")); Set(hud, "inactiveStar", art("GUI_25"));
        Set(hud, "squareButtonSprite", art("GUI_52")); Set(hud, "staffIcon", art("GUI_65")); Set(hud, "controlsIcon", art("GUI_51"));
        Set(hud, "windowSprite", art("GUI_28"));
        Set(hud, "storeWindow", UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopWindow>(true));
        Set(shop, "balance", balance); Set(shop, "money", UnityEngine.Object.FindObjectOfType<MoneyController>(true));
        Set(shop, "warehouse", warehouse); Set(shop, "building", building); Set(shop, "progression", UnityEngine.Object.FindObjectOfType<StoreProgression>(true)); Set(shop, "work", work);
        Set(shop, "employeeModel", VisualCharacter("Man2", folder));
        Set(shop, "registerModel", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Market kit/Models/FBX format/cash-register.fbx"));
        var shopSerialized = new SerializedObject(shop); var models = shopSerialized.FindProperty("customerModels"); models.arraySize = 3;
        string[] modelNames = { "Man1", "Man3", "Girl2" };
        for (int i = 0; i < 3; i++) models.GetArrayElementAtIndex(i).objectReferenceValue = VisualCharacter(modelNames[i], folder);
        shopSerialized.ApplyModifiedPropertiesWithoutUndo();
        var dirt = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Dirt.mat");
        if (dirt == null) { dirt = new Material(Shader.Find("Universal Render Pipeline/Lit")); dirt.SetColor("_BaseColor", new Color(0.32f, 0.22f, 0.08f)); AssetDatabase.CreateAsset(dirt, folder + "/Dirt.mat"); }
        Set(shop, "dirtMaterial", dirt);
        foreach (var spawner in UnityEngine.Object.FindObjectsOfType<AvatarSpawn>(true)) Set(spawner, "shop", shop);
        foreach (var avatar in UnityEngine.Object.FindObjectsOfType<Avatar>(true)) Set(avatar, "shop", shop);
        Set(work, "shop", shop); Set(work, "warehouse", warehouse); Set(work, "worldCamera", cameraInput.GetComponent<Camera>()); Set(work, "cameraInput", cameraInput);
        var legacyCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c => !c.transform.IsChildOf(root.transform)).ToArray();
        foreach (var target in new UnityEngine.Object[] { work, hud })
        {
            var serialized = new SerializedObject(target); var canvases = serialized.FindProperty("legacyCanvases");
            canvases.arraySize = legacyCanvases.Length;
            for (int i = 0; i < legacyCanvases.Length; i++) canvases.GetArrayElementAtIndex(i).objectReferenceValue = legacyCanvases[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Set(hud, "shop", shop); Set(hud, "work", work); Set(hud, "controls", controls); Set(hud, "cameraInput", cameraInput); Set(hud, "settings", settings);
        Set(controls, "hud", legacyHud); Set(controls, "inventoryWindow", UnityEngine.Object.FindObjectOfType<BuildInventoryWindow>(true));
        Set(controls, "territoryMode", UnityEngine.Object.FindObjectOfType<TerritoryPurchaseModeManager>(true)); Set(controls, "settings", settings);
        Set(controls, "pause", UnityEngine.Object.FindObjectOfType<PauseManager>(true)); Set(controls, "cameraInput", cameraInput);
        Set(controls, "building", building); Set(controls, "work", work); Set(controls, "shop", shop); Set(controls, "operationsHud", hud);
        Set(cameraInput, "gameplayControls", controls); Set(building, "gameplayControls", controls);
        Set(UnityEngine.Object.FindObjectOfType<ProductAssignMode>(true), "workMinigame", work);
        Set(UnityEngine.Object.FindObjectOfType<ShelfInfoRaycaster>(true), "workMinigame", work);
        Set(UnityEngine.Object.FindObjectOfType<SaveManager>(true), "shopOperations", shop);
        Set(UnityEngine.Object.FindObjectOfType<GameManager>(true), "wallet", UnityEngine.Object.FindObjectOfType<MoneyController>(true));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Library/ShopGameplayQA");
        File.WriteAllText("Library/ShopGameplayQA/setup.txt", "Configured Game: operations, work games, staffing, controls, Cyrillic UI. Models: " + string.Join(", ", modelNames));
        Debug.Log("Shop gameplay configured. Existing player save was not opened or rewritten.");
    }
    private static T Component<T>(GameObject root) where T : UnityEngine.Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static GameObject VisualCharacter(string name, string folder)
    {
        string output = folder + "/Shop_" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(output);
        if (existing != null) return existing;
        var visual = PrefabUtility.LoadPrefabContents("Assets/Project/Scripts/Human/" + name + ".prefab");
        try
        {
            // Existing pedestrians own a NavMesh/Avatar workflow. Shop actors use dynamic grid routes instead.
            foreach (var transform in visual.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            foreach (var component in visual.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(component);
            foreach (var agent in visual.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) UnityEngine.Object.DestroyImmediate(agent);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var audio in visual.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
            visual.name = "Shop_" + name;
            return PrefabUtility.SaveAsPrefabAsset(visual, output);
        }
        finally { PrefabUtility.UnloadPrefabContents(visual); }
    }
    public static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        if (target == null) throw new InvalidOperationException("Missing component for " + field);
        var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.name + ": missing field " + field);
        property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
