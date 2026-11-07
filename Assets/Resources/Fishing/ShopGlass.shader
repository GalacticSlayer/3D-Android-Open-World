Shader "OpenWorld/ShopGlass"
{
    Properties { _BaseColor("Glass tint", Color)=(0.14,0.6,0.65,0.12) _Water("Water surface", Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Water/Shaders/UnderwaterVisibility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Water;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            V Vert(A i) { V o; o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);return o; }
            half4 Frag(V i):SV_Target
            {
                float3 n=normalize(i.normal);
                n=normalize(n+_Water*float3(sin(i.world.x*2+_Time.y)*0.055,0,cos(i.world.z*2.5+_Time.y)*0.055));
                float3 view=SafeNormalize(GetWorldSpaceViewDir(i.world));
                float edge=pow(1-saturate(abs(dot(n,view))),4);
                Light light=GetMainLight();
                float glint=pow(saturate(abs(dot(n,SafeNormalize(view+light.direction)))),80)*_Water;
                return half4(ApplyUnderwaterVisibility(_BaseColor.rgb+edge*0.18+glint*0.35, i.world),saturate(_BaseColor.a+edge*0.22));
            }
            ENDHLSL
        }
    }
}
