using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Three full authored swim cycles, then a smooth return to a hanging pose.
[DefaultExecutionOrder(900)]
public sealed class CaughtFishFlop : MonoBehaviour
{
    public const string HeldStruggleStateName = "HeldStruggle";
    public System.Action PoseUpdated;
    public float SwingBoost { get; private set; }
    private Transform[] bones;
    private Vector3[] restPositions, restScales;
    private Quaternion[] restRotations;
    private Animator animator;
    private AnimationClip swim;
    private PlayableGraph graph;
    private AnimationClipPlayable clipPlayable;
    private bool animatorWasEnabled;
    private float previousSpeed;
    private AnimatorCullingMode previousCulling;
    private float nextBurst, burstStart = -100f;
    private const float Cycle = 0.22f, Duration = Cycle * 3, Settle = 0.22f;
    private bool hangingDisplay;
    private Vector3 hangingPoint, attachment;
    private Quaternion hangingRotation;
    private float swayPhase;
    private bool playerHeld;
    private int heldSwimState;
    private float heldRestPhase;
    private AnimationClip authoredHeldClip;
    private float heldAnimationStart;

    // The market keeps its existing playback. Player-held fish use the same
    // imported controller that swims in the aquarium, with explicit sampling.
    public void ConfigurePlayerHeld()
    {
        Initialize();
        playerHeld = true;
        if (graph.IsValid()) graph.Destroy();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("Held fish has no authored swim Animator: " + name, this);
            return;
        }
        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        animator.speed = 0f;
        // Rebind after releasing the custom graph so the controller owns the rig.
        Vector3 position = transform.localPosition, scale = transform.localScale;
        Quaternion rotation = transform.localRotation;
        animator.Rebind();
        animator.Update(0f);
        int swimState = Animator.StringToHash("Base Layer.Swim");
        heldSwimState = animator.HasState(0, swimState)
            ? swimState : animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        // Optional dedicated held animation. Swimming remains the controller default
        // in ambient fish, fights, aquariums, UI previews and market displays.
        authoredHeldClip = null;
        int struggleState = Animator.StringToHash("Base Layer." + HeldStruggleStateName);
        if (animator.HasState(0, struggleState))
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == HeldStruggleStateName && clip.length > 0f)
                { authoredHeldClip = clip; break; }
            if (authoredHeldClip != null) heldSwimState = struggleState;
        }
        heldAnimationStart = Time.time;
        heldRestPhase = swim != null && swim.length > 0f
            ? Mathf.Clamp01(5f / Mathf.Max(1f, swim.frameRate) / swim.length) : 0f;
        animator.Play(heldSwimState, 0, authoredHeldClip != null ? 0f : heldRestPhase);
        animator.Update(0f);
        transform.localPosition = position;
        transform.localRotation = rotation;
        transform.localScale = scale;
        // Capture the imported resting pose, not a partly evaluated old graph.
        for (int i = 1; i < bones.Length; i++)
        {
            restPositions[i] = bones[i].localPosition;
            restRotations[i] = bones[i].localRotation;
            restScales[i] = bones[i].localScale;
        }
    }

    // Shared presentation motion for first-person and observer-held catches.
    public static float SmoothHeldMovement(float current, float speed, float dt)
    {
        return Mathf.Lerp(current, Mathf.Clamp01(speed / 5f), 1f - Mathf.Exp(-8f * dt));
    }

    public static void EvaluateHeldSway(float time, float movement, float struggle,
        out Vector3 offset, out Quaternion rotation)
    {
        float gain = .10f + movement * .50f + struggle * .35f;
        offset = new Vector3(
            Mathf.Sin(time * .82f) * .085f + Mathf.Sin(time * 1.63f + .8f) * .022f,
            -Mathf.Abs(Mathf.Sin(time * .82f)) * .010f,
            Mathf.Sin(time * .64f + 1.1f) * .028f) * gain;
        rotation = Quaternion.Euler(
            Mathf.Sin(time * 1.65f) * 10f * gain,
            Mathf.Sin(time * 1.12f) * 8f * gain,
            Mathf.Sin(time * 2.05f) * 18f * gain);
    }

    public void ConfigureHanging(Vector3 point, Vector3 localAttachment)
    {
        hangingDisplay = true;
        hangingPoint = point;
        attachment = localAttachment;
        hangingRotation = transform.rotation;
        swayPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void Initialize()
    {
        if (bones != null) return;
        bones = GetComponentsInChildren<Transform>(true);
        restPositions = new Vector3[bones.Length];
        restRotations = new Quaternion[bones.Length];
        restScales = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            restPositions[i] = bones[i].localPosition;
            restRotations[i] = bones[i].localRotation;
            restScales[i] = bones[i].localScale;
        }
        animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || clip.legacy) continue;
                if (swim == null) swim = clip;
                if (clip.name.IndexOf("swim", System.StringComparison.OrdinalIgnoreCase) >= 0)
                { swim = clip; break; }
            }
            if (swim != null)
            {
                animatorWasEnabled = animator.enabled;
                previousSpeed = animator.speed;
                previousCulling = animator.cullingMode;
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Caught fish authored swim");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                clipPlayable = AnimationClipPlayable.Create(graph, swim);
                var output = AnimationPlayableOutput.Create(graph, "Swim", animator);
                output.SetSourcePlayable(clipPlayable);
                graph.Play();
            }
        }
        nextBurst = Time.time + Random.Range(5f, 10f);
    }

    private void Awake() => Initialize();
    private void LateUpdate()
    {
        if (playerHeld && authoredHeldClip != null && animator != null)
        {
            // Play the entire struggle at its authored speed, without the generic
            // swim-burst/rest override flattening the ray's fin animation.
            Vector3 heldPosition = transform.localPosition, heldScale = transform.localScale;
            Quaternion heldRotation = transform.localRotation;
            animator.speed = 0f;
            float phase = Mathf.Repeat((Time.time - heldAnimationStart) / authoredHeldClip.length, 1f);
            animator.Play(heldSwimState, 0, phase);
            animator.Update(0f);
            transform.localPosition = heldPosition;
            transform.localRotation = heldRotation;
            transform.localScale = heldScale;
            PoseUpdated?.Invoke(); // Keep the string pinned after the struggling pose.
            return;
        }
        // Do not compress an entire long authored motion into 0.22 seconds.
        // Held catches play at up to 2.5x authored speed, with enough screen
        // time to see the full body bend. The market retains its own timing.
        float cycleDuration = playerHeld
            ? Mathf.Max(0.45f, swim != null ? swim.length / 2.5f : Cycle) : Cycle;
        float burstDuration = cycleDuration * 3f;
        if (Time.time >= nextBurst)
        {
            burstStart = Time.time;
            nextBurst = burstStart + burstDuration + Settle + Random.Range(5f, 10f);
        }
        float age = Time.time - burstStart;
        bool active = age >= 0f && age < burstDuration;
        bool settling = age >= burstDuration && age < burstDuration + Settle;
        SwingBoost = Mathf.MoveTowards(SwingBoost, active ? 1f : 0f,
            Time.deltaTime * (active ? 9f : 0.65f));
        Vector3 position = transform.localPosition, scale = transform.localScale;
        Quaternion rotation = transform.localRotation;

        if (playerHeld && animator != null && heldSwimState != 0)
        {
            animator.speed = 0f;
            float phase = active ? (age % cycleDuration) / cycleDuration : settling ? 0.99999f : heldRestPhase;
            animator.Play(heldSwimState, 0, phase);
            animator.Update(0f);
        }
        else if (graph.IsValid())
        {
            // Evaluate through the actual Animator binding. Each attempt plays
            // the ENTIRE imported clip, without suppressing each wag with an envelope.
            float phase = active ? (age % Cycle) / Cycle : settling ? 0.99999f : 0f;
            clipPlayable.SetTime(phase * swim.length);
            graph.Evaluate(0f);
        }
        float blend = active ? 1f : settling ? 1f - Mathf.SmoothStep(0f, 1f, (age - burstDuration) / Settle) : 0f;
        for (int i = 1; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            // During each burst leave ALL authored curves intact, including
            // scale curves. Do not rewrite the animated rig every frame.
            if (playerHeld && active) continue;
            bones[i].localPosition = Vector3.Lerp(restPositions[i], bones[i].localPosition, blend);
            bones[i].localRotation = Quaternion.Slerp(restRotations[i], bones[i].localRotation, blend);
            // Imported scale curves must not undo the model's metre calibration.
            bones[i].localScale = playerHeld
                ? Vector3.Lerp(restScales[i], bones[i].localScale, blend)
                : restScales[i];
        }
        transform.localPosition = position;
        transform.localRotation = rotation;
        transform.localScale = scale;
        if (hangingDisplay)
        {
            float t = Time.time * 2.5f + swayPhase;
            float gain = 1f + SwingBoost * 2f;
            transform.rotation = hangingRotation * Quaternion.Euler(
                Mathf.Sin(t * 0.8f) * 3f * gain, 0f, Mathf.Sin(t) * 5f * gain);
            transform.position += hangingPoint - transform.TransformPoint(attachment);
        }
        PoseUpdated?.Invoke(); // Re-pin the mouth AFTER the authored bones move.
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        if (animator != null)
        {
            if (authoredHeldClip != null && animator.runtimeAnimatorController != null)
            {
                int swimState = Animator.StringToHash("Base Layer.Swim");
                if (animator.HasState(0, swimState)) animator.Play(swimState, 0, 0f);
            }
            animator.enabled = animatorWasEnabled;
            animator.speed = previousSpeed;
            animator.cullingMode = previousCulling;
        }
    }
}
