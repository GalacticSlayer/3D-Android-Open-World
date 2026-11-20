using UnityEngine;

/// <summary>
/// Generated HUD/player helpers are not guaranteed to exist at AfterSceneLoad.
/// Keep a tiny persistent bootstrap that attaches the core-fix runtimes as soon as
/// their real objects appear. This avoids requiring any scene or one-click installer
/// rerun after pulling the update.
/// </summary>
public sealed class OpenWorldCoreFixBootstrap : MonoBehaviour
{
    private const string ObjectName = "__OpenWorldCoreFixBootstrap_v2";
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Accept either bootstrap name so a domain-reload edge case cannot create
        // two scanners in the same play session.
        if (FindFirstObjectByType<OpenWorldCoreFixBootstrap>() != null) return;
        GameObject go = new GameObject(ObjectName);
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<OpenWorldCoreFixBootstrap>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .20f;

        foreach (ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
        {
            if (hud == null) continue;
            if (hud.GetComponent<IslandBiomeIndexPatchRuntime>() == null)
                hud.gameObject.AddComponent<IslandBiomeIndexPatchRuntime>();
            if (hud.GetComponent<AreaEntryBannerRuntime>() == null)
                hud.gameObject.AddComponent<AreaEntryBannerRuntime>();
            if (hud.GetComponent<TravelAndIslandMenuRuntime>() == null)
                hud.gameObject.AddComponent<TravelAndIslandMenuRuntime>();
        }

        foreach (FishingSystem fishing in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if (fishing != null && fishing.GetComponent<FishingBurstDamageRuntime>() == null)
                fishing.gameObject.AddComponent<FishingBurstDamageRuntime>();
        }
    }
}
