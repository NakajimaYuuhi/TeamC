Shader "Custom/Player/SH_PlayerOccludedHighlight"
{
    Properties
    {
        _OutlineWidth("輪郭線の太さ（ピクセル）", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
        }

        Pass
        {
            Name "OccludedOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite Off
            ZTest Greater
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment WhiteFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth;
            CBUFFER_END

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);
                float2 outlineDirection = normalVS.xy;
                float outlineLengthSquared = max(dot(outlineDirection, outlineDirection), 1e-6);
                outlineDirection *= rsqrt(outlineLengthSquared);

                output.positionHCS = TransformWorldToHClip(positionWS);
                float2 pixelOffset = 2.0 * _OutlineWidth * outlineDirection / _ScreenParams.xy;
                output.positionHCS.xy += pixelOffset * output.positionHCS.w;
                return output;
            }

            half4 WhiteFragment(Varyings input) : SV_Target
            {
                return half4(1.0, 1.0, 1.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "OccludedFill"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite Off
            ZTest Greater
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex FillVertex
            #pragma fragment WhiteFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings FillVertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 WhiteFragment(Varyings input) : SV_Target
            {
                return half4(1.0, 1.0, 1.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VisiblePlayerMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings MaskVertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 MaskFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
