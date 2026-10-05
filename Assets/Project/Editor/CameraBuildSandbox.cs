using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reproducible manual smoke-test scene with no economy or player-save components.</summary>
public static class CameraBuildSandbox
{
    private const string Path = "Assets/Project/Editor/CameraBuildSandbox.unity";

    [MenuItem("Retail Empire/Building/Open isolated sandbox")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Requires a saved scene in Edit Mode.");
        var previousBuild = UnityEngine.Object.FindObjectOfType<BuildController>(true);
        Material gridMaterial = previousBuild.gridOverlay.overlayMaterialTemplate;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Sandbox camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(3f, 4f, -5f);
        camera.transform.LookAt(Vector3.zero);
        camera.backgroundColor = new Color(0.2f, 0.3f, 0.25f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.gameObject.AddComponent<AudioListener>();
        var input = camera.gameObject.AddComponent<MainCamera>();
        var light = new GameObject("Sandbox light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Sandbox floor";
        // Reproduce the shop mesh's raised 0.096 floor that used to hide the grid.
        ground.transform.position = new Vector3(0f, 0.046f, 0f);
        ground.transform.localScale = new Vector3(4.1f, 0.1f, 4.1f);
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Territory/Concrete.mat");
        var root = new GameObject("Sandbox building");
        var grid = root.AddComponent<GridSystem>();
        grid.cellSize = 0.1667f;
        var inventory = root.AddComponent<BuildInventory>();
        var territory = root.AddComponent<TerritoryManager>();
        territory.AddPurchasedRect(new Vector3Int(-12, 0, -12), new Vector3Int(11, 0, 11));
        var preview = new GameObject("Sandbox preview").AddComponent<BuildPreview>();
        CameraBuildExperienceSetup.ConfigurePreview(preview);
        var overlay = new GameObject("Sandbox grid").AddComponent<BuildGridOverlay>();
        overlay.grid = grid;
        overlay.territory = territory;
        overlay.overlayMaterialTemplate = gridMaterial;
        overlay.lineWidth = 0.006f;
        var build = root.AddComponent<BuildController>();
        build.grid = grid;
        build.inventory = inventory;
        build.territory = territory;
        build.preview = preview;
        build.gridOverlay = overlay;
        build.worldCamera = camera;
        var walls = camera.gameObject.AddComponent<StoreWallVisibility>();
        var settings = new SerializedObject(walls);
        settings.FindProperty("cameraInput").objectReferenceValue = input;
        settings.FindProperty("buildController").objectReferenceValue = build;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/Wall.prefab");
        for (int i = -3; i <= 3; i++)
            foreach (float z in new[] { -1.8f, 1.8f })
                UnityEngine.Object.Instantiate(wallPrefab, new Vector3(i * 0.5f, 0f, z), Quaternion.identity);
        EditorSceneManager.SaveScene(scene, Path);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        Debug.Log("Isolated sandbox ready. Enter Play Mode, then Building > Begin sandbox placement. No player save or money components exist here.");
    }

    [MenuItem("Retail Empire/Building/Begin sandbox placement")]
    public static void BeginPlacement()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != Path)
            throw new InvalidOperationException("Enter Play Mode in the isolated sandbox first.");
        var build = UnityEngine.Object.FindObjectOfType<BuildController>();
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/WallGoodsShelf_BuildItem.asset");
        build.inventory.Add(item, 3);
        build.EnterBuildMode(item);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }

    [MenuItem("Retail Empire/Building/Record sandbox result")]
    public static void Record()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != Path)
            throw new InvalidOperationException("Sandbox Play Mode required.");
        var build = UnityEngine.Object.FindObjectOfType<BuildController>();
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/WallGoodsShelf_BuildItem.asset");
        int count = UnityEngine.Object.FindObjectsOfType<PlacedObject>().Count(p => p.item == item);
        int hidden = UnityEngine.Object.FindObjectsOfType<StoreWallOccluder>().Count(w => w.GetComponentInChildren<Renderer>().forceRenderingOff);
        Directory.CreateDirectory("Library/CameraBuildQA");
        File.WriteAllText("Library/CameraBuildQA/playmode.txt", $"Sandbox Play Mode: placed={count}, inventory={build.inventory.GetCount(item)}, mode={build.mode}, hiddenWalls={hidden}, camera={build.worldCamera.transform.position}. No player-save components.");
        Debug.Log("Sandbox state recorded.");
    }

    [MenuItem("Retail Empire/Building/Return to Game scene")]
    public static void Return()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path != Path)
            throw new InvalidOperationException("Stop sandbox Play Mode first.");
        EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");
    }
}
