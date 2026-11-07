Shader "OpenWorld/YellowfinTuna"
{
    Properties
    {
        _BaseMap ("Yellowfin Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Smoothness ("Skin Smoothness", Range(0,1)) = 0.34
        _FresnelStrength ("Wet Edge Highlight", Range(0,1)) = 0.055

        _BodyAxis ("Body Axis", Vector) = (1,0,0,0)
        _SideAxis ("Side Axis", Vector) = (0,1,0,0)
        _BodyMin ("Body Min", Float) = -1
        _BodyMax ("Body Max", Float) = 1
        _TailAtMin ("Tail At Min", Float) = 1

        _SwimStrength ("Body Flex Strength", Float) = 0.17
        _SwimSpeed ("Tail Beat Speed", Float) = 5.8
        _SwimPhase ("Swim Phase", Float) = 0

        _TrailYaw25 ("Trail Yaw 25%", Float) = 0
        _TrailYaw50 ("Trail Yaw 50%", Float) = 0
        _TrailYaw75 ("Trail Yaw 75%", Float) = 0
        _TrailYaw100 ("Trail Yaw 100%", Float) = 0
        _PathSideSign ("Path Side Sign", Float) = 1
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _FresnelStrength;

                float4 _BodyAxis;
                float4 _SideAxis;
                float _BodyMin;
                float _BodyMax;
                float _TailAtMin;

                float _SwimStrength;
                float _SwimSpeed;
                float _SwimPhase;

                float _TrailYaw25;
                float _TrailYaw50;
                float _TrailYaw75;
                float _TrailYaw100;
                float _PathSideSign;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float SampleTrailYaw(float t)
            {
                t = saturate(t);

                if (t <= 0.25)
                {
                    return
                        lerp(
                            0.0,
                            _TrailYaw25,
                            smoothstep(
                                0.0,
                                1.0,
                                t / 0.25
                            )
                        );
                }

                if (t <= 0.50)
                {
                    return
                        lerp(
                            _TrailYaw25,
                            _TrailYaw50,
                            smoothstep(
                                0.0,
                                1.0,
                                (t - 0.25) / 0.25
                            )
                        );
                }

                if (t <= 0.75)
                {
                    return
                        lerp(
                            _TrailYaw50,
                            _TrailYaw75,
                            smoothstep(
                                0.0,
                                1.0,
                                (t - 0.50) / 0.25
                            )
                        );
                }

                return
                    lerp(
                        _TrailYaw75,
                        _TrailYaw100,
                        smoothstep(
                            0.0,
                            1.0,
                            (t - 0.75) / 0.25
                        )
                    );
            }

            float3 DeformFish(float3 positionOS)
            {
                float bodyRange =
                    max(
                        0.0001,
                        _BodyMax - _BodyMin
                    );

                float alongBody =
                    dot(
                        positionOS,
                        _BodyAxis.xyz
                    );

                float normalizedBody =
                    saturate(
                        (alongBody - _BodyMin) /
                        bodyRange
                    );

                float tailPosition =
                    normalizedBody;

                if (_TailAtMin > 0.5)
                {
                    tailPosition =
                        1.0 - normalizedBody;
                }

                float bodyFlex =
                    saturate(
                        (tailPosition - 0.12) /
                        0.88
                    );

                bodyFlex =
                    bodyFlex *
                    bodyFlex *
                    (
                        3.0 -
                        2.0 * bodyFlex
                    );

                float rearFlex =
                    saturate(
                        (tailPosition - 0.42) /
                        0.58
                    );

                rearFlex =
                    rearFlex *
                    rearFlex *
                    (
                        3.0 -
                        2.0 * rearFlex
                    );

                float tailFlex =
                    saturate(
                        (tailPosition - 0.74) /
                        0.26
                    );

                tailFlex =
                    tailFlex *
                    tailFlex *
                    (
                        3.0 -
                        2.0 * tailFlex
                    );

                float bodyBeat =
                    sin(_SwimPhase);

                float rearBeat =
                    sin(
                        _SwimPhase -
                        0.30
                    );

                float tailBeat =
                    sin(
                        _SwimPhase -
                        0.58
                    );

                float3 bodyAxis =
                    normalize(
                        _BodyAxis.xyz
                    );

                float3 sideAxis =
                    normalize(
                        _SideAxis.xyz
                    );

                float3 tailAxis =
                    _TailAtMin > 0.5
                        ? -bodyAxis
                        : bodyAxis;

                float3 gameRightAxis =
                    sideAxis *
                    _PathSideSign;

                // Train-track centerline. Each point farther down the tuna
                // uses an older heading that the head actually travelled.
                // The head stays fixed at t=0 and the body is dragged behind.
                float3 curvedCenter =
                    float3(
                        0.0,
                        0.0,
                        0.0
                    );

                const int TRACK_STEPS = 6;

                float stepT =
                    tailPosition /
                    TRACK_STEPS;

                [unroll]
                for (int step = 0;
                     step < TRACK_STEPS;
                     step++)
                {
                    float sampleT =
                        (
                            step +
                            0.5
                        ) *
                        stepT;

                    float angle =
                        SampleTrailYaw(
                            sampleT
                        );

                    float3 tangent =
                        tailAxis *
                        cos(angle) -
                        gameRightAxis *
                        sin(angle);

                    curvedCenter +=
                        tangent *
                        (
                            stepT *
                            bodyRange
                        );
                }

                float3 straightCenter =
                    tailAxis *
                    (
                        tailPosition *
                        bodyRange
                    );

                positionOS +=
                    curvedCenter -
                    straightCenter;

                float turnAmount =
                    saturate(
                        abs(_TrailYaw100) /
                        1.25
                    );

                // Preserve the old good swim. During a strong curve only
                // reduce the free tail beat enough that it cannot fight the
                // train-track shape.
                float swimDuringTurn =
                    lerp(
                        1.0,
                        0.64,
                        turnAmount
                    );

                float sideOffset =
                    _SwimStrength *
                    bodyRange *
                    swimDuringTurn *
                    (
                        bodyFlex *
                        0.24 *
                        bodyBeat +
                        rearFlex *
                        0.34 *
                        rearBeat +
                        tailFlex *
                        0.42 *
                        tailBeat
                    );

                positionOS +=
                    sideAxis *
                    sideOffset;

                return positionOS;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 deformedPosition =
                    DeformFish(
                        input.positionOS.xyz
                    );

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        deformedPosition
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

                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 textureSample =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );

                float3 normalWS =
                    normalize(
                        input.normalWS
                    );

                float3 viewDirection =
                    SafeNormalize(
                        GetWorldSpaceViewDir(
                            input.positionWS
                        )
                    );

                Light mainLight =
                    GetMainLight();

                float diffuseAmount =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );

                float3 halfDirection =
                    SafeNormalize(
                        mainLight.direction +
                        viewDirection
                    );

                float specularAmount =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDirection
                            )
                        ),
                        lerp(
                            18.0,
                            95.0,
                            _Smoothness
                        )
                    );

                float fresnelAmount =
                    pow(
                        1.0 -
                        saturate(
                            dot(
                                normalWS,
                                viewDirection
                            )
                        ),
                        3.0
                    );

                float3 ambientLight =
                    SampleSH(
                        normalWS
                    );

                float3 baseColor =
                    textureSample.rgb *
                    _BaseColor.rgb;

                float3 finalColor =
                    baseColor *
                    (
                        ambientLight * 0.82 +
                        mainLight.color *
                        (
                            0.20 +
                            diffuseAmount * 0.80
                        )
                    );

                finalColor +=
                    mainLight.color *
                    specularAmount *
                    lerp(
                        0.045,
                        0.15,
                        _Smoothness
                    );

                finalColor +=
                    fresnelAmount *
                    _FresnelStrength *
                    float3(
                        0.08,
                        0.12,
                        0.13
                    );

                return half4(
                    finalColor,
                    1.0
                );
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
