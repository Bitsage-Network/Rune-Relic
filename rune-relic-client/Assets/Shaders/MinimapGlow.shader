Shader "RuneRelic/MinimapGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.2, 0.8, 1.0, 1.0)
        _GlowColor ("Glow Color", Color) = (0.2, 0.8, 1.0, 0.6)
        _OutlineWidth ("Outline Width (px)", Range(0, 6)) = 1
        _GlowWidth ("Glow Width (px)", Range(0, 12)) = 4
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UI"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _TintColor;
            fixed4 _OutlineColor;
            fixed4 _GlowColor;
            float _OutlineWidth;
            float _GlowWidth;
            float _GlowIntensity;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPosition = v.vertex;
                o.texcoord = v.texcoord;
                o.color = v.color * _TintColor;
                return o;
            }

            fixed SampleMaxAlpha(float2 uv, float2 offset)
            {
                fixed a0 = tex2D(_MainTex, uv + float2(-offset.x, 0)).a;
                fixed a1 = tex2D(_MainTex, uv + float2(offset.x, 0)).a;
                fixed a2 = tex2D(_MainTex, uv + float2(0, -offset.y)).a;
                fixed a3 = tex2D(_MainTex, uv + float2(0, offset.y)).a;
                fixed a4 = tex2D(_MainTex, uv + offset).a;
                fixed a5 = tex2D(_MainTex, uv - offset).a;
                return max(max(max(a0, a1), max(a2, a3)), max(a4, a5));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texcoord);
                fixed alpha = tex.a * i.color.a;
                fixed3 baseColor = tex.rgb * i.color.rgb;

                float2 texel = _MainTex_TexelSize.xy;
                float2 outlineOffset = texel * _OutlineWidth;
                float2 glowOffset = texel * _GlowWidth;

                fixed outline = saturate(SampleMaxAlpha(i.texcoord, outlineOffset) - alpha);
                fixed glow = saturate(SampleMaxAlpha(i.texcoord, glowOffset) - alpha) * _GlowIntensity;

                fixed3 color = baseColor;
                color = lerp(color, _OutlineColor.rgb, outline * _OutlineColor.a);
                color += _GlowColor.rgb * glow * _GlowColor.a;

                fixed finalAlpha = saturate(alpha + outline * _OutlineColor.a + glow * _GlowColor.a);

                #ifdef UNITY_UI_CLIP_RECT
                finalAlpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalAlpha - 0.001);
                #endif

                return fixed4(color, finalAlpha);
            }
            ENDCG
        }
    }
}
