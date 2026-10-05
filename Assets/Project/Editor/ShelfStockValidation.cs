using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.Shelves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RetailEmpireTycoon.EditorTools
{
    /// <summary>Checks actual prefab behaviour in an isolated, unsaved Play Mode scene.
    /// Batch entry point: -executeMethod RetailEmpireTycoon.EditorTools.ShelfStockValidation.RunBatch
    /// (omit -quit; the validator exits Unity with a success/failure code).</summary>
    [InitializeOnLoad]
    public static class ShelfStockValidation
    {
        private const string Pending = "RetailEmpire.ShelfStockValidation";
        private const string RestoreScene = "RetailEmpire.ShelfStockValidation.RestoreScene";
        private const string Output = "Library/ShelfStockQA";
        private static readonly string[] Shelves =
        {
            "Fresh Market Shelf", "Double-Sided Shelf", "Wall Goods Shelf", "Cold Pantry", "Refrigerated Display"
        };
        private static readonly string[] Products = { "Bread", "SodaCola", "Oil", "Steak", "MilkBottle" };
        private static readonly string[] BuildAssets =
        {
            "FreshMarketShelf_BuildItem", "DoubleSidedShelfBuildItem", "WallGoodsShelf_BuildItem",
            "ColdPantry_BuildItem", "RefrigeratedDisplay_BuildItem"
        };
        private static readonly List<PlacedShelfStock> Stocks = new List<PlacedShelfStock>();
        private static readonly List<ProductItemData> Items = new List<ProductItemData>();
        private static ProductInventory inventory;
        private static ProductSaveService saves;
        private static GameData saved;
        private static int phase;
        private static int frame;
        private static double started;
        private static bool failed;
        private static bool completed;
        private static Camera camera;

        static ShelfStockValidation()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(RestoreScene, "")))
                EditorApplication.update += RestoreEditorScene;
            if (SessionState.GetBool(Pending, false))
            {
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
                Application.logMessageReceived += WatchErrors;
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run validation in a separate batch Unity session.");
            Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Retail Empire/Products/Validate shelf displays")]
        public static void RunInEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.sceneCount != 1)
                throw new InvalidOperationException("Validation requires Edit Mode with one saved scene open.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Save the current scene before validation.");
            SessionState.SetString(RestoreScene, scene.path);
            Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void RestoreEditorScene()
        {
            if (SessionState.GetBool(Pending, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = SessionState.GetString(RestoreScene, "");
            EditorApplication.update -= RestoreEditorScene;
            SessionState.EraseString(RestoreScene);
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
        }

        private static void WatchErrors(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                failed = true;
        }

        private static void Tick()
        {
            if (completed) return;
            if (EditorApplication.timeSinceStartup - started > 120)
            {
                Finish("FAIL: validation timed out.", 1);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (Time.frameCount == frame) return;
            frame = Time.frameCount;
            try
            {
                switch (phase)
                {
                    case 0:
                        CreateFixture();
                        CheckHighlights();
                        for (int i = 0; i < Stocks.Count; i++)
                        {
                            inventory.Add(Items[i], 1);
                            Require(Stocks[i].RefillFromInventory(inventory, Items[i]), "Refill one: " + Shelves[i]);
                            Require(Stocks[i].CurrentAmount == 1 && inventory.GetCount(Items[i]) == 0, "Stock transfer: " + Shelves[i]);
                            Require(!Stocks[i].CanAccept(Items[(i + 1) % Items.Count]), "Reject incompatible product: " + Shelves[i]);
                        }
                        break;
                    case 1:
                        CheckDisplays(1);
                        for (int i = 0; i < Stocks.Count; i++)
                        {
                            Render(i, "one");
                            inventory.Add(Items[i], Stocks[i].MaxAmount + 6);
                            Require(Stocks[i].RefillFromInventory(inventory, Items[i]), "Fill capacity: " + Shelves[i]);
                            Require(Stocks[i].IsFull && inventory.GetCount(Items[i]) == 7, "Capacity and remainder: " + Shelves[i]);
                            Require(!Stocks[i].RefillFromInventory(inventory, Items[i]), "Reject full shelf: " + Shelves[i]);
                        }
                        saved = new GameData
                        {
                            productInventory = saves.BuildWarehouseSaveData(),
                            shelfStocks = saves.BuildShelfSaveData()
                        };
                        Require(saved.shelfStocks.Count == 5, "Save includes all five shelves.");
                        for (int i = 0; i < Stocks.Count; i++)
                        {
                            Require(Stocks[i].TryTakeOne(out ProductItemData item) && item == Items[i], "Take product: " + Shelves[i]);
                            Require(Stocks[i].CurrentAmount == Stocks[i].MaxAmount - 1, "Take decrements stock: " + Shelves[i]);
                        }
                        break;
                    case 2:
                        CheckDisplays(-1);
                        inventory.Clear();
                        foreach (PlacedShelfStock stock in Stocks) stock.ClearStock();
                        break;
                    case 3:
                        CheckDisplays(0);
                        // Uses the same JSON serializer and ProductSaveService as SaveManager.
                        GameData loaded = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(saved));
                        saves.ApplyWarehouseSaveData(loaded.productInventory);
                        saves.ApplyShelfSaveData(loaded.shelfStocks);
                        break;
                    case 4:
                        CheckDisplays(-1);
                        for (int i = 0; i < Stocks.Count; i++)
                        {
                            Require(Stocks[i].CurrentProduct == Items[i] && Stocks[i].IsFull, "Restore shelf: " + Shelves[i]);
                            Require(inventory.GetCount(Items[i]) == 7, "Restore warehouse: " + Shelves[i]);
                            CheckGeometry(i);
                            Render(i, "full");
                        }
                        Finish(failed ? "FAIL: Unity logged errors." :
                            "PASS: all five shelves transfer stock, reject incompatible/full refills, display one/full stock, update after taking/clearing, and restore warehouse + shelf stock through JSON/ProductSaveService. Geometry fits without overlaps. Highlight edges meet all corners at original, small, rotated and non-uniform scales.", failed ? 1 : 0);
                        return;
                }
                phase++;
            }
            catch (Exception error)
            {
                Finish("FAIL: " + error, 1);
            }
        }

        private static void CreateFixture()
        {
            var systems = new GameObject("Shelf validation systems");
            inventory = systems.AddComponent<ProductInventory>();
            var catalog = systems.AddComponent<ProductCatalog>();
            var settings = new SerializedObject(catalog);
            var entries = settings.FindProperty("products");
            entries.arraySize = Products.Length;
            for (int i = 0; i < Products.Length; i++)
            {
                var item = AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/" + Products[i] + "_Product.asset");
                Require(item != null && item.ShelfDisplayPrefab != null, "Product display prefab: " + Products[i]);
                Items.Add(item);
                entries.GetArrayElementAtIndex(i).objectReferenceValue = item;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            catalog.RebuildCache();
            saves = systems.AddComponent<ProductSaveService>();
            for (int i = 0; i < Shelves.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shelf/" + Shelves[i] + ".prefab");
                GameObject shelf = UnityEngine.Object.Instantiate(prefab, new Vector3(i * 3, 0, 0), Quaternion.identity);
                var placed = shelf.AddComponent<PlacedObject>();
                placed.item = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/" + BuildAssets[i] + ".asset");
                Require(placed.item != null, "Build item: " + Shelves[i]);
                placed.anchorCell = new Vector3Int(i * 3, 0, 0);
                var stock = shelf.GetComponent<PlacedShelfStock>();
                Require(stock != null && shelf.GetComponent<ShelfProductDisplay>() != null, "Shelf components: " + Shelves[i]);
                Stocks.Add(stock);
            }
            camera = new GameObject("Validation camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white * 0.8f;
            var light = new GameObject("Validation light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
        }

        private static void CheckHighlights()
        {
            for (int i = 0; i < Stocks.Count; i++)
            {
                var highlight = Stocks[i].GetComponent<ShelfHighlight>();
                Require(highlight != null, "Missing highlight: " + Shelves[i]);
                highlight.SetVisible(true);
                CheckHighlightEdges(highlight);
                Render(i, "highlight");
                highlight.SetVisible(false);
                Require(highlight.transform.Find("Highlight_Tube_0").gameObject.activeSelf == false,
                    "Highlight does not hide: " + Shelves[i]);
            }

            var fixture = new GameObject("Highlight scale regression");
            try
            {
                fixture.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, 2f);
                var highlight = fixture.AddComponent<ShelfHighlight>();
                Vector3[] scales =
                {
                    Vector3.one, Vector3.one * 0.17f, Vector3.one * 0.72f,
                    new Vector3(0.17f, 0.3f, 0.6f)
                };
                foreach (Vector3 scale in scales)
                {
                    fixture.transform.localScale = scale;
                    fixture.transform.position = new Vector3(-5f, 2f, 3f);
                    foreach (float rotation in new[] { 0f, 37f, 90f })
                    {
                        fixture.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
                        highlight.SetVisible(true);
                        CheckHighlightEdges(highlight);
                    }
                }
            }
            finally { UnityEngine.Object.Destroy(fixture); }
        }

        private static void CheckHighlightEdges(ShelfHighlight highlight)
        {
            Require(highlight.transform.Find("Highlight_Corner_0") == null, "Outline still contains corner spheres.");
            var settings = new SerializedObject(highlight);
            Collider collider = (Collider)settings.FindProperty("boundsCollider").objectReferenceValue;
            Bounds bounds = collider.bounds;
            float padding = settings.FindProperty("padding").floatValue;
            float bottom = bounds.min.y + settings.FindProperty("bottomOffset").floatValue;
            float top = bounds.max.y + settings.FindProperty("topOffset").floatValue;
            float left = bounds.min.x - padding, right = bounds.max.x + padding;
            float back = bounds.min.z - padding, front = bounds.max.z + padding;
            Vector3[] corners =
            {
                new Vector3(left, bottom, back), new Vector3(right, bottom, back),
                new Vector3(right, bottom, front), new Vector3(left, bottom, front),
                new Vector3(left, top, back), new Vector3(right, top, back),
                new Vector3(right, top, front), new Vector3(left, top, front)
            };
            int[] starts = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
            int[] ends = { 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };
            for (int edge = 0; edge < starts.Length; edge++)
            {
                Transform tube = highlight.transform.Find("Highlight_Tube_" + edge);
                Vector3 start = corners[starts[edge]], end = corners[ends[edge]];
                Require(tube != null, "Highlight geometry missing.");
                // Unity's primitive cylinder has local end centres at y = -1/+1.
                Require(Vector3.Distance(tube.TransformPoint(Vector3.down), start) < 0.0001f,
                    "Highlight start disconnected: " + highlight.name + " edge=" + edge);
                Require(Vector3.Distance(tube.TransformPoint(Vector3.up), end) < 0.0001f,
                    "Highlight end disconnected: " + highlight.name + " edge=" + edge);
            }
        }

        private static void CheckDisplays(int amount)
        {
            for (int i = 0; i < Stocks.Count; i++)
            {
                Transform root = Stocks[i].transform.Find("ProductSlots");
                Require(root != null, "Slots root: " + Shelves[i]);
                int count = root.Cast<Transform>().Sum(slot => slot.childCount);
                int expected = Mathf.Min(amount >= 0 ? amount : Stocks[i].CurrentAmount, root.childCount);
                Require(count == expected, "Visual count: " + Shelves[i] + " expected=" + expected + " actual=" + count);
                foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                    Require(!collider.enabled, "Display collider blocks clicks: " + Shelves[i]);
            }
        }

        private static void CheckGeometry(int index)
        {
            // Bread is the existing user-authored layout and is not changed by setup.
            if (index == 0) return;
            Transform root = Stocks[index].transform;
            Transform slotsRoot = root.Find("ProductSlots");
            Require(slotsRoot.childCount == Stocks[index].MaxAmount, "Capacity differs from display slots: " + Shelves[index]);
            int depthRows = slotsRoot.Cast<Transform>().Select(slot => Mathf.RoundToInt(slot.localPosition.z * 10000)).Distinct().Count();
            Require(depthRows >= 3, "Missing depth rows: " + Shelves[index]);
            if (index == 1) Require(slotsRoot.Cast<Transform>().Select(slot => slot.localPosition.x).Distinct().Count() == 11,
                "Cola rows do not span the shelf width.");
            if (index == 3) Require(depthRows == 7, "Meat rows do not fill the tray length.");
            if (index == 4) Require(slotsRoot.Cast<Transform>().Select(slot => slot.localPosition.x).Distinct().Count() == 14,
                "Milk rows do not span both bays.");
            if (index == 3)
            {
                MeshFilter lid = root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(filter => filter.name == "freezer.001");
                Require(lid != null && lid.gameObject.activeInHierarchy, "Freezer doors are missing or hidden.");
                Require(root.Find("ProductTrayLeft") != null && root.Find("ProductTrayRight") != null,
                    "Missing supports under the meat.");
            }
            var bounds = new List<Bounds>();
            foreach (Transform slot in root.Find("ProductSlots"))
            {
                foreach (MeshFilter filter in slot.GetComponentsInChildren<MeshFilter>())
                {
                    Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    Bounds box = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                    foreach (Vector3 vertex in vertices) box.Encapsulate(matrix.MultiplyPoint3x4(vertex));
                    bounds.Add(box);
                }
            }
            for (int a = 0; a < bounds.Count; a++)
                for (int b = a + 1; b < bounds.Count; b++)
                    Require(!bounds[a].Intersects(bounds[b]), "Product overlap: " + Shelves[index]);
            // Dense rows should have only a small gap, measured relative to product width.
            for (int i = 1; i < bounds.Count; i++)
            {
                Bounds previous = bounds[i - 1], current = bounds[i];
                if (Mathf.Abs(previous.center.y - current.center.y) > 0.005f ||
                    Mathf.Abs(previous.center.z - current.center.z) > 0.005f) continue;
                if ((index == 3 || index == 4) && previous.center.x < 0 && current.center.x > 0) continue;
                float gap = current.min.x - previous.max.x;
                Require(gap <= Mathf.Max(previous.size.x, current.size.x) * 0.1f,
                    "Products too far apart: " + Shelves[index] + " gap=" + gap);
            }
            foreach (Bounds box in bounds)
            {
                if (index == 1) Require(box.min.x > -0.35f && box.max.x < 0.35f && Mathf.Abs(box.center.z) > 0.05f && box.min.z > -0.25f && box.max.z < 0.25f, "Cola outside shelf.");
                if (index == 2) Require(box.min.x > -0.35f && box.max.x < 0.35f && box.min.z > -0.3012f && box.max.z < -0.101f, "Oil outside shelf.");
                if (index == 3) Require(box.min.x > -0.35f && box.max.x < 0.35f && box.min.z > -0.25f && box.max.z < 0.25f && Mathf.Abs(box.min.y - 0.260f) < 0.001f && box.max.y < 0.2981f && (box.max.x < -0.025f || box.min.x > 0.025f), "Meat outside tray, above lid, or inside centre divider.");
                if (index == 4) Require(box.min.x > -1.9326f && box.max.x < 1.9326f && box.min.z > -1.6259f && box.max.z < -0.4252f && (box.max.x < -0.128f || box.min.x > 0.143f), "Milk outside bay.");
            }
        }

        private static void Render(int index, string suffix)
        {
            Transform shelf = Stocks[index].transform;
            Vector3 centre = shelf.position + Vector3.up * (index == 3 ? 0.13f : 0.3f);
            if (suffix == "highlight") centre += Vector3.up * 0.06f;
            camera.transform.position = centre + new Vector3(0.95f, 0.75f, 1.15f);
            camera.transform.LookAt(centre);
            camera.orthographicSize = suffix == "highlight" ? 0.55f : 0.43f;
            var texture = new RenderTexture(900, 800, 24);
            var image = new Texture2D(900, 800, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 900, 800), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + Products[index] + "_" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(string message, int exitCode)
        {
            completed = true;
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= WatchErrors;
            File.WriteAllText(Output + "/validation.txt", message);
            Debug.Log(message);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else
            {
                EditorApplication.isPlaying = false;
                if (exitCode != 0) Debug.LogError(message);
            }
        }
    }
}
