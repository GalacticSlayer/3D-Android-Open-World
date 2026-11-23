using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Exclusive 150 m Oceanix fishing/discovery zone. Uses the protected
/// datacenter prefab and preserves the old teleport coordinates without its grass marker.
/// </summary>
public static class OceanixDatacenterZone
{
    public const int BiomeId = 6;
    public const string Id = "oceanix-datacenter";
    public const string Name = "Oceanix Datacenter";
    public const float Radius = 150f;

    private static Scene cachedScene;
    private static Transform datacenter;
    // Artist-placed Grass_0410 previously marked this exact foot-level spawn.
    private static readonly Vector3 ArrivalPosition = new Vector3(-249.255f, 5.647f, -3073.303f);

    private static void Resolve()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            datacenter = null;
            return;
        }
        if (scene == cachedScene && datacenter != null)
            return;
        cachedScene = scene;
        datacenter = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Oceanix_Datacenter") datacenter = root.transform;
            if (root.name != UserPlacedScenery.RootName) continue;
            Transform protection = root.transform;
            for (int i = 0; i < protection.childCount; i++)
            {
                Transform child = protection.GetChild(i);
                if (child.name == "Oceanix_Datacenter") datacenter = child;
            }
        }
    }

    public static bool Ready
    {
        get { Resolve(); return datacenter != null; }
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
        if (datacenter == null || !datacenter.gameObject.activeInHierarchy)
            return false;

        position = ArrivalPosition;
        if (capsule == null) return true;

        // The artist's marker is a foot-level position, not the capsule centre.
        // Place the CharacterController's feet just above that surface without
        // shifting the carefully authored horizontal arrival coordinate.
        float feetOffset = capsule.center.y - capsule.height * .5f;
        position.y = ArrivalPosition.y - feetOffset + .16f;
        if (Physics.Raycast(ArrivalPosition + Vector3.up * 2f, Vector3.down,
            out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore) &&
            floor.normal.y >= .6f && Mathf.Abs(floor.point.y - ArrivalPosition.y) < 1f)
            position.y = floor.point.y - feetOffset + .16f;
        return true;
    }
}
