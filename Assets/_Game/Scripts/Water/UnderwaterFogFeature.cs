using UnityEngine.Rendering.Universal;

// Registered on both renderer assets so URP schedules the pass after clearing
// its per-camera queue. Preview cameras without UnderwaterVisuals are untouched.
public sealed class UnderwaterFogFeature : ScriptableRendererFeature
{
    public override void Create() { }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var visuals = renderingData.cameraData.camera.GetComponent<UnderwaterVisuals>();
        if (visuals != null && visuals.isActiveAndEnabled) visuals.EnqueueFog(renderer);
    }
}
