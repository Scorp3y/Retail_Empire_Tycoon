Shader "RetailEmpire/ProductVertexColor" {
 Properties { _BaseColor("Color",Color)=(1,1,1,1) }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass { Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
 struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; half4 color:COLOR; };
 V vert(A a) { V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;return o; }
 half4 frag(V i):SV_Target { Light l=GetMainLight();half3 n=normalize(i.normalWS);return half4(i.color.rgb*(SampleSH(n)+l.color*saturate(dot(n,l.direction))),1); }
 ENDHLSL
 } } }
