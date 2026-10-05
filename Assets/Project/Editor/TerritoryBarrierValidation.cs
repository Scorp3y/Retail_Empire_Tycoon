using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerritoryBarrierValidation
{
    [MenuItem("Retail Empire/Territory/Validate construction barriers")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Validation requires Edit Mode.");
        var original = SceneManager.GetActiveScene();
        if (original.isDirty) throw new InvalidOperationException("Save the current scene before validation.");
        var sources = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerritoryZone>(true)).ToArray();
        Require(sources.Length == 5, "Expected five zones.");
        var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Directory.CreateDirectory("Library/TerritoryBarrierQA");
        try
        {
            var zones = sources.Select(source =>
            {
                var copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.position, source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                SceneManager.MoveGameObjectToScene(copy, temporary);
                var zone = copy.GetComponent<TerritoryZone>();
                var sourceVisual = new SerializedObject(source).FindProperty("_visual").objectReferenceValue as TerritoryVisual;
                Require(sourceVisual != null, "Missing visual reference.");
                var visualCopy = UnityEngine.Object.Instantiate(sourceVisual.gameObject, sourceVisual.transform.position, sourceVisual.transform.rotation);
                visualCopy.transform.localScale = sourceVisual.transform.lossyScale;
                SceneManager.MoveGameObjectToScene(visualCopy, temporary);
                var settings = new SerializedObject(zone);
                settings.FindProperty("_visual").objectReferenceValue = visualCopy.GetComponent<TerritoryVisual>();
                settings.ApplyModifiedPropertiesWithoutUndo();
                var visualSettings = new SerializedObject(visualCopy.GetComponent<TerritoryVisual>());
                visualSettings.FindProperty("_constructionBarrier").objectReferenceValue = copy.GetComponentInChildren<TerritoryConstructionBarrier>(true);
                visualSettings.ApplyModifiedPropertiesWithoutUndo();
                foreach (Transform child in copy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                foreach (Transform child in visualCopy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                return zone;
            }).ToArray();
            var progression = new GameObject("Isolated progression").AddComponent<StoreProgression>();
            SceneManager.MoveGameObjectToScene(progression.gameObject, temporary);
            foreach (var zone in zones)
            {
                var barrier = zone.GetComponentInChildren<TerritoryConstructionBarrier>(true);
                Require(barrier != null, "Missing barrier: " + zone.Id);
                Require(barrier.GetComponentsInChildren<Collider>(true).Length == 0, "Decorations obstruct clicks.");
                Require(barrier.GetComponentInChildren<TextMesh>(true).text.Contains(zone.Price.ToString("N0").Replace(',', ' ')), "Incorrect price.");
                var label = barrier.GetComponentInChildren<TextMesh>(true);
                Require(Mathf.Abs(label.transform.lossyScale.y - 1f) < 0.01f, "Sign text is distorted.");
                Require(label.GetComponent<Renderer>().bounds.size.x <= 1f, "Sign text exceeds its panel.");
                zone.Bind(progression);
                zone.SetPurchaseMode(false);
                Check(zone, zone.Id == TerritoryId.Purple);
                Require(!zone.CanPurchase(), "Purchase allowed outside purchase mode.");
                zone.SetPurchaseMode(true);
                zone.SetHover(true);
                zone.SetHover(false);
                zone.SetPurchaseMode(false);
            }
            Render(zones.First(z => z.Id == TerritoryId.Purple), temporary, "available");
            foreach (var id in new[] { TerritoryId.Purple, TerritoryId.Red, TerritoryId.Green, TerritoryId.Yellow, TerritoryId.Pink })
            {
                var target = zones.First(z => z.Id == id);
                target.SetPurchaseMode(true);
                Require(target.CanPurchase(), "Progression order changed: " + id);
                progression.MarkPurchased(id);
                foreach (var zone in zones)
                {
                    zone.RefreshView();
                    Check(zone, progression.IsTerritoryAvailable(zone.Id));
                }
                Check(target, false);
            }
            string json = JsonUtility.ToJson(progression.BuildSaveData());
            progression.ApplySaveData(JsonUtility.FromJson<TerritorySaveData>(json));
            foreach (var zone in zones)
            {
                zone.RefreshView();
                Check(zone, false);
                Require(zone.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Purchased collider still enabled.");
            }
            File.WriteAllText("Library/TerritoryBarrierQA/validation.txt",
                "PASS: five barriers, real prices, no decorative colliders, no neon lines, visibility outside purchase mode, five sequential purchases, JSON restoration and purchased collider disabling. Original scene and user game save untouched.");
            Debug.Log("Territory construction barrier validation: PASS.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/TerritoryBarrierQA/validation.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        finally
        {
            EditorSceneManager.CloseScene(temporary, true);
            SceneManager.SetActiveScene(original);
        }
    }

    private static void Check(TerritoryZone zone, bool visible)
    {
        var barrier = zone.GetComponentInChildren<TerritoryConstructionBarrier>(true);
        Require(barrier.transform.Find("Model").gameObject.activeSelf == visible, "Incorrect visibility: " + zone.Id);
        var visual = new SerializedObject(zone).FindProperty("_visual").objectReferenceValue as TerritoryVisual;
        Require(visual.GetComponentsInChildren<LineRenderer>(true).All(line => !line.enabled), "Neon border remains.");
    }

    private static void Render(TerritoryZone zone, Scene scene, string name)
    {
        var model = zone.GetComponentInChildren<TerritoryConstructionBarrier>(true).transform.Find("Model");
        Bounds bounds = model.GetComponent<Renderer>().bounds;
        var camera = new GameObject("Barrier preview camera").AddComponent<Camera>();
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.cullingMask = 1 << 31;
        camera.backgroundColor = new Color(0.27f, 0.39f, 0.28f);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * 0.63f;
        camera.transform.position = bounds.center + new Vector3(-4f, 7f, -10f);
        camera.transform.LookAt(bounds.center);
        var light = new GameObject("Barrier preview light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
        SceneManager.MoveGameObjectToScene(light.gameObject, scene);
        var texture = new RenderTexture(1100, 850, 24);
        var image = new Texture2D(1100, 850, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1100, 850), 0, 0);
            image.Apply();
            File.WriteAllBytes("Library/TerritoryBarrierQA/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
