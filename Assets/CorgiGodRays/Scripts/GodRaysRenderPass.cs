namespace CorgiGodRays
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.Rendering.Universal.Internal;

    public class GodRaysRenderPass : ScriptableRenderPass
    {
        private const string _profilerTag = "GodRaysRenderPass";

        private static readonly int _GodRaysTexture = Shader.PropertyToID("_GodRaysTexture");
        private static readonly int _CorgiGrabpass = Shader.PropertyToID("_CorgiGrabpass");
        private static readonly int _CopyBlitTex = Shader.PropertyToID("_CopyBlitTex");
        private static readonly int _TempBlurTex = Shader.PropertyToID("_TempBlurTex");
        private static readonly int _BlurInputTex = Shader.PropertyToID("_BlurInputTex");
        private static readonly int _CorgiDepthGrabpassFullRes = Shader.PropertyToID("_CorgiDepthGrabpassFullRes");
        private static readonly int _CorgiDepthGrabpassNonFullRes = Shader.PropertyToID("_CorgiDepthGrabpassNonFullRes");

        private static readonly int _CorgiInverseProjection = Shader.PropertyToID("_CorgiInverseProjection");
        private static readonly int _CorgiCameraToWorld = Shader.PropertyToID("_CorgiCameraToWorld");
        private static readonly int _CorgiInverseProjectionArray = Shader.PropertyToID("_CorgiInverseProjectionArray");
        private static readonly int _GodRaysParams = Shader.PropertyToID("_GodRaysParams");
        private static readonly int _MainLightScattering = Shader.PropertyToID("_MainLightScattering");
        private static readonly int _AdditionalLightScattering = Shader.PropertyToID("_AdditionalLightScattering");
        private static readonly int _Jitter = Shader.PropertyToID("_Jitter");
        private static readonly int _MaxDistance = Shader.PropertyToID("_MaxDistance");
        private static readonly int _MainLightIntensity = Shader.PropertyToID("_MainLightIntensity");
        private static readonly int _AdditionalLightIntensity = Shader.PropertyToID("_AdditionalLightIntensity");
        private static readonly int _TintColor = Shader.PropertyToID("_TintColor");
        private static readonly int _CorgiVisibleLightCount = Shader.PropertyToID("_CorgiVisibleLightCount");
        private static readonly int _CorgiVisibleLightData = Shader.PropertyToID("_CorgiVisibleLightData");
        private static readonly int _CorgiLightIndexToShadowIndex = Shader.PropertyToID("_CorgiLightIndexToShadowIndex");
        private static readonly int _CorgiGodraysIntensityCurveTexture = Shader.PropertyToID("_CorgiGodraysIntensityCurveTexture");

        [System.NonSerialized] private GodRaysRenderFeature.GodRaysSettings _settings;
        [System.NonSerialized] private ScriptableRenderer _renderer;
        [System.NonSerialized] private Matrix4x4[] _InverseProjectionArray = new Matrix4x4[2];
        [System.NonSerialized] private MaterialPropertyBlock _propertyBlock;
        [System.NonSerialized] private Texture2D _intensityCurveTexture;

        // used for detecting unity's SSS 
        [System.NonSerialized] private System.Reflection.PropertyInfo _cacheRenderFeaturesPropertyInfo;
        [System.NonSerialized] private static GraphicsBuffer _additionalLightsBuffer;
        [System.NonSerialized] private static GraphicsBuffer _lightsToShadowIndexBuffer;
        [System.NonSerialized] private const int MaxLightCount = 256;

        public void Setup(GodRaysRenderFeature.GodRaysSettings settings, ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _settings = settings;
            _renderer = renderer;

            _propertyBlock = new MaterialPropertyBlock();

            switch(settings.renderOrder)
            {
                case GodRaysRenderFeature.GodraysRenderOrder.AfterOpaque:
                    renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                    break;
                case GodRaysRenderFeature.GodraysRenderOrder.AfterTransparent:
                    renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                    break;
                case GodRaysRenderFeature.GodraysRenderOrder.Custom:
                    renderPassEvent = settings.customRenderPassEvent + settings.customRenderPassOffset;
                    break; 
            }

            var input = ScriptableRenderPassInput.Depth;

            if(_settings.temporallyRender && _settings.temporalReprojection)
            {
                input |= ScriptableRenderPassInput.Motion;
            }
            
            ConfigureInput(input);
        }

        public void Initialize()
        {
            Dispose();

            _additionalLightsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxLightCount, System.Runtime.InteropServices.Marshal.SizeOf<ShaderInput.LightData>());
            _lightsToShadowIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxLightCount, System.Runtime.InteropServices.Marshal.SizeOf<int>());
        }

        public void Dispose()
        {
            if (_additionalLightsBuffer != null)
            {
                _additionalLightsBuffer.Dispose(); 
            }

            if (_lightsToShadowIndexBuffer != null)
            {
                _lightsToShadowIndexBuffer.Dispose(); 
            }

            DisposeCurveTexture(); 
        }

        private void DisposeCurveTexture()
        {
            if (_intensityCurveTexture != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    Texture2D.Destroy(_intensityCurveTexture);
                }
                else
                {
                    Texture2D.DestroyImmediate(_intensityCurveTexture);
                }
#else
                Texture2D.Destroy(_intensityCurveTexture);
#endif

                _intensityCurveTexture = null;
            }
        }

#if UNITY_2022_3_OR_NEWER
        MixedLightingSetup m_MixedLightingSetup;

        void InitializeLightConstants(NativeArray<VisibleLight> lights, int lightIndex, out Vector4 lightPos, out Vector4 lightColor, out Vector4 lightAttenuation, out Vector4 lightSpotDir, out Vector4 lightOcclusionProbeChannel, out uint lightLayerMask, out bool isSubtractive)
        {
            UniversalRenderPipeline.InitializeLightConstants_Common(lights, lightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionProbeChannel);
            lightLayerMask = 0;
            isSubtractive = false;

            // When no lights are visible, main light will be set to -1.
            // In this case we initialize it to default values and return
            if (lightIndex < 0)
                return;

            ref VisibleLight lightData = ref UnsafeElementAtMutable(lights, lightIndex);
            Light light = lightData.light;
            var lightBakingOutput = light.bakingOutput;
            isSubtractive = lightBakingOutput.isBaked && lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed && lightBakingOutput.mixedLightingMode == MixedLightingMode.Subtractive;

            if (light == null)
                return;

            if (lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed &&
                lightData.light.shadows != LightShadows.None &&
                m_MixedLightingSetup == MixedLightingSetup.None)
            {
                switch (lightBakingOutput.mixedLightingMode)
                {
                    case MixedLightingMode.Subtractive:
                        m_MixedLightingSetup = MixedLightingSetup.Subtractive;
                        break;
                    case MixedLightingMode.Shadowmask:
                        m_MixedLightingSetup = MixedLightingSetup.ShadowMask;
                        break;
                }
            }

            var additionalLightData = light.GetUniversalAdditionalLightData();
            lightLayerMask = ToValidRenderingLayers(additionalLightData.renderingLayers);
        }

        // note: unity's "UniversalRenderPipelineGlobalSettings" is marked as internal, making it inaccessible to us easily here
        // so, we don't support the render layer feature in godrays :( 
        public static uint ToValidRenderingLayers(uint renderingLayers)
        {
            return int.MaxValue;
        }

        public static unsafe ref T UnsafeElementAtMutable<T>(NativeArray<T> array, int index) where T : struct
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
        }
#else


        // this wasn't exposed by URP, so copy/pasting it here to extract some data ourselves 
        // i really wish unity would just expose this stuff 
        void InitializeLightConstants(NativeArray<VisibleLight> lights, int lightIndex, out Vector4 lightPos, out Vector4 lightColor, out Vector4 lightAttenuation, out Vector4 lightSpotDir, out Vector4 lightOcclusionProbeChannel
#if UNITY_2021_1_OR_NEWER
            ,out uint lightLayerMask
#endif
            )
        {

#if UNITY_2021_1_OR_NEWER
            lightLayerMask = 0;
#endif
            UniversalRenderPipeline.InitializeLightConstants_Common(lights, lightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionProbeChannel);

            // When no lights are visible, main light will be set to -1.
            // In this case we initialize it to default values and return
            if (lightIndex < 0)
                return;

            VisibleLight lightData = lights[lightIndex];
            Light light = lightData.light;

            if (light == null)
                return;

#if UNITY_2021_1_OR_NEWER
            var additionalLightData = light.GetUniversalAdditionalLightData();
            lightLayerMask = (uint) light.renderingLayerMask;
#endif
        }
#endif

        private struct VisibleLightRemap
        {
            public VisibleLight lightData;
            public int visibleLightIndex;
        }

        private int _temporal_pass_index;
        private bool _curveTextureForceRefreshTrigger;

        private static Mesh s_FullscreenMesh;

        // Unity's obsolete fullscreenMesh 
        public static Mesh fullscreenMesh
        {
            get
            {
                if (s_FullscreenMesh != null)
                    return s_FullscreenMesh;

                float topV = 1.0f;
                float bottomV = 0.0f;

                s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
                s_FullscreenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f,  1.0f, 0.0f)
                });

                s_FullscreenMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, bottomV),
                    new Vector2(0.0f, topV),
                    new Vector2(1.0f, bottomV),
                    new Vector2(1.0f, topV)
                });

                s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                s_FullscreenMesh.UploadMeshData(true);
                return s_FullscreenMesh;
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var universalRenderer = _renderer as UniversalRenderer;

            if (_settings == null || !renderingData.postProcessingEnabled)
            {
                return;
            }

            if (VolumeManager.instance == null)
            {
                Debug.LogWarning($"VolumeManager.instance is null.");
                return;
            }

            if (VolumeManager.instance.stack == null)
            {
                Debug.LogWarning($"VolumeManager.instance.stack is null.");
                return;
            }

            var volumeSettings = VolumeManager.instance.stack.GetComponent<GodRaysVolume>();
            if(volumeSettings == null || (volumeSettings.MainLightIntensity.value == 0f && volumeSettings.AdditionalLightsIntensity.value == 0f))
            {
                return;
            }

            if(!_settings.allowMainLight && !_settings.allowAdditionalLights)
            {
                return; 
            }

            var command = CommandBufferPool.Get(_profilerTag);
                command.Clear();

            // handle custom light stuff 
            if(_settings.allowAdditionalLights && _settings.AdditionalLightLayers != 0)
            {
                var m_AdditionalLightsShadowCasterPassField = universalRenderer.GetType().GetField("m_AdditionalLightsShadowCasterPass", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var m_AdditionalLightsShadowCasterPass = (AdditionalLightsShadowCasterPass)m_AdditionalLightsShadowCasterPassField.GetValue(universalRenderer);

                var visibleLights = renderingData.lightData.visibleLights;
                var visibleLightCount = visibleLights.Length;
                var corgiLightCount = 0;

                var additionalLightsData = new NativeArray<ShaderInput.LightData>(visibleLightCount, Allocator.Temp);
                var corgiLightsToShadowIndex = new NativeArray<int>(visibleLightCount, Allocator.Temp);

                var maxLightCount = _settings.maxAdditionalLightCount;

                var visibleLightCopy = new List<VisibleLightRemap>(renderingData.lightData.visibleLights.Length);

                for(var l = 0; l < renderingData.lightData.visibleLights.Length; ++l)
                {
                    visibleLightCopy.Add(new VisibleLightRemap()
                    {
                        lightData = renderingData.lightData.visibleLights[l],
                        visibleLightIndex = l
                    });
                }

                var cameraPosition = renderingData.cameraData.camera.transform.position;
                visibleLightCopy.Sort((a, b) =>
                {
                    var distance_a = Vector3.Distance(a.lightData.light.transform.position, cameraPosition);
                    var distance_b = Vector3.Distance(b.lightData.light.transform.position, cameraPosition);
                    return distance_a.CompareTo(distance_b);
                });

                for (int i = 0, lightIter = 0; i < visibleLightCopy.Count && lightIter < MaxLightCount; ++i)
                {
                    var lightRemap = visibleLightCopy[i];
                    var light = lightRemap.lightData;
                    var layer = light.light.gameObject.layer;
                    var layermask = 1 << layer;
                    var lightIndex = lightRemap.visibleLightIndex;

                    if((layermask & _settings.AdditionalLightLayers.value) == 0)
                    {
                        continue;
                    }

                    if (renderingData.lightData.mainLightIndex != lightIndex)
                    {
                        ShaderInput.LightData data;

#if UNITY_2022_3_OR_NEWER
                        InitializeLightConstants(visibleLights, lightIndex, out data.position, out data.color, out data.attenuation,
                            out data.spotDirection, out data.occlusionProbeChannels, out data.layerMask, out bool isSubtractive);
#elif UNITY_2021_1_OR_NEWER
                        InitializeLightConstants(visibleLights, lightIndex,
                            out data.position, out data.color, out data.attenuation,
                            out data.spotDirection, out data.occlusionProbeChannels,out data.layerMask); 
#else
                        InitializeLightConstants(visibleLights, lightIndex,
                            out data.position, out data.color, out data.attenuation,
                            out data.spotDirection, out data.occlusionProbeChannels);
#endif

                        additionalLightsData[lightIter] = data;
                        corgiLightsToShadowIndex[lightIter] = m_AdditionalLightsShadowCasterPass.GetShadowLightIndexFromLightIndex(lightIndex); 

                        lightIter++;
                        corgiLightCount++;
                        if(corgiLightCount >= maxLightCount)
                        {
                            break;
                        }
                    }
                }

                _additionalLightsBuffer.SetData(additionalLightsData);
                _lightsToShadowIndexBuffer.SetData(corgiLightsToShadowIndex);

                command.SetGlobalBuffer(_CorgiVisibleLightData, _additionalLightsBuffer);
                command.SetGlobalBuffer(_CorgiLightIndexToShadowIndex, _lightsToShadowIndexBuffer);
                command.SetGlobalFloat(_CorgiVisibleLightCount, corgiLightCount);

                additionalLightsData.Dispose();
                corgiLightsToShadowIndex.Dispose();
            }

            if (_settings.allowAdditionalLightShadows)
            {
                command.EnableShaderKeyword("GODRAYS_ADDITIVE_LIGHT_SHADOWS");
            }
            else
            {
                command.DisableShaderKeyword("GODRAYS_ADDITIVE_LIGHT_SHADOWS");
            }

            // handle sss 
            var screenSpaceShadowsEnabled = false;

            if(_settings.supportUnityScreenSpaceShadows)
            {
                if(_cacheRenderFeaturesPropertyInfo == null)
                {
                    var typeData = _renderer.GetType();
                    _cacheRenderFeaturesPropertyInfo = typeData.GetProperty("rendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }

                if(_cacheRenderFeaturesPropertyInfo != null)
                {
                    var feature = _cacheRenderFeaturesPropertyInfo.GetValue(_renderer, null) as List<ScriptableRendererFeature>;
                    if(feature != null)
                    {
                        screenSpaceShadowsEnabled = feature.FindIndex(other => other != null && other.isActive && other.name == "ScreenSpaceShadows") != -1;
                    }
                }
            }

            if(screenSpaceShadowsEnabled)
            {
                var shadowsData = renderingData.shadowData;
                
                CoreUtils.SetKeyword(command, ShaderKeywordStrings.MainLightShadows, shadowsData.mainLightShadowCascadesCount > 0);
                CoreUtils.SetKeyword(command, ShaderKeywordStrings.MainLightShadowCascades, shadowsData.mainLightShadowCascadesCount == 0);

#if UNITY_2021_1_OR_NEWER
                CoreUtils.SetKeyword(command, ShaderKeywordStrings.MainLightShadowScreen, false);
#endif
            }

            // handle curve texture setup 
            if(_settings.useVariableIntensity)
            {
                EnsureCurveTexture();
                command.SetGlobalTexture(_CorgiGodraysIntensityCurveTexture, _intensityCurveTexture);
                command.EnableShaderKeyword("GODRAYS_VARIABLE_INTENSITY");
            }
            else
            {
                command.DisableShaderKeyword("GODRAYS_VARIABLE_INTENSITY");
            }

            // handle main light toggle
            if(_settings.allowMainLight)
            {
                command.EnableShaderKeyword("GODRAYS_MAIN_LIGHT");
            }
            else
            {
                command.DisableShaderKeyword("GODRAYS_MAIN_LIGHT");
            }

            // configure projection matrices 
            var projection = GL.GetGPUProjectionMatrix(renderingData.cameraData.GetProjectionMatrix(0), false);
            var inverseProjection = projection.inverse;

            command.SetGlobalMatrix(_CorgiCameraToWorld, renderingData.cameraData.camera.cameraToWorldMatrix);
            command.SetGlobalMatrix(_CorgiInverseProjection, inverseProjection);

            // handle VR / single pass instanced rendering 
            var eyeCount = renderingData.cameraData.cameraTargetDescriptor.volumeDepth;
            if(eyeCount > 1)
            {
                for (var eyeIndex = 0; eyeIndex < 2; ++eyeIndex)
                {
                    var eye_projection = GL.GetGPUProjectionMatrix(renderingData.cameraData.GetProjectionMatrix(eyeIndex), false);
                    var eye_inverseProjection = eye_projection.inverse;
                    _InverseProjectionArray[eyeIndex] = eye_inverseProjection;
                }

                command.SetGlobalMatrixArray(_CorgiInverseProjectionArray, _InverseProjectionArray);
            }

            // color grabpass 
            var cameraColorDesc = renderingData.cameraData.cameraTargetDescriptor;
                cameraColorDesc.msaaSamples = 1;
                cameraColorDesc.bindMS = false;

            command.GetTemporaryRT(_CorgiGrabpass, cameraColorDesc);

#if UNITY_2022_1_OR_NEWER
            var cameraColorTargetHandle = _renderer.cameraColorTargetHandle;
            var cameraDepthTargetHandle = _renderer.cameraDepthTargetHandle;
#else
            var cameraColorTargetHandle = _renderer.cameraColorTarget;
            var cameraDepthTargetHandle = _renderer.cameraDepthTarget;
#endif

            command.SetGlobalTexture(_CopyBlitTex, cameraColorTargetHandle);
            command.SetRenderTarget(_CorgiGrabpass, 0, CubemapFace.Unknown, -1);
            command.DrawMesh(fullscreenMesh, Matrix4x4.identity, _settings.renderData.Grabpass, 0, 0);

            if (_settings.useUnityDepthDirectly)
            {
                command.EnableShaderKeyword("_GODRAYS_USE_UNITY_DEPTH");
            }
            else
            {
                command.DisableShaderKeyword("_GODRAYS_USE_UNITY_DEPTH");
            }

            // depth grabpass  (full res)
            var depthTargetDescriptorFullRes = cameraColorDesc;
                depthTargetDescriptorFullRes.colorFormat = RenderTextureFormat.RFloat;
                depthTargetDescriptorFullRes.depthBufferBits = 0;
                depthTargetDescriptorFullRes.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                depthTargetDescriptorFullRes.msaaSamples = 1;
                depthTargetDescriptorFullRes.bindMS = false;

            command.SetGlobalTexture(_CopyBlitTex, cameraDepthTargetHandle);
            command.GetTemporaryRT(_CorgiDepthGrabpassFullRes, depthTargetDescriptorFullRes);
            command.SetRenderTarget(_CorgiDepthGrabpassFullRes, 0, CubemapFace.Unknown, -1);
            command.DrawMesh(fullscreenMesh, Matrix4x4.identity, _settings.renderData.DepthGrabpass, 0, 0);
            command.SetGlobalTexture(_CorgiDepthGrabpassFullRes, _CorgiDepthGrabpassFullRes);

            // configure the godrays texture 
            var godRaysTexDesc = cameraColorDesc;

            if(_settings.temporallyRender && _settings.temporalReprojection)
            {
                godRaysTexDesc.enableRandomWrite = true;
            }

            if (_settings.encodeLightColor)
            {
                command.EnableShaderKeyword("GODRAYS_ENCODE_LIGHT_COLOR");
                godRaysTexDesc.colorFormat = _settings.enableHighQualityTextures ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf;
            }
            else
            {
                command.DisableShaderKeyword("GODRAYS_ENCODE_LIGHT_COLOR");
                godRaysTexDesc.colorFormat = _settings.enableHighQualityTextures ? RenderTextureFormat.RFloat : RenderTextureFormat.RHalf;
            }

            var textureQualityDivider = (int)_settings.textureQuality;
            godRaysTexDesc.width /= textureQualityDivider;
            godRaysTexDesc.height /= textureQualityDivider;

            command.SetGlobalVector(_GodRaysParams, new Vector4(godRaysTexDesc.width, godRaysTexDesc.height, 1f / godRaysTexDesc.width, 1f / godRaysTexDesc.height));

            // depth grabpass  (non full res)
            if(_settings.depthAwareUpsampling && _settings.textureQuality != GodRaysRenderFeature.VolumeTextureQuality.High)
            {
                var depthTargetDescriptorNonFullRes = godRaysTexDesc;
                    depthTargetDescriptorNonFullRes.colorFormat = RenderTextureFormat.RFloat;
                    depthTargetDescriptorNonFullRes.msaaSamples = 1;
                    depthTargetDescriptorNonFullRes.bindMS = false;

                command.SetGlobalTexture(_CopyBlitTex, cameraDepthTargetHandle);
                command.GetTemporaryRT(_CorgiDepthGrabpassNonFullRes, depthTargetDescriptorNonFullRes);
                command.SetRenderTarget(_CorgiDepthGrabpassNonFullRes, 0, CubemapFace.Unknown, -1);
                command.DrawMesh(fullscreenMesh, Matrix4x4.identity, _settings.renderData.DepthGrabpass, 0, 0);
            }

            // create the godrays texture 

            // configure settings 
            _propertyBlock.Clear();
            _propertyBlock.SetFloat(_MainLightScattering, volumeSettings.MainLightScattering.value);
            _propertyBlock.SetFloat(_AdditionalLightScattering, volumeSettings.AdditionalLightsScattering.value);
            _propertyBlock.SetFloat(_MainLightIntensity, volumeSettings.MainLightIntensity.value);
            _propertyBlock.SetFloat(_AdditionalLightIntensity, volumeSettings.AdditionalLightsIntensity.value);
            _propertyBlock.SetFloat(_Jitter, _settings.Jitter);
            _propertyBlock.SetFloat(_MaxDistance, _settings.maxDistance);

            if (_settings.stepQuality == GodRaysRenderFeature.VolumeStepQuality.Low) command.EnableShaderKeyword("VOLUME_STEPS_LOW"); else command.DisableShaderKeyword("VOLUME_STEPS_LOW");
            if(_settings.stepQuality == GodRaysRenderFeature.VolumeStepQuality.Med) command.EnableShaderKeyword("VOLUME_STEPS_MED"); else command.DisableShaderKeyword("VOLUME_STEPS_MED");
            if(_settings.stepQuality == GodRaysRenderFeature.VolumeStepQuality.High) command.EnableShaderKeyword("VOLUME_STEPS_HIGH"); else command.DisableShaderKeyword("VOLUME_STEPS_HIGH");

            if (_settings.allowAdditionalLights) command.EnableShaderKeyword("GODRAYS_ADDITIVE_LIGHTS"); else command.DisableShaderKeyword("GODRAYS_ADDITIVE_LIGHTS");

            command.GetTemporaryRT(_GodRaysTexture, godRaysTexDesc);

            if (_settings.temporallyRender)
            {
                if(_settings.temporalUseDiscard) command.EnableShaderKeyword("GODRAYS_DISCARD_TEMPORAL"); else command.DisableShaderKeyword("GODRAYS_DISCARD_TEMPORAL");

                _temporal_pass_index++;
                if (_temporal_pass_index >= _settings.temporalDuration)
                {
                    _temporal_pass_index = 0;
                }

                _propertyBlock.SetInt("_CorgiTemporallyRendered", 1);
                _propertyBlock.SetInt("_CorgiTemporalPassIndex", _temporal_pass_index);
                _propertyBlock.SetInt("_CorgiTemporalPassCount", _settings.temporalDuration);

                if (_settings.temporallyRender && _settings.temporalReprojection)
                {
                    // note: would be cool to get rid of this blit 
                    var _Temporal = Shader.PropertyToID("_TemporalGodrays");
                    command.GetTemporaryRT(_Temporal, godRaysTexDesc);
                    command.CopyTexture(_GodRaysTexture, 0, 0, _Temporal, 0, 0);

                    var reprojectionShader = _settings.renderData.TemporalReprojectionShader;

                    var kernal_reprojection = 0;
                    command.SetComputeTextureParam(reprojectionShader, kernal_reprojection, "Input", _Temporal);
                    command.SetComputeTextureParam(reprojectionShader, kernal_reprojection, "Output", _GodRaysTexture);

                    command.SetComputeIntParam(reprojectionShader, "texture_width", godRaysTexDesc.width);
                    command.SetComputeIntParam(reprojectionShader, "texture_height", godRaysTexDesc.height);

                    command.SetComputeIntParam(reprojectionShader, "TemporalPassIndex", _temporal_pass_index);
                    command.SetComputeIntParam(reprojectionShader, "TemporalPassCount", _settings.temporalDuration);

                    command.SetComputeTextureParam(reprojectionShader, kernal_reprojection, "_CameraMotionVectorsTexture", "_MotionVectorTexture");
                    command.SetComputeVectorParam(reprojectionShader, "_CameraMotionVectorsTexture_Resolution", new Vector4(cameraColorDesc.width, cameraColorDesc.height));
                    command.DispatchCompute(reprojectionShader, kernal_reprojection, godRaysTexDesc.width / 32, godRaysTexDesc.height / 32, 1);
                }
            }
            else
            {
                _propertyBlock.SetInt("_CorgiTemporallyRendered", 0);
            }

            _propertyBlock.SetVector("_CorgiScreenParams", new Vector4(godRaysTexDesc.width, godRaysTexDesc.height, 1f / godRaysTexDesc.width, 1f / godRaysTexDesc.height));

            // draw 
            command.SetRenderTarget(_GodRaysTexture, 0, CubemapFace.Unknown, -1);
            command.DrawMesh(fullscreenMesh, Matrix4x4.identity, _settings.renderData.ScreenSpaceGodRays, 0, 0, _propertyBlock);

            if(_settings.blur)
            {
                // setup temp texture 
                command.GetTemporaryRT(_TempBlurTex, godRaysTexDesc);

                if (_settings.blurSamples == GodRaysRenderFeature.BilateralBlurSamples.Low) command.EnableShaderKeyword("SAMPLE_COUNT_LOW"); else command.DisableShaderKeyword("SAMPLE_COUNT_LOW");
                if (_settings.blurSamples == GodRaysRenderFeature.BilateralBlurSamples.Med) command.EnableShaderKeyword("SAMPLE_COUNT_MED"); else command.DisableShaderKeyword("SAMPLE_COUNT_MED");
                if (_settings.blurSamples == GodRaysRenderFeature.BilateralBlurSamples.High) command.EnableShaderKeyword("SAMPLE_COUNT_HIGH"); else command.DisableShaderKeyword("SAMPLE_COUNT_HIGH");

                for(var i = 0; i < _settings.BlurCount; ++i)
                {
                    // blur x 
                    command.SetGlobalTexture(_BlurInputTex, _GodRaysTexture);
                    command.SetRenderTarget(_TempBlurTex, 0, CubemapFace.Unknown, -1);
                    command.EnableShaderKeyword("BLUR_X");
                    command.DisableShaderKeyword("BLUR_Y");
                    command.DrawMesh(fullscreenMesh, Matrix4x4.identity, _settings.renderData.BilateralBlur, 0, 0);

                    // blur y 
                    command.SetGlobalTexture(_BlurInputTex, _TempBlurTex);
                    command.SetRenderTarget(_GodRaysTexture, 0, CubemapFace.Unknown, -1);
                    command.DisableShaderKeyword("BLUR_X");
                    command.EnableShaderKeyword("BLUR_Y");
                    command.DrawMesh(fullscreenMesh, Matrix4x4.identity, _settings.renderData.BilateralBlur, 0, 0);
                }

                // cleanup 
                command.ReleaseTemporaryRT(_TempBlurTex);
            }

            if (_settings.depthAwareUpsampling && _settings.textureQuality != GodRaysRenderFeature.VolumeTextureQuality.High)
            {
                command.SetGlobalTexture(_CorgiDepthGrabpassNonFullRes, _CorgiDepthGrabpassNonFullRes);
            }

            // apply the godrays texture to the screen
            if (_settings.depthAwareUpsampling && _settings.textureQuality != GodRaysRenderFeature.VolumeTextureQuality.High) 
                command.EnableShaderKeyword("DEPTH_AWARE_UPSAMPLE"); 
            else 
                command.DisableShaderKeyword("DEPTH_AWARE_UPSAMPLE");

            _propertyBlock.Clear(); 
            _propertyBlock.SetColor(_TintColor, volumeSettings.Tint.value);
            // _propertyBlock.SetFloat(_Intensity, volumeSettings.Intensity.value);

            command.SetGlobalTexture(_GodRaysTexture, _GodRaysTexture);
            command.SetGlobalTexture(_CopyBlitTex, _CorgiGrabpass);

            command.SetRenderTarget(cameraColorTargetHandle, 0, CubemapFace.Unknown, -1);
            command.DrawMesh(fullscreenMesh, Matrix4x4.identity, _settings.renderData.ApplyGodRays, 0, 0, _propertyBlock);

            // cleanup 
            command.ReleaseTemporaryRT(_CorgiGrabpass);
            command.ReleaseTemporaryRT(_CorgiDepthGrabpassFullRes);
            command.ReleaseTemporaryRT(_GodRaysTexture);

            if(_settings.depthAwareUpsampling && _settings.textureQuality != GodRaysRenderFeature.VolumeTextureQuality.High)
            {
                command.ReleaseTemporaryRT(_CorgiDepthGrabpassNonFullRes);
            }

            // restore the state of Unity's SSS keywords 
            if (screenSpaceShadowsEnabled)
            {
                CoreUtils.SetKeyword(command, ShaderKeywordStrings.MainLightShadows, false);
                CoreUtils.SetKeyword(command, ShaderKeywordStrings.MainLightShadowCascades, false);
#if UNITY_2021_1_OR_NEWER
                CoreUtils.SetKeyword(command, ShaderKeywordStrings.MainLightShadowScreen, true);
#endif
            }

            context.ExecuteCommandBuffer(command);
            CommandBufferPool.Release(command); 
        }

        public void TriggerRefreshCurveTexture()
        {
            _curveTextureForceRefreshTrigger = true; 
        }

        private void EnsureCurveTexture()
        {
            if(_curveTextureForceRefreshTrigger)
            {
                _curveTextureForceRefreshTrigger = false;
                DisposeCurveTexture();
            }

            if(_intensityCurveTexture != null)
            {
                return;
            }

            const int width = 64;
            _intensityCurveTexture = new Texture2D(width, 1, TextureFormat.RFloat, false, true);
            var pixels = _intensityCurveTexture.GetPixelData<float>(0);

            for (var i = 0; i < width; ++i)
            {
                var t = (float) i / width;
                pixels[i] = _settings.variableIntensityCurve.Evaluate(t);
            }

            _intensityCurveTexture.SetPixelData<float>(pixels, 0);
            _intensityCurveTexture.Apply(); 
        }
    }
}