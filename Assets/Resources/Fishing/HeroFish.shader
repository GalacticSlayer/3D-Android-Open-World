Shader "OpenWorld/HeroFish"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.78
        _NormalStrength ("Scale Relief", Range(0,2)) = 0.72
        _FresnelStrength ("Iridescence", Range(0,1)) = 0.30
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _NormalStrength;
                float _FresnelStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
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
                        input.normalOS,
                        input.tangentOS
                    );

                output.positionCS =
                    positionInputs.positionCS;

                output.positionWS =
                    positionInputs.positionWS;

                output.normalWS =
                    normalInputs.normalWS;

                output.tangentWS =
                    normalInputs.tangentWS;

                output.bitangentWS =
                    normalInputs.bitangentWS;

                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 tex =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );

                float3 normalTS =
                    UnpackNormalScale(
                        SAMPLE_TEXTURE2D(
                            _NormalMap,
                            sampler_NormalMap,
                            input.uv
                        ),
                        _NormalStrength
                    );

                float3x3 tbn =
                    float3x3(
                        normalize(input.tangentWS),
                        normalize(input.bitangentWS),
                        normalize(input.normalWS)
                    );

                float3 normalWS =
                    normalize(
                        mul(
                            normalTS,
                            tbn
                        )
                    );

                float3 viewDir =
                    SafeNormalize(
                        GetWorldSpaceViewDir(
                            input.positionWS
                        )
                    );

                Light mainLight =
                    GetMainLight();

                float ndotl =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );

                float3 halfDir =
                    SafeNormalize(
                        mainLight.direction +
                        viewDir
                    );

                float smoothness =
                    saturate(
                        _Smoothness *
                        lerp(
                            0.76,
                            1.08,
                            tex.a
                        )
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
                            20.0,
                            180.0,
                            smoothness
                        )
                    );

                float fresnel =
                    pow(
                        1.0 -
                        saturate(
                            dot(
                                normalWS,
                                viewDir
                            )
                        ),
                        3.2
                    );

                float3 ambient =
                    SampleSH(normalWS);

                float3 baseColor =
                    tex.rgb *
                    _BaseColor.rgb;

                float3 color =
                    baseColor *
                    (
                        ambient * 0.68 +
                        mainLight.color *
                        (
                            0.22 +
                            ndotl * 0.78
                        )
                    );

                float3 iridescence =
                    lerp(
                        float3(
                            0.04,
                            0.12,
                            0.20
                        ),
                        float3(
                            0.10,
                            0.24,
                            0.16
                        ),
                        saturate(
                            normalWS.y *
                            0.5 +
                            0.5
                        )
                    );

                color +=
                    iridescence *
                    fresnel *
                    _FresnelStrength;

                color +=
                    mainLight.color *
                    specular *
                    lerp(
                        0.20,
                        0.62,
                        smoothness
                    );

                return half4(
                    color,
                    1.0
                );
            }

            ENDHLSL
        }
    }
}
