#ifndef OPENWORLD_UNDERWATER_VISIBILITY_INCLUDED
#define OPENWORLD_UNDERWATER_VISIBILITY_INCLUDED
// Set only while the submerged gameplay camera renders. No texture reads or variants.
float4 _UnderwaterVisibilityColor;
float4 _UnderwaterVisibilityRange;

half UnderwaterVisibilityFog(float3 positionWS)
{
    float eyeDepth = -TransformWorldToView(positionWS).z;
    return saturate((eyeDepth - _UnderwaterVisibilityRange.x) * _UnderwaterVisibilityRange.y)
        * _UnderwaterVisibilityColor.a;
}

half3 ApplyUnderwaterVisibility(half3 color, float3 positionWS)
{
    return lerp(color, _UnderwaterVisibilityColor.rgb, UnderwaterVisibilityFog(positionWS));
}
#endif
