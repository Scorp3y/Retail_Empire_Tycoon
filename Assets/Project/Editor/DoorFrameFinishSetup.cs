using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEngine;

public static class DoorFrameFinishSetup
{
    private const string Materials = DoorCollectionBatch.Root + "/Materials/";

    [MenuItem("Retail Empire/Art/Bind door frame finishes")]
    public static void Apply()
    {
        var palette = CreatePalette();
        foreach (var style in DoorCollectionDesigns.All)
            EditPrefab($"{DoorCollectionBatch.Root}/Prefabs/{style.Id}.prefab", root => BindCollection(root, style.Number, palette));
        foreach (var file in new[] { "Assets/Prefabs/Structures/Door.asset", "Assets/Prefabs/City/unloading_gate_01.asset" })
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(file);
            EditPrefab(AssetDatabase.GetAssetPath(item.prefab), root => BindLegacy(root, palette));
        }
        for (int number = 1; number <= WallCollectionFinishes.Styles.Length; number++)
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildItemData>($"Assets/Resources/WallCollection/wall_finish_{number:00}.asset");
            SetRecommended(item, Recommended(number));
        }
        SetRecommended(AssetDatabase.LoadAssetAtPath<BuildItemData>(
            "Assets/Resources/WallCollection/" + BuildItemData.StoreStripeCornerId + ".asset"), DoorFinish.DarkGreen);
        AssetDatabase.SaveAssets();
        Debug.Log("Bound shared finishes to all 24 collection doors and the two current starter openings.");
    }

    internal static DoorFinish Recommended(int wallNumber) => wallNumber switch
    {
        1 or 3 or 4 or 8 or 17 or 26 or 30 => DoorFinish.DarkGreen,
        2 or 6 or 7 or 9 or 12 or 14 or 22 or 27 => DoorFinish.Cream,
        5 or 15 or 29 => DoorFinish.Bronze,
        _ => DoorFinish.Graphite
    };

    private static void SetRecommended(BuildItemData item, DoorFinish finish)
    {
        if (item.recommendedDoorFinish == finish) return;
        item.recommendedDoorFinish = finish;
        EditorUtility.SetDirty(item);
    }

    private static void EditPrefab(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException("Could not update door frame bindings: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    internal static Material[] CreatePalette()
    {
        var dark = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Dark.mat");
        var cream = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Cream.mat");
        var pine = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Pine.mat");
        if (dark == null || cream == null || pine == null) return null;
        string bronzePath = Materials + "FrameBronze.mat";
        var bronze = AssetDatabase.LoadAssetAtPath<Material>(bronzePath);
        if (bronze == null)
        {
            bronze = new Material(dark) { name = "Frame Bronze" };
            bronze.SetColor("_BaseColor", new Color32(112, 83, 61, 255));
            bronze.SetFloat("_Metallic", .24f);
            bronze.SetFloat("_Smoothness", .28f);
            AssetDatabase.CreateAsset(bronze, bronzePath);
        }
        return new[] { dark, cream, pine, bronze };
    }

    internal static void BindCollection(GameObject root, int number, Material[] palette)
    {
        var frame = root.transform.Find("Frame");
        var renderers = frame.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => IsFixedFrame(renderer.transform, frame));
        Bind(root, renderers, palette, number == 19, material =>
            material.name is "Pine" or "Cream" or "Sage" || number == 26 && material.name == "Steel");
    }

    private static bool IsFixedFrame(Transform node, Transform frame)
    {
        for (var current = node; current != frame; current = current.parent)
            if (current.name.StartsWith("Leaf", StringComparison.Ordinal) || current.name is "Track" or "Drum" or "Sensor")
                return false;
        return true;
    }

    internal static void BindLegacy(GameObject root, Material[] palette)
    {
        if (palette == null) return;
        Bind(root, root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer.name is "LeftJamb" or "RightJamb" or "ContinuousTopBand"), palette, false, _ => true);
    }

    private static void Bind(GameObject root, IEnumerable<MeshRenderer> renderers, Material[] palette, bool corner, Predicate<Material> eligible)
    {
        if (palette == null) throw new InvalidOperationException("The door finish materials are missing.");
        var slots = new List<DoorFrameFinish.Slot>();
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && eligible(materials[i]))
                    slots.Add(new DoorFrameFinish.Slot { renderer = renderer, index = i, original = materials[i] });
        }
        if (slots.Count == 0) throw new InvalidOperationException("No frame surfaces found on " + root.name);
        var finish = root.GetComponent<DoorFrameFinish>() ?? root.AddComponent<DoorFrameFinish>();
        finish.Configure(slots.ToArray(), palette, corner);
    }
}
