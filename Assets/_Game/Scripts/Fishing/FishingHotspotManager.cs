using UnityEngine;

/// <summary>Five pooled ocean hotspots. Placement uses the saved terrain, including edited shores.</summary>
[DefaultExecutionOrder(2500)]
[DisallowMultipleComponent]
public sealed class FishingHotspotManager : MonoBehaviour
{
    private static FishingHotspotManager active;
    public const int MaximumHotspots = 5;
    public const float MaximumPlayerDistance = 60f;
    public const float LandClearance = 15f;
    // 1.8x the original 5.6m-wide visual and casting footprint.
    public const float RippleScale = 1.8f;
    public const float Radius = 2.8f * RippleScale;
    private readonly FishingHotspotEffect[] pool = new FishingHotspotEffect[MaximumHotspots];
    private readonly Collider[] obstacles = new Collider[64];
    private OceanWater ocean;
    private GameObject prefab;
    private Material rippleMaterial, bubbleMaterial;
    private Mesh rippleMesh;
    private float nextPlacement;
    private bool initialized;

    private void Awake()
    {
        // Enforce the global cap even if a second player/system is added to a scene.
        if (active != null && active != this) { enabled = false; return; }
        active = this;
    }

    private void Start()
    {
        ocean = FindFirstObjectByType<OceanWater>();
        prefab = Resources.Load<GameObject>("Fishing/Hotspots/FishingHotspot");
        Shader shader = Resources.Load<Shader>("Fishing/Hotspots/HotspotParticles");
        Texture2D ripple = Resources.Load<Texture2D>("Fishing/Hotspots/Ripple");
        Texture2D bubble = Resources.Load<Texture2D>("Fishing/Hotspots/Bubble");
        if (ocean == null || prefab == null || shader == null || ripple == null || bubble == null)
        {
            Debug.LogError("Fishing hotspots could not load their ocean, prefab or textures.");
            enabled = false;
            return;
        }
        rippleMaterial = new Material(shader) { name = "Shared Hotspot Ripple" };
        rippleMaterial.SetTexture("_MainTex", ripple);
        rippleMaterial.SetFloat("_FollowSurface", 1f);
        bubbleMaterial = new Material(shader) { name = "Shared Hotspot Bubble" };
        bubbleMaterial.SetTexture("_MainTex", bubble);
        rippleMesh = FishingHotspotEffect.CreateRippleMesh();
        for (int i = 0; i < pool.Length; i++)
        {
            var instance = Instantiate(prefab);
            instance.name = "FishingHotspot_" + (i + 1);
            pool[i] = instance.GetComponent<FishingHotspotEffect>();
            pool[i].Initialize(ocean, rippleMaterial, bubbleMaterial, rippleMesh);
            instance.SetActive(false);
        }
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;
        bool away = ShopDimensionManager.Instance != null &&
            (ShopDimensionManager.Instance.InDimension || ShopDimensionManager.Instance.Traveling);
        for (int i = 0; i < pool.Length; i++)
        {
            var spot = pool[i];
            if (spot.gameObject.activeSelf && (away ||
                HorizontalDistance(spot.transform.position, transform.position) > MaximumPlayerDistance - Radius ||
                Time.time >= spot.ExpiresAt)) spot.gameObject.SetActive(false);
        }
        if (away || Time.time < nextPlacement) return;
        nextPlacement = Time.time + 1f;
        // At most eight placement probes and one new hotspot per second on Android.
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].gameObject.activeSelf) continue;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * (MaximumPlayerDistance - Radius);
                if (offset.sqrMagnitude < 10f * 10f) continue;
                Vector3 p = transform.position + new Vector3(offset.x, 0f, offset.y);
                if (!ValidPosition(p)) continue;
                pool[i].Place(p, Time.time + Random.Range(90f, 150f));
                break;
            }
            break;
        }
    }

    public float LandingMultiplier(Vector3 landing)
    {
        if (!initialized || !isActiveAndEnabled) return 1f;
        float best = 1f;
        foreach (var spot in pool)
        {
            if (!spot.gameObject.activeInHierarchy || Time.time >= spot.ExpiresAt ||
                HorizontalDistance(spot.transform.position, transform.position) > MaximumPlayerDistance - Radius) continue;
            best = Mathf.Max(best, SizeMultiplier(HorizontalDistance(landing, spot.transform.position)));
        }
        return best;
    }

    public static float SizeMultiplier(float distance)
    {
        if (distance < 0f || distance > Radius) return 1f;
        return Mathf.Lerp(1.5f, 1.1f, Mathf.Clamp01(distance / Radius));
    }

    private bool ValidPosition(Vector3 p)
    {
        if (ocean == null ||
            (PondWater.Active != null && PondWater.Active.Contains(p))) return false;
        // Select only the tile actually beneath this point. Terrain.activeTerrain
        // may be Suncrest even when the player is 3 km south at the oil rig.
        Terrain terrain = null;
        foreach (Terrain candidate in WorldTerrain.Tiles)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.terrainData == null) continue;
            Vector3 localPoint = p - candidate.transform.position;
            Vector3 bounds = candidate.terrainData.size;
            if (localPoint.x < 0f || localPoint.z < 0f ||
                localPoint.x > bounds.x || localPoint.z > bounds.z) continue;
            terrain = candidate;
            break;
        }
        if (terrain == null) return false;
        foreach (var spot in pool)
            if (spot != null && spot.gameObject.activeSelf && HorizontalDistance(p, spot.transform.position) < 12f) return false;

        // Check EVERY heightmap vertex in a conservatively expanded disk, not just
        // a few radial samples which can miss a narrow spit or a user-edited rock island.
        // The entire 10.08m effect footprint stays >=15m from land, not only its centre.
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 local = p - origin;
        float sx = data.size.x / (data.heightmapResolution - 1);
        float sz = data.size.z / (data.heightmapResolution - 1);
        float clearance = LandClearance + Radius;
        float search = clearance + Mathf.Sqrt(sx * sx + sz * sz);
        if (local.x < search || local.z < search || local.x > data.size.x - search || local.z > data.size.z - search) return false;
        float sea = ocean.BaseWaterLevel;
        if (terrain.SampleHeight(p) + origin.y > sea - 1.2f) return false;
        int x0 = Mathf.FloorToInt((local.x - search) / sx);
        int z0 = Mathf.FloorToInt((local.z - search) / sz);
        int x1 = Mathf.CeilToInt((local.x + search) / sx);
        int z1 = Mathf.CeilToInt((local.z + search) / sz);
        float[,] heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        for (int z = 0; z < heights.GetLength(0); z++)
        for (int x = 0; x < heights.GetLength(1); x++)
        {
            float dx = (x0 + x) * sx - local.x, dz = (z0 + z) * sz - local.z;
            if (dx * dx + dz * dz <= search * search &&
                heights[z, x] * data.size.y + origin.y >= sea - 0.15f) return false;
        }
        // Also reject exposed static mesh rocks/structures added in the Scene editor.
        p.y = sea;
        int count = Physics.OverlapSphereNonAlloc(p, clearance, obstacles, ~0, QueryTriggerInteraction.Ignore);
        if (count == obstacles.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider c = obstacles[i];
            if (c is TerrainCollider || c.attachedRigidbody != null || c.transform.IsChildOf(transform) ||
                c.GetComponentInParent<OceanWater>() != null || c.GetComponentInParent<BoatController>() != null ||
                c.name == "BoatPlacementWater") continue;
            if (c.bounds.max.y >= sea && c.bounds.min.y <= sea + 2f) return false;
        }
        return true;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x, z = a.z - b.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private void OnDisable()
    {
        foreach (var spot in pool) if (spot != null) spot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
        foreach (var spot in pool) if (spot != null) Destroy(spot.gameObject);
        if (rippleMaterial != null) Destroy(rippleMaterial);
        if (bubbleMaterial != null) Destroy(bubbleMaterial);
        if (rippleMesh != null) Destroy(rippleMesh);
    }
}
