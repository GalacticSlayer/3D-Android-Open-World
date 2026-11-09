using UnityEngine;

/// <summary>Bounds-aware queries across the editable main map and added terrain tiles.</summary>
public static class WorldTerrain
{
    private static Terrain[] tiles;
    private static int cachedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { tiles = null; cachedFrame = -1; }

    public static Terrain[] Tiles
    {
        get
        {
            // Terrain.activeTerrains returns an array. Share it across the many
            // route/footprint samples this frame rather than allocate per sample.
            if (tiles == null || cachedFrame != Time.frameCount)
            {
                tiles = Terrain.activeTerrains;
                cachedFrame = Time.frameCount;
            }
            return tiles;
        }
    }

    public static bool TryGetHeight(Vector3 position, out float height)
    {
        height = float.NegativeInfinity;
        bool found = false;
        foreach (Terrain tile in Tiles)
        {
            if (tile == null || !tile.isActiveAndEnabled || tile.terrainData == null) continue;
            Vector3 local = position - tile.transform.position;
            Vector3 size = tile.terrainData.size;
            // SampleHeight clamps to the edge outside a tile: never sample there.
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;
            height = Mathf.Max(height, tile.SampleHeight(position) + tile.transform.position.y);
            found = true;
        }
        return found;
    }

    // Called once at bite time for offshore specimen rolls, never every frame.
    // Measure horizontally against stable sea level, including placed rock islands.
    public static bool AtLeastThirtyMetresFromShore(Vector3 hook, OceanWater water)
    {
        if (water == null) return false;
        const float radius = 30f;
        float sea = water.BaseWaterLevel;
        hook.y = sea;
        if (TryGetHeight(hook, out float ground) && ground >= sea) return false;

        // Inspect only nearby heightmap cells, intersecting their triangles with
        // the water plane. This avoids thousands of SampleHeight calls per bite.
        foreach (Terrain tile in Tiles)
        {
            if (tile == null || !tile.isActiveAndEnabled || tile.terrainData == null) continue;
            TerrainData data = tile.terrainData;
            Vector3 origin = tile.transform.position;
            Vector3 size = data.size;
            if (hook.x+radius < origin.x || hook.x-radius > origin.x+size.x ||
                hook.z+radius < origin.z || hook.z-radius > origin.z+size.z) continue;
            int last = data.heightmapResolution-1;
            float dx = size.x/last, dz = size.z/last;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((hook.x-radius-origin.x)/dx),0,last-1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((hook.z-radius-origin.z)/dz),0,last-1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((hook.x+radius-origin.x)/dx),x0+1,last);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((hook.z+radius-origin.z)/dz),z0+1,last);
            float[,] heights = data.GetHeights(x0,z0,x1-x0+1,z1-z0+1);
            for (int z=z0; z<z1; z++)
            for (int x=x0; x<x1; x++)
            {
                int ix=x-x0, iz=z-z0;
                Vector3 a=origin+new Vector3(x*dx,heights[iz,ix]*size.y,z*dz);
                Vector3 b=origin+new Vector3((x+1)*dx,heights[iz,ix+1]*size.y,z*dz);
                Vector3 c=origin+new Vector3(x*dx,heights[iz+1,ix]*size.y,(z+1)*dz);
                Vector3 d=origin+new Vector3((x+1)*dx,heights[iz+1,ix+1]*size.y,(z+1)*dz);
                if (ShoreTriangleNear(a,b,c,hook,radius) || ShoreTriangleNear(b,d,c,hook,radius)) return false;
            }
        }

        // Only rock/terrain shoreline counts; boats, fish, docks and triggers do
        // not. Test actual waterline intersections, not underwater rock bounds.
        foreach (Collider rock in Physics.OverlapSphere(hook, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!BrinebreakRockRegion.IsRock(rock) && !SuncrestMainRockCollision.IsRock(rock)) continue;
            if (rock.bounds.min.y > sea || rock.bounds.max.y < sea) continue;
            Vector3 toward = rock.bounds.center - hook;
            toward.y = 0f;
            if (toward.sqrMagnitude > .0001f &&
                rock.Raycast(new Ray(hook, toward.normalized), out RaycastHit hit, radius) && hit.distance < radius)
                return false;
            // Sub-degree fan: at 30m adjacent rays are less than 0.27m apart.
            for (int angle = 0; angle < 720; angle++)
            {
                float radians = angle * (.5f * Mathf.Deg2Rad);
                Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                if (rock.Raycast(new Ray(hook, direction), out hit, radius) && hit.distance < radius)
                    return false;
            }
        }
        return true;
    }

    private static bool ShoreTriangleNear(Vector3 a,Vector3 b,Vector3 c,Vector3 hook,float radius)
    {
        Vector3 first=Vector3.zero, second=Vector3.zero;
        int count=0;
        ShoreEdge(a,b,hook.y,ref first,ref second,ref count);
        ShoreEdge(b,c,hook.y,ref first,ref second,ref count);
        ShoreEdge(c,a,hook.y,ref first,ref second,ref count);
        if(count==0)return false;
        Vector3 edge=second-first;
        Vector3 closest=count==1 || edge.sqrMagnitude<.000001f ? first :
            first+edge*Mathf.Clamp01(Vector3.Dot(hook-first,edge)/edge.sqrMagnitude);
        return (hook-closest).sqrMagnitude<radius*radius;
    }

    private static void ShoreEdge(Vector3 a,Vector3 b,float sea,ref Vector3 first,ref Vector3 second,ref int count)
    {
        if((a.y<sea && b.y<sea) || (a.y>sea && b.y>sea))return;
        if(Mathf.Abs(a.y-b.y)<.000001f)
        {
            first=a;second=b;count=2;return;
        }
        Vector3 point=Vector3.Lerp(a,b,(sea-a.y)/(b.y-a.y));
        if(count==0)first=point;
        else if((point-first).sqrMagnitude<.000001f)return;
        else second=point;
        count++;
    }

    public static bool SurfaceSegmentClear(Vector3 from, Vector3 to, OceanWater water, float clearance)
    {
        if (water == null) return true;
        Vector3 delta = to - from;
        delta.y = 0f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .4f));
        // Include the origin: a falling wave must not leave a subdued fish under
        // a newly exposed bank. Intermediate samples also prevent fast retrieves
        // from skipping a narrow ridge between otherwise-deep endpoints.
        for (int i = 0; i <= steps; i++)
        {
            Vector3 sample = Vector3.Lerp(from, to, i / (float)steps);
            if (TryGetHeight(sample, out float floor) &&
                water.GetSurfaceHeight(sample) - floor <= clearance) return false;
        }
        return true;
    }
}

