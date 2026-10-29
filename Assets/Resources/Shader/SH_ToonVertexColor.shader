// 頂点カラーを地の色にして、メインライトの向きで2段の影を付ける。URP 用
// NPC のふち線の殻も同じシェーダーで描く
Shader "TeamC/ToonVertexColor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.72, 0.74, 0.86, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.05
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.03
        _LightColorAmount ("Light Color Amount", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half4 _ShadeColor;
        half _ShadeThreshold;
        half _ShadeSoftness;
        half _LightColorAmount;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // 頂点カラーは sRGB で入っているので、リニアの計算に直す
                half3 baseColor = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                baseColor = SRGBToLinear(baseColor);
                #endif
                baseColor *= _BaseColor.rgb;

                Light light = GetMainLight();
                half ndl = dot(normalize(i.normalWS), light.direction);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                half3 color = lerp(baseColor * _ShadeColor.rgb, baseColor, lit);
                color *= lerp(half3(1, 1, 1), light.color, _LightColorAmount);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
