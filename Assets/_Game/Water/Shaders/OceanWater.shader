Shader "OpenWorld/OceanWater"
{
    Properties
    {
        [Toggle(_WATER_REFERENCE_LOOK)] _ReferenceLook ("Improved Ocean Look", Float) = 0
        [Normal] _RippleNormalA ("Broad Irregular Ripples", 2D) = "bump" {}
        _RippleStrength ("Ripple Normal Strength", Range(0, 1)) = 0.18
        _GerstnerStrength ("Gerstner Horizontal Motion", Range(0, 0.35)) = 0.18
        _ShallowColor ("Shallow Color", Color) = (0.05, 0.55, 0.72, 1)
        _DeepColor ("Deep Color", Color) = (0.01, 0.10, 0.28, 1)
        _FoamColor ("Foam Color", Color) = (0.86, 0.96, 1.0, 1)
        _FoamStrength ("Foam Strength", Range(0,1)) = 1
        [HideInInspector] _ContactFoamTex ("Water Contact Foam", 2D) = "black" {}
        [HideInInspector] _ContactFoamParams ("Width Strength Tiling Fade", Vector) = (.6,0,1.1,120)
        _Alpha ("Base Alpha", Range(0.1, 0.95)) = 0.72
        _WaterFogDensity ("Water Column Fog Density", Range(0.01,0.25)) = 0.085
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.8

        _WaveAmplitude1 ("Wave Amplitude 1", Float) = 0.45
        _WaveLength1 ("Wave Length 1", Float) = 20
        _WaveSpeed1 ("Wave Speed 1", Float) = 1.15
        _WaveDirection1 ("Wave Direction 1", Vector) = (1, 0.35, 0, 0)

        _WaveAmplitude2 ("Wave Amplitude 2", Float) = 0.22
        _WaveLength2 ("Wave Length 2", Float) = 9
        _WaveSpeed2 ("Wave Speed 2", Float) = 1.75
        _WaveDirection2 ("Wave Direction 2", Vector) = (-0.4, 1, 0, 0)

        _WaveAmplitude3 ("Wave Amplitude 3", Float) = 0.08
        _WaveLength3 ("Wave Length 3", Float) = 4
        _WaveSpeed3 ("Wave Speed 3", Float) = 2.4
        _WaveDirection3 ("Wave Direction 3", Vector) = (0.8, -0.65, 0, 0)

        [HideInInspector] _IslandWaveEnabled ("Island Waves", Float) = 0
        [HideInInspector] _IslandWaveCenter ("Island center and blend radii", Vector) = (0,0,100,260)
        [HideInInspector] _IslandWaveAmplitude ("Island amplitude", Float) = 2.8
        [HideInInspector] _IslandWaveSpeed ("Island speed", Float) = 1.65
        [HideInInspector] _OceanTime ("Ocean Time", Float) = 0
        [HideInInspector] _WavePhases ("Wave phases", Vector) = (0,0,0,0)
        [HideInInspector] _IslandWavePhases ("Island wave phases", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            // Only bit 0 is ours; preserve URP/UI stencil bits. The cheap boat
            // volume writes this before transparent water (queue 2990).
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 0
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            // Keep both paths in builds for an exact visual/performance A/B comparison.
            #pragma multi_compile_local _ _WATER_REFERENCE_LOOK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Water/Shaders/UnderwaterVisibility.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _Alpha;
                float _FoamStrength;
                float4 _ContactFoamParams;
                float _ContactFoamExclusionCount;
                float4 _ContactFoamExclusions[16];
                float _WaterFogDensity;
                float _Smoothness;
                float _SpecularStrength;
                float _ReferenceLook;
                float _RippleStrength;
                float _GerstnerStrength;

                float _WaveAmplitude1;
                float _WaveLength1;
                float _WaveSpeed1;
                float4 _WaveDirection1;

                float _WaveAmplitude2;
                float _WaveLength2;
                float _WaveSpeed2;
                float4 _WaveDirection2;

                float _WaveAmplitude3;
                float _WaveLength3;
                float _WaveSpeed3;
                float4 _WaveDirection3;

                float _OceanTime;
                float4 _WavePhases;
                float4 _IslandWavePhases;
                float _IslandWaveEnabled;
                float4 _IslandWaveCenter;
                float _IslandWaveAmplitude;
                float _IslandWaveSpeed;
            CBUFFER_END

            TEXTURE2D(_RippleNormalA); SAMPLER(sampler_RippleNormalA);
            TEXTURE2D(_ContactFoamTex); SAMPLER(sampler_ContactFoamTex);
            #include "OceanGerstner.hlsl"

            // Runs on the actual displaced ocean fragment. The depth difference
            // has already been sampled for water fog, so there is no extra depth
            // fetch, camera, geometry overlay or rendering pass for shoreline foam.
            half ContactFoam(float3 positionWS, float column, float cameraDistance)
            {
                float width = max(.1, _ContactFoamParams.x);
                float fadeEnd = max(1, _ContactFoamParams.w);
                // Derivatives must be evaluated before the divergent early-outs.
                float2 dx = ddx(positionWS.xz) * (_ContactFoamParams.z * 256 * 1.73);
                float2 dy = ddy(positionWS.xz) * (_ContactFoamParams.z * 256 * 1.73);
                float mip = clamp(.5 * log2(max(1, max(dot(dx,dx),dot(dy,dy)))), 0, 8);
                [branch] if (_ContactFoamParams.y <= 0 || column >= width || cameraDistance >= fadeEnd)
                    return 0;
                [loop] for (int i = 0; i < 16; i++)
                {
                    if (i >= (int)_ContactFoamExclusionCount) break;
                    float4 region = _ContactFoamExclusions[i];
                    if (all(abs(positionWS.xz-region.xy) < region.zw)) return 0;
                }
                float2 uv = positionWS.xz * _ContactFoamParams.z;
                float t = _Time.y;
                // Explicit LOD avoids implicit derivative sampling in a divergent
                // contact branch. Fade/coarser mips limit distant shimmer.
                half broad = SAMPLE_TEXTURE2D_LOD(_ContactFoamTex, sampler_ContactFoamTex,
                    uv + t * float2(.025,-.019), mip).r;
                half fine = SAMPLE_TEXTURE2D_LOD(_ContactFoamTex, sampler_ContactFoamTex,
                    uv * 1.73 + t * float2(-.018,.028), mip).g;
                float wash = .5 + .5 * sin(t * 2.24 + broad * 3.0);
                float edge = width * lerp(.65,1.0,wash);
                half contact = 1 - smoothstep(edge*.12,edge,column);
                half lace = smoothstep(.18h,.75h,broad*.65h + fine*.35h);
                half distanceFade = 1 - smoothstep(fadeEnd*.55,fadeEnd,cameraDistance);
                return saturate(contact * lerp(.2h,1.0h,lace) * _ContactFoamParams.y * distanceFade);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float waveHeight : TEXCOORD2;
                #if defined(_WATER_REFERENCE_LOOK)
                float2 waterData : TEXCOORD3;
                #endif
            };

            float2 NormalizeDirection(float2 direction)
            {
                float lengthSquared = dot(direction, direction);

                if (lengthSquared < 0.0001)
                    return float2(1.0, 0.0);

                return direction * rsqrt(lengthSquared);
            }

            void EvaluateWave(
                float2 position,
                float temporalPhase,
                float amplitude,
                float wavelength,
                float islandTemporalPhase,
                float2 direction,
                out float height,
                out float2 derivative)
            {
                float2 dir = NormalizeDirection(direction);
                float frequency = 6.28318530718 / max(0.1, wavelength);
                float phase =
                    dot(position, dir) * frequency +
                    temporalPhase;

                height = sin(phase) * amplitude;

                derivative =
                    cos(phase) *
                    amplitude *
                    frequency *
                    dir;
                if (_IslandWaveEnabled > 0.5)
                {
                    float2 delta = position - _IslandWaveCenter.xy;
                    float distance = length(delta);
                    float width = max(0.01, _IslandWaveCenter.w - _IslandWaveCenter.z);
                    float t = saturate((distance - _IslandWaveCenter.z) / width);
                    float blend = 1.0 - t*t*(3.0 - 2.0*t);
                    float roughPhase = dot(position,dir)*frequency + islandTemporalPhase;
                    float roughHeight = sin(roughPhase)*amplitude*_IslandWaveAmplitude;
                    float2 roughDerivative = cos(roughPhase)*amplitude*_IslandWaveAmplitude*frequency*dir;
                    float2 gradient = (-6.0*t*(1.0-t)/width)*delta/max(distance,0.001);
                    derivative = lerp(derivative,roughDerivative,blend)+(roughHeight-height)*gradient;
                    height = lerp(height,roughHeight,blend);
                }
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 worldPosition =
                    TransformObjectToWorld(input.positionOS.xyz);

                #if defined(_WATER_REFERENCE_LOOK)
                float totalHeight;
                float2 totalDerivative, horizontalOffset;
                float4 horizontalJacobian;
                OceanGerstnerSample(worldPosition.xz, horizontalOffset, horizontalJacobian,
                                   totalHeight, totalDerivative);
                worldPosition.xz += horizontalOffset;
                #else
                float height1;
                float height2;
                float height3;

                float2 derivative1;
                float2 derivative2;
                float2 derivative3;

                EvaluateWave(
                    worldPosition.xz,
                    _WavePhases.x,
                    _WaveAmplitude1,
                    _WaveLength1,
                    _IslandWavePhases.x,
                    _WaveDirection1.xy,
                    height1,
                    derivative1
                );

                EvaluateWave(
                    worldPosition.xz,
                    _WavePhases.y,
                    _WaveAmplitude2,
                    _WaveLength2,
                    _IslandWavePhases.y,
                    _WaveDirection2.xy,
                    height2,
                    derivative2
                );

                EvaluateWave(
                    worldPosition.xz,
                    _WavePhases.z,
                    _WaveAmplitude3,
                    _WaveLength3,
                    _IslandWavePhases.z,
                    _WaveDirection3.xy,
                    height3,
                    derivative3
                );

                float totalHeight =
                    height1 + height2 + height3;

                float2 totalDerivative =
                    derivative1 + derivative2 + derivative3;
                #endif

                worldPosition.y += totalHeight;

                output.positionWS = worldPosition;
                output.positionCS =
                    TransformWorldToHClip(worldPosition);

                output.normalWS = normalize(
                    float3(
                        -totalDerivative.x,
                        1.0,
                        -totalDerivative.y
                    )
                );

                #if defined(_WATER_REFERENCE_LOOK)
                float3 tangentX = float3(1.0 + horizontalJacobian.x,
                    totalDerivative.x, horizontalJacobian.z);
                float3 tangentZ = float3(horizontalJacobian.y,
                    totalDerivative.y, 1.0 + horizontalJacobian.w);
                output.normalWS = normalize(cross(tangentZ, tangentX));
                #endif

                output.waveHeight = totalHeight;

                #if defined(_WATER_REFERENCE_LOOK)
                // Move linear eye depth and the local crest amplitude out of Frag.
                float islandBlend = 1.0 - smoothstep(_IslandWaveCenter.z,
                    max(_IslandWaveCenter.z + 0.01, _IslandWaveCenter.w),
                    distance(worldPosition.xz, _IslandWaveCenter.xy));
                float localScale = lerp(1.0, _IslandWaveAmplitude,
                                       islandBlend * saturate(_IslandWaveEnabled));
                output.waterData = float2(-TransformWorldToView(worldPosition).z,
                    max(0.001, (_WaveAmplitude1 + _WaveAmplitude2 + _WaveAmplitude3) * localScale));
                #endif

                return output;
            }

            #if defined(_WATER_REFERENCE_LOOK)
            half4 Frag(Varyings input) : SV_Target
            {
                float3 toCamera = GetWorldSpaceViewDir(input.positionWS);
                float cameraDistance = max(length(toCamera), 0.001);
                half3 viewDirection = toCamera / cameraDistance;

                // One broad, world-anchored scrolling normal field. Eliminating the
                // fine layer reduces scattered small ripples and saves one fragment
                // texture fetch. Mipmaps and distance fade reduce offshore shimmer.
                float2 worldXZ = input.positionWS.xz;
                float2 uvA = worldXZ * 0.040 + _Time.y * float2(0.009, 0.004);
                half3 normalA = UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormalA, sampler_RippleNormalA, uvA));
                half detailFade = 1.0 - smoothstep(22.0, 125.0, cameraDistance);
                half2 ripple = normalA.xy * _RippleStrength * detailFade;
                half3 normalWS = normalize(input.normalWS + half3(ripple.x, 0.0, ripple.y));

                Light mainLight = GetMainLight();
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half facing = saturate(abs(dot(normalWS, viewDirection)));
                half fresnel = 1.0 - facing;
                fresnel *= fresnel;
                fresnel *= fresnel;

                // Reuse the depth texture already required by UnderwaterVisuals.
                // Optical path length, rather than player biome, controls transparency.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = max(0.01, input.waterData.x);
                float column = min(500.0, max(0.0, sceneDepth - surfaceDepth)
                                   * cameraDistance / surfaceDepth);
                half waterFog = 1.0 - exp(-column * max(0.01, _WaterFogDensity));

                // Retain the biome material/property-block tints, shifting the green
                // reef palette toward turquoise and the offshore palette toward blue.
                half3 shallowColor = _ShallowColor.rgb * half3(0.60, 0.90, 1.16);
                half3 deepColor = _DeepColor.rgb * half3(0.65, 0.66, 1.18);
                half3 baseColor = lerp(shallowColor, deepColor, waterFog);
                half3 color = baseColor * (0.55 + mainLight.color * diffuse * 0.45);

                // Cheap sky tint at grazing angles; no reflection camera, cubemap,
                // opaque-texture copy or screen-space reflection pass.
                half horizon = saturate((cameraDistance - 75.0) / 325.0);
                half3 skyTint = lerp(half3(0.12, 0.30, 0.49),
                                    half3(0.28, 0.43, 0.57), horizon);
                color = lerp(color, skyTint * (0.40 + mainLight.color * 0.60),
                             fresnel * 0.48);

                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                // Keep the sun glint soft so the broad ripples do not turn the sea white.
                half specularPower = lerp(48.0, lerp(90.0, 300.0, _Smoothness), detailFade);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower);
                color += specular * mainLight.color * _SpecularStrength
                         * lerp(0.08, 0.31, detailFade);

                // Account for the existing local rough-water multiplier so Brinebreak
                // does not become a continuous white sheet at larger wave amplitudes.
                half crest = smoothstep(0.74, 0.98, input.waveHeight / max(0.001, input.waterData.y));
                half breakup = smoothstep(0.08, 0.36, length(normalA.xy));
                half foam = crest * 0.06 * breakup
                            * _FoamStrength * detailFade;
                half contactFoam = ContactFoam(input.positionWS, column, cameraDistance);
                foam = max(foam, contactFoam);
                color = lerp(color, _FoamColor.rgb, saturate(foam));

                // No painted light bands: highlights now come only from the
                // irregular surface normal and sunlight, avoiding a white lattice.

                half alpha = saturate(_Alpha * lerp(0.65, 1.0, waterFog)
                                      + fresnel * 0.25 + foam * 0.10);
                alpha = lerp(alpha, 1.0, waterFog);
                alpha = lerp(alpha, 1.0, contactFoam * .85h);
                return half4(ApplyUnderwaterVisibility(color, input.positionWS),
                    lerp(alpha, 1.0, UnderwaterVisibilityFog(input.positionWS)));
            }
            #else
            // Legacy water look also receives the live contact effect.
            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirection =
                    SafeNormalize(
                        GetWorldSpaceViewDir(input.positionWS)
                    );

                Light mainLight = GetMainLight();

                float diffuse =
                    saturate(
                        dot(normalWS, mainLight.direction)
                    );

                float fresnel =
                    pow(
                        1.0 -
                        saturate(
                            abs(
                                dot(
                                    normalWS,
                                    viewDirection
                                )
                            )
                        ),
                        4.0
                    );

                // Water-only distance fog. Measure the opaque scene behind this surface
                // so an observer on ANY shore cannot see the distant bottom through it.
                float2 screenUV=GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth=LinearEyeDepth(SampleSceneDepth(screenUV),_ZBufferParams);
                float surfaceDepth=max(.01,-TransformWorldToView(input.positionWS).z);
                float rayScale=length(GetCameraPositionWS()-input.positionWS)/surfaceDepth;
                float column=min(500.0,max(0.0,sceneDepth-surfaceDepth)*rayScale);
                float waterFog=1.0-exp(-column*max(.01,_WaterFogDensity));

                float3 baseColor =
                    lerp(
                        _ShallowColor.rgb,
                        _DeepColor.rgb,
                        saturate(
                            max(waterFog,0.25 + fresnel * 0.75)
                        )
                    );

                float3 halfDirection =
                    SafeNormalize(
                        mainLight.direction +
                        viewDirection
                    );

                float specularPower =
                    lerp(
                        36.0,
                        220.0,
                        _Smoothness
                    );

                float specular =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDirection
                            )
                        ),
                        specularPower
                    );

                float totalAmplitude =
                    max(
                        0.001,
                        _WaveAmplitude1 +
                        _WaveAmplitude2 +
                        _WaveAmplitude3
                    );

                float normalizedCrest =
                    input.waveHeight /
                    totalAmplitude;

                float foam =
                    smoothstep(
                        0.62,
                        0.94,
                        normalizedCrest
                    );

                foam *= _FoamStrength;

                float lighting =
                    0.35 + diffuse * 0.65;

                float3 color =
                    baseColor * (0.55 + mainLight.color * diffuse * 0.45);

                color =
                    lerp(
                        color,
                        _FoamColor.rgb,
                        foam * 0.65
                    );

                color +=
                    specular *
                    mainLight.color *
                    _SpecularStrength;

                float alpha =
                    saturate(
                        _Alpha +
                        fresnel * 0.16 +
                        foam * 0.10
                    );

                // Long paths become opaque blue-green instead of transparent or black.
                alpha=lerp(alpha,1.0,waterFog);
                half contactFoam = ContactFoam(input.positionWS, column, length(GetCameraPositionWS()-input.positionWS));
                color = lerp(color, _FoamColor.rgb, contactFoam);
                alpha = lerp(alpha, 1.0, contactFoam * .85h);
                return half4(ApplyUnderwaterVisibility(color, input.positionWS),
                    lerp(alpha, 1.0, UnderwaterVisibilityFog(input.positionWS)));
            }
            #endif

            ENDHLSL
        }
    }
}


