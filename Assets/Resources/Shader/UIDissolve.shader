Shader "UI/CustomDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Dissolve)]
        _Progress ("Progress", Range(0,1)) = 0
        _NoiseScale ("Noise Scale", Range(1,200)) = 80
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.15

        [Header(Edge)]
        _EdgeWidth ("Edge Width", Range(0,0.3)) = 0.05
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _EdgeIntensity ("Edge Intensity", Range(0,10)) = 2

        [Header(Glitch)]
        _GlitchStrength ("Glitch Strength", Range(0,0.1)) = 0.02
        _GlitchFrequency ("Glitch Frequency", Range(1,100)) = 40

        [Header(UI)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            Name "UIDissolve"

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TextureSampleAdd;

            float4 _Color;

            float _Progress;
            float _NoiseScale;
            float _NoiseStrength;

            float _EdgeWidth;
            float4 _EdgeColor;
            float _EdgeIntensity;

            float _GlitchStrength;
            float _GlitchFrequency;

            float4 _ClipRect;

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);

                return frac(p.x * p.y);
            }

            v2f vert(appdata v)
            {
                v2f o;

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // 消失中だけGlitchを発生させる
                float active = step(0.001, _Progress)
                             * (1.0 - step(0.999, _Progress));

                float glitchTime = floor(_Time.y * 30.0);

                float glitchLine = floor(uv.y * _GlitchFrequency);

                float glitchNoise = Hash(float2(glitchLine, glitchTime));

                float glitchOffset =
                    (glitchNoise - 0.5) *
                    _GlitchStrength *
                    active;

                float2 sampleUV = uv;

                sampleUV.x += glitchOffset;

                fixed4 col =
                    (tex2D(_MainTex, sampleUV) + _TextureSampleAdd)
                    * i.color;

                // ノイズ生成
                float2 cell = floor(uv * _NoiseScale);

                float noise = Hash(cell);

                // 左から右へ消失する閾値
                float threshold =
                    uv.x +
                    (noise - 0.5) * _NoiseStrength;

                // 進行度の補正
                float progress =
                    lerp(
                        -_NoiseStrength,
                        1.0 + _NoiseStrength,
                        _Progress
                    );

                // 消失判定
                float visible = step(progress, threshold);

                // 消失境界
                float edgeDistance = threshold - progress;

                float edge =
                    step(0.0, edgeDistance) *
                    (1.0 - step(_EdgeWidth, edgeDistance));

                edge *= active;

                // 発光
                col.rgb +=
                    _EdgeColor.rgb *
                    edge *
                    _EdgeIntensity *
                    col.a;

                // 透明度
                col.a *= visible;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(
                    i.worldPosition.xy,
                    _ClipRect
                );
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }

            ENDCG
        }
    }
}