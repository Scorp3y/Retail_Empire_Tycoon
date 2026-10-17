Shader "Universal Render Pipeline/Bakery Lettering"
{
    Properties
    {
        _BaseMap("Frozen font atlas",2D)="white"{}
        _BaseColor("Ink",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Input { float4 position:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 position:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Output Vert(Input input)
            {
                Output output=(Output)0; UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position=TransformObjectToHClip(input.position.xyz);output.uv=input.uv;output.fog=ComputeFogFactor(output.position.z);
                return output;
            }
            half4 Frag(Output input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a;
                clip(alpha-.35h);
                return half4(MixFog(_BaseColor.rgb,input.fog),1);
            }
            ENDHLSL
        }
    }
}
