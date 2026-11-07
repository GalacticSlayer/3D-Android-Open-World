using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the original FBX animation on an artist-placed WindTurbine instance.
/// It never creates or replaces geometry, rescales the model, or changes the
/// scene-authored turbine transform.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class WindgrovePlacedTurbineAnimation : MonoBehaviour
{
    private static AnimationClip sharedClip;
    private static bool clipSearched;

    private Vector3 authoredLocalPosition;
    private Quaternion authoredLocalRotation;
    private Vector3 authoredLocalScale;
    private AnimationClip clip;

    private static AnimationClip FindImportedClip()
    {
        if (clipSearched) return sharedClip;
        clipSearched = true;

        // Use the animation supplied inside the original FBX; do not synthesize
        // a blade mesh or alter the asset's transform hierarchy.
        foreach (AnimationClip candidate in Resources.LoadAll<AnimationClip>(WindgroveTurbine.ModelResource))
        {
            if (candidate == null || candidate.length <= 0f ||
                candidate.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            if (sharedClip == null || candidate.length > sharedClip.length)
                sharedClip = candidate;
        }

        if (sharedClip == null)
        {
            GameObject model = Resources.Load<GameObject>(WindgroveTurbine.ModelResource);
            Animation importedAnimation = model != null ? model.GetComponent<Animation>() : null;
            if (importedAnimation != null && importedAnimation.clip != null &&
                importedAnimation.clip.length > 0f)
                sharedClip = importedAnimation.clip;
        }

        if (sharedClip == null)
            Debug.LogError("Windgrove: The WindTurbine FBX has no imported animation. Check its Animation import settings.");
        return sharedClip;
    }

    private void Awake()
    {
        // These are the exact local values saved by the user in PrototypeWorld.
        authoredLocalPosition = transform.localPosition;
        authoredLocalRotation = transform.localRotation;
        authoredLocalScale = transform.localScale;

        clip = FindImportedClip();
        if (clip == null)
        {
            enabled = false;
            return;
        }

        // No competing animation component may overwrite our explicitly
        // sampled FBX pose. This only affects this already-placed turbine.
        foreach (Animation animation in GetComponentsInChildren<Animation>(true))
            animation.enabled = false;
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
    }

    private void LateUpdate()
    {
        // LateUpdate makes the imported rotor motion visible without relying
        // on culling-sensitive Animation/Animator playback.
        float playbackTime = Mathf.Repeat(Time.unscaledTime, clip.length);
        clip.SampleAnimation(gameObject, playbackTime);

        // Some FBX takes contain a root transform track. Restore the artist's
        // placement even in that case; only the imported child parts may move.
        transform.localPosition = authoredLocalPosition;
        transform.localRotation = authoredLocalRotation;
        transform.localScale = authoredLocalScale;
    }
}

/// <summary>
/// Installs animation at runtime on directly dragged WindTurbine FBX prefabs.
/// Nothing is serialized back into the scene, so manual placement is untouched.
/// Also handles a later scene load, without searching or changing unrelated assets.
/// </summary>
internal static class WindgrovePlacedTurbineInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        for (int index = 0; index < SceneManager.sceneCount; index++)
            InstallInScene(SceneManager.GetSceneAt(index));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInScene(scene);
    }

    private static void InstallInScene(Scene scene)
    {
        if (!scene.isLoaded) return;

        int installed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // These are the nine directly placed instances in PrototypeWorld.
            // The old procedurally-generated wrappers use different names.
            if (root.name != "WindTurbine" &&
                !root.name.StartsWith("WindTurbine (", StringComparison.Ordinal))
                continue;
            if (root.GetComponent<WindgroveTurbine>() != null ||
                root.GetComponent<WindgrovePlacedTurbineAnimation>() != null)
                continue;

            root.AddComponent<WindgrovePlacedTurbineAnimation>();
            installed++;
        }

        if (installed > 0)
            Debug.Log("Windgrove: attached original FBX animation to " +
                      installed + " manually placed wind turbines in " + scene.name + ".");
    }
}
