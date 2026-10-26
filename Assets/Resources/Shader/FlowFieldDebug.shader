// 流れのデバッグ表示
//   Color : 向き = 色相、強さ = 濃さ
//   Arrow : セルごとに矢印(向き = 矢印の向き/色、強さ = 長さ)
//   Both  : 薄い色 + 矢印
Shader "Debug/FlowField"
{
    Properties
    {
        _FlowTex ("Flow (RG)", 2D) = "black" {}
        _MaxStrength ("Max Strength", Float) = 5
        _Alpha ("Alpha", Range(0, 1)) = 0.7
        _GridSize ("Grid Size (xy)", Vector) = (128, 128, 0, 0)
        _GridLine ("Grid Line Alpha", Range(0, 1)) = 0.15
        [Enum(Color,0,Arrow,1,Both,2)] _Mode ("Mode", Float) = 0
        _ArrowStep ("Arrow Step (cells)", Float) = 1
        _ArrowThickness ("Arrow Thickness", Range(0.01, 0.2)) = 0.06
        _MinStrength ("Hide Below (0-1)", Range(0, 0.2)) = 0.02
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FlowTex;
            float _MaxStrength;
            float _Alpha;
            float4 _GridSize;
            float _GridLine;
            float _Mode;
            float _ArrowStep;
            float _ArrowThickness;
            float _MinStrength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 Hue(float h)
            {
                return saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
            }

            float3 DirColor(float2 f)
            {
                return Hue(atan2(f.y, f.x) / (2 * UNITY_PI) + 0.5);
            }

            // 線分 a-b までの距離
            float SegmentDist(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float t = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * t);
            }

            // 矢印のマスク(0~1)。p は矢印のローカル座標(+x が矢印の向き)
            float ArrowMask(float2 p, float halfLen, float th, float aa)
            {
                float headLen = min(0.18, halfLen);
                float headW   = 0.14;

                // 軸
                float dShaft = SegmentDist(p, float2(-halfLen, 0), float2(halfLen - headLen, 0)) - th * 0.5;

                // 先端の三角形(簡易SDF)
                float k = headW / headLen;
                float dHead = max(max(p.x - halfLen, (halfLen - headLen) - p.x),
                                  abs(p.y) - (halfLen - p.x) * k);

                float d = min(dShaft, dHead);
                return 1 - smoothstep(-aa, aa, d);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 gridPos = i.uv * _GridSize.xy;

                // ---- Color ----
                float2 f = tex2D(_FlowTex, i.uv).rg;
                float mag = saturate(length(f) / _MaxStrength);
                float3 col = DirColor(f);
                float a = _Alpha * saturate(mag * 2);
                if (_Mode > 1.5) a *= 0.35;            // Both のときは背景として薄く
                if (_Mode > 0.5 && _Mode < 1.5) a = 0;  // Arrow のときは色なし

                // ---- Arrow ----
                if (_Mode > 0.5)
                {
                    float step_ = max(1, _ArrowStep);
                    float2 ag = gridPos / step_;
                    float2 cellId = floor(ag);
                    float2 local = frac(ag) - 0.5;      // -0.5?0.5

                    // 矢印のセル中心の値を読む(補間なしで1矢印=1つの値)
                    float2 centerUV = (cellId + 0.5) * step_ / _GridSize.xy;
                    float2 fc = tex2Dlod(_FlowTex, float4(centerUV, 0, 0)).rg;
                    float len = length(fc);
                    float m = saturate(len / _MaxStrength);

                    if (m > _MinStrength)
                    {
                        float2 dir = fc / len;
                        // 流れの向きが +x になるように回転
                        float2 p = float2(dot(local, dir), dot(local, float2(-dir.y, dir.x)));
                        float halfLen = lerp(0.15, 0.45, m);
                        float aa = fwidth(ag.x) * 1.0;
                        float arrow = ArrowMask(p, halfLen, _ArrowThickness, aa);

                        float3 arrowCol = DirColor(fc);
                        col = lerp(col, arrowCol, arrow);
                        a = max(a, arrow);
                    }
                }

                // ---- セル境界線 ----
                float2 cell = frac(gridPos);
                float2 fw = fwidth(gridPos);
                float2 lineMask = step(cell, fw);
                float grid = max(lineMask.x, lineMask.y) * _GridLine;
                col = lerp(col, 1, grid * (1 - a));
                a = max(a, grid);

                return float4(col, a);
            }
            ENDCG
        }
    }
}
