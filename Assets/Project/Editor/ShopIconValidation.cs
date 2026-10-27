using System;
using System.IO;
using System.Reflection;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Checks native icon geometry and renders a preview without opening or saving game scenes.</summary>
public static class ShopIconValidation
{
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        const string output = "Library/ShopIconQA";
        Directory.CreateDirectory(output);
        RenderTexture target = null;
        Texture2D preview = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Icon preview camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ShopUiTheme.Paper;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            target = new RenderTexture(620, 340, 24);
            target.Create();
            camera.targetTexture = target;

            var canvas = new GameObject("Icon preview", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label(canvas.transform, font, "Store departments", 24, new Vector2(26, -10), new Vector2(560, 40));
            Row(canvas.transform, font, ShopIcon.Equipment, "Equipment", "Shelves, displays, registers and construction", -62);
            Row(canvas.transform, font, ShopIcon.Products, "Product orders", "Order stock for your shelves", -140);
            Label(canvas.transform, font, "Small-size check: 24 / 32 px", 16, new Vector2(26, -232), new Vector2(560, 30));
            int index = 0;
            foreach (var kind in new[] { ShopIcon.Equipment, ShopIcon.Products })
            foreach (int size in new[] { 24, 32 })
            {
                var graphic = Icon(canvas.transform, kind, new Vector2(30 + index++ * 62, -275), size);
                CheckGeometry(graphic);
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            preview = new Texture2D(620, 340, TextureFormat.RGBA32, false);
            preview.ReadPixels(new Rect(0, 0, 620, 340), 0, 0);
            preview.Apply();
            File.WriteAllBytes(output + "/StoreIcons.png", preview.EncodeToPNG());
            File.WriteAllText(output + "/result.txt", "PASS Equipment and Products: native meshes at 24/32/48 px, tint, bounds, non-square layout, transparent gaps. Native Canvas preview rendered. No game scenes or saves modified.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText(output + "/result.txt", "FAIL " + exception);
            EditorApplication.Exit(1);
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (preview != null) Object.DestroyImmediate(preview);
        }
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Label(Transform parent, Font font, string value, int size, Vector2 position, Vector2 dimensions)
    {
        var text = Rect(parent, value, position, dimensions).gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = ShopUiTheme.Ink;
        text.alignment = TextAnchor.MiddleLeft;
    }

    private static ShopIconGraphic Icon(Transform parent, ShopIcon kind, Vector2 position, int size)
    {
        var graphic = Rect(parent, kind.ToString(), position, new Vector2(size, size)).gameObject.AddComponent<ShopIconGraphic>();
        graphic.Icon = kind;
        graphic.color = ShopUiTheme.Ink;
        graphic.raycastTarget = false;
        return graphic;
    }

    private static void Row(Transform parent, Font font, ShopIcon kind, string title, string subtitle, float y)
    {
        var row = Rect(parent, title, new Vector2(20, y), new Vector2(580, 70));
        row.gameObject.AddComponent<Image>().color = new Color32(232, 235, 216, 255);
        var icon = Icon(row, kind, new Vector2(18, -11), 48);
        CheckGeometry(icon);
        Label(row, font, title, 22, new Vector2(86, -5), new Vector2(480, 30));
        Label(row, font, subtitle, 14, new Vector2(86, -34), new Vector2(480, 26));
    }

    private static void CheckGeometry(ShopIconGraphic graphic)
    {
        var populate = typeof(ShopIconGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { typeof(VertexHelper) }, null);
        Vector2 originalSize = graphic.rectTransform.sizeDelta;
        Color originalColor = graphic.color;
        foreach (var dimensions in new[] { originalSize, new Vector2(64, 32) })
        foreach (var tint in new[] { ShopUiTheme.Ink, new Color(.8f, .3f, .2f, .5f) })
        {
            graphic.rectTransform.sizeDelta = dimensions;
            graphic.color = tint;
            using (var vertices = new VertexHelper())
            {
                populate.Invoke(graphic, new object[] { vertices });
                if (vertices.currentVertCount == 0 || vertices.currentIndexCount % 3 != 0)
                    throw new InvalidOperationException("Empty or invalid icon mesh: " + graphic.Icon);
                for (int i = 0; i < vertices.currentVertCount; i++)
                {
                    var vertex = new UIVertex();
                    vertices.PopulateUIVertex(ref vertex, i);
                    var bounds = graphic.rectTransform.rect;
                    if (float.IsNaN(vertex.position.x) || float.IsNaN(vertex.position.y)
                        || vertex.position.x < bounds.xMin || vertex.position.x > bounds.xMax
                        || vertex.position.y < bounds.yMin || vertex.position.y > bounds.yMax)
                        throw new InvalidOperationException("Icon exceeds its rect: " + graphic.Icon);
                    if (!vertex.color.Equals((Color32)tint))
                        throw new InvalidOperationException("Icon paints background-colored cutouts: " + graphic.Icon);
                }
            }
        }
        graphic.rectTransform.sizeDelta = originalSize;
        graphic.color = originalColor;
    }
}
