Shader "UI/FragmentDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Animation)]
        _Progress ("Progress", Range(0,1)) = 0

        [Header(Fragment)]
        _FragmentCount ("Fragment Count", Range(3,60)) = 18
        _FragmentRandomness ("Fragment Randomness", Range(0,1)) = 0.8
        _FragmentSpread ("Fragment Spread", Range(0,0.5)) = 0.08
        _FragmentRotation ("Fragment Rotation", Range(0,180)) = 25
        _FragmentDelay ("Fragment Delay", Range(0,0.8)) = 0.3

        [Header(Edge)]
        _EdgeWidth ("Edge Width", Range(0,0.1)) = 0.012
        _EdgeColor ("Edge Color", Color) = (1,0.95,0.8,1)
        _EdgeIntensity ("Edge Intensity", Range(0,5)) = 1.5

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
            Name "FragmentDissolve"

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
                float2 localPosition : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TextureSampleAdd;

            float4 _Color;
            float4 _ClipRect;

            float _Progress;

            float _FragmentCount;
            float _FragmentRandomness;
            float _FragmentSpread;
            float _FragmentRotation;
            float _FragmentDelay;

            float _EdgeWidth;
            float4 _EdgeColor;
            float _EdgeIntensity;

            float Hash(float2 p)
            {
                return frac(
                    sin(dot(p, float2(127.1, 311.7))) *
                    43758.5453
                );
            }

            float2 Hash2(float2 p)
            {
                return float2(
                    Hash(p),
                    Hash(p + 19.19)
                );
            }

            // Voronoi領域の情報
            struct VoronoiResult
            {
                float2 cell;
                float2 center;
                float distance;
                float edgeDistance;
            };

            VoronoiResult GetVoronoi(float2 uv)
            {
                float2 gridUV = uv * _FragmentCount;

                float2 baseCell = floor(gridUV);

                float minDistance = 1000.0;

                float2 nearestCell = 0;
                float2 nearestCenter = 0;

                // 最も近いVoronoi中心を検索
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell =
                            baseCell + float2(x, y);

                        float2 random = Hash2(cell);

                        float2 center =
                            cell + 0.5 +
                            (random - 0.5) *
                            _FragmentRandomness;

                        float distance =
                            length(gridUV - center);

                        if (distance < minDistance)
                        {
                            minDistance = distance;

                            nearestCell = cell;
                            nearestCenter = center;
                        }
                    }
                }

                // 2番目に近い中心までの距離
                float secondDistance = 1000.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell =
                            baseCell + float2(x, y);

                        if (all(cell == nearestCell))
                            continue;

                        float2 random = Hash2(cell);

                        float2 center =
                            cell + 0.5 +
                            (random - 0.5) *
                            _FragmentRandomness;

                        float distance =
                            length(gridUV - center);

                        secondDistance =
                            min(secondDistance, distance);
                    }
                }

                VoronoiResult result;

                result.cell = nearestCell;
                result.center = nearestCenter / _FragmentCount;
                result.distance = minDistance;

                result.edgeDistance =
                    secondDistance - minDistance;

                return result;
            }

            float2 Rotate2D(float2 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);

                return float2(
                    p.x * c - p.y * s,
                    p.x * s + p.y * c
                );
            }

            v2f vert(appdata v)
            {
                v2f o;

                o.worldPosition = v.vertex;

                o.vertex =
                    UnityObjectToClipPos(v.vertex);

                o.uv =
                    TRANSFORM_TEX(v.uv, _MainTex);

                o.localPosition = v.uv;

                o.color = v.color * _Color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.localPosition;

                // 初期状態
                if (_Progress <= 0.0001)
                {
                    fixed4 col =
                        (tex2D(_MainTex, i.uv) +
                        _TextureSampleAdd) *
                        i.color;

                    #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(
                        i.worldPosition.xy,
                        _ClipRect
                    );
                    #endif

                    return col;
                }

                if (_Progress >= 0.9999)
                {
                    return fixed4(0,0,0,0);
                }

                // Voronoi領域取得
                VoronoiResult voronoi =
                    GetVoronoi(uv);

                float2 cell = voronoi.cell;

                // 破片ごとのランダム値
                float randomDelay = Hash(cell + 11.7);
                float randomAngle = Hash(cell + 25.3);
                float randomSpeed = Hash(cell + 41.9);

                // 破片ごとの進行度
                float delay =
                    randomDelay * _FragmentDelay;

                float localProgress =
                    saturate(
                        (_Progress - delay) /
                        max(0.001, 1.0 - _FragmentDelay)
                    );

                // 破片の縮小率
                float scale =
                    1.0 - localProgress;

                scale = max(scale, 0.001);

                // 破片の移動方向
                float angle =
                    randomAngle * 6.2831853;

                float2 direction =
                    float2(cos(angle), sin(angle));

                float spread =
                    localProgress *
                    _FragmentSpread *
                    (0.5 + randomSpeed);

                float2 offset =
                    direction * spread;

                // 破片の回転
                float rotation =
                    radians(_FragmentRotation) *
                    (randomAngle * 2.0 - 1.0) *
                    localProgress;

                // 破片中心からの相対座標
                float2 relative =
                    uv - voronoi.center - offset;

                relative =
                    Rotate2D(relative, -rotation);

                relative /= scale;

                // 元のImage上の座標
                float2 sampleUV =
                    voronoi.center + relative;

                // 破片の縮小判定
                VoronoiResult sampleVoronoi =
                    GetVoronoi(sampleUV);

                float sameCell =
                    all(sampleVoronoi.cell == cell)
                    ? 1.0 : 0.0;

                // 元画像をサンプリング
                fixed4 col =
                    (tex2D(_MainTex, sampleUV) +
                    _TextureSampleAdd) *
                    i.color;

                // 輪郭の検出
                float edge =
                    1.0 -
                    smoothstep(
                        0.0,
                        _EdgeWidth,
                        sampleVoronoi.edgeDistance
                    );

                // 崩壊開始後だけ発光
                float edgeActive =
                    smoothstep(
                        0.0,
                        0.2,
                        localProgress
                    );

                col.rgb +=
                    _EdgeColor.rgb *
                    edge *
                    _EdgeIntensity *
                    edgeActive *
                    _EdgeColor.a;

                // 透明度
                col.a *= sameCell;

                // 終盤で徐々に透明化
                col.a *=
                    1.0 -
                    smoothstep(
                        0.75,
                        1.0,
                        localProgress
                    );

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