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
    [SerializeField] private float transitionSpeed = 5f;

    // Opaque scenery is fogged from scene depth; transparent shaders use the same range.
    private const float FogStart = 10f;
    private const float FogEnd = 75f;
    private const float UnderwaterRenderDistance = 250f;
    private const float WaterlineOverlap = 0.08f;
    private const float SurfaceMargin = 0.02f;
    private static readonly int FogColorId = Shader.PropertyToID("_UnderwaterVisibilityColor");
    private static readonly int WaterlineOverlapId = Shader.PropertyToID("_UnderwaterWaterlineOverlap");
    private static readonly int WaterlinePlaneId = Shader.PropertyToID("_UnderwaterWaterlinePlane");
    private static readonly int FogRangeId = Shader.PropertyToID("_UnderwaterVisibilityRange");
    private static readonly Color DeepFogColor = new Color(0.025f, 0.19f, 0.27f, 1f);

    private Camera viewCamera;
    private DeepOceanWaterRuntime deepWater;
    private UnderwaterFogPass fogPass;
    private Material fogMaterial;
    private float originalFarClip;
    private MeshRenderer hiddenHorizon;
    private bool originalHorizonForceOff;
    private DistantOceanRuntime distantOcean;
    private readonly Vector3[] nearCorners = new Vector3[4];
    private bool waterContact;
    private float deepVisualBlend;
    private bool renderingUnderwater;
    private float originalWaterlineOverlap;
    private Vector4 originalShaderColor, originalShaderRange, originalWaterlinePlane;

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();
        // Already required by the existing ocean water-column shader.
        viewCamera.depthTextureMode |= DepthTextureMode.Depth;
        var additional = viewCamera.GetComponent<UniversalAdditionalCameraData>();
        if (additional == null) additional = viewCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        additional.requiresDepthTexture = true;
        Shader shader = Resources.Load<Shader>("UnderwaterDepthFog");
        if (shader != null)
        {
            fogMaterial = CoreUtils.CreateEngineMaterial(shader);
            fogPass = new UnderwaterFogPass(fogMaterial);
        }
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

        // The render pass may run for a partly wet near plane. Its shader decides
        // wet/dry per pixel; this flag only skips work for a completely dry view.
        float margin = waterContact ? SurfaceMargin + 0.03f : SurfaceMargin;
        waterContact = TouchesWater(camera.transform.position, margin);
        bool fullySubmerged = TouchesWater(camera.transform.position, -SurfaceMargin);
        camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), camera.nearClipPlane,
            Camera.MonoOrStereoscopicEye.Mono, nearCorners);
        for (int i = 0; i < nearCorners.Length; i++)
        {
            Vector3 point = camera.transform.position + camera.transform.TransformDirection(nearCorners[i]);
            waterContact |= TouchesWater(point, margin);
            fullySubmerged &= TouchesWater(point, -SurfaceMargin);
        }

        // A wave crest can pass through the middle while all four corners are dry.
        Vector3 center = camera.transform.position + camera.transform.forward * camera.nearClipPlane;
        waterContact |= TouchesWater(center, margin);
        fullySubmerged &= TouchesWater(center, -SurfaceMargin);

        float depthBlend = deepWater != null ? Mathf.Clamp01(deepWater.VisibilityBlend) : 0f;
        deepVisualBlend = Mathf.MoveTowards(deepVisualBlend, depthBlend, transitionSpeed * Time.deltaTime);
        // A full-screen UI tint also colors the sky and emerged geometry. The depth
        // pass now supplies the tint only to submerged pixels instead.
        if (underwaterOverlay != null) underwaterOverlay.color = Color.clear;
        if (!waterContact || fogPass == null) return;

        originalFarClip = camera.farClipPlane;
        originalWaterlineOverlap = Shader.GetGlobalFloat(WaterlineOverlapId);
        originalShaderColor = Shader.GetGlobalVector(FogColorId);
        originalShaderRange = Shader.GetGlobalVector(FogRangeId);
        originalWaterlinePlane = Shader.GetGlobalVector(WaterlinePlaneId);
        renderingUnderwater = true;
        Color fog = Color.Lerp(underwaterFogColor, DeepFogColor, deepVisualBlend);
        Color shaderFog = QualitySettings.activeColorSpace == ColorSpace.Linear ? fog.linear : fog;
        // Fit the local wave surface at the near plane. Sampling the same live
        // surface as swimming/boats also supports flat pond and aquarium water.
        ShopWaterVolume.TrySurface(center, oceanWater, out float surface);
        const float sampleOffset = 0.25f;
        float slopeX = SurfaceSlope(center, Vector3.right * sampleOffset, surface);
        float slopeZ = SurfaceSlope(center, Vector3.forward * sampleOffset, surface);
        Vector4 plane = new Vector4(-slopeX, 1f, -slopeZ,
            slopeX * center.x + slopeZ * center.z - surface);
        Vector4 range = new Vector4(FogStart, 1f / (FogEnd - FogStart), BaseSurface(center, surface), 0.12f);
        Vector4 color = new Vector4(shaderFog.r, shaderFog.g, shaderFog.b, 1f);
        Shader.SetGlobalVector(FogColorId, color);
        Shader.SetGlobalVector(FogRangeId, range);
        Shader.SetGlobalVector(WaterlinePlaneId, plane);
        Shader.SetGlobalFloat(WaterlineOverlapId, WaterlineOverlap);
        fogMaterial.SetVector(FogColorId, color);
        fogMaterial.SetVector(FogRangeId, range);
        fogMaterial.SetVector(WaterlinePlaneId, plane);
        fogMaterial.SetFloat(WaterlineOverlapId, WaterlineOverlap);
        // A global far clip is safe only when the eye and entire near plane are
        // submerged. Leave the dry half of a split view at its normal distance.
        if (fullySubmerged)
        {
            camera.farClipPlane = Mathf.Min(originalFarClip, UnderwaterRenderDistance);
            // The distant ocean deliberately bypasses normal far-plane clipping.
            hiddenHorizon = distantOcean != null ? distantOcean.OuterRenderer : null;
            if (hiddenHorizon != null)
            {
                originalHorizonForceOff = hiddenHorizon.forceRenderingOff;
                hiddenHorizon.forceRenderingOff = true;
            }
        }
    }

    private float BaseSurface(Vector3 point, float waveSurface)
    {
        if (ShopWaterVolume.Surface(point, out float localSurface)) return localSurface;
        if (PondWater.TrySurface(point, out localSurface)) return localSurface;
        return oceanWater != null ? oceanWater.BaseWaterLevel : waveSurface;
    }

    private float SurfaceSlope(Vector3 center, Vector3 offset, float fallback)
    {
        if (!ShopWaterVolume.TrySurface(center + offset, oceanWater, out float high)) high = fallback;
        if (!ShopWaterVolume.TrySurface(center - offset, oceanWater, out float low)) low = fallback;
        return (high - low) / (2f * offset.magnitude);
    }

    public void EnqueueFog(ScriptableRenderer renderer)
    {
        if (renderingUnderwater && fogPass != null) renderer.EnqueuePass(fogPass);
    }

    private bool TouchesWater(Vector3 point, float margin)
    {
        if (oceanWater == null || !ShopWaterVolume.TrySurface(point, oceanWater, out float surface)) return false;
        // Bridge the band between the animated wave and base water plane. Match
        // the shader's overlap so CPU activation cannot leave an unprocessed gap.
        float ceiling = Mathf.Max(surface, BaseSurface(point, surface)) + WaterlineOverlap;
        return point.y <= ceiling + margin;
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
        if (viewCamera != null) viewCamera.farClipPlane = originalFarClip;
        if (hiddenHorizon != null) hiddenHorizon.forceRenderingOff = originalHorizonForceOff;
        hiddenHorizon = null;
        Shader.SetGlobalFloat(WaterlineOverlapId, originalWaterlineOverlap);
        Shader.SetGlobalVector(FogColorId, originalShaderColor);
        Shader.SetGlobalVector(FogRangeId, originalShaderRange);
        Shader.SetGlobalVector(WaterlinePlaneId, originalWaterlinePlane);
    }

    private void OnDestroy()
    {
        CoreUtils.Destroy(fogMaterial);
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreRendering();
        waterContact = false;
        deepVisualBlend = 0f;
        if (underwaterOverlay != null)
        {
            Color color = underwaterOverlay.color;
            color.a = 0f;
            underwaterOverlay.color = color;
        }
    }
}
