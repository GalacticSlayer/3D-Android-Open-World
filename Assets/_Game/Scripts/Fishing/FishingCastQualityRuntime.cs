using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Applies a biome-wide depth-quality multiplier after the normal species/weight roll.
///
/// For Suncrest and Brinebreak, the reference depth is NOT local to the landing point.
/// It is computed once for the whole biome by sampling many shoreline points and finding
/// the deepest stable water reachable within a 30 m outward cast from those shores.
/// Every ocean cast in that biome -- from land OR from a boat -- is compared with that
/// same reference. Shallow casts therefore produce much smaller/weaker fish, while a
/// cast that reaches the biome's shore-cast reference depth receives full potential.
///
/// Palm Pond is exempt and keeps its dedicated tiny-fish rules.
/// </summary>
[DefaultExecutionOrder(2000)]
public sealed class FishingCastQualityRuntime : MonoBehaviour
{
    private const float ShoreReferenceCastMetres = 30f;
    private const float MinimumQuality = 0.20f; // maximum penalty = 80%
    private const float DepthCurveExponent = 1.55f;
    private const int ShoreDirections = 96;
    private const float ShoreSearchInsideMetres = 24f;
    private const float ShoreSearchOutsideMetres = 28f;
    private const float ShoreSearchStepMetres = 1f;
    private const float ReferenceSampleStepMetres = 1f;
    private const float ShoreWaterThreshold = 0.08f;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo StateField = typeof(FishingSystem).GetField("state", Flags);
    private static readonly FieldInfo PondCastField = typeof(FishingSystem).GetField("pondCast", Flags);
    private static readonly FieldInfo OriginalCastDistanceField = typeof(FishingSystem).GetField("originalCastDistance", Flags);
    private static readonly FieldInfo HookedWeightKgField = typeof(FishingSystem).GetField("hookedWeightKg", Flags);
    private static readonly FieldInfo FishMaxHealthField = typeof(FishingSystem).GetField("fishMaxHealth", Flags);
    private static readonly FieldInfo FishHealthPointsField = typeof(FishingSystem).GetField("fishHealthPoints", Flags);
    private static readonly FieldInfo FishHealthField = typeof(FishingSystem).GetField("fishHealth", Flags);
    private static readonly FieldInfo OceanWaterField = typeof(FishingSystem).GetField("oceanWater", Flags);

    private readonly float[] biomeReferenceDepth = { -1f, -1f, -1f };

    private FishingSystem fishing;
    private OceanWater ocean;
    private Terrain terrain;
    private string previousState = string.Empty;
    private bool measured;
    private bool applied;
    private bool pond;
    private int biome;
    private float castDistance;
    private float castDepth;
    private float referenceDepth;
    private float depthRatio = 1f;
    private float quality = 1f;
    public float OriginalWeight { get; private set; }
    public float DifficultyMultiplier => applied && !pond ? FightQuality(quality) : 1f;
    public static float FightQuality(float sizeQuality) => 0.5f + 0.5f * Mathf.Clamp01(sizeQuality);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if (system != null && system.GetComponent<FishingCastQualityRuntime>() == null)
                system.gameObject.AddComponent<FishingCastQualityRuntime>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        ocean = fishing != null && OceanWaterField != null ? OceanWaterField.GetValue(fishing) as OceanWater : null;
        terrain = Terrain.activeTerrain;

        if (fishing == null || StateField == null ||
            PondCastField == null || OriginalCastDistanceField == null || HookedWeightKgField == null ||
            FishMaxHealthField == null || FishHealthPointsField == null || FishHealthField == null)
        {
            Debug.LogError("FishingCastQualityRuntime could not bind FishingSystem fields and was disabled.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (!enabled || fishing == null) return;
        if (ocean == null && OceanWaterField != null) ocean = OceanWaterField.GetValue(fishing) as OceanWater;
        if (terrain == null) terrain = Terrain.activeTerrain;

        string state = StateName();

        if ((state == "Charging" || state == "Casting") && state != previousState)
            ResetForNewCast();

        // Measure the saved landing, independent of lure/fish movement.
        if (state == "Waiting" && !measured)
            MeasureLandingQuality();

        if (state == "Fighting" && !applied)
            PrepareHookedFish();

        if (state == "Idle" && previousState == "Fighting")
            ResetForNewCast();

        previousState = state;
    }

    private void ResetForNewCast()
    {
        measured = false;
        applied = false;
        pond = false;
        biome = 0;
        castDistance = 0f;
        castDepth = 0f;
        referenceDepth = 0f;
        depthRatio = 1f;
        quality = 1f;
        OriginalWeight = 0f;
    }

    private void MeasureLandingQuality()
    {
        measured = true;
        pond = PondCastField != null && (bool)PondCastField.GetValue(fishing);
        castDistance = OriginalCastDistanceField != null ? Mathf.Max(0f, (float)OriginalCastDistanceField.GetValue(fishing)) : 0f;

        if (pond)
        {
            quality = 1f;
            depthRatio = 1f;
            return;
        }

        Vector3 landing = fishing.CastLandingPoint;
        biome = fishing.CastBiome;
        castDepth = StableWaterDepth(landing);

        // On EVERY island and in EVERY current or future fishing biome, a cast
        // landing inside an active fishing ripple has full depth/close-cast
        // quality. FishingSystem already froze eligibility and the 10-50%
        // size bonus at water impact. Do not query later lure/player movement
        // or a ripple that has since expired. Pond casts remain exempt above.
        if (ShouldWaiveShallowPenalty(biome, fishing.CastHitHotspot))
        {
            quality = 1f;
            depthRatio = 1f;
            referenceDepth = castDepth;
            return;
        }

        referenceDepth = ReferenceDepthForBiome(biome);
        depthRatio = referenceDepth > 0.001f ? Mathf.Clamp01(castDepth / referenceDepth) : 1f;
        quality = QualityFromDepthRatio(depthRatio);
    }

    /// <summary>
    /// All fishing biomes (existing and future) waive shallow/close-water penalties
    /// when the cast hit an active ripple. The biome argument is retained for
    /// existing callers, but intentionally never gates eligibility.
    /// </summary>
    public static bool ShouldWaiveShallowPenalty(int castBiome, bool hitRipple)
        => hitRipple;

    /// <summary>
    /// On ordinary non-ripple casts, depth alone owns quality. The absolute
    /// floor is 20% weight potential; fight HP/difficulty bottoms out at 60%.
    /// </summary>
    public static float QualityFromDepthRatio(float ratio)
    {
        float depth01 = Mathf.Clamp01(ratio);
        float progress = Mathf.Pow(depth01, DepthCurveExponent);
        return Mathf.Lerp(MinimumQuality, 1f, progress);
    }

    // Called synchronously by StartFight before any fish presentation/catch consumer.
    // Update remains a fallback; the guard prevents applying the penalty twice.
    public void PrepareHookedFish()
    {
        if (!enabled || fishing == null || applied) return;
        if (!measured) MeasureLandingQuality();
        ApplyToHookedFish();
    }

    private void ApplyToHookedFish()
    {
        applied = true;
        if (pond) return;

        float oldWeight = Mathf.Max(0.001f, (float)HookedWeightKgField.GetValue(fishing));
        int oldMaxHealth = Mathf.Max(1, (int)FishMaxHealthField.GetValue(fishing));

        float newWeight = Mathf.Max(0.001f, oldWeight * quality);
        OriginalWeight = oldWeight;
        int newMaxHealth = Mathf.Max(1, Mathf.RoundToInt(oldMaxHealth * FightQuality(quality)));

        HookedWeightKgField.SetValue(fishing, newWeight);
        FishMaxHealthField.SetValue(fishing, newMaxHealth);
        FishHealthPointsField.SetValue(fishing, newMaxHealth);
        FishHealthField.SetValue(fishing, 1f);

        Debug.Log(
            "[FISH CAST QUALITY] biome=" + FishingTuning.BiomeId(biome) +
            " cast=" + castDistance.ToString("0.0") + "m" +
            " depth=" + castDepth.ToString("0.00") + "m" +
            " biomeShore30mRef=" + referenceDepth.ToString("0.00") + "m" +
            " depthRatio=" + (depthRatio * 100f).ToString("0") + "%" +
            " quality=" + (quality * 100f).ToString("0") + "%" +
            " weight=" + oldWeight.ToString("0.###") + "->" + newWeight.ToString("0.###") + "kg" +
            " HP=" + oldMaxHealth + "->" + newMaxHealth);
    }

    private int ResolveBiome(Vector3 point)
    {
        IslandExpansionWorld expansion = IslandExpansionWorld.Active;
        if (expansion != null && expansion.Ready)
            return Mathf.Clamp(expansion.BiomeAt(point), 0, 2);
        return 0;
    }

    private float ReferenceDepthForBiome(int targetBiome)
    {
        if (targetBiome == ReefCatalog.SnapperBiomeId)
        {
            var snapper = fishing.GetComponent<SnapperIslandFishingRuntime>();
            if (snapper == null) snapper = fishing.gameObject.AddComponent<SnapperIslandFishingRuntime>();
            float snapperDepth = snapper.ReferenceDepth();
            return snapperDepth > 0.001f ? snapperDepth : Mathf.Max(1f, DeepestTerrainWaterDepth());
        }
        targetBiome = Mathf.Clamp(targetBiome, 0, 2);
        if (biomeReferenceDepth[targetBiome] > 0.001f)
            return biomeReferenceDepth[targetBiome];

        float reference = 0f;
        IslandExpansionWorld expansion = IslandExpansionWorld.Active;
        ReefZone reef = ReefZone.Active;

        if (targetBiome == 0 && reef != null)
        {
            reference = ScanIslandShoreReference(
                reef.center,
                new Vector2(reef.islandRadiusX, reef.islandRadiusZ),
                0);
        }
        else if (targetBiome == 1 && expansion != null && expansion.Ready && expansion.Config != null)
        {
            reference = ScanIslandShoreReference(expansion.NewCenter, expansion.Config.IslandRadii, 1);
        }
        else if (targetBiome == 2 && expansion != null && expansion.Ready && expansion.Config != null)
        {
            // Deep Ocean has no land shoreline of its own. Its authored ocean depth
            // is the stable full-potential reference for boat casts in that biome.
            reference = Mathf.Max(1f, expansion.Config.OceanDepth);
        }

        // Safe fallback for old scenes without the expansion geometry.
        if (reference <= 0.001f)
            reference = Mathf.Max(1f, DeepestTerrainWaterDepth());

        biomeReferenceDepth[targetBiome] = reference;
        Debug.Log("[FISH CAST REFERENCE] " + FishingTuning.BiomeId(targetBiome) +
                  " full-potential depth=" + reference.ToString("0.00") +
                  "m (deepest water reachable by a 30m shore cast; Deep Ocean uses authored ocean depth).");
        return reference;
    }

    private float ScanIslandShoreReference(Vector3 center, Vector2 radii, int targetBiome)
    {
        float best = 0f;

        for (int i = 0; i < ShoreDirections; i++)
        {
            float angle = i * (Mathf.PI * 2f / ShoreDirections);
            Vector3 ellipseEdge = center + new Vector3(Mathf.Cos(angle) * radii.x, 0f, Mathf.Sin(angle) * radii.y);
            Vector3 outward = ellipseEdge - center;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f) continue;
            outward.Normalize();

            Vector3 shore;
            if (!TryFindActualShore(ellipseEdge, outward, targetBiome, out shore))
                continue;

            for (float distance = 0f; distance <= ShoreReferenceCastMetres + 0.01f; distance += ReferenceSampleStepMetres)
            {
                Vector3 sample = shore + outward * distance;
                if (ResolveBiome(sample) != targetBiome) continue;
                best = Mathf.Max(best, StableWaterDepth(sample));
            }
        }

        return best;
    }

    private bool TryFindActualShore(Vector3 approximateEdge, Vector3 outward, int targetBiome, out Vector3 shore)
    {
        shore = approximateEdge;
        bool sawLand = false;

        for (float offset = -ShoreSearchInsideMetres; offset <= ShoreSearchOutsideMetres; offset += ShoreSearchStepMetres)
        {
            Vector3 sample = approximateEdge + outward * offset;
            float depth = StableWaterDepthSigned(sample);

            if (depth <= ShoreWaterThreshold)
            {
                sawLand = true;
                continue;
            }

            if (sawLand && ResolveBiome(sample) == targetBiome)
            {
                shore = sample;
                return true;
            }
        }

        return false;
    }

    private float DeepestTerrainWaterDepth()
    {
        if (terrain == null || ocean == null) return 1f;
        TerrainData data = terrain.terrainData;
        float best = 0f;
        const int grid = 28;
        for (int z = 0; z <= grid; z++)
        for (int x = 0; x <= grid; x++)
        {
            Vector3 p = terrain.transform.position + new Vector3(
                data.size.x * x / grid,
                0f,
                data.size.z * z / grid);
            best = Mathf.Max(best, StableWaterDepth(p));
        }
        return best;
    }

    private float StableWaterDepth(Vector3 position)
    {
        return Mathf.Max(0f, StableWaterDepthSigned(position));
    }

    private float StableWaterDepthSigned(Vector3 position)
    {
        if (ocean == null || !WorldTerrain.TryGetHeight(position, out float ground)) return 0f;
        return ocean.BaseWaterLevel - ground;
    }

    private string StateName()
    {
        object value = StateField.GetValue(fishing);
        return value != null ? value.ToString() : string.Empty;
    }
}

