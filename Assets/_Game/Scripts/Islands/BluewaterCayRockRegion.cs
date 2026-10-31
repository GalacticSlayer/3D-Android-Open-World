using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static collider and 150-metre fishing biome cache for artist-placed Bluewater rocks.
/// Does not alter any authored transforms, meshes, materials or terrain.
/// </summary>
public static class BluewaterCayRockRegion
{
    public const float SearchRadius = 300f;
    public const float BiomeRadius = 150f;
    private const string MainName = "Main Blue Water Cay Rock";

    private static readonly List<Collider> rocks = new List<Collider>(96);
    private static readonly HashSet<Collider> rockSet = new HashSet<Collider>();
    private static readonly List<Vector2> anchors = new List<Vector2>(96);
    private static readonly Collider[] overlaps = new Collider[128];
    private static int sceneHandle = -1;
    private static GameObject main;
    private static bool cached;
    private static OceanWater water;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        rocks.Clear();
        rockSet.Clear();
        anchors.Clear();
        sceneHandle = -1;
        cached = false;
        main = null;
        water = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Ensure(); // Player and prop colliders are ready even before a fish is spawned.
    }

    private static bool IsRockName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("cliff", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("boulder", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("outcrop", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AddAnchor(Vector3 world)
    {
        Vector2 p = new Vector2(world.x, world.z);
        for (int i = 0; i < anchors.Count; i++)
            if ((anchors[i] - p).sqrMagnitude < 16f) return;
        anchors.Add(p);
    }

    private static void Collect(GameObject root)
    {
        if (root == null || !root.activeInHierarchy) return;
        AddAnchor(root.transform.position);
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || !filter.gameObject.activeInHierarchy)
                continue;
            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic) continue;
            if (filter.GetComponent<Collider>() == null)
            {
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
            }
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled) AddAnchor(renderer.bounds.center);
        }
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy || rockSet.Contains(collider) ||
                collider.bounds.size.sqrMagnitude < .000001f) continue;
            rocks.Add(collider);
            rockSet.Add(collider);
        }
    }

    private static bool Ensure()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded) return false;
        if (cached && sceneHandle == scene.handle && main != null) return true;
        cached = false;
        sceneHandle = scene.handle;
        main = null;
        rocks.Clear();
        rockSet.Clear();
        anchors.Clear();
        water = null;
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (string.Equals(root.name, MainName, StringComparison.OrdinalIgnoreCase))
            {
                main = root;
                break;
            }
            if (root.name != UserPlacedScenery.RootName) continue;
            foreach (Transform child in root.transform)
                if (string.Equals(child.name, MainName, StringComparison.OrdinalIgnoreCase))
                { main = child.gameObject; break; }
            if (main != null) break;
        }
        if (main == null || !main.activeInHierarchy) return false;

        Vector3 center = main.transform.position;
        float maxDistanceSq = SearchRadius * SearchRadius;
        Collect(main);
        foreach (GameObject root in roots)
        {
            if (root == main) continue;
            if (root.name == UserPlacedScenery.RootName)
            {
                foreach (Transform child in root.transform)
                {
                    if (child.gameObject == main || !IsRockName(child.name)) continue;
                    Vector3 p = child.position - center;
                    p.y = 0f;
                    if (p.sqrMagnitude <= maxDistanceSq) Collect(child.gameObject);
                }
                continue;
            }
            if (!IsRockName(root.name)) continue;
            Vector3 d = root.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude <= maxDistanceSq) Collect(root);
        }
        Physics.SyncTransforms();
        cached = true;
        Debug.Log("[BLUEWATER ROCKS] Registered " + rocks.Count + " colliders, " +
            anchors.Count + " biome anchors for the authored Bluewater cluster.");
        return true;
    }

    /// <summary>
    /// Union of 150 m circles around each placed rock / mesh within 300 m
    /// of the main Bluewater rock. The small inset keeps fish within the biome.
    /// </summary>
    public static bool ContainsBiome(Vector3 point, float edgeInset = 0f)
    {
        if (!Ensure()) return false;
        float r = Mathf.Max(0f, BiomeRadius - Mathf.Max(0f, edgeInset));
        float r2 = r * r;
        Vector2 p = new Vector2(point.x, point.z);
        for (int i = 0; i < anchors.Count; i++)
            if ((p - anchors[i]).sqrMagnitude <= r2) return true;
        return false;
    }

    /// <summary>
    /// True when the player's position is within 'metres' of the physical
    /// surface of ANY authored Bluewater rock collider. Unlike biome anchors,
    /// this tests rock geometry rather than distance to the rock's centre.
    /// </summary>
    public static bool WithinDistanceOfRock(Vector3 point,float metres)
    {
        if(!Ensure())return false;
        float radius=Mathf.Max(0f,metres);
        float radiusSq=radius*radius;
        for(int i=0;i<rocks.Count;i++)
        {
            Collider collider=rocks[i];
            if(collider==null || !collider.enabled ||
               !collider.gameObject.activeInHierarchy)continue;
            if(collider.bounds.SqrDistance(point)>radiusSq)continue;
            Vector3 nearest=collider.ClosestPoint(point);
            if((nearest-point).sqrMagnitude<=radiusSq)return true;
        }
        return false;
    }

    private static float SeaLevel()
    {
        if (water == null)
            water = IslandExpansionWorld.Active != null ? IslandExpansionWorld.Active.Water :
                UnityEngine.Object.FindFirstObjectByType<OceanWater>();
        return water != null ? water.BaseWaterLevel : 0f;
    }

    private static bool PointClear(Vector3 p, float r)
    {
        if (!Ensure()) return true;
        r = Mathf.Max(.15f, r);
        float r2 = r * r;
        bool possible = false;
        for (int i = 0; i < rocks.Count; i++)
        {
            Collider c = rocks[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            if (c.bounds.SqrDistance(p) <= r2) { possible = true; break; }
        }
        if (!possible) return true;
        int count = Physics.OverlapSphereNonAlloc(p, r, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (rockSet.Contains(overlaps[i])) return false;
        return count < overlaps.Length;
    }

    private static bool SegmentMayHit(Vector3 a, Vector3 b, float r, float low, float high)
    {
        if (!Ensure()) return false;
        float minX = Mathf.Min(a.x, b.x) - r, maxX = Mathf.Max(a.x, b.x) + r;
        float minZ = Mathf.Min(a.z, b.z) - r, maxZ = Mathf.Max(a.z, b.z) + r;
        for (int i = 0; i < rocks.Count; i++)
        {
            Collider c = rocks[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds bounds = c.bounds;
            if (bounds.max.x >= minX && bounds.min.x <= maxX &&
                bounds.max.z >= minZ && bounds.min.z <= maxZ &&
                bounds.max.y >= low-r && bounds.min.y <= high+r) return true;
        }
        return false;
    }

    public static bool AmbientPointClear(Vector3 p, float r) { return PointClear(p, r); }

    public static bool AmbientSegmentClear(Vector3 a, Vector3 b, float r)
    {
        if (!SegmentMayHit(a,b,r,Mathf.Min(a.y,b.y),Mathf.Max(a.y,b.y))) return true;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a,b)/.35f));
        for (int i = 1; i <= steps; i++)
            if (!PointClear(Vector3.Lerp(a,b,i/(float)steps),r)) return false;
        return true;
    }

    public static bool SurfacePointClear(Vector3 p, float r)
    {
        float sea = SeaLevel();
        p.y = sea - .30f;
        if (!PointClear(p,r)) return false;
        p.y = sea - .95f;
        if (!PointClear(p,r)) return false;
        p.y = sea - 1.60f;
        return PointClear(p,r);
    }

    public static bool SurfaceSegmentClear(Vector3 a, Vector3 b, float r)
    {
        float sea = SeaLevel();
        if (!SegmentMayHit(a,b,r,sea-1.60f,sea-.30f)) return true;
        Vector3 flat = b-a;
        flat.y = 0f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(flat.magnitude/.55f));
        for (int i = 1; i <= steps; i++)
            if (!SurfacePointClear(Vector3.Lerp(a,b,i/(float)steps),r)) return false;
        return true;
    }
}
