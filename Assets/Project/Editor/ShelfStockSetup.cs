using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;

namespace RetailEmpireTycoon.EditorTools
{
    public static class ShelfStockSetup
    {
        private static readonly string[] ShelfNames =
        {
            "Double-Sided Shelf", "Wall Goods Shelf", "Cold Pantry", "Refrigerated Display"
        };

        // Coordinates are in shelf-local space, measured from the imported mesh.
        // The cola model has a centred pivot; its slot height includes the base offset.
        [MenuItem("Retail Empire/Products/Configure shelf displays")]
        public static void ConfigureDisplays()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before configuring shelf prefabs.");
            Configure("Double-Sided Shelf", "SodaCola", 0.55f,
                Rows(new[] { 0.175f, 0.55f }, 11, -0.310f, 0.310f,
                    new[] { -0.211f, -0.148f, -0.085f, 0.085f, 0.148f, 0.211f }), 0.1927f * 0.55f);
            Configure("Wall Goods Shelf", "Oil", 0.5f,
                Rows(new[] { 0.175f, 0.55f }, 12, -0.297f, 0.297f,
                    new[] { -0.153f, -0.210f, -0.267f }), 0f);
            Configure("Cold Pantry", "Steak", 0.3f,
                Rows(new[] { 0.258f }, new[] { -0.2855f, -0.1875f, -0.0895f, 0.0895f, 0.1875f, 0.2855f },
                    new[] { -0.192f, -0.128f, -0.064f, 0f, 0.064f, 0.128f, 0.192f }), 0.0043f * 0.3f);
            Configure("Refrigerated Display", "MilkBottle", 1.35f,
                Rows(new[] { 0.733f, 1.1945f, 2.019f, 2.7323f },
                    new[] { -1.68f, -1.465f, -1.25f, -1.035f, -0.82f, -0.605f, -0.39f,
                        0.39f, 0.605f, 0.82f, 1.035f, 1.25f, 1.465f, 1.68f },
                    new[] { -0.70f, -0.92f, -1.14f }), 0f);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured four shelf displays. Existing bread display retained.");
        }

        private static List<Vector3> Rows(float[] heights, int columns, float left, float right, float[] depths)
        {
            var horizontal = new float[columns];
            for (int column = 0; column < columns; column++)
                horizontal[column] = Mathf.Lerp(left, right, (float)column / (columns - 1));
            return Rows(heights, horizontal, depths);
        }

        private static List<Vector3> Rows(float[] heights, float[] horizontal, float[] depths)
        {
            var positions = new List<Vector3>();
            foreach (float height in heights)
                foreach (float depth in depths)
                    foreach (float x in horizontal)
                        positions.Add(new Vector3(x, height, depth));
            return positions;
        }

        private static void Configure(string shelfName, string productName, float scale,
            List<Vector3> positions, float pivotOffset)
        {
            ProductItemData product = AssetDatabase.LoadAssetAtPath<ProductItemData>(
                "Assets/Prefabs/Product/" + productName + "_Product.asset");
            GameObject productPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Product/" + productName + ".prefab");
            if (product == null || productPrefab == null)
                throw new InvalidOperationException("Missing product assets: " + productName);
            var productSettings = new SerializedObject(product);
            productSettings.FindProperty("shelfDisplayPrefab").objectReferenceValue = productPrefab;
            productSettings.ApplyModifiedPropertiesWithoutUndo();

            string path = "Assets/Prefabs/Shelf/" + shelfName + ".prefab";
            GameObject shelf = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var stock = shelf.GetComponent<PlacedShelfStock>();
                if (stock == null || !stock.IsProductAllowed(product))
                    throw new InvalidOperationException("Shelf does not accept product: " + shelfName);
                // The imported lids are frames with openings, not solid panels.
                // Keep them while placing stock on the tray floor underneath.
                if (shelfName == "Cold Pantry")
                {
                    foreach (MeshFilter filter in shelf.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.name == "freezer.001") filter.gameObject.SetActive(true);
                    ConfigureMeatTrays(shelf);
                }
                // ExactAmount displays one model per stocked item. Capacity must
                // match the new multi-row layout so a full shelf fills every row.
                var stockSettings = new SerializedObject(stock);
                stockSettings.FindProperty("maxAmount").intValue = positions.Count;
                stockSettings.ApplyModifiedPropertiesWithoutUndo();
                Transform slotsRoot = shelf.transform.Find("ProductSlots");
                if (slotsRoot == null)
                {
                    slotsRoot = new GameObject("ProductSlots").transform;
                    slotsRoot.SetParent(shelf.transform, false);
                }
                // This menu owns only ProductSlots, never the model or shelf state.
                for (int i = slotsRoot.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(slotsRoot.GetChild(i).gameObject);
                foreach (Vector3 position in positions)
                {
                    Transform slot = new GameObject("Slot_" + slotsRoot.childCount.ToString("D2")).transform;
                    slot.SetParent(slotsRoot, false);
                    slot.localPosition = position + Vector3.up * (pivotOffset + 0.002f);
                    if (shelfName == "Double-Sided Shelf" && position.z < 0f)
                        slot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                ShelfProductDisplay display = shelf.GetComponent<ShelfProductDisplay>();
                if (display == null) display = shelf.AddComponent<ShelfProductDisplay>();
                var settings = new SerializedObject(display);
                settings.FindProperty("shelfStock").objectReferenceValue = stock;
                settings.FindProperty("slotsRoot").objectReferenceValue = slotsRoot;
                settings.FindProperty("slots").arraySize = 0;
                settings.FindProperty("fillMode").enumValueIndex = 0;
                settings.FindProperty("productScale").floatValue = scale;
                settings.FindProperty("clearSlotBeforeSpawn").boolValue = true;
                settings.FindProperty("disableSpawnedColliders").boolValue = true;
                settings.FindProperty("useSlotRotation").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(shelf, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(shelf); }
        }

        [MenuItem("Retail Empire/Products/Inspect shelf meshes")]
        public static void InspectMeshes()
        {
            var report = new StringBuilder();
            foreach (string name in ShelfNames.Concat(new[] { "Fresh Market Shelf" }))
                InspectPrefab("Assets/Prefabs/Shelf/" + name + ".prefab", report, true);
            foreach (string name in new[] { "Bread", "SodaCola", "Oil", "Steak", "MilkBottle" })
                InspectPrefab("Assets/Prefabs/Product/" + name + ".prefab", report, false);
            Directory.CreateDirectory("Library/ShelfStockQA");
            File.WriteAllText("Library/ShelfStockQA/meshes.txt", report.ToString());
            Debug.Log("Shelf mesh inspection completed.");
        }

        private static void ConfigureMeatTrays(GameObject shelf)
        {
            const string materialPath = "Assets/Prefabs/Shelf/FreezerTray.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Missing URP Lit shader for freezer trays.");
                material = new Material(shader) { name = "FreezerTray" };
                material.SetColor("_BaseColor", new Color(0.48f, 0.51f, 0.53f));
                material.SetFloat("_Smoothness", 0.2f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            // Raised inserts support the meat below the lid without leaving it
            // floating or hidden behind the deep freezer walls.
            foreach (float side in new[] { -1f, 1f })
            {
                string name = side < 0 ? "ProductTrayLeft" : "ProductTrayRight";
                Transform tray = shelf.transform.Find(name);
                if (tray == null)
                {
                    GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    model.name = name;
                    UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
                    tray = model.transform;
                    tray.SetParent(shelf.transform, false);
                }
                tray.localPosition = new Vector3(side * 0.1875f, 0.211f, 0f);
                tray.localRotation = Quaternion.identity;
                tray.localScale = new Vector3(0.325f, 0.094f, 0.45f);
                tray.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static void InspectPrefab(string path, StringBuilder report, bool surfaces)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                report.AppendLine(path + " scale=" + root.transform.localScale.ToString("F4"));
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null) continue;
                    Matrix4x4 matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Vector3[] vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                    if (vertices.Length == 0) continue;
                    Bounds bounds = new Bounds(vertices[0], Vector3.zero);
                    foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
                    report.AppendLine("  " + filter.name + " min=" + bounds.min.ToString("F4") + " max=" + bounds.max.ToString("F4"));
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        report.AppendLine("    materials=" + string.Join(", ", renderer.sharedMaterials.Select(m => m == null ? "null" : m.name + " (" + m.shader.name + ")")));
                    if (!surfaces || filter.GetComponentInParent<RectTransform>() != null) continue;
                    int[] indices = mesh.triangles;
                    var planes = new Dictionary<float, List<Vector3>>();
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                        if (Mathf.Abs(a.y - b.y) > 0.001f || Mathf.Abs(a.y - c.y) > 0.001f) continue;
                        if (Vector3.Cross(b - a, c - a).y <= 0.00001f) continue;
                        float height = (float)Math.Round(a.y, 3);
                        if (!planes.TryGetValue(height, out List<Vector3> points)) planes[height] = points = new List<Vector3>();
                        points.AddRange(new[] { a, b, c });
                    }
                    foreach (var plane in planes.OrderBy(p => p.Key))
                        report.AppendLine("    surface y=" + plane.Key + " x=" + plane.Value.Min(v => v.x) + ".." + plane.Value.Max(v => v.x) + " z=" + plane.Value.Min(v => v.z) + ".." + plane.Value.Max(v => v.z));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
