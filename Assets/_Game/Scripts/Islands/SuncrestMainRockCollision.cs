using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds missing solid MeshColliders to the artist's Suncrest beach main rock v2 and
/// uses them for player physics, ambient fish and surface/fighting fish.
/// Never edits authored meshes, transforms, materials or other scenery.
/// </summary>
public static class SuncrestMainRockCollision
{
    public const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    public const string RockName = "Suncrest beach main rock v2";
    public const string RockFbx = "Assets/Manually Added Assets/Suncrest beach main rock v2.fbx";

    private static readonly List<Collider> Rocks = new List<Collider>(16);
    private static readonly HashSet<Collider> RockSet = new HashSet<Collider>();
    private static readonly Collider[] Overlaps = new Collider[128];
    private static GameObject rock;
    private static Bounds bounds;
    private static int sceneHandle = -1;
    private static bool cached;
    private static OceanWater water;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        sceneHandle = -1;
        cached = false;
        rock = null;
        water = null;
        Rocks.Clear();
        RockSet.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install() { Ensure(); }

    public static GameObject FindRock(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, RockName, StringComparison.OrdinalIgnoreCase))
                return root;
            if (root.name != "USER PLACED SCENERY - DO NOT MODIFY BY CODE") continue;
            foreach (Transform child in root.transform)
                if (string.Equals(child.name, RockName, StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;
        }
        return null;
    }

    public static IEnumerable<MeshFilter> PlacedMeshes(Scene scene)
    {
        GameObject target = FindRock(scene);
        if (target == null) yield break;
        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
            if (filter != null && filter.sharedMesh != null && filter.gameObject.activeInHierarchy)
                yield return filter;
    }

    public static bool HasSolidShape(MeshFilter filter)
    {
        foreach (MeshCollider collider in filter.GetComponents<MeshCollider>())
            if (collider.enabled && !collider.isTrigger &&
                collider.sharedMesh == filter.sharedMesh) return true;
        return false;
    }

    public static int EnsureColliders(Scene scene)
    {
        int added = 0;
        foreach (MeshFilter filter in PlacedMeshes(scene))
        {
            if (HasSolidShape(filter)) continue;
            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic) continue;
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.convex = false;
            collider.isTrigger = false;
            collider.sharedMesh = filter.sharedMesh;
            added++;
        }
        return added;
    }

    private static bool Ensure()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded) return false;
        if (cached && sceneHandle == scene.handle && rock != null && rock.activeInHierarchy)
            return Rocks.Count != 0;

        sceneHandle = scene.handle;
        cached = true;
        rock = FindRock(scene);
        Rocks.Clear();
        RockSet.Clear();
        water = null;
        if (rock == null || !rock.activeInHierarchy) return false;

        int added = EnsureColliders(scene);
        Physics.SyncTransforms();
        foreach (Collider collider in rock.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy ||
                collider.bounds.size.sqrMagnitude < .000001f ||
                !RockSet.Add(collider)) continue;
            if (Rocks.Count == 0) bounds = collider.bounds;
            else bounds.Encapsulate(collider.bounds);
            Rocks.Add(collider);
        }
        if (Rocks.Count == 0)
            Debug.LogWarning("[SUNCREST ROCK] No usable solid colliders.");
        else
            Debug.Log("[SUNCREST ROCK] " + Rocks.Count +
                " solid colliders active (" + added + " added at load).");
        return Rocks.Count > 0;
    }

    // AABB broadphase restricts expensive physics tests to water actually near the rock.
    private static bool MayHit(Vector3 a, Vector3 b, float r, float low, float high)
    {
        if (!Ensure()) return false;
        float minX = Mathf.Min(a.x, b.x) - r, maxX = Mathf.Max(a.x, b.x) + r;
        float minZ = Mathf.Min(a.z, b.z) - r, maxZ = Mathf.Max(a.z, b.z) + r;
        if (bounds.min.x > maxX || bounds.max.x < minX ||
            bounds.min.z > maxZ || bounds.max.z < minZ ||
            bounds.min.y > high + r || bounds.max.y < low - r) return false;
        for (int i = 0; i < Rocks.Count; i++)
        {
            Collider c = Rocks[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds box = c.bounds;
            if (box.min.x <= maxX && box.max.x >= minX &&
                box.min.z <= maxZ && box.max.z >= minZ &&
                box.min.y <= high + r && box.max.y >= low - r) return true;
        }
        return false;
    }

    private static bool PointClear(Vector3 p, float r)
    {
        r = Mathf.Max(.15f, r);
        if (!MayHit(p, p, r, p.y, p.y)) return true;
        int count = Physics.OverlapSphereNonAlloc(p, r, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (RockSet.Contains(Overlaps[i])) return false;
        return count < Overlaps.Length;
    }

    public static bool IsRock(Collider collider) =>
        (Ensure() && collider != null && RockSet.Contains(collider)) ||
        SuncrestPlacedRockCollision.IsRock(collider);

    public static bool AmbientPointClear(Vector3 p, float r)
    {
        return PointClear(p,r) && SuncrestPlacedRockCollision.AmbientPointClear(p,r);
    }

    public static bool AmbientSegmentClear(Vector3 a, Vector3 b, float r)
    {
        if (!SuncrestPlacedRockCollision.AmbientSegmentClear(a,b,r)) return false;
        if (!MayHit(a,b,r,Mathf.Min(a.y,b.y),Mathf.Max(a.y,b.y))) return true;
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.35f));
        for(int i=1;i<=steps;i++)
            if(!PointClear(Vector3.Lerp(a,b,(float)i/steps),r))return false;
        return true;
    }

    private static float SeaLevel()
    {
        if(water==null)water=UnityEngine.Object.FindFirstObjectByType<OceanWater>();
        return water!=null?water.BaseWaterLevel:0f;
    }

    private static bool MainSurfacePointClear(Vector3 p,float r)
    {
        float sea=SeaLevel();
        if(!MayHit(p,p,r,sea-1.60f,sea-.30f))return true;
        p.y=sea-.30f;if(!PointClear(p,r))return false;
        p.y=sea-.95f;if(!PointClear(p,r))return false;
        p.y=sea-1.60f;return PointClear(p,r);
    }

    public static bool SurfacePointClear(Vector3 p,float r)
    {
        return MainSurfacePointClear(p,r) && SuncrestPlacedRockCollision.SurfacePointClear(p,r);
    }

    public static bool SurfaceSegmentClear(Vector3 a,Vector3 b,float r)
    {
        if(!SuncrestPlacedRockCollision.SurfaceSegmentClear(a,b,r))return false;
        float sea=SeaLevel();
        if(!MayHit(a,b,r,sea-1.60f,sea-.30f))return true;
        Vector3 flat=b-a;flat.y=0f;
        int steps=Mathf.Max(1,Mathf.CeilToInt(flat.magnitude/.55f));
        for(int i=1;i<=steps;i++)
            if(!MainSurfacePointClear(Vector3.Lerp(a,b,(float)i/steps),r))return false;
        return true;
    }
}

