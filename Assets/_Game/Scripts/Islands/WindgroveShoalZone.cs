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

    public static bool TryGetArrival(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded || !HasTurbines) return false;

        // Uses the exact position of the former Grass_0843 on the staircase.
        // The authored, movable marker lives as a separate root in the scene;
        // no turbine or staircase transform is changed.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != ArrivalMarkerName || !root.activeInHierarchy) continue;
            position = root.transform.position + Vector3.up * 0.12f; // footing clearance
            rotation = Quaternion.LookRotation(Vector3.right, Vector3.up); // East (+X)
            return true;
        }
        Debug.LogError("Windgrove Shoal Arrival marker is missing from PrototypeWorld.");
        return false;
    }
}
