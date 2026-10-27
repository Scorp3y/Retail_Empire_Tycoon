using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports three isolated art candidates, never changes the live customer roster or player save.</summary>
[InitializeOnLoad]
public static class ShopperStyleStudyReview
{
    private const string Root = "Assets/Art/ShopperStyleStudy";
    private const string Evidence = "Library/ShopperStyleStudyQA";
    private const string Pending = "ShopperStyleStudy.Pending";
    private static IEnumerator review;
    private static double deadline;
    static ShopperStyleStudyReview() { EditorApplication.update += Tick; }

    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Evidence);
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Root + "/StudyPalette.png");
        textureImporter.sRGBTexture = true; textureImporter.mipmapEnabled = false;
        textureImporter.wrapMode = TextureWrapMode.Clamp; textureImporter.filterMode = FilterMode.Bilinear;
        textureImporter.textureCompression = TextureImporterCompression.Uncompressed; textureImporter.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Study.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, Root + "/Study.mat");
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/StudyPalette.png"));
        material.SetFloat("_Smoothness", .05f);
        for (int i = 1; i <= 3; i++) Import(Model(i));
        var clips = AssetDatabase.LoadAllAssetsAtPath(Model(1)).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Study.controller");
        if (controller == null)
        {
        controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "/Study.controller");
        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isBrowsing", AnimatorControllerParameterType.Bool);
        foreach (var clip in clips)
        {
            var state = controller.layers[0].stateMachine.AddState(clip.name); state.motion = clip;
            if (clip.name == "Idle") controller.layers[0].stateMachine.defaultState = state;
            if (clip.name == "Walk") state.speed = 2;
            var transition = controller.layers[0].stateMachine.AddAnyStateTransition(state);
            transition.hasExitTime = false; transition.duration = .1f; transition.canTransitionToSelf = false;
            transition.AddCondition(clip.name == "Walk" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "isWalking");
            if (clip.name != "Walk") transition.AddCondition(clip.name == "Browse" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "isBrowsing");
        }
        }
        var prefabs = Enumerable.Range(1, 3).Select(i => Prefab(i, material, controller)).ToArray();
        AssetDatabase.SaveAssets();
        ShopperFleetBatch.ValidateAssets(prefabs);
        ShopperFleetReview.CaptureStudy(prefabs);
        File.Copy(ShopperFleetBatch.Evidence + "/StyleStudy.png", Evidence + "/UnityStudy.png", true);
        ShopperFleetReview.CaptureMotion(prefabs[0]);
        File.Copy(ShopperFleetBatch.Evidence + "/UnityMotion.png", Evidence + "/UnityMotion.png", true);
        EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");
        ShopGameplaySandbox.Open();
        foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }

    private static string Model(int i) => Root + "/Study_" + i.ToString("00") + ".fbx";
    private static string PrefabPath(int i) => Root + "/Study_" + i.ToString("00") + ".prefab";
    private static void Import(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true; importer.optimizeGameObjects = false; importer.isReadable = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importNormals = ModelImporterNormals.Import; importer.importLights = importer.importCameras = false;
        importer.SaveAndReimport();
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = new[] { "Idle", "Walk", "Browse" }.Single(n => clip.name.Contains(n));
            clip.loopTime = true; clip.loopPose = true; clip.keepOriginalPositionXZ = true; clip.keepOriginalOrientation = true;
        }
        importer.clipAnimations = clips; importer.SaveAndReimport();
    }

    private static GameObject Prefab(int number, Material material, AnimatorController controller)
    {
        var root = new GameObject("Study_" + number.ToString("00"));
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model(number)));
            visual.transform.SetParent(root.transform, false); visual.name = "Visual";
            var marker = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "FacingMarker");
            visual.transform.localRotation = Quaternion.Euler(0, marker.position.z < 0 ? 180 : 0, 0);
            var animator = visual.GetComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.Rebind(); controller.animationClips.Single(c => c.name == "Idle").SampleAnimation(visual, 0);
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(); skin.sharedMaterial = material; skin.quality = SkinQuality.Bone1;
            var baked = new Mesh(); skin.BakeMesh(baked, false);
            var points = baked.vertices.Select(p => root.transform.InverseTransformPoint(skin.transform.TransformPoint(p))).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            Object.DestroyImmediate(baked); root.AddComponent<ShopperAppearance>().Configure(number, bounds);
            foreach (var clip in controller.animationClips)
            {
                var bones = visual.GetComponentsInChildren<Transform>();
                clip.SampleAnimation(visual, clip.length * .2f); var rotations = bones.Select(b => b.localRotation).ToArray();
                clip.SampleAnimation(visual, clip.length * .65f);
                if (!bones.Where((b, i) => Quaternion.Angle(b.localRotation, rotations[i]) > .5f).Any())
                    throw new InvalidOperationException("Static animation: " + root.name + " / " + clip.name);
            }
            controller.animationClips.Single(c => c.name == "Idle").SampleAnimation(visual, 0);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(number));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (review == null) { deadline = EditorApplication.timeSinceStartup + 150; review = InGame(); }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Study review timed out.");
            if (review.MoveNext()) return;
            SessionState.SetBool(Pending, false);
            File.WriteAllText(Evidence + "/result.txt", "PASS three isolated candidates: valid rigs, Idle/Walk/Browse motion, fitted in real Game lighting. Live roster and player save untouched.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            SessionState.SetBool(Pending, false); Debug.LogException(error);
            File.WriteAllText(Evidence + "/result.txt", "FAIL " + error); EditorApplication.Exit(1);
        }
    }

    private static IEnumerator InGame()
    {
        for (int i = 0; i < 4; i++) yield return null;
        if (Object.FindObjectOfType<SaveManager>(true) != null) throw new InvalidOperationException("Player save writer in review scene.");
        var starter = Object.FindObjectOfType<StarterStoreInitialization>(); starter.cityTrip.orders.save = null;
        var initial = starter.CreateNewGame(12000); Object.FindObjectOfType<StoreProgression>().ApplySaveData(initial.territory);
        var spawner = Object.FindObjectOfType<StorePrefabSpawner>();
        spawner.UsesModularStore = true; starter.ConfigureSite(true); spawner.Spawn(StoreLevelId.Lvl1); starter.ApplyNewGameBuildings(initial);
        for (int i = 0; i < 4; i++) yield return null;
        var shop = Object.FindObjectOfType<StoreOperations>(); shop.enabled = false; shop.SetOpen(false); shop.Advance(.1f);
        var outward = (shop.ArrivalPosition - shop.EntrancePosition).normalized; outward.y = 0; outward.Normalize();
        var side = Vector3.Cross(Vector3.up, outward);
        var centre = shop.EntrancePosition + outward * .65f;
        for (int i = 1; i <= 3; i++)
        {
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i)));
            actor.GetComponent<ShopperAppearance>().FitHeight(.48f);
            var target = centre + side * ((i - 2) * .60f);
            if (shop.Navigation.TryNearest(target, out var walkable, true)) target = walkable;
            actor.transform.position += target;
            actor.transform.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(0, -12, 0);
            actor.GetComponentInChildren<Animator>().Update(.1f);
        }
        var camera = Camera.main; var input = camera.GetComponent<MainCamera>(); if (input != null) input.enabled = false;
        centre += Vector3.up * .24f;
        var offset = outward * 2 + side * .45f + Vector3.up * 1.1f;
        camera.transform.SetPositionAndRotation(centre + offset, Quaternion.LookRotation(-offset));
        camera.orthographic = true; camera.orthographicSize = .85f; camera.nearClipPlane = .01f;
        var targetTexture = new RenderTexture(1800, 1100, 24); var previous = RenderTexture.active;
        var image = new Texture2D(1800, 1100, TextureFormat.RGBA32, false);
        try
        {
            targetTexture.Create(); camera.targetTexture = targetTexture; camera.Render(); RenderTexture.active = targetTexture;
            image.ReadPixels(new Rect(0, 0, 1800, 1100), 0, 0); image.Apply();
            File.WriteAllBytes(Evidence + "/InGameStudy.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            targetTexture.Release(); Object.Destroy(targetTexture); Object.Destroy(image);
        }
    }
}
