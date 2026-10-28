Shader "UI/CustomBurnDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Dissolve)]
        _Progress ("Progress", Range(0,1)) = 0

        [Enum(LeftToRight,0,RightToLeft,1,BottomToTop,2,TopToBottom,3)]
        _Direction ("Direction", Float) = 0

        [Header(Noise)]
        _NoiseScale ("Noise Scale", Range(1,100)) = 18
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.18
        _NoiseSpeed ("Noise Speed", Range(0,10)) = 1.5

        [Header(Edge)]
        _EdgeWidth ("Edge Width", Range(0,0.2)) = 0.018
        _EdgeColor ("Edge Color", Color) = (1,0.95,0.8,1)
        _EdgeIntensity ("Edge Intensity", Range(0,5)) = 1.5

        _OuterEdgeWidth ("Outer Edge Width", Range(0,0.3)) = 0.06
        _OuterEdgeColor ("Outer Edge Color", Color) = (0.8,0.5,0.2,1)

        [Header(UI)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)]
        _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "BurnDissolve"

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
                float2 localUV : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TextureSampleAdd;

            float4 _Color;

            float _Progress;
            float _Direction;

            float _NoiseScale;
            float _NoiseStrength;
            float _NoiseSpeed;

            float _EdgeWidth;
            float4 _EdgeColor;
            float _EdgeIntensity;

            float _OuterEdgeWidth;
            float4 _OuterEdgeColor;

            float4 _ClipRect;

            // 疑似乱数
            float Hash(float2 p)
            {
                return frac(
                    sin(dot(p, float2(127.1, 311.7))) *
                    43758.5453
                );
            }

            // Value Noise
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(a, b, u.x),
                    lerp(c, d, u.x),
                    u.y
                );
            }

            // Fractal Brownian Motion
            float FBM(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 4; i++)
                {
                    value += ValueNoise(p) * amplitude;

                    p *= 2.0;
                    amplitude *= 0.5;
                }

                return value / 0.9375;
            }

            // 消失方向の取得
            float GetDirection(float2 uv)
            {
                if (_Direction < 0.5)
                    return uv.x;

                if (_Direction < 1.5)
                    return 1.0 - uv.x;

                if (_Direction < 2.5)
                    return uv.y;

                return 1.0 - uv.y;
            }

            v2f vert(appdata v)
            {
                v2f o;

                o.worldPosition = v.vertex;

                o.vertex = UnityObjectToClipPos(v.vertex);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // 消失判定用UV
                o.localUV = v.uv;

                o.color = v.color * _Color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.localUV;

                fixed4 col =
                    (tex2D(_MainTex, i.uv) + _TextureSampleAdd)
                    * i.color;

                // 進行度0では完全表示
                if (_Progress <= 0.0001)
                {
                    #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(
                        i.worldPosition.xy,
                        _ClipRect
                    );
                    #endif

                    return col;
                }

                // 進行度1では完全消失
                if (_Progress >= 0.9999)
                {
                    return fixed4(0,0,0,0);
                }

                // 時間によるノイズの移動
                float time = _Time.y * _NoiseSpeed;

                float2 noiseUV =
                    uv * _NoiseScale +
                    float2(0.0, time);

                // 大きなうねり
                float largeNoise = FBM(noiseUV * 0.5);

                // 細かい揺らぎ
                float smallNoise = FBM(noiseUV * 2.0);

                // ノイズ合成
                float noise =
                    largeNoise * 0.75 +
                    smallNoise * 0.25;

                noise = noise * 2.0 - 1.0;

                // 消失方向
                float direction = GetDirection(uv);

                // 炎状の境界
                float threshold =
                    direction +
                    noise * _NoiseStrength;

                // 進行度補正
                float progress = lerp(
                    -_NoiseStrength,
                    1.0 + _NoiseStrength,
                    _Progress
                );

                // 境界までの距離
                float distance = threshold - progress;

                // 消失マスク
                float visible = step(0.0, distance);

                // 内側の細いエッジ
                float innerEdge =
                    step(0.0, distance) *
                    (1.0 - step(_EdgeWidth, distance));

                // 外側の広いエッジ
                float outerEdge =
                    step(0.0, distance) *
                    (1.0 - step(_OuterEdgeWidth, distance));

                // 外側の色
                col.rgb = lerp(
                    col.rgb,
                    _OuterEdgeColor.rgb,
                    outerEdge * _OuterEdgeColor.a
                );

                // 内側の明るい色
                col.rgb +=
                    _EdgeColor.rgb *
                    innerEdge *
                    _EdgeIntensity *
                    _EdgeColor.a;

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