Shader "OpenWorld/ShopSign"
{
    Properties { _MainTex("Font atlas",2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Water/Shaders/UnderwaterVisibility.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD1; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V Vert(A i) { V o; o.world=TransformObjectToWorld(i.p.xyz); o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 Frag(V i):SV_Target { return half4(ApplyUnderwaterVisibility(i.color.rgb, i.world),i.color.a*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a); }
            ENDHLSL
        }
    }
}
