Shader "Hidden/CorgiGodRays/ApplyGodRays"
{
    Properties
    {
        _Intensity("_Intensity", Float) = 1.0
        _TintColor("_TintColor", Color) = (1,1,1,1)
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
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

    TEXTURE2D_X(_CopyBlitTex);
    SAMPLER(sampler_CopyBlitTex);

    TEXTURE2D_X(_GodRaysTexture);
    SAMPLER(sampler_GodRaysTexture);

    // in sharedinput 
    // TEXTURE2D_X(_CorgiDepthGrabpassFullRes);
    // SAMPLER(sampler_CorgiDepthGrabpassFullRes);

    TEXTURE2D_X(_CorgiDepthGrabpassNonFullRes);
    SAMPLER(sampler_CorgiDepthGrabpassNonFullRes);

    // float _Intensity;
    half4 _TintColor;
    
    float4 Frag(VaryingsDefault i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

        float4 color = SAMPLE_TEXTURE2D_X(_CopyBlitTex, sampler_CopyBlitTex, i.uv);

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
        float3 godrays = float3(0.0, 0.0, 0.0);
#else
        float godrays = 0.0;
#endif

#ifdef DEPTH_AWARE_UPSAMPLE
        float hd0 = SAMPLE_TEXTURE2D_X(_CorgiDepthGrabpassFullRes, sampler_CorgiDepthGrabpassFullRes, i.uv).r;
        float dd0 = SAMPLE_TEXTURE2D_X_BIAS(_CorgiDepthGrabpassNonFullRes, sampler_CorgiDepthGrabpassNonFullRes, i.uv, int2(0, 1)).r;
        float dd1 = SAMPLE_TEXTURE2D_X_BIAS(_CorgiDepthGrabpassNonFullRes, sampler_CorgiDepthGrabpassNonFullRes, i.uv, int2(0, -1)).r;
        float dd2 = SAMPLE_TEXTURE2D_X_BIAS(_CorgiDepthGrabpassNonFullRes, sampler_CorgiDepthGrabpassNonFullRes, i.uv, int2(1, 0)).r;
        float dd3 = SAMPLE_TEXTURE2D_X_BIAS(_CorgiDepthGrabpassNonFullRes, sampler_CorgiDepthGrabpassNonFullRes, i.uv, int2(-1, 0)).r;

        float d0 = abs(hd0 - dd0);
        float d1 = abs(hd0 - dd1);
        float d2 = abs(hd0 - dd2);
        float d3 = abs(hd0 - dd3);

        float minD = min(min(d0, d1), min(d2, d3));

        if (minD == d0) 
        {
            godrays = SAMPLE_TEXTURE2D_X_BIAS(_GodRaysTexture, sampler_GodRaysTexture, i.uv, int2(0, 1));
        }
        else if (minD == d1)
        {
            godrays = SAMPLE_TEXTURE2D_X_BIAS(_GodRaysTexture, sampler_GodRaysTexture, i.uv, int2(0, -1));
        }
        else if (minD == d2)
        {
            godrays = SAMPLE_TEXTURE2D_X_BIAS(_GodRaysTexture, sampler_GodRaysTexture, i.uv, int2(1, 0));
        }
        else if (minD == d3)
        {
            godrays = SAMPLE_TEXTURE2D_X_BIAS(_GodRaysTexture, sampler_GodRaysTexture, i.uv, int2(-1, 0));
        }
        else
        {
            godrays = SAMPLE_TEXTURE2D_X(_GodRaysTexture, sampler_GodRaysTexture, i.uv);
        }
#else
        godrays = SAMPLE_TEXTURE2D_X(_GodRaysTexture, sampler_GodRaysTexture, i.uv);
#endif
        

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
        float3 lightColor = float3(0.0, 0.0, 0.0);
#else
        #ifdef GODRAYS_MAIN_LIGHT
            Light mainLight = GetMainLight();
            float3 lightColor = mainLight.color;
        #else
            float3 lightColor = float3(0.0, 0.0, 0.0);
        #endif

        #if defined(GODRAYS_ADDITIVE_LIGHTS) && (defined(_ADDITIONAL_LIGHTS_VERTEX) || (_ADDITIONAL_LIGHTS))
            float3 worldPosition = GetWorldSpacePosition(i.uv);

            // additional light colors will only be right if they're near an opaque surface, when it's done like this
            // if we want the volumetrics to be colored, it'll need to store the color in the godrays texture 
            uint pixelLightCount = GetCorgiLightCount();
            CORGI_LIGHT_LOOP_BEGIN(pixelLightCount)
                Light light = GetCorgiAdditionalPerObjectLight(lightIndex, worldPosition);
                lightColor += light.color * light.distanceAttenuation;
            CORGI_LIGHT_LOOP_END
        #endif
#endif

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
        color.rgb += (godrays * _TintColor);
#else
        color.rgb += (lightColor * _TintColor) * (godrays);
#endif

        return color;
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
                #pragma multi_compile_instancing

                #pragma multi_compile _ DEPTH_AWARE_UPSAMPLE
                #pragma multi_compile _ GODRAYS_MAIN_LIGHT
                #pragma multi_compile _ GODRAYS_ADDITIVE_LIGHTS
                #pragma multi_compile _ GODRAYS_ENCODE_LIGHT_COLOR

                // Universal Pipeline keywords
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

                // single pass instanced rendering 
                // #pragma multi_compile _ USING_STEREO_MATRICES
                // #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED

            ENDHLSL
        }
    }
}