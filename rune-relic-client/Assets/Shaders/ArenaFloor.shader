Shader "RuneRelic/ArenaFloor"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.1, 0.1, 0.15, 1.0)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

        [Header(Grid)]
        _GridColor ("Grid Color", Color) = (0.2, 0.3, 0.4, 1.0)
        _GridScale ("Grid Scale", Range(1, 50)) = 10.0
        _GridThickness ("Grid Thickness", Range(0.01, 0.2)) = 0.05
        _GridIntensity ("Grid Intensity", Range(0, 2)) = 0.5

        [Header(Glow Lines)]
        _GlowColor ("Glow Color", Color) = (0.3, 0.6, 1.0, 1.0)
        _GlowSpeed ("Glow Speed", Range(0, 10)) = 2.0
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.3

        [Header(Boundary)]
        _BoundaryColor ("Boundary Color", Color) = (0.0, 0.8, 1.0, 1.0)
        _BoundaryWidth ("Boundary Width", Range(0, 10)) = 2.0
        _BoundaryGlow ("Boundary Glow", Range(0, 5)) = 2.0
        _ArenaWidth ("Arena Width", Float) = 100
        _ArenaHeight ("Arena Height", Float) = 100

        [Header(Shrink Zone)]
        _ShrinkColor ("Shrink Zone Color", Color) = (1.0, 0.2, 0.2, 1.0)
        _CurrentWidth ("Current Width", Float) = 100
        _CurrentHeight ("Current Height", Float) = 100
        _ShrinkPulse ("Shrink Pulse Speed", Range(0, 10)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _GridColor;
                half _GridScale;
                half _GridThickness;
                half _GridIntensity;
                half4 _GlowColor;
                half _GlowSpeed;
                half _GlowIntensity;
                half4 _BoundaryColor;
                half _BoundaryWidth;
                half _BoundaryGlow;
                half _ArenaWidth;
                half _ArenaHeight;
                half4 _ShrinkColor;
                half _CurrentWidth;
                half _CurrentHeight;
                half _ShrinkPulse;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Base color
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                // Grid pattern
                float2 gridPos = input.positionWS.xz * _GridScale;
                float2 grid = abs(frac(gridPos) - 0.5);
                float gridLine = min(grid.x, grid.y);
                float gridMask = 1.0 - saturate(gridLine / _GridThickness);
                half3 gridColor = _GridColor.rgb * gridMask * _GridIntensity;

                // Animated glow lines
                float glowLine1 = sin(input.positionWS.x * 0.5 + _Time.y * _GlowSpeed);
                float glowLine2 = sin(input.positionWS.z * 0.5 - _Time.y * _GlowSpeed * 0.7);
                float glow = saturate(glowLine1 * 0.5 + 0.5) * saturate(glowLine2 * 0.5 + 0.5);
                half3 glowColor = _GlowColor.rgb * glow * _GlowIntensity;

                // Calculate distance to boundary
                float halfWidth = _ArenaWidth * 0.5;
                float halfHeight = _ArenaHeight * 0.5;
                float distX = halfWidth - abs(input.positionWS.x);
                float distZ = halfHeight - abs(input.positionWS.z);
                float distToBoundary = min(distX, distZ);

                // Boundary glow
                float boundaryMask = 1.0 - saturate(distToBoundary / _BoundaryWidth);
                boundaryMask = pow(boundaryMask, _BoundaryGlow);
                half3 boundaryColor = _BoundaryColor.rgb * boundaryMask;

                // Shrink zone danger indicator
                half3 shrinkColor = half3(0, 0, 0);
                if (_CurrentWidth < _ArenaWidth)
                {
                    float halfCurrentWidth = _CurrentWidth * 0.5;
                    float halfCurrentHeight = _CurrentHeight * 0.5;

                    // Check if outside current safe zone
                    bool outsideSafe = abs(input.positionWS.x) > halfCurrentWidth ||
                                       abs(input.positionWS.z) > halfCurrentHeight;

                    if (outsideSafe)
                    {
                        float pulse = sin(_Time.y * _ShrinkPulse) * 0.5 + 0.5;
                        shrinkColor = _ShrinkColor.rgb * (0.3 + pulse * 0.4);
                    }
                    else
                    {
                        // Warning zone near edge
                        float distToShrinkX = halfCurrentWidth - abs(input.positionWS.x);
                        float distToShrinkZ = halfCurrentHeight - abs(input.positionWS.z);
                        float distToShrink = min(distToShrinkX, distToShrinkZ);

                        if (distToShrink < 5.0)
                        {
                            float warnMask = 1.0 - (distToShrink / 5.0);
                            float pulse = sin(_Time.y * _ShrinkPulse * 2) * 0.5 + 0.5;
                            shrinkColor = _ShrinkColor.rgb * warnMask * pulse * 0.3;
                        }
                    }
                }

                // Combine
                half3 finalColor = baseColor.rgb + gridColor + glowColor + boundaryColor + shrinkColor;

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
