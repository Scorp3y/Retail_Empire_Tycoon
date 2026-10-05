Shader "RetailEmpireTycoon/BuildGridOverlay_URP"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0,0,0,0.75)
        _FillColor ("Fill Color", Color) = (0,0,0,0)
        _CellSize ("Cell Size", Float) = 1
        _LineWidth ("Line Width", Float) = 0.03
        _WorldOrigin ("World Origin", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GridColor;
                half4 _FillColor;
                float _CellSize;
                float _LineWidth;
                float4 _WorldOrigin;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.worldPos = pos.positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 local = IN.worldPos.xz - _WorldOrigin.xz;
                float2 gridUV = local / max(_CellSize, 0.0001);
                float2 f = frac(gridUV);
                float2 edge = min(f, 1.0 - f);

                float normalizedLineWidth = _LineWidth / max(_CellSize, 0.0001);
                float lineMask = 1.0 - saturate(min(edge.x, edge.y) / max(normalizedLineWidth, 0.0001));

                return lerp(_FillColor, _GridColor, lineMask);
            }
            ENDHLSL
        }
    }
}
