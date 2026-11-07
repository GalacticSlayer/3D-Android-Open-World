Shader "OpenWorld/HeroFishFin"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,0.72,0.08,0.88)
        _Smoothness ("Smoothness", Range(0,1)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Water/Shaders/UnderwaterVisibility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs pos =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );

                VertexNormalInputs normal =
                    GetVertexNormalInputs(
                        input.normalOS
                    );

                output.positionCS =
                    pos.positionCS;

                output.positionWS =
                    pos.positionWS;

                output.normalWS =
                    normal.normalWS;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS =
                    normalize(
                        input.normalWS
                    );

                float3 viewDir =
                    SafeNormalize(
                        GetWorldSpaceViewDir(
                            input.positionWS
                        )
                    );

                Light mainLight =
                    GetMainLight();

                float lightAmount =
                    0.38 +
                    saturate(
                        abs(
                            dot(
                                normalWS,
                                mainLight.direction
                            )
                        )
                    ) *
                    0.62;

                float edge =
                    pow(
                        1.0 -
                        saturate(
                            abs(
                                dot(
                                    normalWS,
                                    viewDir
                                )
                            )
                        ),
                        2.0
                    );

                float3 color =
                    _BaseColor.rgb *
                    mainLight.color *
                    lightAmount;

                color +=
                    edge *
                    0.16 *
                    float3(
                        1.0,
                        0.82,
                        0.26
                    );

                return half4(
                    ApplyUnderwaterVisibility(color, input.positionWS),
                    _BaseColor.a
                );
            }

            ENDHLSL
        }
    }
}
