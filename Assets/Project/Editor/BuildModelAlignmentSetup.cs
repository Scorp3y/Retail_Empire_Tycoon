using System;
using System.IO;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit calibration of current shop models, excluding the floating stock marker.</summary>
public static class BuildModelAlignmentSetup
{
    public static readonly string[] ItemPaths =
    {
        "Assets/Prefabs/Shelf/ColdPantry_BuildItem.asset",
        "Assets/Prefabs/Shelf/DoubleSidedShelfBuildItem.asset",
        "Assets/Prefabs/Shelf/FreshMarketShelf_BuildItem.asset",
        "Assets/Prefabs/Shelf/RefrigeratedDisplay_BuildItem.asset",
        "Assets/Prefabs/Shelf/WallGoodsShelf_BuildItem.asset",
        "Assets/Prefabs/Structures/Door.asset",
        "Assets/Prefabs/Structures/Wall.asset",
        "Assets/Prefabs/Structures/Wall_Corner.asset"
    };

    public static void Configure(GridSystem grid)
    {
        var report = new StringBuilder();
        foreach (string path in ItemPaths)
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
            if (item == null || item.prefab == null) throw new InvalidOperationException("Missing build model: " + path);
            Bounds bounds = Measure(item.prefab);
            item.placementBounds = bounds;
            item.alignModelToFootprint = true;
            item.footprint = new Vector2Int(Mathf.CeilToInt((bounds.size.x - 0.00001f) / grid.cellSize), Mathf.CeilToInt((bounds.size.z - 0.00001f) / grid.cellSize));
            item.frontFacing = 0; // +Z: doors/front of wall shelf; bread's low serving edge.
            item.twoSidedAccess = path.Contains("DoubleSidedShelf");
            item.accessibilitySides = item.twoSidedAccess ? 2 : 1;
            EditorUtility.SetDirty(item);
            report.AppendLine(item.name + ": " + item.footprint + " bounds=" + bounds + " twoSided=" + item.twoSidedAccess);
        }
        Directory.CreateDirectory("Library/CameraBuildQA");
        File.WriteAllText("Library/CameraBuildQA/alignment.txt", report.ToString());
    }

    public static Bounds Measure(GameObject prefab)
    {
        Bounds result = default;
        bool found = false;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || IsStockMarker(filter.transform, prefab.transform)) continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled) continue;
            Matrix4x4 matrix = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds local = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                if (!found) { result = new Bounds(corner, Vector3.zero); found = true; }
                else result.Encapsulate(corner);
            }
        }
        if (!found) throw new InvalidOperationException("No static model geometry: " + prefab.name);
        return result;
    }

    private static bool IsStockMarker(Transform node, Transform root)
    {
        for (Transform current = node; current != null; current = current.parent)
        {
            if (current.name == "EmptyProductMarker") return true;
            if (current == root) break;
        }
        return false;
    }
}
