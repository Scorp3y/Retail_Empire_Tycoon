Shader "Hidden/Grabpass"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

    TEXTURE2D_X(_CopyBlitTex);
    SAMPLER(sampler_CopyBlitTex);

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

    float4 Frag(VaryingsDefault i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

        float4 color = SAMPLE_TEXTURE2D_X(_CopyBlitTex, sampler_CopyBlitTex, i.uv);
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
                #pragma multi_compile_fog
                #pragma multi_compile_instancing

            ENDHLSL
        }
    }
}