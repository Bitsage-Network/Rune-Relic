Shader "RuneRelic/RuneGlow"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

        [Header(Glow Effect)]
        _GlowColor ("Glow Color", Color) = (0.3, 0.7, 1.0, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.0
        _GlowPulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _GlowPulseAmount ("Pulse Amount", Range(0, 1)) = 0.3

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 3)) = 1.5

        [Header(Holographic)]
        _HoloIntensity ("Holographic Intensity", Range(0, 1)) = 0.5
        _HoloSpeed ("Holographic Speed", Range(0, 10)) = 1.0
        _HoloScale ("Holographic Scale", Range(1, 100)) = 20.0

        [Header(Rainbow Mode)]
        [Toggle] _RainbowMode ("Rainbow Mode (Chaos Rune)", Float) = 0
        _RainbowSpeed ("Rainbow Speed", Range(0.1, 5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature _RAINBOWMODE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _GlowColor;
                half _GlowIntensity;
                half _GlowPulseSpeed;
                half _GlowPulseAmount;
                half _FresnelPower;
                half _FresnelIntensity;
                half _HoloIntensity;
                half _HoloSpeed;
                half _HoloScale;
                half _RainbowSpeed;
            CBUFFER_END

            // HSV to RGB conversion
            half3 HSVtoRGB(half3 hsv)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
                return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Base color
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                // Rainbow mode for chaos rune
                half3 glowColor = _GlowColor.rgb;
                #ifdef _RAINBOWMODE_ON
                    half hue = frac(_Time.y * _RainbowSpeed);
                    glowColor = HSVtoRGB(half3(hue, 1.0, 1.0));
                #endif

                // Fresnel effect
                half fresnel = pow(1.0 - saturate(dot(viewDirWS, normalWS)), _FresnelPower);
                fresnel *= _FresnelIntensity;

                // Glow pulse
                half pulse = 1.0 + sin(_Time.y * _GlowPulseSpeed) * _GlowPulseAmount;
                half3 glow = glowColor * _GlowIntensity * pulse;

                // Holographic scan lines
                half holo = 0;
                if (_HoloIntensity > 0)
                {
                    half scanLine = sin(input.positionWS.y * _HoloScale + _Time.y * _HoloSpeed);
                    holo = saturate(scanLine) * _HoloIntensity;
                }

                // Combine
                half3 finalColor = baseColor.rgb + glow * fresnel + glowColor * holo;
                half alpha = saturate(baseColor.a + fresnel * 0.5 + holo * 0.3);

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
