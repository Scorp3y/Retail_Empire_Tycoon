Shader "Hidden/CorgiGodRays/ScreenSpaceGodRays"
{
    Properties
    {
        _Scattering("_Scattering", Float) = 0.5
        _Jitter("_Jitter", Float) = 0.5
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
    #include "SharedInputs.hlsl"

    #pragma target 5.0

    struct AttributesDefault
    {
        float4 positionHCS : POSITION;
        float2 uv          : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct VaryingsDefault
    {
        float4 positionCS  : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 positionHCS : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    VaryingsDefault VertDefault(AttributesDefault v)
    {
        VaryingsDefault o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.positionCS = float4(v.positionHCS.xyz, 1.0);
        o.positionHCS = v.positionHCS;

#if UNITY_UV_STARTS_AT_TOP
        o.positionCS.y *= -1;
#endif

        o.uv = v.uv;

        return o;
    }

    float _MainLightScattering;
    float _AdditionalLightScattering;
    float _MainLightIntensity;
    float _AdditionalLightIntensity;
    float _Jitter;
    float _MaxDistance;

#ifdef GODRAYS_VARIABLE_INTENSITY
    TEXTURE2D(_CorgiGodraysIntensityCurveTexture);
    SamplerState _LinearClamp;
#endif

    // Mie scaterring approximated with Henyey-Greenstein phase function.
    float ComputeScattering(float lightDotView, float scatterAmount)
    {
        float scatterSquared = scatterAmount * scatterAmount;
        float result = 1.0f - scatterSquared;
        result /= (4.0f * PI * pow(1.0f + scatterSquared - (2.0f * scatterAmount) * lightDotView, 1.5f));
        return result;
    }

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
    float3 GetMainLightContribution(float3 worldPosition, float rayDirDotLightDir)
#else
    float GetMainLightContribution(float3 worldPosition, float rayDirDotLightDir)
#endif
    {
        #ifndef GODRAYS_MAIN_LIGHT
            return 0.0;
        #endif

        float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
        float shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
        
        float scatterColor = ComputeScattering(rayDirDotLightDir, _MainLightScattering);
        shadowAttenuation = scatterColor * shadowAttenuation * _MainLightIntensity;
       
        #ifdef GODRAYS_ENCODE_LIGHT_COLOR
            float3 result = shadowAttenuation * _MainLightColor;
        #else
            float result = shadowAttenuation;
        #endif
        
        #if defined(_LIGHT_COOKIES)
            float3 cookieColor = SampleMainLightCookie(worldPosition);

            #ifdef GODRAYS_ENCODE_LIGHT_COLOR
                    result *= cookieColor;
            #else
                    result *= CalcLuminance(cookieColor);
            #endif
        #endif

        return result;
    }

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
    float3 GetAdditionalLightContribution(float3 rayDirection, float3 worldPosition)
#else
    float GetAdditionalLightContribution(float3 rayDirection, float3 worldPosition)
#endif
    {
        #ifdef GODRAYS_ENCODE_LIGHT_COLOR
            float3 contribution = float3(0.0, 0.0, 0.0);
        #else
            float contribution = 0.0;
        #endif

        uint pixelLightCount = GetCorgiLightCount(); 
        CORGI_LIGHT_LOOP_BEGIN(pixelLightCount)
            int perObjectLightIndex = lightIndex;
            int shadowLightIndex = CorgiLightToShadowIndex(lightIndex);

            Light light = GetCorgiAdditionalPerObjectLight(perObjectLightIndex, worldPosition);

            #ifdef GODRAYS_ADDITIVE_LIGHT_SHADOWS
                // directional lights as additional lights were supported in 2021.x.x+ (URP 12+)
                #if UNITY_VERSION >= 2021000
                    light.shadowAttenuation = AdditionalLightRealtimeShadow(shadowLightIndex, worldPosition, light.direction);
                #else
                    light.shadowAttenuation = AdditionalLightRealtimeShadow(shadowLightIndex, worldPosition);
                #endif
            #endif

            #if defined(_LIGHT_COOKIES)
                float3 cookieColor = SampleAdditionalLightCookie(perObjectLightIndex, worldPosition);
                light.color *= cookieColor;
            #endif

            float rayDirDotLightDir = dot(rayDirection, -light.direction);
            float scatter = ComputeScattering(rayDirDotLightDir, _AdditionalLightScattering);

            #ifdef GODRAYS_ENCODE_LIGHT_COLOR
                contribution += light.color * (light.shadowAttenuation * light.distanceAttenuation* scatter * _AdditionalLightIntensity);
            #else
                contribution += CalcLuminance(light.color) * light.shadowAttenuation * light.distanceAttenuation* scatter * _AdditionalLightIntensity;
            #endif
        CORGI_LIGHT_LOOP_END

        return contribution;
    }

    // https://thebookofshaders.com/10/
    float random(float2 p)
    {
        return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453123);
    }


    // temporal stuff 
    int _CorgiTemporallyRendered;
    int _CorgiTemporalPassIndex;
    int _CorgiTemporalPassCount;
    float4 _CorgiScreenParams;

    #define TEMPORAL_SKIP(id) if (_CorgiTemporallyRendered > 0) { if (id.x % _CorgiTemporalPassCount != _CorgiTemporalPassIndex && id.y % _CorgiTemporalPassCount != _CorgiTemporalPassIndex) return 0; }

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
    float3 Frag(VaryingsDefault i) : SV_Target
#else
    float Frag(VaryingsDefault i) : SV_Target
#endif
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 

        // temporal skip 
        float2 scaler = float2(_CorgiScreenParams.x, _CorgiScreenParams.y);
        float2 screenPixelId = i.uv.xy * scaler.xy;
        int2 temporalId = int2(screenPixelId.x, screenPixelId.y); 
        // TEMPORAL_SKIP(screenPixelId)

        if (_CorgiTemporallyRendered > 0) 
        { 
            if (temporalId.x % _CorgiTemporalPassCount != _CorgiTemporalPassIndex && temporalId.y % _CorgiTemporalPassCount != _CorgiTemporalPassIndex)
            {
                #ifdef GODRAYS_DISCARD_TEMPORAL
                    discard; 
                #else
                    return 0;
                #endif
            }
        }

        float3 depthWorldPosition = GetWorldSpacePosition(i.uv);

        float3 rayOrigin = _WorldSpaceCameraPos;
        float3 rayDirection = normalize(depthWorldPosition - rayOrigin);
        float rayDistance = length(depthWorldPosition - rayOrigin);

        float maxDistance = _MaxDistance;
        if (rayDistance > maxDistance)
        {
            rayDistance = maxDistance;
        }

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
        float3 accumulated = float3(0.0, 0.0, 0.0);
#else
        float accumulated = 0.0;
#endif

#if defined(VOLUME_STEPS_LOW)
        const int steps = 8;
#elif defined(VOLUME_STEPS_MED)
        const int steps = 16;
#elif defined(VOLUME_STEPS_HIGH)
        const int steps = 32;
#else
        const int steps = 8;
#endif

        float rayDirDotLightDir = dot(rayDirection, -_CorgiMainLightDirection);
        float jitter = random(i.uv) * _Jitter;

        rayOrigin += rayDirection * jitter;

        float maxDistanceRecp = 1.0 / _MaxDistance;

#if defined(VOLUME_STEPS_HIGH)
        [loop]
#endif
        for (int i = 0; i < steps - 1; ++i)
        {
            float t = ((float) i) / ((float) steps); 

            float3 sampleWorldPos = rayOrigin + rayDirection * (t * rayDistance);

            // main light
            #ifdef GODRAYS_ENCODE_LIGHT_COLOR
                float3 contribution = GetMainLightContribution(sampleWorldPos, rayDirDotLightDir);
            #else
                float contribution = GetMainLightContribution(sampleWorldPos, rayDirDotLightDir);
            #endif

            // additional lights
            #if defined(GODRAYS_ADDITIVE_LIGHTS) && (defined(_ADDITIONAL_LIGHTS_VERTEX) || (_ADDITIONAL_LIGHTS))
                contribution += GetAdditionalLightContribution(rayDirection, sampleWorldPos);
            #endif

            #ifdef GODRAYS_VARIABLE_INTENSITY
                float it = (t * rayDistance) * maxDistanceRecp;
                float intensityMult = SAMPLE_TEXTURE2D(_CorgiGodraysIntensityCurveTexture, _LinearClamp, float2(it, 0)).x;
                contribution *= intensityMult;
            #endif

            accumulated += contribution;
        }

        accumulated /= (float) steps; 
        accumulated = clamp(accumulated, -1, 1); 

        return accumulated;
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment Frag
                
                // Universal Pipeline keywords
                // note: screenspace shadows should never be used for this - we need to trace through world space data
                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE // _MAIN_LIGHT_SHADOWS_SCREEN 
                #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
                #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
                #pragma multi_compile_fragment _ _SHADOWS_SOFT
                #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
                #pragma multi_compile_fragment _ _LIGHT_LAYERS
                #pragma multi_compile_fragment _ _LIGHT_COOKIES

                // Unity keywords
                #pragma multi_compile_instancing
                #pragma multi_compile _ _CLUSTERED_RENDERING
                #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
                #pragma multi_compile _ SHADOWS_SHADOWMASK
                #pragma multi_compile _ DIRLIGHTMAP_COMBINED
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ DYNAMICLIGHTMAP_ON

                // our settings 
                #pragma multi_compile VOLUME_STEPS_LOW VOLUME_STEPS_MED VOLUME_STEPS_HIGH 
                #pragma multi_compile _ GODRAYS_MAIN_LIGHT
                #pragma multi_compile _ GODRAYS_ADDITIVE_LIGHTS 
                #pragma multi_compile _ GODRAYS_ADDITIVE_LIGHT_SHADOWS
                #pragma multi_compile _ GODRAYS_VARIABLE_INTENSITY 
                #pragma multi_compile _ GODRAYS_ENCODE_LIGHT_COLOR
                #pragma multi_compile _ GODRAYS_DISCARD_TEMPORAL

            ENDHLSL
        }
    }
}