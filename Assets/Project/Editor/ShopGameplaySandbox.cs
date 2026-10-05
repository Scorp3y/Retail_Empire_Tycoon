using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>A copy of Game with player-save and tutorial-writing components removed for safe play tests.</summary>
public static class ShopGameplaySandbox
{
    public const string Path = "Assets/Project/Editor/ShopGameplaySandbox.unity";
    [MenuItem("Retail Empire/Shop/Open isolated gameplay sandbox")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Requires a saved Game scene in Edit Mode.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Project/Scenes/Game.unity") throw new InvalidOperationException("Open Game first.");
        EditorSceneManager.SaveScene(scene, Path, true);
        scene = EditorSceneManager.OpenScene(Path);
        foreach (var component in UnityEngine.Object.FindObjectsOfType<SaveManager>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in UnityEngine.Object.FindObjectsOfType<TutorialController>(true))
        {
            if (component.tutorialChoiceUI != null) component.tutorialChoiceUI.SetActive(false);
            if (component.tutorialUI != null) component.tutorialUI.SetActive(false);
            if (component.characterRoot != null) component.characterRoot.SetActive(false);
            UnityEngine.Object.DestroyImmediate(component);
        }
        foreach (var component in UnityEngine.Object.FindObjectsOfType<TutorialDialogue>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in UnityEngine.Object.FindObjectsOfType<ButtonFadeIn>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var spawner in UnityEngine.Object.FindObjectsOfType<AvatarSpawn>(true)) spawner.enabled = false;
        foreach (var canvasGroup in UnityEngine.Object.FindObjectsOfType<CanvasGroup>(true)) { canvasGroup.alpha = 1; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
        foreach (var fader in UnityEngine.Object.FindObjectsOfType<ScreenFader>(true)) fader.SetInstant(0);
        EditorSceneManager.SaveScene(scene);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        Debug.Log("Shop sandbox ready. Player save components and tutorial preference writers are absent. Enter Play Mode.");
    }
    [MenuItem("Retail Empire/Shop/Prepare sandbox activities")]
    public static void PrepareActivities()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != Path) throw new InvalidOperationException("Use sandbox Play Mode.");
        var build = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.BuildSystem.BuildController>();
        var controls = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.GameplayControls>(); controls.CloseWindows();
        foreach (var fader in UnityEngine.Object.FindObjectsOfType<ScreenFader>(true)) fader.SetInstant(0);
        if (UnityEngine.Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.PlacedShelfStock>().Length == 0)
        {
            foreach (string name in new[] { "DoubleSidedShelfBuildItem", "RefrigeratedDisplay_BuildItem", "ColdPantry_BuildItem" })
            {
                var item = AssetDatabase.LoadAssetAtPath<RetailEmpireTycoon.Core.BuildItemData>("Assets/Prefabs/Shelf/" + name + ".asset");
                build.inventory.Add(item, 1); build.EnterBuildMode(item); bool placed = false;
                foreach (var rect in build.territory.PurchasedRects)
                {
                    for (int x = rect.min.x + 2; x < rect.max.x - 3 && !placed; x++)
                        for (int z = rect.min.z + 2; z < rect.max.z - 3 && !placed; z++)
                            placed = build.TryPlaceAt(new Vector3Int(x, 0, z));
                    if (placed) break;
                }
                if (!placed) throw new InvalidOperationException("Could not place QA furniture: " + name);
            }
            build.ExitBuildMode();
        }
        var warehouse = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.Products.ProductInventory>();
        var cola = AssetDatabase.LoadAssetAtPath<RetailEmpireTycoon.Core.ProductItemData>("Assets/Prefabs/Product/SodaCola_Product.asset");
        warehouse.Add(cola, 30);
        Debug.Log("Sandbox has real shelf prefabs and temporary warehouse stock. Player save remains untouched.");
    }
    [MenuItem("Retail Empire/Shop/Load player layout read only %#F6")]
    public static void LoadPlayerLayout()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != Path) throw new InvalidOperationException("Use sandbox Play Mode.");
        string path = System.IO.Path.Combine(Application.persistentDataPath, "gameData.json");
        if (!File.Exists(path)) throw new FileNotFoundException("No existing layout to preview.", path);
        var data = JsonUtility.FromJson<GameData>(File.ReadAllText(path));
        var shop = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();
        shop.StartCoroutine(ApplyLayout(data));
    }
    private static System.Collections.IEnumerator ApplyLayout(GameData data)
    {
        var controls = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.GameplayControls>(); controls.CloseWindows();
        UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.WorkMinigame>().Cancel();
        var build = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.BuildSystem.BuildController>(); build.ExitBuildMode();
        var progression = UnityEngine.Object.FindObjectOfType<StoreProgression>(); progression.ApplySaveData(data.territory);
        UnityEngine.Object.FindObjectOfType<StorePrefabSpawner>().Spawn(progression.State.CurrentLevel);
        var catalog = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.BuildSystem.BuildItemCatalog>();
        build.ApplyPlacedSaveData(data.placedObjects, catalog);
        UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.BuildSystem.FloorPainter>().ApplySaveData(data.floorTiles, catalog);
        yield return null; yield return null;
        var products = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.SaveSystem.ProductSaveService>();
        products.ApplyWarehouseSaveData(data.productInventory); products.ApplyShelfSaveData(data.shelfStocks);
        UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.Economy.MoneyController>().SetMoney(data.playerMoney);
        UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>().ApplySaveData(data.shopOperations);
        foreach (var fader in UnityEngine.Object.FindObjectsOfType<ScreenFader>(true)) fader.SetInstant(0);
        Debug.Log("Player layout loaded into sandbox READ ONLY. No SaveManager exists in this scene.");
    }
    [MenuItem("Retail Empire/Shop/Preview player restocking %#F8")]
    public static void PreviewRestocking()
    {
        PrepareActivities();
        var shelf = UnityEngine.Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.PlacedShelfStock>().First(s => s.ShelfType == RetailEmpireTycoon.Core.ShelfStorageType.DoubleSided);
        var product = AssetDatabase.LoadAssetAtPath<RetailEmpireTycoon.Core.ProductItemData>("Assets/Prefabs/Product/SodaCola_Product.asset");
        shelf.ClearStock();
        if (!UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.WorkMinigame>().BeginRestock(shelf, product))
            throw new InvalidOperationException("Could not preview player restocking.");
    }
    [MenuItem("Retail Empire/Shop/Return to Game")]
    public static void Return()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path != Path)
            throw new InvalidOperationException("Stop sandbox Play Mode first.");
        EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");
    }
    [MenuItem("Retail Empire/Shop/Preview milk restocking %#F11")]
    public static void PreviewMilk() { PreviewProduct("MilkBottle_Product"); }
    [MenuItem("Retail Empire/Shop/Preview bread restocking")]
    public static void PreviewBread() { PreviewProduct("Bread_Product"); }
    private static void PreviewProduct(string name)
    {
        PrepareActivities();
        var work = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.WorkMinigame>(); work.Cancel();
        var product = AssetDatabase.LoadAssetAtPath<RetailEmpireTycoon.Core.ProductItemData>("Assets/Prefabs/Product/" + name + ".asset");
        var shelf = UnityEngine.Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.PlacedShelfStock>().First(s => s.IsProductAllowed(product));
        shelf.ClearStock(); UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.Products.ProductInventory>().Add(product, 5);
        if (!work.BeginRestock(shelf, product)) throw new InvalidOperationException("Cannot preview " + name);
    }
    [MenuItem("Retail Empire/Shop/Record runtime diagnostics %#F9")]
    public static void Record()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != Path) throw new InvalidOperationException("Use sandbox Play Mode.");
        var shop = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();
        var build = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.BuildSystem.BuildController>();
        Directory.CreateDirectory("Library/ShopGameplayQA");
        File.WriteAllText("Library/ShopGameplayQA/runtime.txt", $"enabled={shop.enabled}, level={shop.Level}, rating={shop.Rating}, open={shop.IsOpen}, visits={shop.Visits.Count}, checkout={shop.CheckoutPosition}, notice={shop.Notice}\nRects={build.territory.PurchasedRects.Count}, shelves=" + string.Join("; ", UnityEngine.Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.PlacedShelfStock>().Select(s => $"{s.name} pos={s.transform.position} stock={s.CurrentAmount}"))
            + "\nDoors=" + string.Join("; ", UnityEngine.Object.FindObjectsOfType<RetailEmpireTycoon.BuildSystem.PlacedObject>().Where(p => p.item != null && p.item.isDoorway).Select(p => $"{p.name} at {p.transform.position}, cells={p.occupiedCells.Count}")));
        var tray = GameObject.Find("Product tray");
        if (tray != null)
        {
            var rect = (RectTransform)tray.transform; var point = rect.TransformPoint(rect.rect.center);
            var raycasts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var system = UnityEngine.EventSystems.EventSystem.current;
            system.RaycastAll(new UnityEngine.EventSystems.PointerEventData(system) { position = point }, raycasts);
            File.AppendAllText("Library/ShopGameplayQA/runtime.txt", $"\nScreen={Screen.width}x{Screen.height}; mouse={Input.mousePosition}; tray={point}, drag={tray.GetComponent<RetailEmpireTycoon.StoreOperations.WorkProductDrag>().LastEvent}\nUI hits="
                + string.Join("; ", raycasts.Select(r => r.gameObject.name))
                + "\nTarget=" + GameObject.Find("Work target 0").transform.position
                + "\nRaycasters=" + string.Join("; ", UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.GraphicRaycaster>().Select(r => $"{r.name}: {r.enabled}")));
        }
    }
}
