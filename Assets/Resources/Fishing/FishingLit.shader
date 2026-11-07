Shader "OpenWorld/FishingLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS
                    );

                output.positionCS =
                    positionInputs.positionCS;

                output.positionWS =
                    positionInputs.positionWS;

                output.normalWS =
                    normalInputs.normalWS;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS =
                    normalize(input.normalWS);

                Light mainLight =
                    GetMainLight();

                float ndotl =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );

                float3 viewDir =
                    SafeNormalize(
                        GetWorldSpaceViewDir(
                            input.positionWS
                        )
                    );

                float3 halfDir =
                    SafeNormalize(
                        mainLight.direction +
                        viewDir
                    );

                float specular =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDir
                            )
                        ),
                        lerp(
                            12.0,
                            96.0,
                            _Smoothness
                        )
                    );

                float3 ambient =
                    SampleSH(normalWS);

                float3 color =
                    _BaseColor.rgb *
                    (
                        ambient * 0.75 +
                        mainLight.color *
                        (
                            0.28 +
                            ndotl * 0.72
                        )
                    );

                color +=
                    mainLight.color *
                    specular *
                    _Smoothness *
                    0.45;

                return half4(
                    color,
                    _BaseColor.a
                );
            }

            ENDHLSL
        }
    }
}
