using System;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Imports only shopper assets in the isolated project. No town/scene generation or save access.</summary>
public static class ShopperFleetBatch
{
    public const string Root="Assets/Art/Shoppers";
    public const string ResourcesFolder="Assets/Resources/Shoppers";
    public const string Evidence="Library/ShopperQA";
    [Serializable] public sealed class Manifest { public int light_skin_count; public Design[] characters; }
    [Serializable] public sealed class Design {public int number,triangles,bones;public string id,name,source,source_sha256,fbx_sha256,skin;public int[] skin_cell;}
    public static void Pilot()=>Build(false);
    public static void Probe()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Evidence);
        const string path=Root+"/Models/Shopper_01.fbx";
        Import(path);
        var prefab=CreatePrefab(path,CreateMaterial(),CreateController());
        ValidateAssets(new[]{prefab});
        ShopperFleetReview.Capture(new[]{prefab});
        EditorApplication.Exit(0);
    }
    public static void Build()=>Build(true);
    public static void Review()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var roster=Resources.Load<ShopperRoster>("Shoppers/Roster");
        if(roster==null||roster.Variants.Count!=50)throw new InvalidOperationException("Shopper roster incomplete.");
        ShopperFleetReview.Capture(roster.Variants.ToArray());
        ShopperFleetReview.CaptureMotion(roster.Variants[0]);
        EditorApplication.Exit(0);
    }
    private static void Build(bool full)
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Evidence);
        try
        {
            var paths=Directory.GetFiles(Root+"/Models","*.fbx").OrderBy(p=>p).ToArray();
            if(full&&paths.Length!=50)throw new InvalidOperationException("Expected exactly fifty exports.");
            foreach(var path in paths)Import(path.Replace('\\','/'));
            var material=CreateMaterial();
            var controller=CreateController();
            Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(ResourcesFolder);AssetDatabase.Refresh();
            var prefabs=paths.Select(path=>CreatePrefab(path.Replace('\\','/'),material,controller)).ToArray();
            if(full)
            {
                string rosterPath=ResourcesFolder+"/Roster.asset";
                var roster=AssetDatabase.LoadAssetAtPath<ShopperRoster>(rosterPath);
                if(roster==null){roster=ScriptableObject.CreateInstance<ShopperRoster>();AssetDatabase.CreateAsset(roster,rosterPath);}
                var serialized=new SerializedObject(roster);var variants=serialized.FindProperty("variants");variants.arraySize=prefabs.Length;
                for(int i=0;i<prefabs.Length;i++)variants.GetArrayElementAtIndex(i).objectReferenceValue=prefabs[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(roster);
            }
            AssetDatabase.SaveAssets();ValidateAssets(prefabs);
            ShopperFleetReview.Capture(prefabs);
            ShopperFleetReview.CaptureMotion(prefabs[0]);
            File.WriteAllText(Evidence+(full?"/assets.txt":"/pilot.txt"),"PASS "+prefabs.Length+" independently skinned shoppers, one shared palette material, Idle/Walk/Browse clips and common generic controller.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Evidence+"/failure.txt",error.ToString());EditorApplication.Exit(1);}
    }
    private static void Import(string path)
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;
        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects=false;importer.importCameras=false;importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importBlendShapes=false;importer.isReadable=true;importer.importNormals=ModelImporterNormals.Import;
        importer.animationCompression=ModelImporterAnimationCompression.Optimal;
        importer.SaveAndReimport();
        var clips=importer.defaultClipAnimations;
        var trace=new StringBuilder(path+"\n");
        foreach(var clip in clips)
        {
            trace.AppendLine(clip.name+" "+clip.firstFrame+".."+clip.lastFrame);
            string name=new[]{"Idle","Walk","Browse"}.FirstOrDefault(n=>clip.name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0);
            if(name==null)throw new InvalidOperationException("Unknown Blender action: "+clip.name);
            clip.name=name;clip.loopTime=true;clip.loopPose=true;
            clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalOrientation=true;
        }
        File.AppendAllText(Evidence+"/import.txt",trace.ToString());
        importer.clipAnimations=clips;importer.SaveAndReimport();
    }
    private static Material CreateMaterial()
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/ShopperPalette.png");
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=false;
        // V3 retains the mini-character pack's authored UV gradients within each palette swatch.
        importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        string path=Root+"/Shoppers.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ShopperPalette.png"));
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.08f);material.SetFloat("_Metallic",0);
        EditorUtility.SetDirty(material);return material;
    }
    private static AnimatorController CreateController()
    {
        string path=Root+"/Shopper.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var clips=AssetDatabase.LoadAllAssetsAtPath(Root+"/Models/Shopper_01.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        foreach(string name in new[]{"Idle","Walk","Browse"})
            if(clips.Count(c=>c.name==name)!=1)throw new InvalidOperationException("Expected a single "+name+" clip. Found: "+string.Join(",",clips.Select(c=>c.name)));
        if(controller!=null)
        {
            // Preserve the asset GUID, but never retain motions authored for an older bind pose.
            var states=controller.layers[0].stateMachine.states.Select(child=>child.state).ToArray();
            if(states.Length!=3||states.Any(state=>!clips.Any(clip=>clip.name==state.name)))
                throw new InvalidOperationException("Unexpected shopper controller structure; do not overwrite artist changes.");
            foreach(var state in states)
            {
                state.motion=clips.Single(clip=>clip.name==state.name);
                state.speed=state.name=="Walk"?2:1;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }
        controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("isWalking",AnimatorControllerParameterType.Bool);controller.AddParameter("isBrowsing",AnimatorControllerParameterType.Bool);
        var machine=controller.layers[0].stateMachine;
        foreach(var clip in clips)
        {
            var state=machine.AddState(clip.name);state.motion=clip;state.writeDefaultValues=true;
            if(clip.name=="Idle")machine.defaultState=state;
            if(clip.name=="Walk")state.speed=2;
            var transition=machine.AddAnyStateTransition(state);transition.hasExitTime=false;transition.duration=.10f;transition.canTransitionToSelf=false;
            transition.AddCondition(clip.name=="Walk"?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"isWalking");
            if(clip.name!="Walk")transition.AddCondition(clip.name=="Browse"?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"isBrowsing");
        }
        return controller;
    }
    private static GameObject CreatePrefab(string path,Material material,AnimatorController controller)
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var root=new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(root.transform,false);instance.name="Visual";
            var marker=instance.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FacingMarker");
            Vector3 localFront=instance.transform.InverseTransformPoint(marker.position);
            if(Mathf.Abs(localFront.z)<.01f)throw new InvalidOperationException("Character front was not mapped to Unity Z.");
            instance.transform.localRotation=Quaternion.Euler(0,localFront.z<0?180:0,0);
            var animator=instance.GetComponent<Animator>()??instance.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            animator.Rebind();animator.Update(0);
            controller.animationClips.Single(c=>c.name=="Idle").SampleAnimation(animator.gameObject,0);
            var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(skins.Length!=1)throw new InvalidOperationException("Expected one skin per appearance: "+path);
            skins[0].sharedMaterial=material;skins[0].quality=SkinQuality.Bone1;
            var baked=new Mesh();skins[0].BakeMesh(baked,false);
            File.AppendAllText(Evidence+"/bounds.txt",path+"\nmesh: "+skins[0].sharedMesh.bounds+"\nbaked: "+baked.bounds+
                "\nrenderer: "+skins[0].bounds+"\n"+string.Join("\n",instance.GetComponentsInChildren<Transform>(true).Select(t=>t.name+" local="+t.localScale+" world="+t.lossyScale+" at="+t.position))+"\n");
            var rest=new Bounds();bool first=true;
            foreach(var vertex in baked.vertices)
            {
                var point=root.transform.InverseTransformPoint(skins[0].transform.TransformPoint(vertex));
                if(first){rest=new Bounds(point,Vector3.zero);first=false;}else rest.Encapsulate(point);
            }
            Object.DestroyImmediate(baked);
            root.AddComponent<ShopperAppearance>().Configure(int.Parse(root.name.Substring(root.name.Length-2)),rest);
            var bounds=skins[0].localBounds;bounds.Expand(new Vector3(.3f,.2f,.5f));skins[0].localBounds=bounds;
            return PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+root.name+".prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
    public static void ValidateAssets(GameObject[] prefabs)
    {
        foreach(var prefab in prefabs)
        {
            var skin=prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            if(skin==null||skin.sharedMesh==null||skin.sharedMesh.triangles.Length/3>5000||skin.bones.Length<12)
                throw new InvalidOperationException("Invalid or over-budget shopper: "+prefab.name);
            if(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c==null))throw new InvalidOperationException("Missing script: "+prefab.name);
            var animator=prefab.GetComponentInChildren<Animator>();
            if(animator.runtimeAnimatorController==null||animator.avatar==null||!animator.avatar.isValid||animator.applyRootMotion)
                throw new InvalidOperationException("Invalid generic animation rig: "+prefab.name);
            var bounds=prefab.GetComponent<ShopperAppearance>().RestBounds;
            if(bounds.size.y<.1f||bounds.size.y>3f)throw new InvalidOperationException("Unexpected imported body height: "+bounds.size.y);
        }
    }
}
