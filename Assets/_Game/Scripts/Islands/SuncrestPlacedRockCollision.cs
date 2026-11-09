using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Collision for hand-placed Suncrest rocks. Auto-generated scenery is foliage only.
/// Uses existing mesh geometry (no box surrogates), without changing rock transforms.
/// The original Suncrest main rock continues to use SuncrestMainRockCollision.
/// </summary>
public static class SuncrestPlacedRockCollision
{
    public const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    public const string GeneratedName = "SUNCREST AUTO SCENERY - FOLIAGE ONLY";
    public const string LegacyGeneratedName = "SUNCREST AUTO SCENERY - FOLIAGE AND BEACH ROCKS";
    private static readonly List<Collider> Rocks = new List<Collider>(128);
    private static readonly HashSet<Collider> RockSet = new HashSet<Collider>();
    private static readonly Collider[] Overlaps = new Collider[128];
    private static Bounds combined;
    private static int cachedScene = -1;
    private static bool cached;
    private static OceanWater water;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Rocks.Clear(); RockSet.Clear(); cachedScene=-1; cached=false; water=null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install() { Ensure(); }

    private static bool IsRock(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("rock",StringComparison.OrdinalIgnoreCase)>=0 ||
               name.IndexOf("boulder",StringComparison.OrdinalIgnoreCase)>=0 ||
               name.IndexOf("cliff",StringComparison.OrdinalIgnoreCase)>=0 ||
               name.IndexOf("mountain",StringComparison.OrdinalIgnoreCase)>=0 ||
               name.IndexOf("stone",StringComparison.OrdinalIgnoreCase)>=0;
    }

    // WorldDecor/Rocks is the existing artist-maintained set of independent rocks.
    // Include individually added FBX rock objects as well, but only near Suncrest.
    public static IEnumerable<GameObject> PlacedRocks(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) yield break;
        // Rock colliders must follow the rebuilt main rock, not the retired
        // reef center. The 25 m extra margin includes coastal overhangs.
        GameObject main = SuncrestMainRockCollision.FindRock(scene);
        Vector3 center = main != null ? main.transform.position : Vector3.zero;
        if(main!=null)
        {
            Renderer[] renderers=main.GetComponentsInChildren<Renderer>(true);
            bool found=false;
            Bounds bounds=new Bounds(center,Vector3.zero);
            foreach(Renderer r in renderers)
            {
                if(r==null || !r.enabled)continue;
                if(!found){bounds=r.bounds;found=true;}
                else bounds.Encapsulate(r.bounds);
            }
            if(found)center=bounds.center;
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Do not treat auto-generated beach stones (from an obsolete
            // generation) as user-authored collision; the editor removes them.
            if (root.name == GeneratedName || root.name == LegacyGeneratedName)
                continue;
            if (root.name == "WorldDecor")
            {
                Transform placed = root.transform.Find("Rocks");
                if (placed != null)
                    foreach (Transform item in placed)
                        if (item.gameObject.activeInHierarchy) yield return item.gameObject;
                continue;
            }
            if (root.name == "USER PLACED SCENERY - DO NOT MODIFY BY CODE")
            {
                foreach (Transform item in root.transform)
                {
                    if (!IsRock(item.name) || item.name == SuncrestMainRockCollision.RockName) continue;
                    Vector3 d=item.position-center; d.y=0f;
                    if (d.sqrMagnitude<=175f*175f) yield return item.gameObject;
                }
                continue;
            }
            if (root.name == SuncrestMainRockCollision.RockName ||
                root.name == "WorldDecor" || !IsRock(root.name)) continue;
            Vector3 offset=root.transform.position-center; offset.y=0f;
            if (offset.sqrMagnitude<=175f*175f) yield return root;
        }
    }

    public static int EnsureColliders(Scene scene)
    {
        int added=0;
        foreach (GameObject placed in PlacedRocks(scene))
        foreach (MeshFilter filter in placed.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter==null || filter.sharedMesh==null || !filter.gameObject.activeInHierarchy) continue;
            bool existing=false;
            foreach (MeshCollider c in filter.GetComponents<MeshCollider>())
                if (c.enabled && !c.isTrigger && c.sharedMesh==filter.sharedMesh) {existing=true;break;}
            if (existing) continue;
            Rigidbody body=filter.GetComponentInParent<Rigidbody>();
            if (body!=null && !body.isKinematic) continue;
            MeshCollider collider=filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=filter.sharedMesh;
            collider.convex=false;
            collider.isTrigger=false;
            added++;
        }
        return added;
    }

    private static bool Ensure()
    {
        Scene scene=SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded) return false;
        if (cached && scene.handle==cachedScene) return Rocks.Count>0;
        cached=true; cachedScene=scene.handle;
        Rocks.Clear(); RockSet.Clear(); water=null;
        int added=EnsureColliders(scene);
        Physics.SyncTransforms();
        foreach (GameObject placed in PlacedRocks(scene))
        foreach (Collider c in placed.GetComponentsInChildren<Collider>(true))
        {
            if (c==null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy ||
                c.bounds.size.sqrMagnitude<.000001f || !RockSet.Add(c)) continue;
            if(Rocks.Count==0) combined=c.bounds;
            else combined.Encapsulate(c.bounds);
            Rocks.Add(c);
        }
        if (added>0) Debug.Log("[SUNCREST PLACED ROCKS] Added "+added+" missing MeshColliders.");
        return Rocks.Count>0;
    }

    private static bool MayHit(Vector3 a,Vector3 b,float r,float low,float high)
    {
        if (!Ensure()) return false;
        float left=Mathf.Min(a.x,b.x)-r,right=Mathf.Max(a.x,b.x)+r;
        float back=Mathf.Min(a.z,b.z)-r,front=Mathf.Max(a.z,b.z)+r;
        if(combined.min.x>right || combined.max.x<left ||
           combined.min.z>front || combined.max.z<back ||
           combined.min.y>high+r || combined.max.y<low-r) return false;
        for(int i=0;i<Rocks.Count;i++)
        {
            Collider c=Rocks[i];if(c==null || !c.enabled || !c.gameObject.activeInHierarchy)continue;
            Bounds box=c.bounds;
            if(box.min.x<=right && box.max.x>=left && box.min.z<=front &&
               box.max.z>=back && box.min.y<=high+r && box.max.y>=low-r) return true;
        }
        return false;
    }

    private static bool Clear(Vector3 p,float r)
    {
        r=Mathf.Max(.15f,r);
        if(!MayHit(p,p,r,p.y,p.y))return true;
        int count=Physics.OverlapSphereNonAlloc(p,r,Overlaps,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)if(RockSet.Contains(Overlaps[i]))return false;
        return count<Overlaps.Length;
    }

    public static bool IsRock(Collider collider) => Ensure() && collider != null && RockSet.Contains(collider);

    public static bool AmbientPointClear(Vector3 p,float r){return Clear(p,r);}
    public static bool AmbientSegmentClear(Vector3 a,Vector3 b,float r)
    {
        if(!MayHit(a,b,r,Mathf.Min(a.y,b.y),Mathf.Max(a.y,b.y)))return true;
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.35f));
        for(int i=1;i<=steps;i++)if(!Clear(Vector3.Lerp(a,b,(float)i/steps),r))return false;
        return true;
    }

    private static float Sea()
    {
        if(water==null)water=UnityEngine.Object.FindFirstObjectByType<OceanWater>();
        return water!=null?water.BaseWaterLevel:0f;
    }

    public static bool SurfacePointClear(Vector3 p,float r)
    {
        float sea=Sea();
        if(!MayHit(p,p,r,sea-1.60f,sea-.30f))return true;
        p.y=sea-.30f;if(!Clear(p,r))return false;
        p.y=sea-.95f;if(!Clear(p,r))return false;
        p.y=sea-1.60f;return Clear(p,r);
    }

    public static bool SurfaceSegmentClear(Vector3 a,Vector3 b,float r)
    {
        float sea=Sea();
        if(!MayHit(a,b,r,sea-1.60f,sea-.30f))return true;
        Vector3 delta=b-a;delta.y=0f;
        int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.55f));
        for(int i=1;i<=steps;i++)
            if(!SurfacePointClear(Vector3.Lerp(a,b,(float)i/steps),r))return false;
        return true;
    }
}

