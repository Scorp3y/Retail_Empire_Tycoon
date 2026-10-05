using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Catalog art is rendered from the actual game models, not unrelated stock illustrations.</summary>
public static class ShopUiPreviews
{
    private const string Folder = "Assets/Art/ShopUi/Items";
    [MenuItem("Retail Empire/UI/Render catalog previews")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Render previews in Edit Mode.");
        Directory.CreateDirectory(Folder);
        foreach (var guid in AssetDatabase.FindAssets("t:BuildItemData", new[] { "Assets/Prefabs" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.prefab == null) continue;
            ProductItemData example = null;
            if (item.id == "shelf_fresh_01") example = AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/Bread_Product.asset");
            if (item.id == "shelf_produce_01") example = AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/Assortment/apple_Product.asset");
            item.icon = Render(item.prefab, item.id, item.frontFacing * 90 + 25, example);
            EditorUtility.SetDirty(item);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ProductItemData", new[] { "Assets/Prefabs" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.ShelfDisplayPrefab == null) continue;
            ShopUiSetup.Set(item, "icon", Render(item.ShelfDisplayPrefab, item.Id, 25));
        }
        AssetDatabase.SaveAssets(); Debug.Log("Unified catalog previews rendered from the existing game models.");
    }
    private static Sprite Render(GameObject prefab, string id, float yaw, ProductItemData example = null)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null; Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            model.transform.position = Vector3.zero;
            if (example != null)
            {
                var shelf = model.GetComponent<PlacedShelfStock>();
                shelf.SetStockFromSave(example, shelf.MaxAmount);
                model.GetComponent<ShelfProductDisplay>().Refresh();
            }
            foreach (var component in model.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
            foreach (var marker in model.GetComponentsInChildren<EmptyProductMarker>(true)) marker.gameObject.SetActive(false);
            var renderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No visible model for " + id);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            var cameraRoot = new GameObject("UI preview camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 30;
            Vector3 direction = Quaternion.Euler(0, yaw, 0) * new Vector3(0, .65f, 1).normalized;
            float radius = Mathf.Max(.01f, bounds.extents.magnitude);
            camera.transform.position = bounds.center + direction * radius * 4;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position);
            camera.orthographicSize = radius * 1.12f; camera.nearClipPlane = .001f; camera.farClipPlane = radius * 10 + 1;
            Light(scene, "Key light", new Vector3(40, -35, 0), 1.2f);
            Light(scene, "Fill light", new Vector3(30, 145, 0), .6f);
            target = new RenderTexture(384,384,24,RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            image = new Texture2D(384,384,TextureFormat.RGBA32,false); image.ReadPixels(new Rect(0,0,384,384),0,0); image.Apply();
            if (image.GetPixels32().Count(p => p.a > 32) < 100) throw new InvalidOperationException("Empty preview: " + id);
            string path = Folder + "/" + id + ".png"; File.WriteAllBytes(path, image.EncodeToPNG()); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (image != null) Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void Light(Scene scene, string name, Vector3 rotation, float intensity)
    {
        var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root,scene); root.transform.rotation = Quaternion.Euler(rotation);
        var light = root.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = intensity; light.cullingMask = 1 << 30;
    }
}
