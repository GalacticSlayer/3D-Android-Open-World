using UnityEngine;

/// <summary>
/// The scene object's transform belongs to the artist. Only the imported child
/// is animated; no runtime placement, terrain snapping or root motion is applied.
/// </summary>
[DisallowMultipleComponent]
public sealed class WindgroveTurbine : MonoBehaviour
{
    public const string ModelResource = "Environment/Windgrove/WindTurbine";
    public const float InitialHeight = 1.8f * 500f;
    [SerializeField, Range(0f, 1f)] private float phase;
    [SerializeField, HideInInspector] private GameObject animatedModel;
    [SerializeField, HideInInspector] private AnimationClip rotationClip;

    public bool Ready => animatedModel != null && rotationClip != null && rotationClip.length > 0f;

    private void Awake()
    {
        EnsureVisual();
        if (!Ready) Debug.LogError("Windgrove turbine model or rotation clip is missing.", this);
    }

    private void OnEnable() { Sample(); }
    private void Update() { Sample(); }

    private void Sample()
    {
        if (!Ready) return;
        // Explicit sampling is independent of renderer visibility, Animator
        // culling and timeScale. Re-enabling resumes the ongoing loop.
        float time = Mathf.Repeat(Time.unscaledTime + phase * rotationClip.length, rotationClip.length);
        rotationClip.SampleAnimation(animatedModel, time);
    }

    // Used once by the editor installer, with a runtime fallback for a scene
    // loaded before its first editor bake. Never modifies this object's transform.
    public bool EnsureVisual()
    {
        if (Ready) return false;
        AnimationClip longest = null;
        foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>(ModelResource))
        {
            if (clip.name.StartsWith("__preview__")) continue;
            if (longest == null || clip.length > longest.length) longest = clip;
        }
        GameObject source = Resources.Load<GameObject>(ModelResource);
        if (source == null || longest == null || longest.length <= 0f) return false;
        rotationClip = longest;
        if (animatedModel != null) return true;

        var scaleRoot = new GameObject("Uniform Model Scale");
        scaleRoot.transform.SetParent(transform, false);
        animatedModel = Instantiate(source, scaleRoot.transform, false);
        animatedModel.name = "Animated Turbine Model";
        foreach (Animation animation in animatedModel.GetComponentsInChildren<Animation>(true))
            animation.enabled = false;
        foreach (Animator animator in animatedModel.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
        rotationClip.SampleAnimation(animatedModel, 0f);

        bool first = true;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in animatedModel.GetComponentsInChildren<Renderer>(true))
        {
            Bounds local = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f,
                                (i & 2) == 0 ? -1f : 1f,
                                (i & 4) == 0 ? -1f : 1f));
                Vector3 point = transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
            // These giant animated structures should not render enormous shadows.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (!first && bounds.size.y > .001f)
        {
            float scale = InitialHeight / bounds.size.y;
            scaleRoot.transform.localScale = Vector3.one * scale;
            scaleRoot.transform.localPosition = Vector3.up * (-bounds.min.y * scale);
        }
        return true;
    }
}
