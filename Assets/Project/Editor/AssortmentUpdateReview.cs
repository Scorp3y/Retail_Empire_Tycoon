using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Approval sheet composed from the exact imported model previews used by the in-game catalogue.</summary>
public static class AssortmentUpdateReview
{
    public static void Capture()
    {
        var pack = StoreAssortmentPack.Load();
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null; Texture2D result = null; var previous = RenderTexture.active;
        try
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            void Text(string content, float x, float y, float size, float maximumWidth = 19, TextAnchor anchor = TextAnchor.MiddleCenter)
            {
                var obj = new GameObject(content); SceneManager.MoveGameObjectToScene(obj, scene); obj.layer = 30;
                obj.transform.position = new Vector3(x, y, -.1f);
                var text = obj.AddComponent<TextMesh>(); text.text = content; text.font = font; text.fontSize = 64;
                text.characterSize = size; text.anchor = anchor; text.color = new Color(.14f, .27f, .21f);
                text.GetComponent<Renderer>().sharedMaterial = font.material;
                float width = text.GetComponent<Renderer>().bounds.size.x;
                if (width > maximumWidth) obj.transform.localScale *= maximumWidth / width;
            }
            void Picture(Sprite sprite, float x, float y, float width, float height)
            {
                var obj = new GameObject(sprite.name); SceneManager.MoveGameObjectToScene(obj, scene); obj.layer = 30;
                obj.transform.position = new Vector3(x, y, 0);
                var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                obj.transform.localScale = Vector3.one * Mathf.Min(width / sprite.bounds.size.x, height / sprite.bounds.size.y);
            }
            Text("NEW STORE ASSORTMENT", 0, 6.05f, .11f);
            Text("48 PRODUCTS  /  8 CATEGORIES  /  ACTUAL UNITY MODELS", 0, 5.61f, .048f);
            var headings = new[] { "FRESH PRODUCE", "DAIRY", "MEAT & SEAFOOD", "PANTRY", "SNACKS & SWEETS", "DRINKS & HOT BREWS", "FROZEN FOOD", "HOUSEHOLD & CARE" };
            for (int category = 0; category < 8; category++)
            {
                float cx = -7.5f + category % 4 * 5; float top = 5.08f - category / 4 * 3.8f;
                Text((category + 1).ToString("D2") + "  " + headings[category], cx, top, .066f, 4.5f);
                for (int i = 0; i < 6; i++)
                {
                    var product = pack.Products[category * 6 + i]; float x = cx + (i % 3 - 1) * 1.58f; float y = top - .88f - i / 3 * 1.55f;
                    Picture(product.Icon, x, y, 1.36f, 1.16f);
                    Text(product.DisplayName, x, y - .69f, .045f, 1.43f);
                }
            }
            Text("NEW STOCKABLE FIXTURES", 0, -2.85f, .105f);
            for (int i = 0; i < 4; i++)
            {
                float x = -7.5f + i * 5; Picture(pack.Fixtures[i].icon, x, -4.15f, 4.5f, 2.15f);
                Text(pack.Fixtures[i].displayName.ToUpperInvariant(), x, -5.44f, .075f, 4.5f);
            }
            Text("Stock arrives through suppliers and deliveries. Display contents follow real shelf inventory.", 0, -5.95f, .06f);
            var cameraRoot = new GameObject("Assortment review camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -20); camera.orthographic = true; camera.orthographicSize = 6.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.95f, .93f, .86f); camera.cullingMask = 1 << 30;
            target = new RenderTexture(3200, 2000, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            result = new Texture2D(3200, 2000, TextureFormat.RGB24, false); result.ReadPixels(new Rect(0, 0, 3200, 2000), 0, 0); result.Apply();
            Directory.CreateDirectory("Docs/AssortmentUpdate"); File.WriteAllBytes("Docs/AssortmentUpdate/Models.png", result.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (result != null) Object.DestroyImmediate(result);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
