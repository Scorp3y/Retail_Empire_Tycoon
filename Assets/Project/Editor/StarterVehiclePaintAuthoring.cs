using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEngine;

public static class StarterVehiclePaintAuthoring
{
    [MenuItem("Tools/Retail Empire Tycoon/Auto Works/Rebuild starter paint mesh")]
    public static void Build()
    {
        var pickup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/Pickup.prefab");
        var original = pickup.GetComponentsInChildren<MeshFilter>(true)
            .Single(filter => filter.name == VehiclePaint.StarterRenderer).sharedMesh;
        var uv = original.uv;
        var indices = original.triangles;
        var trim = new List<int>();
        var paint = new List<int>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            var center = (uv[indices[i]] + uv[indices[i + 1]] + uv[indices[i + 2]]) / 3f;
            // Only the orange body swatch. Headlamps and dark trim use other atlas tiles.
            bool body = center.x >= .25f && center.x < .375f && center.y >= .625f && center.y < .75f;
            var section = body ? paint : trim;
            for (int j = 0; j < 3; j++) section.Add(indices[i + j]);
        }
        if (paint.Count == 0 || trim.Count == 0) throw new InvalidOperationException("Starter paint atlas layout changed.");

        var mesh = new Mesh {name = "StarterPaintBody", indexFormat = original.indexFormat};
        mesh.vertices = original.vertices;
        mesh.normals = original.normals;
        mesh.tangents = original.tangents;
        mesh.uv = uv;
        mesh.uv2 = original.uv2;
        mesh.colors = original.colors;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(trim, 0);
        mesh.SetTriangles(paint, 1);
        mesh.bounds = original.bounds;
        Directory.CreateDirectory("Assets/Resources/VehicleTuning");
        AssetDatabase.Refresh();
        TownBakeryPlacement.SaveAsset(mesh, "Assets/Resources/" + VehiclePaint.StarterMesh + ".asset");
        AssetDatabase.SaveAssets();
        Debug.Log($"Starter paint: {paint.Count / 3} body and {trim.Count / 3} trim triangles.");
    }

    public static void BuildBatch()
    {
        try { TownWorldSafety.RequireIsolatedEditor(); Build(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
