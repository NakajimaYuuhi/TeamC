Shader "Custom/SplatZoneFrame"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.3, 1, 0.05, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SplatZoneFrame"

            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Blend SrcAlpha OneMinusSrcAlpha

            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseColor;
                float _Alpha;

            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = _BaseColor;

                color.a *= _Alpha;

                return color;
            }

            ENDHLSL
        }
    }
}