Shader "Hidden/DepthGrabpass"
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
        UNITY_VERTEX_OUTPUT_STEREO
    };

    // TEXTURE2D_X(_CopyBlitTex);
    // SAMPLER(sampler_CopyBlitTex);

#if _GODRAYS_USE_UNITY_DEPTH
    float SampleDepth(float2 uv)
    {
        return SampleSceneDepth(uv); 
    }
#else
    #if defined(_DEPTH_MSAA_2)
        #define MSAA_SAMPLES 2
    #elif defined(_DEPTH_MSAA_4)
        #define MSAA_SAMPLES 4
    #elif defined(_DEPTH_MSAA_8)
        #define MSAA_SAMPLES 8
    #else
        #define MSAA_SAMPLES 1
    #endif

    #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
        #define DEPTH_TEXTURE_MS(name, samples) Texture2DMSArray<float, samples> name
        #define DEPTH_TEXTURE(name) TEXTURE2D_ARRAY_FLOAT(name)
        #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_ARRAY_MSAA(_CopyBlitTex, uv, unity_StereoEyeIndex, sampleIndex)
        #define SAMPLE(uv) SAMPLE_TEXTURE2D_ARRAY(_CopyBlitTex, sampler_CopyBlitTex, uv, unity_StereoEyeIndex).r
    #else
        #define DEPTH_TEXTURE_MS(name, samples) Texture2DMS<float, samples> name
        #define DEPTH_TEXTURE(name) TEXTURE2D_FLOAT(name)
        #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_MSAA(_CopyBlitTex, uv, sampleIndex)
        #define SAMPLE(uv) SAMPLE_DEPTH_TEXTURE(_CopyBlitTex, sampler_CopyBlitTex, uv)
    #endif

    #if UNITY_REVERSED_Z
        #define DEPTH_DEFAULT_VALUE 1.0
        #define DEPTH_OP min
    #else
        #define DEPTH_DEFAULT_VALUE 0.0
        #define DEPTH_OP max
    #endif

    #if MSAA_SAMPLES == 1
        DEPTH_TEXTURE(_CopyBlitTex);
        SAMPLER(sampler_CopyBlitTex);
    #else
        DEPTH_TEXTURE_MS(_CopyBlitTex, MSAA_SAMPLES);
        float4 _CopyBlitTex_TexelSize;
    #endif

        
    float SampleDepth(float2 uv)
    {
        #if MSAA_SAMPLES == 1
            return SAMPLE(uv);
        #else
            int2 coord = int2(uv * _CopyBlitTex_TexelSize.zw);
            float outDepth = DEPTH_DEFAULT_VALUE;

            UNITY_UNROLL
                for (int i = 0; i < MSAA_SAMPLES; ++i)
                    outDepth = DEPTH_OP(LOAD(coord, i), outDepth);
            return outDepth;
        #endif
    }
#endif



    VaryingsDefault VertDefault(AttributesDefault v)
    {
        VaryingsDefault o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.positionCS = float4(v.positionHCS.xyz, 1.0);

#if UNITY_UV_STARTS_AT_TOP
        o.positionCS.y *= -1;
#endif

        o.uv = v.uv;

        return o;
    }

    float Frag(VaryingsDefault i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

        return SampleDepth(i.uv);
        // return SAMPLE_TEXTURE2D_X(_CopyBlitTex, sampler_CopyBlitTex, i.uv).r;
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
                #pragma multi_compile_fog
                #pragma multi_compile_instancing

                #pragma multi_compile _ _DEPTH_MSAA_2 _DEPTH_MSAA_4 _DEPTH_MSAA_8
                #pragma multi_compile _ _GODRAYS_USE_UNITY_DEPTH

            ENDHLSL
        }
    }
}