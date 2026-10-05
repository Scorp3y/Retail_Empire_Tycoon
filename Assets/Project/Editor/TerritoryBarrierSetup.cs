using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Bakes construction borders from the existing plot outlines.</summary>
public static class TerritoryBarrierSetup
{
    private const string Folder = "Assets/Prefabs/Territory";
    // Only the fence height changes; plot boundaries and the readable sign stay fixed.
    private const float FenceHeightScale = 0.8f;

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");
        Configure();
        TerritoryBarrierValidation.Run();
        string result = File.ReadAllText("Library/TerritoryBarrierQA/validation.txt");
        EditorApplication.Exit(result.StartsWith("PASS:") ? 0 : 1);
    }

    [MenuItem("Retail Empire/Territory/Configure construction barriers")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before configuring territory barriers.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Project/Scenes/Game.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Game scene before configuring barriers.");
        var zones = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TerritoryZone>(true)).ToArray();
        if (zones.Length != 5) throw new InvalidOperationException("Expected five territory zones.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        Material[] materials =
        {
            CreateMaterial("Concrete", new Color(0.36f, 0.40f, 0.41f)),
            CreateMaterial("WarningYellow", new Color(1f, 0.72f, 0.035f)),
            CreateMaterial("WarningBlack", new Color(0.035f, 0.045f, 0.045f)),
            CreateMaterial("SignCream", new Color(1f, 0.94f, 0.72f))
        };
        foreach (var zone in zones) Build(zone, materials);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Configured five construction barriers; prices, click planes and progression retained.");
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Build(TerritoryZone zone, Material[] materials)
    {
        var visual = new SerializedObject(zone).FindProperty("_visual").objectReferenceValue as TerritoryVisual;
        if (visual == null) throw new InvalidOperationException("Missing visual: " + zone.name);
        var visualSettings = new SerializedObject(visual);
        var line = visualSettings.FindProperty("_border").objectReferenceValue as LineRenderer;
        if (line == null || line.positionCount != 4) throw new InvalidOperationException("Expected rectangle: " + zone.name);
        var points = new Vector3[4];
        line.GetPositions(points);
        if (!line.useWorldSpace)
            for (int i = 0; i < points.Length; i++) points[i] = line.transform.TransformPoint(points[i]);
        float left = points.Min(p => p.x) + 0.10f, right = points.Max(p => p.x) - 0.10f;
        float front = points.Min(p => p.z) + 0.10f, back = points.Max(p => p.z) - 0.10f;
        float ground = points.Min(p => p.y) - 0.10f;
        Transform root = zone.transform.Find("ConstructionBarrier");
        if (root == null)
        {
            root = new GameObject("ConstructionBarrier").transform;
            root.SetParent(zone.transform, false);
        }
        Transform old = root.Find("Model");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        Transform model = new GameObject("Model").transform;
        model.SetParent(root, false);
        var geometry = new BarrierGeometry(model);
        float gateCenter = (left + right) * 0.5f;
        float gateHalf = Mathf.Min(0.60f, (right - left) * 0.15f);
        geometry.Fence(new Vector3(left, ground, back), new Vector3(right, ground, back));
        geometry.Fence(new Vector3(left, ground, front), new Vector3(left, ground, back));
        geometry.Fence(new Vector3(right, ground, front), new Vector3(right, ground, back));
        geometry.Fence(new Vector3(left, ground, front), new Vector3(gateCenter - gateHalf, ground, front));
        geometry.Fence(new Vector3(gateCenter + gateHalf, ground, front), new Vector3(right, ground, front));
        geometry.Rail(new Vector3(gateCenter - gateHalf, ground + 0.36f * FenceHeightScale, front), new Vector3(gateCenter + gateHalf, ground + 0.36f * FenceHeightScale, front), 0.30f * FenceHeightScale);
        float signX = Mathf.Min(gateCenter + gateHalf + 0.76f, right - 0.76f);
        Vector3 sign = new Vector3(signX, ground + 1.14f, front + 0.12f);
        geometry.Box(sign, new Vector3(1.42f, 0.72f, 0.08f), 0);
        geometry.Box(sign + Vector3.back * 0.046f, new Vector3(1.31f, 0.61f, 0.012f), 3);
        foreach (float offset in new[] { -0.60f, 0.60f })
            geometry.Box(new Vector3(signX + offset, ground + 0.57f, sign.z + 0.02f), new Vector3(0.10f, 1.14f, 0.10f), 0);
        geometry.Box(sign + new Vector3(-0.46f, -0.055f, -0.058f), new Vector3(0.24f, 0.21f, 0.018f), 1);
        foreach (float offset in new[] { -0.08f, 0.08f })
            geometry.Box(sign + new Vector3(-0.46f + offset, 0.10f, -0.060f), new Vector3(0.035f, 0.13f, 0.018f), 2);
        geometry.Box(sign + new Vector3(-0.46f, 0.165f, -0.060f), new Vector3(0.19f, 0.035f, 0.018f), 2);
        geometry.Box(sign + new Vector3(-0.46f, -0.055f, -0.070f), new Vector3(0.026f, 0.07f, 0.018f), 2);
        string meshPath = Folder + "/Barrier_" + zone.Id + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Barrier_" + zone.Id };
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        geometry.Apply(mesh);
        EditorUtility.SetDirty(mesh);
        model.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = model.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        var textObject = new GameObject("PurchaseSignText");
        textObject.transform.SetParent(model, false);
        textObject.transform.position = sign + new Vector3(0.16f, 0f, -0.061f);
        textObject.transform.rotation = Quaternion.identity;
        // The existing zone rotates and scales its axes differently. Compensate
        // after aligning the text, using its own world axes rather than the mesh's.
        Vector3 textScale = textObject.transform.lossyScale;
        textObject.transform.localScale = new Vector3(1f / textScale.x, 1f / textScale.y, 1f / textScale.z);
        var text = textObject.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.characterSize = 0.023f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(0.08f, 0.09f, 0.10f);
        textObject.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        var barrier = root.GetComponent<TerritoryConstructionBarrier>();
        if (barrier == null) barrier = root.gameObject.AddComponent<TerritoryConstructionBarrier>();
        var settings = new SerializedObject(barrier);
        settings.FindProperty("_model").objectReferenceValue = model.gameObject;
        settings.FindProperty("_fenceRenderer").objectReferenceValue = renderer;
        settings.FindProperty("_priceLabel").objectReferenceValue = text;
        settings.ApplyModifiedPropertiesWithoutUndo();
        barrier.SetPrice(zone.Price);
        visualSettings.FindProperty("_constructionBarrier").objectReferenceValue = barrier;
        visualSettings.ApplyModifiedPropertiesWithoutUndo();
        line.enabled = false;
        model.gameObject.SetActive(zone.Id == TerritoryId.Purple);
    }

    // One baked renderer per plot, shared materials, no colliders blocking clicks.
    private sealed class BarrierGeometry
    {
        private readonly Transform root;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        public BarrierGeometry(Transform root) { this.root = root; }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, bool both = false)
        {
            int start = vertices.Count;
            foreach (Vector3 point in new[] { a, b, c, d }) vertices.Add(root.InverseTransformPoint(point));
            triangles[material].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            if (both) triangles[material].AddRange(new[] { start + 2, start + 1, start, start + 3, start + 2, start });
        }

        public void Box(Vector3 center, Vector3 size, int material)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                center + new Vector3(-h.x,-h.y,-h.z), center + new Vector3(h.x,-h.y,-h.z),
                center + new Vector3(h.x,h.y,-h.z), center + new Vector3(-h.x,h.y,-h.z),
                center + new Vector3(-h.x,-h.y,h.z), center + new Vector3(h.x,-h.y,h.z),
                center + new Vector3(h.x,h.y,h.z), center + new Vector3(-h.x,h.y,h.z)
            };
            Quad(p[3],p[2],p[1],p[0],material); Quad(p[4],p[5],p[6],p[7],material);
            Quad(p[0],p[4],p[7],p[3],material); Quad(p[2],p[6],p[5],p[1],material);
            Quad(p[7],p[6],p[2],p[3],material); Quad(p[0],p[1],p[5],p[4],material);
        }

        public void Fence(Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b);
            Vector3 dir = (b - a).normalized;
            Vector3 size = Mathf.Abs(dir.x) > 0.5f ? new Vector3(length,0.12f * FenceHeightScale,0.20f) : new Vector3(0.20f,0.12f * FenceHeightScale,length);
            Box((a+b)*0.5f + Vector3.up*0.06f * FenceHeightScale, size, 0);
            int sections = Mathf.Max(1, Mathf.CeilToInt(length / 1.65f));
            for (int i = 0; i <= sections; i++)
            {
                Vector3 point = Vector3.Lerp(a,b,(float)i/sections);
                Box(point + Vector3.up*0.37f * FenceHeightScale,new Vector3(0.14f,0.74f * FenceHeightScale,0.14f),0);
                Box(point + Vector3.up*0.11f * FenceHeightScale,new Vector3(0.25f,0.14f * FenceHeightScale,0.25f),0);
                Box(point + Vector3.up*0.75f * FenceHeightScale,new Vector3(0.18f,0.05f * FenceHeightScale,0.18f),0);
            }
            Rail(a+Vector3.up*0.31f * FenceHeightScale,b+Vector3.up*0.31f * FenceHeightScale,0.085f * FenceHeightScale);
            Rail(a+Vector3.up*0.60f * FenceHeightScale,b+Vector3.up*0.60f * FenceHeightScale,0.085f * FenceHeightScale);
        }

        public void Rail(Vector3 a, Vector3 b, float height)
        {
            float length = Vector3.Distance(a,b);
            Vector3 dir = (b-a).normalized;
            Vector3 normal = Vector3.Cross(dir,Vector3.up);
            Box((a+b)*0.5f, Mathf.Abs(dir.x)>0.5f ? new Vector3(length,height,0.035f) : new Vector3(0.035f,height,length),1);
            for (float x = -height; x < length; x += 0.26f)
                foreach (float side in new[] {-1f,1f})
                {
                    float bottom0=Mathf.Clamp(x,0,length), bottom1=Mathf.Clamp(x+0.13f,0,length);
                    float top0=Mathf.Clamp(x+height,0,length), top1=Mathf.Clamp(x+height+0.13f,0,length);
                    Vector3 offset=normal*side*0.018f;
                    Quad(a+dir*bottom0-Vector3.up*height*0.5f+offset,
                        a+dir*bottom1-Vector3.up*height*0.5f+offset,
                        a+dir*top1+Vector3.up*height*0.5f+offset,
                        a+dir*top0+Vector3.up*height*0.5f+offset,2,true);
                }
        }

        public void Apply(Mesh mesh)
        {
            mesh.Clear(); mesh.SetVertices(vertices); mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
    }
}
