using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Windgrove is the union of horizontal 150 m circles around the nine
/// artist-placed WindTurbine FBX instances in PrototypeWorld.
/// Never modifies an authored scene object or terrain.
/// </summary>
public static class WindgroveShoalZone
{
    public const int BiomeId = 5;
    public const string Id = "windgrove-shoal";
    public const string Name = "The Windgrove Shoal";
    public const float Radius = 150f;

    private static Scene cachedScene;
    private static Transform[] turbines;

    private static Transform[] Turbines()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            turbines = null;
            return null;
        }

        if (turbines != null && cachedScene == scene)
        {
            bool valid = turbines.Length > 0;
            foreach (Transform t in turbines)
                if (t == null) { valid = false; break; }
            if (valid) return turbines;
        }

        cachedScene = scene;
        var found = new System.Collections.Generic.List<Transform>(12);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Direct FBX placements only, never the retired generated wrappers.
            string name = root.name;
            if ((name == "WindTurbine" ||
                 (name.StartsWith("WindTurbine (", StringComparison.Ordinal) && name.EndsWith(")", StringComparison.Ordinal))) &&
                root.activeInHierarchy)
                found.Add(root.transform);
        }

        turbines = found.ToArray();
        return turbines;
    }

    public static bool HasTurbines
    {
        get { Transform[] anchors = Turbines(); return anchors != null && anchors.Length > 0; }
    }

    public static bool Contains(Vector3 point, float inset = 0f)
    {
        Transform[] anchors = Turbines();
        if (anchors == null) return false;

        float radius = Mathf.Max(0f, Radius - inset);
        float r2 = radius * radius;
        foreach (Transform anchor in anchors)
        {
            float dx = point.x - anchor.position.x;
            float dz = point.z - anchor.position.z;
            if (dx * dx + dz * dz <= r2) return true;
        }
        return false;
    }

    public static bool TryGetNorthmost(out Transform northmost)
    {
        northmost = null;
        Transform[] anchors = Turbines();
        if (anchors == null) return false;
        foreach (Transform anchor in anchors)
            if (northmost == null || anchor.position.z > northmost.position.z)
                northmost = anchor;
        return northmost != null;
    }

    private const string ArrivalMarkerName = "Windgrove Shoal Arrival";

    /// <summary>
    /// Finds a safe player-sized landing on the actual imported staircase.
    /// The artist's marker is an X/Z target, not a guess at the walkable mesh
    /// height. Recalculate on every teleport, so later scene rescaling or
    /// terrain edits never leave the player embedded in the steps.
    /// </summary>
    public static bool TryGetArrival(CharacterController capsule,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded || !HasTurbines || capsule == null)
            return false;

        Transform marker = null, staircase = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == ArrivalMarkerName && root.activeInHierarchy)
                marker = root.transform;
            else if (root.name == "Wind Turbine Staircase" && root.activeInHierarchy)
                staircase = root.transform;
        }
        if (marker == null || staircase == null)
        {
            Debug.LogError("[WINDGROVE TRAVEL] Missing staircase or Windgrove arrival marker.");
            return false;
        }

        // Auto-generated static MeshColliders are attached to the imported
        // staircase meshes. Their shapes follow its non-uniform scene scale;
        // they must NOT be manually scaled or duplicated.
        MeshCollider[] parts = staircase.GetComponentsInChildren<MeshCollider>();
        if (parts.Length == 0)
        {
            Debug.LogError("[WINDGROVE TRAVEL] Staircase has no MeshColliders. Check its model importer.");
            return false;
        }

        Vector3 scale = capsule.transform.lossyScale;
        float radius = Mathf.Max(0.15f, capsule.radius *
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float halfHeight = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * 0.5f);
        float centerY = capsule.center.y * scale.y;
        float feetOffset = centerY - halfHeight;
        float cylinderHalf = Mathf.Max(0f, halfHeight - radius);

        Physics.SyncTransforms();
        float rayTop = marker.position.y + 4f;
        foreach (MeshCollider part in parts)
            if (part != null && part.enabled && part.gameObject.activeInHierarchy)
                rayTop = Mathf.Max(rayTop, part.bounds.max.y + 2f);

        // Preserve the Grass_0843 horizontal position whenever safe. Small
        // offsets only help if its exact centre lies on a railing or step lip.
        Vector2[] probes =
        {
            Vector2.zero,
            new Vector2(.3f,0f), new Vector2(-.3f,0f),
            new Vector2(0f,.3f), new Vector2(0f,-.3f),
            new Vector2(.6f,0f), new Vector2(-.6f,0f),
            new Vector2(0f,.6f), new Vector2(0f,-.6f),
            new Vector2(.6f,.6f), new Vector2(-.6f,.6f),
            new Vector2(.6f,-.6f), new Vector2(-.6f,-.6f),
            new Vector2(1.0f,0f), new Vector2(-1.0f,0f),
            new Vector2(0f,1.0f), new Vector2(0f,-1.0f)
        };
        float bestScore = float.PositiveInfinity;
        bool found = false;
        foreach (Vector2 probe in probes)
        {
            float x = marker.position.x + probe.x;
            float z = marker.position.z + probe.y;
            if (!TryWalkableStairSurface(parts, x, z, rayTop,
                marker.position.y, out RaycastHit floor)) continue;

            // Reject narrow handrails: the player needs nearby tread surface,
            // not just one horizontal triangle beneath the ray.
            int supported = 0;
            float edgeDistance = Mathf.Max(0.16f, radius * .55f);
            for (int i = 0; i < 4; i++)
            {
                float dx = i == 0 ? edgeDistance : i == 1 ? -edgeDistance : 0f;
                float dz = i == 2 ? edgeDistance : i == 3 ? -edgeDistance : 0f;
                if (TryWalkableStairSurface(parts, x + dx, z + dz,
                    rayTop, marker.position.y, out RaycastHit edge) &&
                    Mathf.Abs(edge.point.y - floor.point.y) <= .36f)
                    supported++;
            }
            if (supported < 3) continue;

            // 32 cm between the sole and the surface accounts for controller
            // skin/capsule geometry. The rotation is ALWAYS east (+X).
            Vector3 candidate = new Vector3(x, floor.point.y + .32f - feetOffset, z);
            Vector3 centre = candidate + Vector3.up * centerY;
            Collider[] obstacles = Physics.OverlapCapsule(
                centre - Vector3.up * cylinderHalf,
                centre + Vector3.up * cylinderHalf,
                Mathf.Max(.1f, radius - .02f), ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            foreach (Collider obstacle in obstacles)
                if (obstacle != null && obstacle != capsule &&
                    !obstacle.transform.IsChildOf(capsule.transform))
                { blocked = true; break; }
            if (blocked) continue;

            float score = probe.sqrMagnitude * 4f +
                Mathf.Abs(floor.point.y - marker.position.y) * .15f +
                (4 - supported) * .25f;
            if (score >= bestScore) continue;
            position = candidate;
            bestScore = score;
            found = true;
        }

        if (!found)
            Debug.LogError("[WINDGROVE TRAVEL] No clear walkable staircase landing near the arrival marker. Teleport cancelled.");
        return found;
    }

    private static bool TryWalkableStairSurface(MeshCollider[] parts,
        float x, float z, float rayTop, float markerY, out RaycastHit best)
    {
        best = default(RaycastHit);
        float highest = float.NegativeInfinity;
        Ray ray = new Ray(new Vector3(x, rayTop, z), Vector3.down);
        foreach (MeshCollider part in parts)
        {
            if (part == null || !part.enabled || !part.gameObject.activeInHierarchy)
                continue;
            if (!part.Raycast(ray, out RaycastHit hit, rayTop - markerY + 4f) ||
                hit.normal.y < .7f || Mathf.Abs(hit.point.y - markerY) > 3f ||
                hit.point.y <= highest)
                continue;
            best = hit;
            highest = hit.point.y;
        }
        return highest > float.NegativeInfinity;
    }
}
