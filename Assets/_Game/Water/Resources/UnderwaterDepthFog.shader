Shader "Hidden/OpenWorld/UnderwaterDepthFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Game/Water/Shaders/UnderwaterVisibility.hlsl"
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                return half4(_UnderwaterVisibilityColor.rgb, UnderwaterVisibilityFog(world));
            }
            ENDHLSL
        }
    }
}
