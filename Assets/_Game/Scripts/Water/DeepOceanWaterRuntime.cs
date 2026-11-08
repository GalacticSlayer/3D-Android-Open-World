using System.Reflection;
using UnityEngine;

/// <summary>
/// Makes progressively deeper ocean water darker, less transparent, and more
/// threatening while keeping the existing OceanWater simulation authoritative.
/// Water visibility is driven by actual seabed depth instead of an abrupt biome
/// boundary; deep-ocean waves then build gradually as the player travels offshore.
/// Brinebreak deliberately suppresses bright specular/foam glare so the water keeps
/// its colour instead of becoming a large white sheet at low viewing angles.
/// </summary>
[DefaultExecutionOrder(-650)]
public sealed class DeepOceanWaterRuntime : MonoBehaviour
{
    private const float DeepWaveMultiplier = 4.5f;
    // Windgrove waves are 30% taller than the fully developed deep-ocean waves.
    private const float WindgroveWaveHeightBonus = 1.30f;
    private const float BrinebreakWaveMultiplier = DeepWaveMultiplier * .90f;
    private const float DeepWaveSpeedMultiplier = 1.35f;
    private const float DeepAlpha = 0.82f;
    private const float BrinebreakSmoothness = 0.55f;
    private const float BrinebreakSpecularStrength = 0.08f;
    private const float BrinebreakFoamStrength = 0.25f;

    private static readonly Color DeepShallowColor = new Color(0.035f, 0.32f, 0.40f, 1f);
    private static readonly Color DeepDeepColor = new Color(0.018f, 0.16f, 0.25f, 1f);

    private OceanWater ocean;
    private MeshRenderer rendererRef;
    private Transform player;
    private MaterialPropertyBlock block;

    private FieldInfo amplitude1Field, amplitude2Field, amplitude3Field;
    private FieldInfo speed1Field, speed2Field, speed3Field;

    private float baseAmplitude1, baseAmplitude2, baseAmplitude3;
    private float baseSpeed1, baseSpeed2, baseSpeed3;
    private Color baseShallowColor, baseDeepColor;
    private float baseAlpha, baseSmoothness, baseFoamStrength, baseSpecularStrength;
    private float roughBlend, windgroveBlend, darkBlend, brinebreakMatteBlend;
    private bool cached;

    public float VisibilityBlend => darkBlend;
    public float WaterDepth { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (OceanWater water in FindObjectsByType<OceanWater>(FindObjectsSortMode.None))
        {
            if (water != null && water.GetComponent<DeepOceanWaterRuntime>() == null)
                water.gameObject.AddComponent<DeepOceanWaterRuntime>();
        }
    }

    private void Awake()
    {
        ocean = GetComponent<OceanWater>();
        rendererRef = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        System.Type type = typeof(OceanWater);
        amplitude1Field = type.GetField("waveAmplitude1", flags);
        amplitude2Field = type.GetField("waveAmplitude2", flags);
        amplitude3Field = type.GetField("waveAmplitude3", flags);
        speed1Field = type.GetField("waveSpeed1", flags);
        speed2Field = type.GetField("waveSpeed2", flags);
        speed3Field = type.GetField("waveSpeed3", flags);
    }

    private void Start()
    {
        CacheBaseline();
        ResolvePlayer();
    }

    private void Update()
    {
        if (!cached) CacheBaseline();
        if (player == null) ResolvePlayer();
        if (!cached) return;

        IslandExpansionWorld expansion = IslandExpansionWorld.Active;
        float targetDark = 0f;
        float targetRough = 0f;
        float targetWindgrove = 0f;
        float targetBrinebreakMatte = 0f;
        WaterDepth = 0f;

        if (expansion != null && expansion.Ready && player != null)
        {
            // Use the generated seabed itself. This makes Suncrest shallows remain
            // readable, then steadily removes bottom visibility as the sea floor
            // falls away instead of changing suddenly at a biome border.
            WaterDepth = Mathf.Max(0f, ocean.BaseWaterLevel - expansion.Height(player.position));
            float depth01 = Mathf.InverseLerp(3.5f, 32f, WaterDepth);
            targetDark = Mathf.SmoothStep(0f, 1f, depth01);

            int biome = expansion.BiomeAt(player.position);
            targetBrinebreakMatte = biome == 1 ? 1f : 0f;

            // Rough-water progression is specifically an outer-ocean effect. At
            // the shelf edge it starts at normal ocean strength and builds over the
            // next ~180 m, avoiding the previous instant wave jump.
            if (biome == WindgroveShoalZone.BiomeId)
            {
                // Start from the same peak height as Deep Ocean, then add 30%.
                targetRough = 1f;
                targetWindgrove = 1f;
            }
            else if (biome == 1 && BrinebreakRockRegion.HasRocks)
                targetRough = Mathf.InverseLerp(1f, DeepWaveMultiplier, BrinebreakWaveMultiplier);
            else if (biome == 2)
            {
                float offshore = expansion.OffshoreAt(player.position);
                targetRough = Mathf.SmoothStep(0f, 1f, offshore);
            }
        }

        roughBlend = Mathf.MoveTowards(roughBlend, targetRough, Time.deltaTime * 0.35f);
        windgroveBlend = Mathf.MoveTowards(windgroveBlend, targetWindgrove, Time.deltaTime * 0.35f);
        darkBlend = Mathf.MoveTowards(darkBlend, targetDark, Time.deltaTime * 0.45f);
        brinebreakMatteBlend = Mathf.MoveTowards(brinebreakMatteBlend, targetBrinebreakMatte, Time.deltaTime * 1.5f);

        ApplyWaveSimulation();
    }

    private void LateUpdate()
    {
        if (!cached || rendererRef == null) return;

        rendererRef.GetPropertyBlock(block);
        block.SetColor("_ShallowColor", Color.Lerp(baseShallowColor, DeepShallowColor, darkBlend));
        block.SetColor("_DeepColor", Color.Lerp(baseDeepColor, DeepDeepColor, darkBlend));
        block.SetFloat("_Alpha", Mathf.Lerp(baseAlpha, DeepAlpha, darkBlend));

        float depthSmoothness = Mathf.Lerp(baseSmoothness, 0.76f, darkBlend);
        float normalFoam = Mathf.Lerp(baseFoamStrength, 1f, roughBlend);
        block.SetFloat("_Smoothness", Mathf.Lerp(depthSmoothness, BrinebreakSmoothness, brinebreakMatteBlend));
        block.SetFloat("_FoamStrength", Mathf.Lerp(normalFoam, BrinebreakFoamStrength, brinebreakMatteBlend));
        block.SetFloat("_SpecularStrength", Mathf.Lerp(baseSpecularStrength, BrinebreakSpecularStrength, brinebreakMatteBlend));
        rendererRef.SetPropertyBlock(block);
    }

    private void CacheBaseline()
    {
        if (cached || ocean == null || rendererRef == null ||
            amplitude1Field == null || amplitude2Field == null || amplitude3Field == null ||
            speed1Field == null || speed2Field == null || speed3Field == null)
            return;

        baseAmplitude1 = (float)amplitude1Field.GetValue(ocean);
        baseAmplitude2 = (float)amplitude2Field.GetValue(ocean);
        baseAmplitude3 = (float)amplitude3Field.GetValue(ocean);
        baseSpeed1 = (float)speed1Field.GetValue(ocean);
        baseSpeed2 = (float)speed2Field.GetValue(ocean);
        baseSpeed3 = (float)speed3Field.GetValue(ocean);

        Material material = rendererRef.sharedMaterial;
        baseShallowColor = material != null && material.HasProperty("_ShallowColor")
            ? material.GetColor("_ShallowColor") : new Color(0.05f, 0.55f, 0.72f, 1f);
        baseDeepColor = material != null && material.HasProperty("_DeepColor")
            ? material.GetColor("_DeepColor") : new Color(0.01f, 0.10f, 0.28f, 1f);
        baseAlpha = material != null && material.HasProperty("_Alpha") ? material.GetFloat("_Alpha") : 0.72f;
        baseSmoothness = material != null && material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.82f;
        baseFoamStrength = material != null && material.HasProperty("_FoamStrength") ? material.GetFloat("_FoamStrength") : 1f;
        baseSpecularStrength = material != null && material.HasProperty("_SpecularStrength") ? material.GetFloat("_SpecularStrength") : 0.8f;
        cached = true;
    }

    private void ResolvePlayer()
    {
        FirstPersonController controller = FindFirstObjectByType<FirstPersonController>();
        player = controller != null ? controller.transform : Camera.main != null ? Camera.main.transform : null;
    }

    private void ApplyWaveSimulation()
    {
        float amplitudeScale = Mathf.Lerp(1f, DeepWaveMultiplier, roughBlend) *
            Mathf.Lerp(1f, WindgroveWaveHeightBonus, windgroveBlend);
        float speedScale = Mathf.Lerp(1f, DeepWaveSpeedMultiplier, roughBlend);
        amplitude1Field.SetValue(ocean, baseAmplitude1 * amplitudeScale);
        amplitude2Field.SetValue(ocean, baseAmplitude2 * amplitudeScale);
        amplitude3Field.SetValue(ocean, baseAmplitude3 * amplitudeScale);
        speed1Field.SetValue(ocean, baseSpeed1 * speedScale);
        speed2Field.SetValue(ocean, baseSpeed2 * speedScale);
        speed3Field.SetValue(ocean, baseSpeed3 * speedScale);
    }

    private void OnDisable()
    {
        WaterDepth = 0f;
        darkBlend = 0f;
        roughBlend = 0f;
        windgroveBlend = 0f;
        brinebreakMatteBlend = 0f;

        if (!cached || ocean == null) return;
        amplitude1Field?.SetValue(ocean, baseAmplitude1);
        amplitude2Field?.SetValue(ocean, baseAmplitude2);
        amplitude3Field?.SetValue(ocean, baseAmplitude3);
        speed1Field?.SetValue(ocean, baseSpeed1);
        speed2Field?.SetValue(ocean, baseSpeed2);
        speed3Field?.SetValue(ocean, baseSpeed3);

        if (rendererRef != null)
        {
            rendererRef.GetPropertyBlock(block);
            block.SetColor("_ShallowColor", baseShallowColor);
            block.SetColor("_DeepColor", baseDeepColor);
            block.SetFloat("_Alpha", baseAlpha);
            block.SetFloat("_Smoothness", baseSmoothness);
            block.SetFloat("_FoamStrength", baseFoamStrength);
            block.SetFloat("_SpecularStrength", baseSpecularStrength);
            rendererRef.SetPropertyBlock(block);
        }
    }
}

