Shader "Hidden/CorgiGodRays/BilateralBlur"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

    TEXTURE2D_X(_BlurInputTex);
    SAMPLER(sampler_BlurInputTex);

    float4 _GodRaysParams;

    float GetSceneDepth(float2 uv)
    {
        float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
        return depth; 
    }

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
    float3 Frag(VaryingsDefault i) : SV_Target
#else
    float Frag(VaryingsDefault i) : SV_Target
#endif
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

        // todo: unity_reversed_z
        float depth = GetSceneDepth(i.uv);
        
#ifdef SAMPLE_COUNT_LOW
        const int sampleCount = 1;
#elif SAMPLE_COUNT_MED
        const int sampleCount = 2;
#else
        const int sampleCount = 4;
#endif

#ifdef BLUR_X
        const float2 direction = float2(_GodRaysParams.z, 0);
#else 
        const float2 direction = float2(0, _GodRaysParams.w);
#endif

        const float blurDepthFalloff = 100.0;
        const float gaussianWeights[] = { 0.14446445, 0.13543542, 0.11153505, 0.08055309, 0.05087564, 0.02798160, 0.01332457, 0.00545096 };

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
        float3 accumulatedColors = float3(0.0, 0.0, 0.0);
#else
        float accumulatedColors = 0;
#endif
        float accumulatedWeights = 0; 

        for (int index = -sampleCount; index <= sampleCount; ++index)
        {
            float2 uv = i.uv + direction * index;

#ifdef GODRAYS_ENCODE_LIGHT_COLOR
            float3 sampleColor = SAMPLE_TEXTURE2D_X(_BlurInputTex, sampler_BlurInputTex, uv);
#else
            float sampleColor = SAMPLE_TEXTURE2D_X(_BlurInputTex, sampler_BlurInputTex, uv).r;
#endif
            float sampleDepth = GetSceneDepth(uv);

            float depthDifference = abs(depth - sampleDepth);
            float r2 = depthDifference * blurDepthFalloff;
            float g = exp(-r2 * r2); 
            float weight = g * gaussianWeights[abs(index)];

            accumulatedColors += sampleColor * weight;
            accumulatedWeights += weight; 
        }
        
        return accumulatedColors / accumulatedWeights;
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

                #pragma multi_compile BLUR_X BLUR_Y
                #pragma multi_compile SAMPLE_COUNT_LOW SAMPLE_COUNT_MED SAMPLE_COUNT_HIGH
                #pragma multi_compile _ GODRAYS_ENCODE_LIGHT_COLOR

            ENDHLSL
        }
    }
}