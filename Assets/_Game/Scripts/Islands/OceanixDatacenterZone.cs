using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Exclusive 150 m Oceanix fishing/discovery zone. Reads the user's existing
/// protected scene placements; never moves or replaces the Datacenter or marker.
/// </summary>
public static class OceanixDatacenterZone
{
    public const int BiomeId = 6;
    public const string Id = "oceanix-datacenter";
    public const string Name = "Oceanix Datacenter";
    public const float Radius = 150f;

    private static Scene cachedScene;
    private static Transform datacenter;
    private static Transform arrival;

    private static void Resolve()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            datacenter = arrival = null;
            return;
        }
        if (scene == cachedScene && datacenter != null && arrival != null)
            return;
        cachedScene = scene;
        datacenter = arrival = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Oceanix_Datacenter") datacenter = root.transform;
            if (root.name == "Grass_0410") arrival = root.transform;
            if (root.name != UserPlacedScenery.RootName) continue;
            Transform protection = root.transform;
            for (int i = 0; i < protection.childCount; i++)
            {
                Transform child = protection.GetChild(i);
                if (child.name == "Oceanix_Datacenter") datacenter = child;
                else if (child.name == "Grass_0410") arrival = child;
            }
        }
    }

    public static bool Ready
    {
        get { Resolve(); return datacenter != null && arrival != null; }
    }

    public static bool Contains(Vector3 point)
    {
        Resolve();
        if (datacenter == null) return false;
        float dx = point.x - datacenter.position.x;
        float dz = point.z - datacenter.position.z;
        return dx * dx + dz * dz <= Radius * Radius;
    }

    public static bool TryGetArrival(CharacterController capsule, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        // N-E-E is the east-northeast bearing (67.5 degrees east of world +Z).
        rotation = Quaternion.Euler(0f, 67.5f, 0f);
        Resolve();
        if (datacenter == null || arrival == null || !arrival.gameObject.activeInHierarchy)
            return false;

        position = arrival.position;
        if (capsule == null) return true;

        // The artist's marker is a foot-level position, not the capsule centre.
        // Place the CharacterController's feet just above that surface without
        // shifting the carefully authored horizontal arrival coordinate.
        float feetOffset = capsule.center.y - capsule.height * .5f;
        position.y = arrival.position.y - feetOffset + .16f;
        if (Physics.Raycast(arrival.position + Vector3.up * 2f, Vector3.down,
            out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore) &&
            floor.normal.y >= .6f && Mathf.Abs(floor.point.y - arrival.position.y) < 1f)
            position.y = floor.point.y - feetOffset + .16f;
        return true;
    }
}
