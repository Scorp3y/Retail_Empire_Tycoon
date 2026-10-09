using System;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Owns the town's shared palette, daylight and scalable URP profiles, leaving source model assets untouched.</summary>
internal static class TownWorldArtDirection
{
    private const string Folder=TownWorldAssembler.Folder;
    public static Material Palette()
    {
        var colors=(Color32[])TownPalette.Colors.Clone();
        colors[(int)TownColor.Grass]=new Color32(116,163,89,255);
        colors[(int)TownColor.LeafLight]=new Color32(147,181,98,255);
        colors[(int)TownColor.Leaf]=new Color32(64,132,78,255);
        colors[(int)TownColor.LeafDark]=new Color32(33,96,72,255);
        colors[(int)TownColor.Asphalt]=new Color32(86,101,111,255);
        colors[(int)TownColor.Pavement]=new Color32(203,199,183,255);
        colors[(int)TownColor.Cream]=new Color32(246,232,198,255);
        colors[(int)TownColor.Brick]=new Color32(203,113,77,255);
        colors[(int)TownColor.Mint]=new Color32(76,163,151,255);
        colors[(int)TownColor.Glass]=new Color32(81,149,178,255);
        colors[(int)TownColor.Sand]=new Color32(217,201,160,255);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/TownPalette.asset");
        if(texture==null){texture=new Texture2D(32,1,TextureFormat.RGBA32,false);AssetDatabase.CreateAsset(texture,Folder+"/TownPalette.asset");}
        texture.SetPixels32(colors);texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;texture.Apply();EditorUtility.SetDirty(texture);
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/TownPalette.mat");
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/TownSurface.shader");
        if(shader==null)throw new InvalidOperationException("Town surface shader is missing.");
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+"/TownPalette.mat");}
        material.shader=shader;
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.16f);EditorUtility.SetDirty(material);
        return material;
    }

    public static void GraphicsProfiles()
    {
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels=settings.FindProperty("m_QualitySettings");
        UniversalRenderPipelineAsset defaultProfile=null;
        for(int i=0;i<levels.arraySize;i++)
        {
            var level=levels.GetArrayElementAtIndex(i);
            var slot=level.FindPropertyRelative("customRenderPipeline");
            var source=slot.objectReferenceValue as UniversalRenderPipelineAsset;
            if(source==null)throw new InvalidOperationException("A quality level has no URP pipeline.");
            string path=Folder+"/TownGraphics_"+i+".asset";
            var profile=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(profile==null){profile=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(profile,path);}
            profile.name="Town Graphics "+level.FindPropertyRelative("name").stringValue;
            profile.shadowDistance=i<2?35:i==2?80:220;
            profile.shadowCascadeCount=i<2?1:2;
            profile.shadowDepthBias=.65f;profile.shadowNormalBias=.45f;
            profile.msaaSampleCount=i<2?2:4;profile.renderScale=1;
            var renderer=Renderer(i>=3);
            var serialized=new SerializedObject(profile);var list=serialized.FindProperty("m_RendererDataList");
            serialized.FindProperty("m_SoftShadowsSupported").boolValue=i>=2;
            serialized.FindProperty("m_Cascade2Split").floatValue=.32f;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue=i<2?1024:i==2?2048:4096;
            list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue=0;serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);slot.objectReferenceValue=profile;
            if(i==3)defaultProfile=profile;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();GraphicsSettings.defaultRenderPipeline=defaultProfile;
        AssetDatabase.SaveAssets();
    }
    private static UniversalRendererData Renderer(bool occlusion)
    {
        string path=Folder+(occlusion?"/TownRendererHigh.asset":"/TownRendererLow.asset");
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if(renderer==null)
        {
            renderer=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Shaders/URP_Renderer.asset"));
            renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,path);
        }
        if(occlusion&&!renderer.rendererFeatures.Any(f=>f!=null&&f.GetType().Name=="ScreenSpaceAmbientOcclusion"))
        {
            var type=typeof(ScriptableRendererFeature).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion",true);
            var feature=(ScriptableRendererFeature)ScriptableObject.CreateInstance(type);feature.name="Town contact shadows";
            AssetDatabase.AddObjectToAsset(feature,renderer);
            var serialized=new SerializedObject(feature);var values=serialized.FindProperty("m_Settings");
            values.FindPropertyRelative("AOMethod").enumValueIndex=1;
            values.FindPropertyRelative("Downsample").boolValue=true;
            values.FindPropertyRelative("Intensity").floatValue=.7f;
            values.FindPropertyRelative("Radius").floatValue=.32f;
            values.FindPropertyRelative("DirectLightingStrength").floatValue=.15f;
            values.FindPropertyRelative("Falloff").floatValue=120;
            serialized.ApplyModifiedPropertiesWithoutUndo();feature.Create();renderer.rendererFeatures.Add(feature);
        }
        renderer.SetDirty();EditorUtility.SetDirty(renderer);return renderer;
    }

    public static void Apply(Light sun,Camera camera,float scale)
    {
        sun.transform.rotation=Quaternion.Euler(38,-32,0);sun.color=new Color(1,.92f,.79f);
        sun.intensity=1.35f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.8f;
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.53f,.69f,.86f);
        RenderSettings.ambientEquatorColor=new Color(.59f,.64f,.58f);
        RenderSettings.ambientGroundColor=new Color(.29f,.33f,.24f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogColor=new Color(.7f,.82f,.9f);RenderSettings.fogStartDistance=280*scale;RenderSettings.fogEndDistance=720*scale;
        string skyPath=Folder+"/TownSky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,skyPath);}
        sky.SetColor("_SkyTint",new Color(.55f,.67f,.8f));sky.SetColor("_GroundColor",new Color(.58f,.65f,.6f));
        sky.SetFloat("_AtmosphereThickness",.8f);sky.SetFloat("_Exposure",1.15f);sky.SetFloat("_SunSize",.025f);
        EditorUtility.SetDirty(sky);RenderSettings.skybox=sky;camera.clearFlags=CameraClearFlags.Skybox;
        camera.allowHDR=true;var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        string profilePath=Folder+"/TownColorGrade.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
        if(!profile.TryGet<ColorAdjustments>(out var color))color=profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(.15f);color.contrast.Override(12);color.saturation.Override(9);
        if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);
        if(!profile.TryGet<WhiteBalance>(out var balance))balance=profile.Add<WhiteBalance>(true);balance.temperature.Override(4);balance.tint.Override(-1);
        foreach(var component in profile.components)if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);
        EditorUtility.SetDirty(profile);
        var root=new GameObject("Town atmosphere",typeof(Volume));var volume=root.GetComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.sharedProfile=profile;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,camera.gameObject.scene);
    }
}
