Shader "Universal Render Pipeline/Town Surface"
{
    Properties
    {
        _BaseMap("Palette",2D)="white" {}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Smoothness("Smoothness",Range(0,1))=.16
        _Metallic("Metallic",Range(0,1))=0
        _Cull("Cull",Float)=2
        _Cutoff("Alpha cutoff",Range(0,1))=.5
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half _Cutoff;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                float2 nativeXZ:TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float2 uv:TEXCOORD2;
                float2 nativeXZ:TEXCOORD3;
                float4 shadowCoord:TEXCOORD4;
                half fog:TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,output);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=position.positionCS;output.positionWS=position.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.uv=input.uv;
                // Native town coordinates are baked into UV1; details retain the same scale in Game and City.
                output.nativeXZ=input.nativeXZ;output.shadowCoord=GetShadowCoord(position);output.fog=ComputeFogFactor(position.positionCS.z);
                return output;
            }
            float Grain(float2 p)
            {
                return frac(sin(dot(floor(p),float2(127.1,311.7)))*43758.5453);
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
                float kind=floor(input.uv.x*32);
                if(input.normalWS.y>.65)
                {
                    float noise=Grain(input.nativeXZ*18);
                    if(kind==7)color*=.97+noise*.06; // Fine aggregate in asphalt, kept subtle at driving distance.
                    else if(kind==8||kind==26)color*=.95+noise*.085;
                    else if(kind==15||kind==12)
                    {
                        float macro=sin(input.nativeXZ.x*.09)*sin(input.nativeXZ.y*.07);
                        color*=.95+macro*.07+noise*.035;
                    }
                    else if(kind==27||kind==25)color*=.92+noise*.15;
                }
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;lighting.normalWS=normalize(input.normalWS);
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord=input.shadowCoord;lighting.fogCoord=input.fog;
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color;surface.alpha=1;surface.metallic=_Metallic;
                surface.smoothness=_Smoothness;surface.occlusion=1;surface.normalTS=half3(0,0,1);
                half4 result=UniversalFragmentPBR(lighting,surface);result.rgb=MixFog(result.rgb,input.fog);return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
