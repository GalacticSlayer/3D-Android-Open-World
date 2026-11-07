using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the imported FBX rotor continuously, independent of framerate culling.
/// All stationary parts keep their authored FBX pose, and each placed turbine's
/// position, rotation and scale are restored after sampling the animation.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class WindgrovePlacedTurbineAnimation : MonoBehaviour
{
    private struct RestPose
    {
        public Transform Target;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
    }

    // The original FBX clip was unreliable on direct imported scene instances.
    // Spin the original three-bladed rotor transform itself, instead of relying
    // on legacy FBX animation import, clip names, or Animator culling.
    [SerializeField, Range(1f, 180f)]
    private float degreesPerSecond = 86.4f; // original 100-frame/24fps full turn

    private Transform rotor;
    private Quaternion rotorInitialRotation;
    private Vector3 spinAxisInParent;
    private RestPose[] stationaryParts;
    private Vector3 rootPosition, rootScale;
    private Quaternion rootRotation;

    private static Transform NamedPart(Transform parent, params string[] patterns)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            foreach (string pattern in patterns)
                if (t.name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    t.GetComponentInChildren<Renderer>(true) != null)
                    return t;
        return null;
    }

    private void FixAnemometerMast()
    {
        // Correct the original FBX's floating mast *in world units* before
        // caching its rest pose. This works for the user's 0.058... scale and
        // any future user-authored uniform FBX scale.
        Transform mast = NamedPart(transform, "Anemometer mast", "Anemometer");
        Transform housing = NamedPart(transform, "Top service cap", "Nacelle roof panel",
            "Nacelle housing", "Housing", "Generator housing");
        if (mast == null || housing == null || housing == mast ||
            housing.IsChildOf(mast)) return;
        Renderer mastRenderer = mast.GetComponentInChildren<Renderer>(true);
        Renderer housingRenderer = housing.GetComponentInChildren<Renderer>(true);
        if (mastRenderer == null || housingRenderer == null) return;

        Bounds mastBounds = mastRenderer.bounds;
        Bounds housingBounds = housingRenderer.bounds;
        if (housingBounds.size.y < .01f || mastBounds.size.y < .01f) return;

        // A mast's foot belongs on the nacelle roof, not hanging above it.
        // Retain its horizontal FBX placement when it sits over the housing.
        Vector3 mastCenter = mastBounds.center, housingCenter = housingBounds.center;
        float maxPlanarOffset = Mathf.Max(housingBounds.size.x, housingBounds.size.z) * .7f;
        float planarOffset = Vector2.Distance(new Vector2(mastCenter.x, mastCenter.z),
            new Vector2(housingCenter.x, housingCenter.z));
        Vector3 correction = Vector3.up * (housingBounds.max.y - mastBounds.min.y);
        if (planarOffset > maxPlanarOffset)
        {
            correction.x = housingCenter.x - mastCenter.x;
            correction.z = housingCenter.z - mastCenter.z;
        }

        if (correction.sqrMagnitude > .000001f)
            mast.position += correction;
    }

    private void Awake()
    {
        rootPosition = transform.localPosition;
        rootRotation = transform.localRotation;
        rootScale = transform.localScale;

        // The source FBX exports the rotor take onto static parts too.
        // Hold all parts except 'Rotor blade 2' fixed to avoid swaying towers
        // and a floating or drifting anemometer during continuous playback.
        FixAnemometerMast();
        rotor = NamedPart(transform, "Rotor blade 2");
        if (rotor == null)
        {
            Debug.LogError("Windgrove: FBX rotor 'Rotor blade 2' not found.", this);
            enabled = false;
            return;
        }

        rotorInitialRotation = rotor.localRotation;
        Transform nacelle = NamedPart(transform, "Nacelle housing", "Nacelle roof panel");
        // The rotor pivot is the hub in the original FBX. The vector from
        // nacelle to rotor points along the rotor shaft in *world* space;
        // convert to rotor-parent space so artist yaw/scale remain untouched.
        Vector3 shaft = nacelle != null ? rotor.position - nacelle.position : transform.forward;
        if (shaft.sqrMagnitude < .0001f) shaft = transform.forward;
        spinAxisInParent = rotor.parent != null
            ? rotor.parent.InverseTransformDirection(shaft.normalized).normalized
            : shaft.normalized;

        var fixedParts = new List<RestPose>();
        foreach (Transform part in GetComponentsInChildren<Transform>(true))
        {
            if (part == transform || part == rotor || part.IsChildOf(rotor)) continue;
            fixedParts.Add(new RestPose
            {
                Target = part,
                Position = part.localPosition,
                Rotation = part.localRotation,
                Scale = part.localScale
            });
        }
        stationaryParts = fixedParts.ToArray();

        foreach (Animation animation in GetComponentsInChildren<Animation>(true))
            animation.enabled = false;
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
    }

    private void LateUpdate()
    {
        if (rotor == null) return;

        // Absolute unscaled clock: no skipped rotation due to off-screen
        // Animator culling, timeScale changes, re-enabling, or FPS differences.
        float angle = Mathf.Repeat(Time.unscaledTime * degreesPerSecond, 360f);
        rotor.localRotation = Quaternion.AngleAxis(angle, spinAxisInParent) *
                              rotorInitialRotation;

        transform.localPosition = rootPosition;
        transform.localRotation = rootRotation;
        transform.localScale = rootScale;

        foreach (RestPose rest in stationaryParts)
        {
            if (rest.Target == null) continue;
            rest.Target.localPosition = rest.Position;
            rest.Target.localRotation = rest.Rotation;
            rest.Target.localScale = rest.Scale;
        }
    }
}

/// <summary>
/// Attach animation only to direct FBX instances, not generated scenery.
/// No scene edits or replacement geometry; installer also covers additive loads.
/// </summary>
internal static class WindgrovePlacedTurbineInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            InstallInScene(SceneManager.GetSceneAt(i));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInScene(scene);
    }

    private static void InstallInScene(Scene scene)
    {
        if (!scene.isLoaded || scene.name != "PrototypeWorld") return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "WindTurbine" &&
                !(root.name.StartsWith("WindTurbine (", StringComparison.Ordinal) &&
                  root.name.EndsWith(")", StringComparison.Ordinal))) continue;
            if (root.GetComponent<WindgroveTurbine>() != null ||
                root.GetComponent<WindgrovePlacedTurbineAnimation>() != null) continue;
            root.AddComponent<WindgrovePlacedTurbineAnimation>();
        }
    }
}
