Shader "OpenWorld/AuthoredLure"
{
    Properties
    {
        [MainTexture] _BaseMap ("Authored Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _MetallicRoughnessMap ("Authored Metallic (B) Roughness (G)", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        _Metallic ("Metallic", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _AUTHORED_METALLIC_ROUGHNESS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicRoughnessMap);
            SAMPLER(sampler_MetallicRoughnessMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _Metallic;
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
                half fogFactor : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Always sample UV0: no material keyword can disable the supplied atlas.
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half metallic = _Metallic;
                half smoothness = _Smoothness;
                #if defined(_AUTHORED_METALLIC_ROUGHNESS)
                    half4 mr = SAMPLE_TEXTURE2D(_MetallicRoughnessMap, sampler_MetallicRoughnessMap, input.uv);
                    metallic = mr.b;
                    smoothness = 1.0h - mr.g;
                #endif
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half ndotl = saturate(dot(normal, light.direction));
                half3 ambient = max(SampleSH(normal), half3(0.12, 0.12, 0.12));
                float3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfway = SafeNormalize(view + light.direction);
                half highlight = pow(saturate(dot(normal, halfway)), lerp(16.0, 96.0, smoothness));
                half3 specularColor = lerp(half3(0.04, 0.04, 0.04), albedo, metallic);
                half3 color = albedo * (ambient + light.color * ndotl);
                color += light.color * specularColor * highlight * smoothness;
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
}
