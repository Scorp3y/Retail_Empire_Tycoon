Shader "Game/URP/FlameBorder"
{
    Properties
    {
        _Color("Color", Color) = (0,1,0,1)
        _Intensity("Intensity", Range(0, 10)) = 2.0

        _MainTex("Main (Strip/Noise)", 2D) = "white" {}
        _MainTiling("Main Tiling", Float) = 6.0
        _Speed("Scroll Speed", Float) = 1.5

        _NoiseTex("Noise (optional)", 2D) = "white" {}
        _NoiseStrength("Noise Strength", Range(0,1)) = 0.35
        _NoiseSpeed("Noise Speed", Float) = 0.8

        _Alpha("Alpha", Range(0,1)) = 1.0
        _Cutoff("Soft Cutoff", Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;

                float4 _MainTex_ST;
                float _MainTiling;
                float _Speed;

                float4 _NoiseTex_ST;
                float _NoiseStrength;
                float _NoiseSpeed;

                float _Alpha;
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 uvMain = IN.uv;
                uvMain.x = uvMain.x * _MainTiling + t * _Speed;

                float mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain).r;

                float2 uvNoise = IN.uv;
                uvNoise.x = uvNoise.x * (_MainTiling * 0.75) + t * _NoiseSpeed;
                uvNoise.y = uvNoise.y + t * (_NoiseSpeed * 0.37);

                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvNoise).r;
                float flame = saturate(mainSample + (noise - 0.5) * 2.0 * _NoiseStrength);

                float center = 1.0 - abs(IN.uv.y * 2.0 - 1.0);
                center = saturate(center);
                center = smoothstep(_Cutoff, 1.0, center);

                float a = flame * center * _Alpha;

                float3 rgb = (_Color.rgb * _Intensity) * a;
                rgb *= IN.color.rgb;

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
