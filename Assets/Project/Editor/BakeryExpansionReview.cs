using System.IO;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Six actual imported model icons with the same camera, not a reused AI illustration.</summary>
public static class BakeryExpansionReview
{
    public static void Capture(string dataRoot = BakeryExpansionBatch.Data, string[] keys = null,
        string heading = "BAKERY & DESSERTS", string footer = "ACTUAL UNITY MODELS  /  BAKERY SUPPLIER  /  EXISTING BAKERY RACK",
        string output = "Docs/BakeryExpansion/Models.png")
    {
        keys ??= BakeryExpansionBatch.Keys;
        var scene = EditorSceneManager.NewPreviewScene(); var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RenderTexture target = null; Texture2D image = null; var previous = RenderTexture.active;
        try
        {
            void Text(string label, float x, float y, float size, float maxWidth)
            {
                var root = new GameObject(label); SceneManager.MoveGameObjectToScene(root, scene); root.layer = 30;
                root.transform.position = new Vector3(x, y, -.1f);
                var text = root.AddComponent<TextMesh>(); text.text = label; text.font = font; text.fontSize = 80;
                text.characterSize = size; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.14f, .27f, .21f);
                var renderer = text.GetComponent<Renderer>(); renderer.sharedMaterial = font.material;
                if (renderer.bounds.size.x > maxWidth) root.transform.localScale *= maxWidth / renderer.bounds.size.x;
            }
            Text(heading, 0, 3.7f, .12f, 11);
            for (int i = 0; i < keys.Length; i++)
            {
                var product = AssetDatabase.LoadAssetAtPath<ProductItemData>(dataRoot + "/Products/" + keys[i] + ".asset");
                float x = (i % 3 - 1) * 4; float y = i < 3 ? 1.85f : -1.35f;
                var root = new GameObject(product.Id); SceneManager.MoveGameObjectToScene(root, scene); root.layer = 30;
                root.transform.position = new Vector3(x, y, 0);
                var picture = root.AddComponent<SpriteRenderer>(); picture.sprite = product.Icon;
                root.transform.localScale = Vector3.one * (3.15f / product.Icon.bounds.size.x);
                Text(product.DisplayName, x, y - 1.40f, .052f, 3.4f);
            }
            Text(footer, 0, -3.65f, .041f, 11.7f);
            var cameraRoot = new GameObject("Bakery preview camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 4.15f; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.96f, .94f, .88f); camera.cullingMask = 1 << 30;
            target = new RenderTexture(2100, 1400, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target; image = new Texture2D(2100, 1400, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 2100, 1400), 0, 0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (image != null) Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
