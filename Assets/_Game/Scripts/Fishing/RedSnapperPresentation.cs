using UnityEngine;

public class RedSnapperPresentation : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [Header("Authored Swim")]
    [SerializeField]
    private float bodyLengthsPerCycle = 0.92f;

    [SerializeField]
    private float minimumCruiseSpeed = 0.36f;

    [SerializeField]
    private float maximumCruiseSpeed = 0.56f;

    private bool held;

    private Transform headBone;
    private Transform frontBodyBone;
    private Transform midBodyBone;
    private Transform rearBodyBone;
    private Transform tailBone;

    private Vector3 headRestLocalPosition;
    private Quaternion headRestLocalRotation =
        Quaternion.identity;

    private Vector3 frontRestLocalPosition;
    private Quaternion frontRestLocalRotation =
        Quaternion.identity;

    private Vector3 midRestLocalPosition;
    private Quaternion midRestLocalRotation =
        Quaternion.identity;

    private Vector3 rearRestLocalPosition;
    private Quaternion rearRestLocalRotation =
        Quaternion.identity;

    private Vector3 tailRestLocalPosition;
    private Quaternion tailRestLocalRotation =
        Quaternion.identity;

    private bool restPoseCaptured;

    private Vector3 trail25 =
        Vector3.forward;

    private Vector3 trail50 =
        Vector3.forward;

    private Vector3 trail75 =
        Vector3.forward;

    private Vector3 trail100 =
        Vector3.forward;

    private Vector3 headForward =
        Vector3.forward;

    private bool hasTrail;
    private float animatorSpeedCurrent = 1f;

    // 0 = untouched authored swim, 1 = train-track turn override.
    private float trackBlend;
    private float trackBlendVelocity;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;

        ResolveBones();
        CaptureRestPose();
        ApplyState();
    }

    public void SetHeld(bool value)
    {
        held = value;
        ApplyState();
    }

    public void SetAquariumLocomotion(
        float worldSpeed)
    {
    }

    public void SetAquariumTurn(
        float normalizedTurn)
    {
        // Compatibility hook only.
        // Actual turn shape comes from the recorded head track.
    }

    public void SetAquariumTrail(
        Vector3 currentHeadForward,
        Vector3 forward25,
        Vector3 forward50,
        Vector3 forward75,
        Vector3 forward100)
    {
        if (held)
            return;

        headForward =
            SafeDirection(
                currentHeadForward,
                transform.forward
            );

        trail25 =
            SafeDirection(
                forward25,
                headForward
            );

        trail50 =
            SafeDirection(
                forward50,
                trail25
            );

        trail75 =
            SafeDirection(
                forward75,
                trail50
            );

        trail100 =
            SafeDirection(
                forward100,
                trail75
            );

        hasTrail = true;
    }

    public bool TryGetHeadPosition(
        Transform referenceSpace,
        out Vector3 position)
    {
        ResolveBones();
        CaptureRestPose();

        if (headBone == null)
        {
            position = Vector3.zero;
            return false;
        }

        if (referenceSpace != null)
        {
            position =
                referenceSpace
                    .InverseTransformPoint(
                        headBone.position
                    );
        }
        else
        {
            position =
                headBone.position;
        }

        return true;
    }

    public bool TryGetHeadPositionWorld(
        out Vector3 position)
    {
        return
            TryGetHeadPosition(
                null,
                out position
            );
    }

    public float GetBodyLengthWorld()
    {
        ResolveBones();

        if (headBone == null ||
            tailBone == null)
        {
            return 0.92f;
        }

        return
            Mathf.Clamp(
                Vector3.Distance(
                    headBone.position,
                    tailBone.position
                ),
                0.30f,
                1.60f
            );
    }

    public float GetRecommendedCruiseSpeed()
    {
        ResolveAnimator();

        float clipLength =
            GetSwimClipLength();

        const float normalizedBodyLength =
            0.92f;

        float desired =
            normalizedBodyLength *
            bodyLengthsPerCycle /
            Mathf.Max(
                0.10f,
                clipLength
            );

        return
            Mathf.Clamp(
                desired,
                minimumCruiseSpeed,
                maximumCruiseSpeed
            );
    }

    private void Awake()
    {
        ResolveAnimator();
        ResolveBones();
        CaptureRestPose();
        ApplyState();
    }

    private void OnEnable()
    {
        ResolveAnimator();
        ResolveBones();
        CaptureRestPose();
        ApplyState();
    }

    private void LateUpdate()
    {
        if (held)
            return;

        ResolveBones();
        CaptureRestPose();

        if (!hasTrail)
            return;

        float trailAngle =
            Mathf.Max(
                Vector3.Angle(
                    headForward,
                    trail50
                ),
                Vector3.Angle(
                    headForward,
                    trail100
                )
            );

        // Straight swimming is the authored animation, untouched.
        // Train-track control only fades in once a real curve develops.
        float targetTrackBlend =
            Mathf.InverseLerp(
                6f,
                24f,
                trailAngle
            );

        targetTrackBlend =
            Mathf.SmoothStep(
                0f,
                1f,
                targetTrackBlend
            );

        trackBlend =
            Mathf.SmoothDamp(
                trackBlend,
                targetTrackBlend,
                ref trackBlendVelocity,
                targetTrackBlend >
                    trackBlend
                    ? 0.10f
                    : 0.16f,
                Mathf.Infinity,
                Time.deltaTime
            );

        // When essentially straight, do not touch ANY animated bones.
        // Let the supplied Red Snapper clip play exactly as authored.
        if (trackBlend < 0.015f)
        {
            animatorSpeedCurrent =
                Mathf.MoveTowards(
                    animatorSpeedCurrent,
                    1f,
                    Time.deltaTime * 5f
                );

            if (animator != null)
            {
                animator.speed =
                    animatorSpeedCurrent;
            }

            return;
        }

        // As a turn develops, progressively reduce the free-running authored
        // wag so it cannot throw the tail against the actual head trail.
        float targetAnimatorSpeed =
            Mathf.Lerp(
                1f,
                0.18f,
                trackBlend
            );

        animatorSpeedCurrent =
            Mathf.MoveTowards(
                animatorSpeedCurrent,
                targetAnimatorSpeed,
                Time.deltaTime * 4f
            );

        if (animator != null)
        {
            animator.speed =
                animatorSpeedCurrent;
        }

        // The head/front only get stabilized while the turn system is active.
        // Straight-line swimming above remains entirely authored.
        if (headBone != null)
        {
            headBone.localPosition =
                Vector3.Lerp(
                    headBone.localPosition,
                    headRestLocalPosition,
                    trackBlend
                );

            headBone.localRotation =
                Quaternion.Slerp(
                    headBone.localRotation,
                    headRestLocalRotation,
                    trackBlend
                );
        }

        float authoredWeight =
            Mathf.Lerp(
                1f,
                0.08f,
                trackBlend
            );

        // Put the chain lengths back at their authored rest distances, then
        // orient each cart toward the older tangent of the same head track.
        PrepareBone(
            frontBodyBone,
            frontRestLocalPosition,
            frontRestLocalRotation,
            authoredWeight
        );

        PrepareBone(
            midBodyBone,
            midRestLocalPosition,
            midRestLocalRotation,
            authoredWeight
        );

        PrepareBone(
            rearBodyBone,
            rearRestLocalPosition,
            rearRestLocalRotation,
            authoredWeight
        );

        float tailAuthoredWeight =
            Mathf.Lerp(
                1f,
                0f,
                trackBlend
            );

        PrepareBone(
            tailBone,
            tailRestLocalPosition,
            tailRestLocalRotation,
            tailAuthoredWeight
        );

        // Bone.001 controls where Bone.002 goes, Bone.002 controls Bone.003,
        // and Bone.003 controls Bone.004. This is literally a train of carts.
        AlignSegmentToTrack(
            frontBodyBone,
            midBodyBone,
            trail25,
            trackBlend
        );

        AlignSegmentToTrack(
            midBodyBone,
            rearBodyBone,
            trail50,
            trackBlend
        );

        AlignSegmentToTrack(
            rearBodyBone,
            tailBone,
            trail75,
            trackBlend
        );

        if (tailBone != null)
        {
            Vector3 up =
                transform.up;

            Vector3 rearForward =
                Vector3.ProjectOnPlane(
                    SafeDirection(
                        trail75,
                        headForward
                    ),
                    up
                );

            Vector3 tailForward =
                Vector3.ProjectOnPlane(
                    SafeDirection(
                        trail100,
                        trail75
                    ),
                    up
                );

            if (rearForward.sqrMagnitude >
                    0.0001f &&
                tailForward.sqrMagnitude >
                    0.0001f)
            {
                float tailYaw =
                    Vector3.SignedAngle(
                        rearForward.normalized,
                        tailForward.normalized,
                        up
                    );

                tailBone.rotation =
                    Quaternion.AngleAxis(
                        tailYaw *
                        trackBlend,
                        up
                    ) *
                    tailBone.rotation;
            }
        }
    }

    private static void PrepareBone(
        Transform bone,
        Vector3 restPosition,
        Quaternion restRotation,
        float authoredWeight)
    {
        if (bone == null)
            return;

        Vector3 authoredPosition =
            bone.localPosition;

        Quaternion authoredRotation =
            bone.localRotation;

        bone.localPosition =
            Vector3.Lerp(
                restPosition,
                authoredPosition,
                authoredWeight
            );

        bone.localRotation =
            Quaternion.Slerp(
                restRotation,
                authoredRotation,
                authoredWeight
            );
    }

    private void AlignSegmentToTrack(
        Transform bone,
        Transform child,
        Vector3 desiredForwardWorld,
        float blend)
    {
        if (bone == null ||
            child == null)
        {
            return;
        }

        Vector3 up =
            transform.up;

        Vector3 currentTailDirection =
            child.position -
            bone.position;

        Vector3 desiredTailDirection =
            -SafeDirection(
                desiredForwardWorld,
                currentTailDirection
            );

        Vector3 currentFlat =
            Vector3.ProjectOnPlane(
                currentTailDirection,
                up
            );

        Vector3 desiredFlat =
            Vector3.ProjectOnPlane(
                desiredTailDirection,
                up
            );

        if (currentFlat.sqrMagnitude <
                0.0001f ||
            desiredFlat.sqrMagnitude <
                0.0001f)
        {
            return;
        }

        // Signed yaw makes CW and CCW exact mirrors. The previous full 3D
        // FromToRotation could choose a different axis on the mirrored turn.
        float yaw =
            Vector3.SignedAngle(
                currentFlat.normalized,
                desiredFlat.normalized,
                up
            );

        bone.rotation =
            Quaternion.AngleAxis(
                yaw *
                Mathf.Clamp01(blend),
                up
            ) *
            bone.rotation;
    }

    private static Vector3 SafeDirection(
        Vector3 value,
        Vector3 fallback)
    {
        if (value.sqrMagnitude <
            0.000001f)
        {
            value = fallback;
        }

        if (value.sqrMagnitude <
            0.000001f)
        {
            value = Vector3.forward;
        }

        return value.normalized;
    }

    private void ResolveAnimator()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(
                    true
                );
        }
    }

    private void ResolveBones()
    {
        if (headBone != null &&
            frontBodyBone != null)
        {
            return;
        }

        Transform[] bones =
            GetComponentsInChildren<Transform>(
                true
            );

        headBone =
            FindBone(
                bones,
                "Bone"
            );

        // Both Chrono and Crimson sharks have a right-suffixed spine.
        // Crimson also contains separate unsuffixed fin bones, so resolve
        // the ENTIRE spine consistently rather than mixing both chains.
        bool rightSpine =
            FindBone(bones, "Bone.004") == null &&
            FindBone(bones, "Bone.004.R") != null;

        string suffix = rightSpine ? ".R" : string.Empty;

        frontBodyBone =
            FindBone(
                bones,
                "Bone.001" + suffix
            );

        midBodyBone =
            FindBone(
                bones,
                "Bone.002" + suffix
            );

        rearBodyBone =
            FindBone(
                bones,
                "Bone.003" + suffix
            );

        tailBone =
            FindBone(
                bones,
                "Bone.004" + suffix
            );

        if (headBone == null)
        {
            headBone =
                FindBone(
                    bones,
                    "Bone.001"
                );

            frontBodyBone =
                FindBone(
                    bones,
                    "Bone.002"
                );

            midBodyBone =
                FindBone(
                    bones,
                    "Bone.003"
                );

            rearBodyBone =
                FindBone(
                    bones,
                    "Bone.004"
                );

            tailBone = null;
        }
    }

    private void CaptureRestPose()
    {
        if (restPoseCaptured ||
            headBone == null ||
            frontBodyBone == null)
        {
            return;
        }

        headRestLocalPosition =
            headBone.localPosition;

        headRestLocalRotation =
            headBone.localRotation;

        frontRestLocalPosition =
            frontBodyBone.localPosition;

        frontRestLocalRotation =
            frontBodyBone.localRotation;

        if (midBodyBone != null)
        {
            midRestLocalPosition =
                midBodyBone.localPosition;

            midRestLocalRotation =
                midBodyBone.localRotation;
        }

        if (rearBodyBone != null)
        {
            rearRestLocalPosition =
                rearBodyBone.localPosition;

            rearRestLocalRotation =
                rearBodyBone.localRotation;
        }

        if (tailBone != null)
        {
            tailRestLocalPosition =
                tailBone.localPosition;

            tailRestLocalRotation =
                tailBone.localRotation;
        }

        restPoseCaptured = true;
    }

    private static Transform FindBone(
        Transform[] bones,
        string boneName)
    {
        if (bones == null)
            return null;

        for (int i = 0;
             i < bones.Length;
             i++)
        {
            if (bones[i] != null &&
                bones[i].name ==
                boneName)
            {
                return bones[i];
            }
        }

        return null;
    }

    private float GetSwimClipLength()
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return 1f;
        }

        AnimationClip[] clips =
            animator
                .runtimeAnimatorController
                .animationClips;

        if (clips == null ||
            clips.Length == 0 ||
            clips[0] == null)
        {
            return 1f;
        }

        return
            Mathf.Max(
                0.10f,
                clips[0].length
            );
    }

    private void ApplyState()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;

        animatorSpeedCurrent =
            held
                ? 1.08f
                : 1f;

        trackBlend = 0f;
        trackBlendVelocity = 0f;

        animator.speed =
            animatorSpeedCurrent;
    }
}
