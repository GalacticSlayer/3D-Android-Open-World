#ifndef OPENWORLD_UNDERWATER_VISIBILITY_INCLUDED
#define OPENWORLD_UNDERWATER_VISIBILITY_INCLUDED
// Scoped to the gameplay camera. The same near-plane mask is used by the opaque
// depth pass and transparent water/fish shaders, including partial submersion.
float4 _UnderwaterVisibilityColor;
float4 _UnderwaterVisibilityRange;
float4 _UnderwaterWaterlinePlane;
float _UnderwaterWaterlineOverlap;

half UnderwaterVisibilityFog(float3 positionWS)
{
    float3 ray = positionWS - _WorldSpaceCameraPos;
    float eyeDepth = max(-TransformWorldToView(positionWS).z, _ProjectionParams.y);
    float3 nearWorld = _WorldSpaceCameraPos + ray * (_ProjectionParams.y / eyeDepth);
    float waveHeight = dot(_UnderwaterWaterlinePlane, float4(nearWorld, 1.0));
    float baseHeight = nearWorld.y - _UnderwaterVisibilityRange.z;
    // The water volume includes the band between a trough and the base plane.
    // A small shared overlap covers numerical/near-plane gaps on downward entry.
    float nearHeight = min(waveHeight, baseHeight) - _UnderwaterWaterlineOverlap;
    // About one pixel of edge smoothing. No full-screen crossfade when the
    // camera origin crosses the surface; each near-plane pixel crosses separately.
    float edgeWidth = max(fwidth(nearHeight), 0.0005);
    float wet = saturate(0.5 - nearHeight / edgeWidth);

    float waterDistance = distance(nearWorld, positionWS);
    // Do not extend a local wave slope for hundreds of metres into the scene.
    float endHeight = nearHeight + positionWS.y - nearWorld.y;
    // Looking up from underwater: stop absorption where the ray leaves water,
    // so a distant sky/shore is not treated as hundreds of metres of water.
    if (endHeight > 0.0)
        waterDistance *= saturate(-nearHeight / max(endHeight - nearHeight, 0.0001));
    float fade = saturate((waterDistance - _UnderwaterVisibilityRange.x) * _UnderwaterVisibilityRange.y);
    fade = fade * fade * (3.0 - 2.0 * fade);
    float tint = _UnderwaterVisibilityRange.w;
    return lerp(tint, 1.0, fade) * wet * _UnderwaterVisibilityColor.a;
}

half3 ApplyUnderwaterVisibility(half3 color, float3 positionWS)
{
    return lerp(color, _UnderwaterVisibilityColor.rgb, UnderwaterVisibilityFog(positionWS));
}
#endif
