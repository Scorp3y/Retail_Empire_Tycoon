using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Small faceted loaf with scored crust. Changes the existing bread prefab, not its saved product identity.</summary>
public static class ShopBakeryModel
{
    private const string Folder = "Assets/Prefabs/Shelf/Assortment";
    public static void Apply()
    {
        const string path = "Assets/Prefabs/Product/Bread.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.localPosition = Vector3.zero;
            root.GetComponent<MeshFilter>().sharedMesh = Loaf();
            root.GetComponent<MeshRenderer>().sharedMaterial = Material("BreadCrust", new Color(.72f, .39f, .14f));
            Transform cuts = root.transform.Find("Scored crust");
            if (cuts == null)
            {
                cuts = new GameObject("Scored crust", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                cuts.SetParent(root.transform, false);
            }
            cuts.GetComponent<MeshFilter>().sharedMesh = Cuts();
            cuts.GetComponent<MeshRenderer>().sharedMaterial = Material("BreadScore", new Color(.93f, .76f, .43f));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static Mesh Loaf()
    {
        string path = Folder + "/BreadLoaf.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        float[] x = { -.11f, -.095f, -.07f, 0, .07f, .095f, .11f };
        float[] radius = { .04f, .72f, 1, 1, 1, .72f, .04f };
        Vector3 Point(int ring, int side)
        {
            float angle = side * Mathf.PI * 2 / 16;
            return new Vector3(x[ring], .049f + Mathf.Sin(angle) * .049f * radius[ring], Mathf.Cos(angle) * .06f * radius[ring]);
        }
        for (int ring = 0; ring < x.Length - 1; ring++) for (int side = 0; side < 16; side++)
            Quad(vertices, triangles, Point(ring, side), Point(ring + 1, side), Point(ring + 1, side + 1), Point(ring, side + 1));
        // Small end polygons cap the rounded tip instead of leaving the mesh open.
        for (int side = 0; side < 16; side++)
        {
            Triangle(vertices, triangles, new Vector3(x[0], .049f, 0), Point(0, side), Point(0, side + 1));
            Triangle(vertices, triangles, new Vector3(x[x.Length - 1], .049f, 0), Point(x.Length - 1, side + 1), Point(x.Length - 1, side));
        }
        return SaveMesh(path, "Faceted bread loaf", vertices, triangles);
    }
    private static Mesh Cuts()
    {
        string path = Folder + "/BreadScores.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        Vector3 Point(float x, float z) => new Vector3(x + z * .3f,
            .049f + Mathf.Sqrt(1 - z * z / (.06f * .06f)) * .049f + .0018f, z);
        foreach (float x in new[] { -.045f, 0, .045f }) for (int i = 0; i < 8; i++)
        {
            float a = Mathf.Lerp(-.042f, .042f, i / 8f), b = Mathf.Lerp(-.042f, .042f, (i + 1) / 8f);
            Quad(vertices, triangles, Point(x - .004f, a), Point(x - .004f, b), Point(x + .004f, b), Point(x + .004f, a));
        }
        return SaveMesh(path, "Bread crust scores", vertices, triangles);
    }
    private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    { Triangle(vertices, triangles, a, b, c); Triangle(vertices, triangles, a, c, d); }
    private static void Triangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
    }
    private static Mesh SaveMesh(string path, string name, List<Vector3> vertices, List<int> triangles)
    {
        var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static Material Material(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .05f);
        AssetDatabase.CreateAsset(material, path); return material;
    }
}
