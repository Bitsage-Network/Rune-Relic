Shader "RuneRelic/PlayerForm"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.1

        [Header(Form Evolution)]
        _FormLevel ("Form Level (0-4)", Range(0, 4)) = 0
        _EvolutionProgress ("Evolution Progress", Range(0, 1)) = 0

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (0.5, 0.8, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.8

        [Header(Energy Effect)]
        _EnergyColor ("Energy Color", Color) = (0.3, 0.6, 1.0, 1.0)
        _EnergyIntensity ("Energy Intensity", Range(0, 3)) = 1.0
        _EnergySpeed ("Energy Speed", Range(0, 10)) = 3.0
        _EnergyScale ("Energy Scale", Range(1, 50)) = 15.0

        [Header(Shield Effect)]
        [Toggle] _ShieldActive ("Shield Active", Float) = 0
        _ShieldColor ("Shield Color", Color) = (0.2, 1.0, 0.5, 1.0)
        _ShieldIntensity ("Shield Intensity", Range(0, 3)) = 1.5

        [Header(Speed Effect)]
        [Toggle] _SpeedActive ("Speed Active", Float) = 0
        _SpeedColor ("Speed Color", Color) = (1.0, 1.0, 0.3, 1.0)
        _SpeedTrailIntensity ("Speed Trail", Range(0, 2)) = 1.0

        [Header(Invulnerable)]
        [Toggle] _Invulnerable ("Invulnerable", Float) = 0
        _InvulnerableColor ("Invulnerable Color", Color) = (1.0, 0.8, 0.2, 1.0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #pragma shader_feature _SHIELDACTIVE_ON
            #pragma shader_feature _SPEEDACTIVE_ON
            #pragma shader_feature _INVULNERABLE_ON

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
                half _Glossiness;
                half _Metallic;
                half _FormLevel;
                half _EvolutionProgress;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _EnergyColor;
                half _EnergyIntensity;
                half _EnergySpeed;
                half _EnergyScale;
                half4 _ShieldColor;
                half _ShieldIntensity;
                half4 _SpeedColor;
                half _SpeedTrailIntensity;
                half4 _InvulnerableColor;
            CBUFFER_END

            // Form colors
            static const half3 FormColors[5] = {
                half3(0.9, 0.95, 1.0),   // Spark - white/blue
                half3(0.6, 0.8, 1.0),    // Glyph - light blue
                half3(0.4, 0.9, 0.6),    // Ward - green
                half3(0.7, 0.4, 1.0),    // Arcane - purple
                half3(1.0, 0.8, 0.3)     // Ancient - gold
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Vertex displacement for evolution effect
                float3 posOS = input.positionOS.xyz;
                if (_EvolutionProgress > 0)
                {
                    float wave = sin(_Time.y * 10 + posOS.y * 5) * _EvolutionProgress * 0.1;
                    posOS += input.normalOS * wave;
                }

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
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
                // Normalize inputs
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Sample texture
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                // Get form color
                int formIndex = clamp((int)_FormLevel, 0, 4);
                half3 formColor = FormColors[formIndex];

                // Blend between current and next form during evolution
                if (_EvolutionProgress > 0 && formIndex < 4)
                {
                    formColor = lerp(formColor, FormColors[formIndex + 1], _EvolutionProgress);
                }

                // Apply form color
                albedo.rgb *= formColor;

                // Get main light
                Light mainLight = GetMainLight();

                // Basic lighting
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = albedo.rgb * mainLight.color * NdotL;
                half3 ambient = albedo.rgb * half3(0.15, 0.15, 0.2);

                // Rim light (stronger for higher forms)
                half rimPower = _RimPower - (_FormLevel * 0.3);
                half rim = pow(1.0 - saturate(dot(viewDirWS, normalWS)), rimPower);
                half rimIntensity = _RimIntensity + (_FormLevel * 0.2);
                half3 rimLight = _RimColor.rgb * formColor * rim * rimIntensity;

                // Energy lines (more prominent for higher forms)
                half energy = 0;
                if (_FormLevel > 0)
                {
                    half energyLine = sin(input.positionWS.y * _EnergyScale + _Time.y * _EnergySpeed);
                    energyLine = saturate(energyLine * 2 - 1);
                    energy = energyLine * _EnergyIntensity * (_FormLevel / 4.0);
                }
                half3 energyColor = _EnergyColor.rgb * formColor * energy;

                // Shield effect
                half3 shieldEffect = half3(0, 0, 0);
                #ifdef _SHIELDACTIVE_ON
                    half shieldPulse = sin(_Time.y * 5) * 0.5 + 0.5;
                    shieldEffect = _ShieldColor.rgb * rim * _ShieldIntensity * shieldPulse;
                #endif

                // Speed effect
                half3 speedEffect = half3(0, 0, 0);
                #ifdef _SPEEDACTIVE_ON
                    half speedStreak = saturate(sin(input.positionWS.z * 10 + _Time.y * 20));
                    speedEffect = _SpeedColor.rgb * speedStreak * _SpeedTrailIntensity * rim;
                #endif

                // Invulnerable effect
                half3 invulnEffect = half3(0, 0, 0);
                #ifdef _INVULNERABLE_ON
                    half invulnPulse = sin(_Time.y * 8) * 0.5 + 0.5;
                    invulnEffect = _InvulnerableColor.rgb * invulnPulse * 0.5;
                    // Add golden glow
                    invulnEffect += _InvulnerableColor.rgb * rim * 2;
                #endif

                // Combine
                half3 finalColor = diffuse + ambient + rimLight + energyColor;
                finalColor += shieldEffect + speedEffect + invulnEffect;

                // Evolution flash
                if (_EvolutionProgress > 0.8)
                {
                    half flash = (_EvolutionProgress - 0.8) * 5; // 0.8-1.0 -> 0-1
                    finalColor = lerp(finalColor, half3(1, 1, 1), flash);
                }

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // Shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
