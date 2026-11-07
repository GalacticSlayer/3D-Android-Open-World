using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class UnderwaterVisuals : MonoBehaviour
{
    [SerializeField] private OceanWater oceanWater;
    [SerializeField] private Image underwaterOverlay;
    [Header("Underwater Look")]
    [SerializeField] private Color underwaterFogColor = new Color(0.015f, 0.16f, 0.22f, 1f);
    [SerializeField] private Color underwaterTint = new Color(0f, 0.24f, 0.34f, 1f);
    [SerializeField] private float overlayAlpha = 0.15f;
    [SerializeField] private float transitionSpeed = 5f;

    // Visibility is immediate; only the cosmetic tint is eased. No extra render pass.
    private const float FogStart = 20f;
    private const float FogEnd = 100f;
    private const float FarClip = 105f;
    private const float SurfaceMargin = 0.02f;
    private static readonly int FogColorId = Shader.PropertyToID("_UnderwaterVisibilityColor");
    private static readonly int FogRangeId = Shader.PropertyToID("_UnderwaterVisibilityRange");
    private static readonly Color DeepFogColor = new Color(0.025f, 0.19f, 0.27f, 1f);
    private static readonly Color DeepTint = new Color(0.025f, 0.24f, 0.32f, 1f);

    private Camera viewCamera;
    private DeepOceanWaterRuntime deepWater;
    private DistantOceanRuntime distantOcean;
    private MeshRenderer hiddenHorizon;
    private bool originalHorizonForceOff;
    private readonly Vector3[] nearCorners = new Vector3[4];
    private bool waterContact;
    private float underwaterBlend;
    private float deepVisualBlend;
    private bool renderingUnderwater;
    private bool originalFogEnabled;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity, originalFogStart, originalFogEnd;
    private float originalFarClip;
    private CameraClearFlags originalClearFlags;
    private Color originalBackground;
    private Vector4 originalShaderColor, originalShaderRange;

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();
        // Already required by the existing ocean water-column shader.
        viewCamera.depthTextureMode |= DepthTextureMode.Depth;
        var additional = viewCamera.GetComponent<UniversalAdditionalCameraData>();
        if (additional == null) additional = viewCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        additional.requiresDepthTexture = true;
        ResolveWater();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != viewCamera) return;
        RestoreRendering();
        ResolveWater();

        // Runs after camera movement in LateUpdate, before URP culls/draws this frame.
        // The near plane can cut through a wave before the camera origin crosses it.
        float margin = waterContact ? SurfaceMargin + 0.03f : SurfaceMargin;
        waterContact = TouchesWater(camera.transform.position, margin);
        if (!waterContact && oceanWater != null)
        {
            camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), camera.nearClipPlane,
                Camera.MonoOrStereoscopicEye.Mono, nearCorners);
            for (int i = 0; i < nearCorners.Length; i++)
            {
                Vector3 point = camera.transform.position + camera.transform.TransformDirection(nearCorners[i]);
                if (TouchesWater(point, margin)) { waterContact = true; break; }
            }
        }

        float depthBlend = deepWater != null ? Mathf.Clamp01(deepWater.VisibilityBlend) : 0f;
        underwaterBlend = Mathf.MoveTowards(underwaterBlend, waterContact ? 1f : 0f,
            transitionSpeed * Time.deltaTime);
        deepVisualBlend = Mathf.MoveTowards(deepVisualBlend, depthBlend, transitionSpeed * Time.deltaTime);
        if (underwaterOverlay != null)
        {
            Color tint = Color.Lerp(underwaterTint, DeepTint, deepVisualBlend);
            tint.a = waterContact ? Mathf.Lerp(overlayAlpha, 0.12f, deepVisualBlend) * underwaterBlend : 0f;
            underwaterOverlay.color = tint;
        }
        if (!waterContact) return;

        // Save per-render state so preview cameras, travel and dry views retain their own look.
        originalFogEnabled = RenderSettings.fog;
        originalFogMode = RenderSettings.fogMode;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogStart = RenderSettings.fogStartDistance;
        originalFogEnd = RenderSettings.fogEndDistance;
        originalFarClip = camera.farClipPlane;
        originalClearFlags = camera.clearFlags;
        originalBackground = camera.backgroundColor;
        originalShaderColor = Shader.GetGlobalVector(FogColorId);
        originalShaderRange = Shader.GetGlobalVector(FogRangeId);
        renderingUnderwater = true;

        Color fog = Color.Lerp(underwaterFogColor, DeepFogColor, deepVisualBlend);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fog;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;
        Color shaderFog = QualitySettings.activeColorSpace == ColorSpace.Linear ? fog.linear : fog;
        Shader.SetGlobalVector(FogColorId, new Vector4(shaderFog.r, shaderFog.g, shaderFog.b, 1f));
        Shader.SetGlobalVector(FogRangeId, new Vector4(FogStart, 1f / (FogEnd - FogStart), 0f, 0f));
        camera.farClipPlane = Mathf.Min(originalFarClip, FarClip);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = fog;
        // The horizon shader intentionally bypasses far clipping above water.
        hiddenHorizon = distantOcean != null ? distantOcean.OuterRenderer : null;
        if (hiddenHorizon != null)
        {
            originalHorizonForceOff = hiddenHorizon.forceRenderingOff;
            hiddenHorizon.forceRenderingOff = true;
        }
    }

    private bool TouchesWater(Vector3 point, float margin)
    {
        return oceanWater != null && ShopWaterVolume.TrySurface(point, oceanWater, out float surface)
            && point.y <= surface + margin;
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == viewCamera) RestoreRendering();
    }

    public void Configure(OceanWater water, Image overlay)
    {
        distantOcean = null;
        oceanWater = water;
        underwaterOverlay = overlay;
        deepWater = oceanWater != null ? oceanWater.GetComponent<DeepOceanWaterRuntime>() : null;
    }

    private void ResolveWater()
    {
        if (oceanWater == null) oceanWater = FindFirstObjectByType<OceanWater>();
        if (deepWater == null && oceanWater != null) deepWater = oceanWater.GetComponent<DeepOceanWaterRuntime>();
        if (distantOcean == null && oceanWater != null) distantOcean = oceanWater.GetComponent<DistantOceanRuntime>();
    }

    private void RestoreRendering()
    {
        if (!renderingUnderwater) return;
        renderingUnderwater = false;
        if (hiddenHorizon != null) hiddenHorizon.forceRenderingOff = originalHorizonForceOff;
        hiddenHorizon = null;
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogStartDistance = originalFogStart;
        RenderSettings.fogEndDistance = originalFogEnd;
        Shader.SetGlobalVector(FogColorId, originalShaderColor);
        Shader.SetGlobalVector(FogRangeId, originalShaderRange);
        if (viewCamera != null)
        {
            viewCamera.farClipPlane = originalFarClip;
            viewCamera.clearFlags = originalClearFlags;
            viewCamera.backgroundColor = originalBackground;
        }
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreRendering();
        waterContact = false;
        underwaterBlend = deepVisualBlend = 0f;
        if (underwaterOverlay != null)
        {
            Color color = underwaterOverlay.color;
            color.a = 0f;
            underwaterOverlay.color = color;
        }
    }
}
