using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Builds a dedicated animated robber prefab without editing the shopper roster or scenes.</summary>
public static class RobberModelImport
{
    public const string Root = "Assets/Art/Robber";
    public const string PrefabPath = "Assets/Resources/Characters/Robber.prefab";
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory("Assets/Resources/Characters"); AssetDatabase.Refresh();
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Root + "/Palette.png");
        textureImporter.mipmapEnabled = false; textureImporter.filterMode = FilterMode.Bilinear;
        textureImporter.wrapMode = TextureWrapMode.Clamp; textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        textureImporter.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Robber.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, Root + "/Robber.mat");
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Palette.png"));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .08f);
        material.SetFloat("_Metallic", 0); EditorUtility.SetDirty(material);
        string path = Root + "/Robber.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.optimizeGameObjects = false;
        importer.importCameras = importer.importLights = false; importer.isReadable = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = new[] { "Idle", "Walk", "Browse" }.Single(n => clip.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
            clip.loopTime = clip.loopPose = true; clip.lockRootPositionXZ = clip.lockRootRotation = true;
            clip.keepOriginalPositionXZ = clip.keepOriginalOrientation = true;
        }
        importer.clipAnimations = clips; importer.SaveAndReimport();
        var animations = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Robber.controller");
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "/Robber.controller");
        var machine = controller.layers[0].stateMachine;
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var state in machine.states) machine.RemoveState(state.state);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isBrowsing", AnimatorControllerParameterType.Bool);
        foreach (var clip in animations)
        {
            var state = machine.AddState(clip.name); state.motion = clip;
            if (clip.name == "Idle") machine.defaultState = state;
            if (clip.name == "Walk") state.speed = 2;
            var transition = machine.AddAnyStateTransition(state);
            transition.hasExitTime = false; transition.duration = .12f; transition.canTransitionToSelf = false;
            transition.AddCondition(clip.name == "Walk" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "isWalking");
            if (clip.name != "Walk") transition.AddCondition(clip.name == "Browse" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "isBrowsing");
        }
        var root = new GameObject("Robber");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.transform.SetParent(root.transform, false);
            var marker = model.GetComponentsInChildren<Transform>().Single(t => t.name == "FacingMarker");
            if (model.transform.InverseTransformPoint(marker.position).z < 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.Rebind(); animator.Update(0);
            animations.Single(c => c.name == "Idle").SampleAnimation(model, 0);
            var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
            skin.sharedMaterial = material; skin.quality = SkinQuality.Bone1;
            var baked = new Mesh(); skin.BakeMesh(baked, false);
            var points = baked.vertices.Select(v => root.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            Object.DestroyImmediate(baked);
            root.AddComponent<ShopperAppearance>().Configure(1, bounds);
            var culling = skin.localBounds; culling.Expand(.5f); skin.localBounds = culling;
            if (skin.bones.Length != 14 || animator.avatar == null || !animator.avatar.isValid || animations.Length != 3)
                throw new InvalidOperationException("Robber rig or animation import is invalid.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(root); }
    }
}
