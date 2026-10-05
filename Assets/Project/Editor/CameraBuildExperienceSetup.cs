using System;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Wires presentation without replacing shop data, saves or territory configuration.</summary>
public static class CameraBuildExperienceSetup
{
    public const string MaterialFolder = "Assets/Prefabs/BuildPresentation";

    [MenuItem("Retail Empire/Building/Configure camera and placement")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before configuration.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Project/Scenes/Game.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Game scene before configuration.");
        var roots = scene.GetRootGameObjects();
        var camera = roots.SelectMany(r => r.GetComponentsInChildren<MainCamera>(true)).Single();
        var build = roots.SelectMany(r => r.GetComponentsInChildren<BuildController>(true)).Single();
        if (build.preview == null) throw new InvalidOperationException("Build preview reference missing.");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "BuildPresentation");
        ConfigurePreview(build.preview);
        BuildModelAlignmentSetup.Configure(build.grid);
        camera.minY = 1.5f;
        camera.maxY = 18f;
        camera.maxZoomDistance = 25f;
        var visibility = camera.GetComponent<StoreWallVisibility>();
        if (visibility == null) visibility = camera.gameObject.AddComponent<StoreWallVisibility>();
        var settings = new SerializedObject(visibility);
        settings.FindProperty("cameraInput").objectReferenceValue = camera;
        settings.FindProperty("buildController").objectReferenceValue = build;
        settings.ApplyModifiedPropertiesWithoutUndo();
        if (build.gridOverlay != null)
        {
            build.gridOverlay.gridColor = new Color(0f, 0f, 0f, 0.75f);
            build.gridOverlay.fillColor = Color.clear;
            build.gridOverlay.yOffset = 0.11f;
            build.gridOverlay.lineWidth = 0.006f;
        }
        foreach (string path in new[] { "Assets/Prefabs/Structures/Wall.prefab", "Assets/Prefabs/Structures/Wall_Corner.prefab" })
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (contents.GetComponent<StoreWallOccluder>() == null) contents.AddComponent<StoreWallOccluder>();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Camera and placement configured; save format and territory barriers unchanged.");
    }

    public static void ConfigurePreview(BuildPreview preview)
    {
        var settings = new SerializedObject(preview);
        settings.FindProperty("validGhostMaterial").objectReferenceValue = Material("GhostValid", new Color(0.25f, 0.95f, 0.35f, 0.42f));
        settings.FindProperty("invalidGhostMaterial").objectReferenceValue = Material("GhostInvalid", new Color(1f, 0.2f, 0.15f, 0.42f));
        settings.FindProperty("validFootprintMaterial").objectReferenceValue = Material("FootprintValid", new Color(0.2f, 1f, 0.35f, 0.55f));
        settings.FindProperty("invalidFootprintMaterial").objectReferenceValue = Material("FootprintInvalid", new Color(1f, 0.18f, 0.12f, 0.55f));
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material Material(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("URP Unlit shader missing.");
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
