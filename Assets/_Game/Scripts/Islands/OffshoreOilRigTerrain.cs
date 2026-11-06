using UnityEngine;

/// <summary>
/// The rig terrain is an ordinary, saved Unity TerrainData asset. Only its
/// renderer is distance-gated; the TerrainCollider and editable data stay intact.
/// This never shifts coordinates, player rigs, boats or multiplayer messages.
/// </summary>
[DisallowMultipleComponent]
public sealed class OffshoreOilRigTerrain : MonoBehaviour
{
    public Terrain terrain;
    // Only the offshore tile uses this persisted one-time editor migration.
    // Bumping it never resculpts the original Suncrest/South terrain.
    [SerializeField, HideInInspector] private int seabedProfileVersion;
    public int SeabedProfileVersion => seabedProfileVersion;

#if UNITY_EDITOR
    public void MarkSeabedProfileApplied(int version)
    {
        seabedProfileVersion = version;
    }
#endif

    [SerializeField] private float renderDistance = 900f;
    private Transform player;
    private float nextCheck;

    private void Awake()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
    }

    private void LateUpdate()
    {
        if (terrain == null || terrain.terrainData == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .75f;
        if (player == null)
        {
            FirstPersonController local = FindFirstObjectByType<FirstPersonController>();
            if (local == null) return;
            player = local.transform;
        }
        Vector3 p = player.position;
        Vector3 a = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        // Distance to the tile rectangle, rather than its centre: no pop near an edge.
        float dx = Mathf.Max(0f, Mathf.Max(a.x - p.x, p.x - a.x - size.x));
        float dz = Mathf.Max(0f, Mathf.Max(a.z - p.z, p.z - a.z - size.z));
        bool nearby = dx * dx + dz * dz <= renderDistance * renderDistance;
        if (terrain.drawHeightmap != nearby) terrain.drawHeightmap = nearby;
    }
}
