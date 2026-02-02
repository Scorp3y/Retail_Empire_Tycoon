
Shader "MyShop/URP_GreenLaserFence"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 1, 0.35, 1)
        _Intensity ("Intensity", Range(0, 50)) = 12
        _BaseAlpha ("Base Alpha", Range(0, 2)) = 0.35

        _NoiseScale ("Noise Scale", Range(0.1, 50)) = 8
        _NoiseSpeed ("Noise Speed", Range(0, 10)) = 1.2
        _NoiseAmt ("Noise Amount", Range(0, 2)) = 0.6

        _GridFreq ("Grid Freq", Range(0, 200)) = 60
        _GridSpeed ("Grid Speed", Range(0, 10)) = 1.5
        _GridAmt ("Grid Amount", Range(0, 2)) = 0.35

        _FresnelPower ("Fresnel Power", Range(0.5, 12)) = 4
        _FresnelAmt ("Fresnel Amount", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Name "UnlitAdditive"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _Intensity, _BaseAlpha;
                half  _NoiseScale, _NoiseSpeed, _NoiseAmt;
                half  _GridFreq, _GridSpeed, _GridAmt;
                half  _FresnelPower, _FresnelAmt;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
            };

            // tiny hash + value noise (no texture)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(positionWS);

                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS);

                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float t = _Time.y;

                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);

                float ndv = saturate(dot(n, v));
                float fres = pow(1.0 - ndv, _FresnelPower) * _FresnelAmt;

                float2 nuv = IN.uv * _NoiseScale + float2(t * _NoiseSpeed, t * (_NoiseSpeed * 0.37));
                float nval = valueNoise(nuv) * _NoiseAmt;

                float gridU = sin((IN.uv.x * _GridFreq) + t * _GridSpeed) * 0.5 + 0.5;
                float gridV = sin((IN.uv.y * (_GridFreq * 0.35)) - t * (_GridSpeed * 0.8)) * 0.5 + 0.5;
                float grid = (gridU * gridV) * _GridAmt;

                float alpha = saturate(_BaseAlpha + fres + nval + grid);

                float3 col = _Color.rgb * (_Intensity * alpha);
                return half4(col, 1.0);
            }
            ENDHLSLPROGRAM
        }
    }

    FallBack Off
}
