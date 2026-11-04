using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// A visual-only network avatar. The local player still owns movement, collisions and fishing.
[DefaultExecutionOrder(200)]
public sealed class FishingRemotePlayer : MonoBehaviour
{
    private const string AvatarResource = "Multiplayer/FemaleRemoteAvatar";
    private const float WalkingThreshold = 0.12f;
    private const float AvatarScale = 1.32f;
    private const float FeetOffset = -.10f;
    private const float DefaultHeight = 2.05f * AvatarScale;

    public int World { get; private set; }
    public float BodyHeight { get; private set; } = 1.72f * AvatarScale;
    public Vector3 BodyCenter => drivingBlend > .95f && leftHip != null && rightHip != null
        ? (leftHip.position + rightHip.position) * .5f + transform.up * .65f
        : transform.position + Vector3.up * (FeetOffset + BodyHeight * .5f);
    public Transform RodTip => observerRodView != null ? observerRodView.RodTip : null;

    // A read-only visual attachment used for the first-person *rendered* eye.
    // The gameplay camera's saved simulation pose is never moved for aiming/input.
    public bool TryGetMotorboatSeatedHead(out Vector3 worldHead)
    {
        worldHead = Vector3.zero;
        if (drivingBlend < .95f || visualRoot == null) return false;
        if (headBone != null)
        {
            worldHead = headBone.position;
            return true;
        }
        // Compatible fallback for rigs that omit a named Head bone.
        if (leftHip != null && rightHip != null)
        {
            worldHead = (leftHip.position + rightHip.position) * .5f + transform.up * .85f;
            return true;
        }
        return false;
    }
    private FishingMultiplayer localSource;

    public void FollowLocal(FishingMultiplayer source)
    {
        localSource = source;
        initialized = false;
        speed = 0f;
        if (source != null) Receive(source.LocalPose(forLocalPresentation: true));
    }

    private Vector3 target, previous;
    private Quaternion facing;
    private string rideOwner,rideKey;
    private int rideSeat=-1;
    private float rideYaw;
    private Vector3 rideTarget,ridePosition;
    private MultiplayerBoat riddenBoat;
    private bool initialized, swimming, sprinting;
    private float speed, lastPacket, walkClock, idleClock, walkBlend, runClock, runBlend;
    private Transform label;

    private bool fishPosing;
    private float fishPoseClock;
    private AnimationClip smallPoseClip;
    private AnimationClipPlayable smallPosePlayable;
    private PlayableGraph animationGraph;
    private AnimationMixerPlayable animationMixer;
    private AnimationClipPlayable runPlayable, walkPlayable, idlePlayable, castPlayable, reelPlayable, pullPlayable, walkReelPlayable, holdFishPlayable, walkHoldFishPlayable, drivePlayable, passengerPlayable;
    private Transform visualRoot, headBone, leftHip, rightHip, leftUpperArm, leftForearm, rightUpperArm, rightForearm;
    private Transform leftShin, rightShin;
    private Vector3 restingVisualOffset;
    private float drivingBlend, drivingClock;
    private AnimationClip runClip, walkClip, idleClip, castClip, reelClip, pullClip, walkReelClip, holdFishClip, walkHoldFishClip, driveClip, passengerClip;
    private Transform leftHand, rodSocket, rightHand, heldMouth;
    private GameObject heldFish;
    private LineRenderer heldString;
    private Material heldStringMaterial;
    private int heldSpecies = -1;
    private float heldWeight, walkReelClock, holdFishClock, walkReelBlend, holdFishBlend, walkHoldFishClock, walkHoldFishBlend;
    private Vector3 rightGripLocal;
    private Transform rightStringAnchor;
    private CaughtFishFlop heldFishFlop;
    private float heldFishMovementSwing, heldFishSwayPhase;
    private FishingRemoteLine remoteLine;
    private GameObject heldRod;
    private FishingRodView observerRodView;
    private bool observerRodBent;
    private int rodTier = -1, displayedReelTier = -1, seenCastSequence;
    private bool rodEquipped, casting, receivedFishingState;
    private float castClock, castDuration = .72f, castBlend;
    private bool reeling, hasFish, lureInWater;
    private float reelClock, pullClock, reelBlend, pullBlend, lastAuthorReelTime = -1f;

    public void Initialize(string displayName, string identity, bool showName = true)
    {
        float labelHeight = CreateFemaleModel();
        if (!showName) return;

        var text = new GameObject("Name").AddComponent<TextMesh>();
        label = text.transform;
        label.SetParent(transform, false);
        label.localPosition = Vector3.up * labelHeight;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font != null)
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.text = displayName;
        text.anchor = TextAnchor.MiddleCenter;
        text.characterSize = .045f;
        text.fontSize = 48;
        text.color = Color.white;
        text.richText = false;
    }

    private float CreateFemaleModel()
    {
        FishingRemoteAvatarAsset asset = Resources.Load<FishingRemoteAvatarAsset>(AvatarResource);
        if (asset == null || asset.ModelPrefab == null)
        {
            Debug.LogError("Female multiplayer avatar is missing. In the Unity editor, run Tools/Open World/Rebuild Female Multiplayer Avatar before making a build.");
            return DefaultHeight;
        }

        GameObject visual = Instantiate(asset.ModelPrefab, transform, false);
        visual.name = "Female Character Model";
        visualRoot = visual.transform;
        headBone = FishingRemoteAvatarAsset.FindBone(visualRoot, "Head");
        if (headBone == null) headBone = FishingRemoteAvatarAsset.FindBone(visualRoot, "Neck");
        leftHip = FishingRemoteAvatarAsset.FindBone(visualRoot, "Pelvis.L");
        rightHip = FishingRemoteAvatarAsset.FindBone(visualRoot, "Pelvis.R");
        leftShin = FishingRemoteAvatarAsset.FindBone(visualRoot, "Shin.L");
        rightShin = FishingRemoteAvatarAsset.FindBone(visualRoot, "Shin.R");
        leftUpperArm = FishingRemoteAvatarAsset.FindBone(visualRoot, "UpperArm.L");
        leftForearm = FishingRemoteAvatarAsset.FindBone(visualRoot, "Forearm.L");
        rightUpperArm = FishingRemoteAvatarAsset.FindBone(visualRoot, "UpperArm.R");
        rightForearm = FishingRemoteAvatarAsset.FindBone(visualRoot, "Forearm.R");
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // The remote player is only a representation of another client's position.
        // Its FBX must not obstruct local players, boats, casts or triggers.
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visual.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController = null;

        leftHand = string.IsNullOrEmpty(asset.LeftHandPath) ? null : visual.transform.Find(asset.LeftHandPath);
        if (leftHand == null) leftHand = FishingRemoteAvatarAsset.FindBone(visual.transform, "Hand.L");
        if (leftHand == null)
            Debug.LogError("Multiplayer: left hand was not found on the instantiated avatar. Rebuild Female Multiplayer Avatar.");
        rightHand = FishingRemoteAvatarAsset.FindBone(visual.transform, "Hand.R");
        if (rightHand != null)
        {
            Transform index = FishingRemoteAvatarAsset.FindBone(rightHand, "Second1.R");
            Transform pinky = FishingRemoteAvatarAsset.FindBone(rightHand, "Pinky1.R");
            if (index != null && pinky != null)
                rightGripLocal = (rightHand.InverseTransformPoint(index.position) + rightHand.InverseTransformPoint(pinky.position)) * .4f;
        }
        // Follow the animated middle finger rather than a fixed palm midpoint.
        rightStringAnchor = rightHand != null
            ? FishingRemoteAvatarAsset.FindBone(rightHand, "Third1.R") : null;
        if (rightStringAnchor == null) rightStringAnchor = rightHand;
        walkReelClip = asset.WalkReelClip;
        holdFishClip = asset.HoldFishClip;
        smallPoseClip = asset.SmallFishPoseClip;
        walkHoldFishClip = asset.WalkHoldFishClip;
        reelClip = asset.ReelClip;
        pullClip = asset.PullClip;
        if (reelClip == null || pullClip == null)
            Debug.LogError("Multiplayer: reeling/pulling clips are missing. Rebuild the game with both animation FBXs.");
        castClip = asset.CastClip;
        if (castClip == null)
            Debug.LogError("Multiplayer: casting clip is missing from the avatar catalog. Rebuild the game with the casting FBX.");
        driveClip = asset.DriveMotorboatClip;
        passengerClip = asset.PassengerMotorboatClip;
        runClip = asset.RunClip;
        walkClip = asset.WalkClip;
        idleClip = asset.IdleClip != null ? asset.IdleClip : walkClip;

        if (walkClip != null && idleClip != null)
        {
            animationGraph = PlayableGraph.Create("Remote Female Character Walk");
            animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            animationMixer = AnimationMixerPlayable.Create(animationGraph, 12);
            idlePlayable = AnimationClipPlayable.Create(animationGraph, idleClip);
            walkPlayable = AnimationClipPlayable.Create(animationGraph, walkClip);
            animationGraph.Connect(idlePlayable, 0, animationMixer, 0);
            animationGraph.Connect(walkPlayable, 0, animationMixer, 1);
            if (castClip != null)
            {
                castPlayable = AnimationClipPlayable.Create(animationGraph, castClip);
                animationGraph.Connect(castPlayable, 0, animationMixer, 2);
            }
            if (reelClip != null)
            {
                reelPlayable = AnimationClipPlayable.Create(animationGraph, reelClip);
                animationGraph.Connect(reelPlayable, 0, animationMixer, 3);
            }
            if (pullClip != null)
            {
                pullPlayable = AnimationClipPlayable.Create(animationGraph, pullClip);
                animationGraph.Connect(pullPlayable, 0, animationMixer, 4);
            }
            if (walkReelClip != null)
            {
                walkReelPlayable = AnimationClipPlayable.Create(animationGraph, walkReelClip);
                animationGraph.Connect(walkReelPlayable, 0, animationMixer, 5);
            }
            if (holdFishClip != null)
            {
                holdFishPlayable = AnimationClipPlayable.Create(animationGraph, holdFishClip);
                animationGraph.Connect(holdFishPlayable, 0, animationMixer, 6);
            }
            if (walkHoldFishClip != null)
            {
                walkHoldFishPlayable = AnimationClipPlayable.Create(animationGraph, walkHoldFishClip);
                animationGraph.Connect(walkHoldFishPlayable, 0, animationMixer, 7);
            }
            if (runClip != null)
            {
                runPlayable = AnimationClipPlayable.Create(animationGraph, runClip);
                animationGraph.Connect(runPlayable, 0, animationMixer, 8);
            }
            if (driveClip != null)
            {
                drivePlayable = AnimationClipPlayable.Create(animationGraph, driveClip);
                animationGraph.Connect(drivePlayable, 0, animationMixer, 9);
            }
            if (passengerClip != null)
            {
                passengerPlayable = AnimationClipPlayable.Create(animationGraph, passengerClip);
                animationGraph.Connect(passengerPlayable, 0, animationMixer, 10);
            }
            if (smallPoseClip != null)
            {
                smallPosePlayable = AnimationClipPlayable.Create(animationGraph, smallPoseClip);
                animationGraph.Connect(smallPosePlayable, 0, animationMixer, 11);
                smallPosePlayable.SetTime(0f);
            }
            animationMixer.SetInputWeight(11, 0f);
            animationMixer.SetInputWeight(10, 0f);
            animationMixer.SetInputWeight(9, 0f);
            animationMixer.SetInputWeight(8, 0f);
            animationMixer.SetInputWeight(7, 0f);
            animationMixer.SetInputWeight(5, 0f);
            animationMixer.SetInputWeight(6, 0f);
            animationMixer.SetInputWeight(3, 0f);
            animationMixer.SetInputWeight(4, 0f);
            animationMixer.SetInputWeight(2, 0f);
            animationMixer.SetInputWeight(0, 1f);
            animationMixer.SetInputWeight(1, 0f);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph, "Remote Avatar", animator);
            output.SetSourcePlayable(animationMixer);
            animationGraph.Play();
            animationGraph.Evaluate(0f);
        }
        else
        {
            Debug.LogError("Female multiplayer avatar was imported without a usable walking animation.");
        }

        // Respect original model proportions while matching the old avatar's height
        // and keeping its feet at the network player's ground position.
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!hasBounds || bounds.size.y < .001f)
            return DefaultHeight;

        float uniformScale = Mathf.Clamp(1.72f * AvatarScale / bounds.size.y, .01f, 100f);
        visual.transform.localScale *= uniformScale;

        hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        BodyHeight = bounds.size.y;
        visual.transform.position += Vector3.up * (transform.position.y + FeetOffset - bounds.min.y);
        restingVisualOffset = visual.transform.localPosition;
        return Mathf.Clamp(bounds.max.y - bounds.min.y + FeetOffset + .22f, 1.5f, 3f);
    }

    public void Receive(FishingMultiplayer.Pose peer)
    {
        World = peer.world;
        if(label!=null && peer is FishingMultiplayer.Peer namedPeer)
        {
            var nameText=label.GetComponent<TextMesh>();
            if(nameText!=null && nameText.text!=namedPeer.name)nameText.text=namedPeer.name;
        }
        // Local third person already renders the original fishing line and lure.
        if(localSource==null)
        {
            // Water impacts can arrive even if the entire brief line state was
            // missed between snapshots (e.g. quick lure retrieval).
            if(remoteLine==null && (peer.lineKind!=0 || peer.castImpactSequence>0))
                remoteLine=gameObject.AddComponent<FishingRemoteLine>();
            if(remoteLine!=null)remoteLine.Receive(peer);
        }
        bool changedBoat=rideOwner!=peer.rideOwner || rideKey!=peer.rideKey || rideSeat!=peer.rideSeat;
        rideOwner=peer.rideOwner;rideKey=peer.rideKey;rideSeat=peer.rideSeat;rideYaw=peer.rideYaw;
        rideTarget=new Vector3(peer.rideX,peer.rideY,peer.rideZ);
        if(changedBoat){ridePosition=rideTarget;speed=0f;}
        riddenBoat=rideSeat>=0?MultiplayerBoat.Find(rideOwner,rideKey):null;
        bool holding = peer.heldFishSpecies >= 0 && peer.heldFishWeight > 0f && !peer.rodEquipped && !peer.swimming;
        UpdateHeldFish(holding ? peer.heldFishSpecies : -1, peer.heldFishWeight);
        bool wasPosing = fishPosing;
        fishPosing = holding && peer.fishPosing && peer.rideSeat <= 0 && smallPosePlayable.IsValid();
        if (!fishPosing || !wasPosing) fishPoseClock = 0f;
        bool motorboatDriver = (peer.rideSeat == 1 || peer.rideSeat == 2) && (riddenBoat == null || riddenBoat.Boat.IsMotorboat);
        UpdateHeldRod(peer.rodEquipped && !peer.swimming && !holding && !motorboatDriver, peer.rodTier, peer.reelTier);
        // The sequence survives the short local Casting state. HTTP polling can
        // miss that state entirely, so a new sequence must still play once even
        // when castAge is already -1. Do not abort playback on subsequent idle packets.
        bool newCast = peer.castSequence != 0 && peer.castSequence != seenCastSequence;
        bool playCast = newCast && (receivedFishingState || peer.castAge >= 0f);
        hasFish = rodEquipped && peer.hasFish;
        lureInWater = rodEquipped && peer.lureInWater;
        reeling = rodEquipped && peer.reeling && (hasFish || lureInWater);
        // The sender's accumulated reeling time preserves the exact stopped pose,
        // including a quick press/release that happens between observer polls.
        if (!reeling || newCast || peer.reelTime != lastAuthorReelTime)
            reelClock = Mathf.Max(0f, peer.reelTime);
        lastAuthorReelTime = peer.reelTime;
        if (newCast) pullClock = 0f;
        seenCastSequence = peer.castSequence;
        receivedFishingState = true;
        if (castClip != null && rodEquipped && playCast)
        {
            castClock = 0f;
            castDuration = Mathf.Max(.1f, castClip.length);
            casting = true;
        }
        if (!rodEquipped) casting = false;
        swimming = peer.swimming;
        sprinting = peer.sprinting && !swimming;
        target = new Vector3(peer.x, peer.y, peer.z);
        facing = Quaternion.Euler(0, peer.yaw, 0);
        lastPacket = Time.realtimeSinceStartup;

        if (!initialized || (target - transform.position).sqrMagnitude > 225f)
        {
            transform.SetPositionAndRotation(target, facing);
            previous = target;
            initialized = true;
        }
    }

    private void Update()
    {
        if (localSource != null) Receive(localSource.LocalPose(forLocalPresentation: true));
        float dt = Time.unscaledDeltaTime;
        if (!initialized || dt <= 0f) return;

        float blend = 1f - Mathf.Exp(-12f * dt);
        // Local third-person must work without a server connection, while the
        // remote players use the existing replicated helm seat number.
        BoatPassenger localPassenger = localSource != null ? localSource.GetComponent<BoatPassenger>() : null;
        BoatController localHelmBoat = localPassenger != null && localPassenger.Seated &&
            localPassenger.Boat != null && localPassenger.Boat.IsMotorboat ? localPassenger.Boat : null;
        float boatWalkingSpeed=-1f;
        if(riddenBoat!=null)
        {
            Vector3 oldLocal=ridePosition;
            ridePosition=localSource!=null?rideTarget:Vector3.Lerp(ridePosition,rideTarget,blend);
            Transform seat=rideSeat==2?(riddenBoat.Boat.IsMotorboat?riddenBoat.Boat.GuestSeat:riddenBoat.Boat.SecondRowingSeat):rideSeat==1?riddenBoat.Boat.DriverSeat:null;
            target=seat!=null?seat.position:riddenBoat.transform.TransformPoint(ridePosition);
            // A seated driver leans with the boat (including wave pitch and bank).
            facing=rideSeat>0 && riddenBoat.Boat.IsMotorboat
                ? riddenBoat.transform.rotation
                : Quaternion.Euler(0f,riddenBoat.transform.eulerAngles.y+(seat!=null?0f:rideYaw),0f);
            boatWalkingSpeed=seat!=null?0f:Vector3.ProjectOnPlane(ridePosition-oldLocal,Vector3.up).magnitude/dt;
        }
        if (localHelmBoat != null)
        {
            target = localHelmBoat.SeatFor(localPassenger).position;
            facing = localHelmBoat.transform.rotation;
            boatWalkingSpeed = 0f;
        }
        transform.position = localSource != null || riddenBoat!=null ? target : Vector3.Lerp(transform.position, target, blend);
        // Smooth the visual avatar only; local movement and aiming stay immediate.
        // Seated figures must stay rigidly aligned with a turning/banking hull.
        bool motorboatSeated = localHelmBoat != null || (riddenBoat != null && rideSeat > 0 && riddenBoat.Boat.IsMotorboat);
        transform.rotation = motorboatSeated ? facing : Quaternion.Slerp(transform.rotation, facing, blend);
        float measured = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude / dt;
        if(boatWalkingSpeed>=0f)measured=boatWalkingSpeed;
        previous = transform.position;
        speed = Mathf.Lerp(speed, Time.realtimeSinceStartup - lastPacket > 1f ? 0f : measured, blend);

        heldFishMovementSwing = CaughtFishFlop.SmoothHeldMovement(
            heldFishMovementSwing, heldFish != null ? speed : 0f, Time.deltaTime);

        // The authored seated pose is chosen by the same replicated rideSeat
        // already used for boat controls: works for local third person and peers.
        BoatController visualHelmBoat = localHelmBoat != null ? localHelmBoat :
            riddenBoat != null && rideSeat > 0 && riddenBoat.Boat.IsMotorboat ? riddenBoat.Boat : null;
        MotorboatDrivingSeat drivingSeat = MotorboatDrivingSeat.For(visualHelmBoat);
        bool passengerPose = localHelmBoat != null ? localPassenger.Sitting : rideSeat == 2;
        bool drivingMotorboat = drivingSeat != null && (passengerPose ? passengerPlayable.IsValid() : drivePlayable.IsValid());
        drivingBlend = Mathf.MoveTowards(drivingBlend, drivingMotorboat ? 1f : 0f, dt * 12f);
        if (visualRoot != null) visualRoot.localPosition = restingVisualOffset;

        if (animationGraph.IsValid())
        {
            bool moving = speed > WalkingThreshold && !swimming;
            bool running = moving && sprinting && runClip != null && Time.realtimeSinceStartup - lastPacket < 1f;
            runBlend = Mathf.MoveTowards(runBlend, running ? 1f : 0f, dt * 8f);
            if (running) runClock += dt * 1.25f;
            if (runPlayable.IsValid())
                runPlayable.SetTime(runClock % Mathf.Max(.01f, runClip.length));
            float desiredBlend = moving ? Mathf.Clamp01(speed / .7f) : 0f;
            walkBlend = Mathf.MoveTowards(walkBlend, desiredBlend, dt * 7f);
            if (moving)
                walkClock += dt * Mathf.Clamp(speed / 2f, .7f, 2f);

            // Manually wrap the supplied FBX clip; this also works when the imported
            // animation's Loop Time checkbox was not enabled.
            walkPlayable.SetTime(walkClock % Mathf.Max(.01f, walkClip.length));
            if (idleClip == walkClip)
                idlePlayable.SetTime(0f); // No idle clip: hold the walk's starting pose.
            else
            {
                idleClock += dt * .65f;
                idlePlayable.SetTime(idleClock % Mathf.Max(.01f, idleClip.length));
            }

            if (casting)
            {
                castClock += dt;
                if (castClock >= castDuration) casting = false;
            }
            castBlend = Mathf.MoveTowards(castBlend, casting && rodEquipped ? 1f : 0f, dt * 12f);
            if (castPlayable.IsValid())
                castPlayable.SetTime(Mathf.Clamp01(castClock / castDuration) * castClip.length);
            bool showReel = rodEquipped && reelClip != null &&
                (reeling || (lureInWater && !hasFish && !moving && reelClock > 0f));
            bool showPull = rodEquipped && pullClip != null && hasFish && !reeling;
            if (reeling && !casting && Time.realtimeSinceStartup - lastPacket < 1f)
                reelClock += dt;
            if (showPull && !casting) pullClock += dt * .8f;
            if (reelPlayable.IsValid())
                // Scale at sampling so network updates and paused lure poses use the same speed.
                reelPlayable.SetTime((reelClock * .75f) % Mathf.Max(.01f, reelClip.length));
            if (pullPlayable.IsValid())
                pullPlayable.SetTime(pullClock % Mathf.Max(.01f, pullClip.length));
            reelBlend = Mathf.MoveTowards(reelBlend, showReel ? 1f : 0f, dt * 8f);
            pullBlend = Mathf.MoveTowards(pullBlend, showPull ? 1f : 0f, dt * 8f);
            bool showWalkReel = moving && rodEquipped && (reeling || hasFish) && walkReelClip != null;
            bool showHold = heldFish != null && holdFishClip != null;
            bool showWalkHold = showHold && !fishPosing && moving && walkHoldFishClip != null;
            if (showWalkHold) walkHoldFishClock += dt * Mathf.Clamp(speed / 2f, .7f, 2f);
            if (walkHoldFishPlayable.IsValid())
                walkHoldFishPlayable.SetTime(walkHoldFishClock % Mathf.Max(.01f, walkHoldFishClip.length));
            walkHoldFishBlend = Mathf.MoveTowards(walkHoldFishBlend, showWalkHold ? 1f : 0f, dt * 8f);
            if (showWalkReel) walkReelClock += dt * .75f;
            if (showHold) holdFishClock += dt;
            if (walkReelPlayable.IsValid())
                walkReelPlayable.SetTime(walkReelClock % Mathf.Max(.01f, walkReelClip.length));
            if (holdFishPlayable.IsValid())
                holdFishPlayable.SetTime(holdFishClock % Mathf.Max(.01f, holdFishClip.length));
            walkReelBlend = Mathf.MoveTowards(walkReelBlend, showWalkReel ? 1f : 0f, dt * 8f);
            holdFishBlend = Mathf.MoveTowards(holdFishBlend, showHold ? 1f : 0f, dt * 8f);
            float fishingWeight = Mathf.Clamp01(reelBlend + pullBlend);
            float freeWeight = (1f - holdFishBlend) * (1f - castBlend);
            float stationaryWeight = freeWeight * (1f - walkReelBlend);
            float locomotionWeight = (1f - fishingWeight) * stationaryWeight;
            // The graph is manually evaluated: advance the pose clip explicitly,
            // just like the existing walking and holding clips, at authored speed.
            if (fishPosing) fishPoseClock += dt;
            if (smallPosePlayable.IsValid())
                smallPosePlayable.SetTime(fishPoseClock % Mathf.Max(.01f, smallPoseClip.length));
            float poseWeight = fishPosing ? 1f - drivingBlend : 0f;
            float regularWeight = (1f - drivingBlend) * (fishPosing ? 0f : 1f);
            animationMixer.SetInputWeight(11, poseWeight);
            animationMixer.SetInputWeight(0, (1f - walkBlend) * locomotionWeight * regularWeight);
            animationMixer.SetInputWeight(1, walkBlend * (1f - runBlend) * locomotionWeight * regularWeight);
            animationMixer.SetInputWeight(8, walkBlend * runBlend * locomotionWeight * regularWeight);
            animationMixer.SetInputWeight(2, castBlend * (1f - holdFishBlend) * regularWeight);
            animationMixer.SetInputWeight(3, reelBlend * stationaryWeight * regularWeight);
            animationMixer.SetInputWeight(4, pullBlend * stationaryWeight * regularWeight);
            animationMixer.SetInputWeight(5, walkReelBlend * freeWeight * regularWeight);
            animationMixer.SetInputWeight(6, holdFishBlend * (1f - walkHoldFishBlend) * regularWeight);
            animationMixer.SetInputWeight(7, holdFishBlend * walkHoldFishBlend * regularWeight);
            if (drivingMotorboat) drivingClock += dt;
            if (drivePlayable.IsValid())
            {
                drivePlayable.SetTime(drivingClock % Mathf.Max(.01f, driveClip.length));
                animationMixer.SetInputWeight(9, passengerPose ? 0f : drivingBlend);
            }
            if (passengerPlayable.IsValid())
            {
                passengerPlayable.SetTime(drivingClock % Mathf.Max(.01f, passengerClip.length));
                animationMixer.SetInputWeight(10, passengerPose ? drivingBlend : 0f);
            }
            animationGraph.Evaluate(0f);
            if (drivingMotorboat && drivingBlend > .001f && visualRoot != null)
            {
                // Align the animated pelvis to the actual seat cushion, rather
                // than treating a seated character as standing on the deck.
                Vector3 hips = leftHip != null && rightHip != null
                    ? (leftHip.position + rightHip.position) * .5f
                    : visualRoot.position + transform.up * .70f;
                // Passenger matches the previous driver placement exactly except
                // for lateral chair spacing; the driver retains her authored arm pose.
                Vector3 pelvisTarget = (passengerPose ? drivingSeat.PassengerSurface : drivingSeat.SeatSurface)
                    + visualHelmBoat.transform.forward * (passengerPose ? .05f : .07f);
                // Restore the driver's cushion-based position, adding only 2 cm
                // toward the wheel to her original 5 cm forward offset.
                visualRoot.position += (pelvisTarget - hips) * drivingBlend;
                if (passengerPose)
                {
                    // The driver sits to the passenger's left and looks along
                    // boat +X. Positive rotation around that viewing direction
                    // is clockwise. Apply after sampling, never cumulatively,
                    // to the tibias only; each foot follows its shin naturally.
                    Quaternion shinOffset = Quaternion.AngleAxis(15f * drivingBlend,
                        visualHelmBoat.transform.right);
                    if (leftShin != null) leftShin.rotation = shinOffset * leftShin.rotation;
                    if (rightShin != null) rightShin.rotation = shinOffset * rightShin.rotation;
                }
            }
        }

        if (label != null && Camera.main != null)
            label.rotation = Camera.main.transform.rotation;
    }

    private void UpdateHeldRod(bool equipped, int tier, int equippedReelTier)
    {
        rodEquipped = equipped;
        if (!equipped)
        {
            if (heldRod != null) heldRod.SetActive(false);
            return;
        }
        if (leftHand == null) return;
        tier = Mathf.Clamp(tier, 0, ShopCatalog.MaxRodTier);
        equippedReelTier = Mathf.Clamp(equippedReelTier, 0, ShopCatalog.MaxReelTier);
        if (heldRod == null || tier != rodTier || equippedReelTier != displayedReelTier)
        {
            GameObject prefab = Resources.Load<GameObject>(ShopCatalog.RodPrefabResource(tier));
            if (prefab == null) prefab = Resources.Load<GameObject>(ShopCatalog.RodPrefabResource(0));
            if (prefab == null)
            {
                if (rodTier != -2) Debug.LogError("Multiplayer: no fishing rod prefab in Resources/Fishing.");
                rodTier = -2;
                return;
            }
            if (heldRod != null) { heldRod.SetActive(false); Destroy(heldRod); }
            if (rodSocket == null) CreateLeftHandSocket();
            heldRod = Instantiate(prefab, rodSocket, false);
            heldRod.name = "Remote Left Hand Fishing Rod";
            // The same five meshes and exact ReelMount pivots as the local rod:
            // the third-person camera and multiplayer observers both display
            // the equipped Level 7 reel, not a default/starter placeholder.
            ApplyRemoteReelVisual(heldRod, equippedReelTier);
            Transform reelMount = heldRod.transform.Find("ReelMount");
            if (reelMount != null)
            {
                // Observer-only: scale around the rod axis so the reel stays attached.
                // Move the attachment 3.5 cm along the prefab's +Y (toward its tip).
                Vector3 mount = reelMount.localPosition;
                reelMount.localScale *= .4f; // 2.5 times smaller.
                reelMount.localPosition = new Vector3(mount.x * .4f, mount.y + .035f, mount.z * .4f);
            }
            // These prefabs point along +Y with the handle just above the reel seat.
            // Cancel FBX unit scaling so the rod remains a sensible world size.
            Vector3 scale = rodSocket.lossyScale;
            heldRod.transform.localScale = new Vector3(
                AvatarScale / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                AvatarScale / Mathf.Max(.0001f, Mathf.Abs(scale.y)),
                AvatarScale / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            heldRod.transform.localRotation = Quaternion.identity;
            heldRod.transform.position = rodSocket.position - rodSocket.up * (.23f * AvatarScale);
            foreach (Collider collider in heldRod.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (MonoBehaviour script in heldRod.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (Animation animation in heldRod.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
            foreach (Animator animator in heldRod.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Rigidbody body in heldRod.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.detectCollisions = false; }
            // Reuse the first-person mesh flex on this observer-only mesh instance.
            // The component stays disabled; only explicit presentation calls run.
            observerRodView = heldRod.GetComponent<FishingRodView>();
            if (observerRodView != null) observerRodView.InitializePose();
            observerRodBent = false;
            rodTier = tier;
            displayedReelTier = equippedReelTier;
        }
        heldRod.SetActive(true);
    }

    private static readonly string[] RemoteReelParts =
        { "ReelFootAndBody", "Rotor", "ReelHousing", "Spool", "Handle" };

    private static void ApplyRemoteReelVisual(GameObject rod, int tier)
    {
        GameObject source = Resources.Load<GameObject>(ShopCatalog.ReelPrefabResource(tier));
        if (source == null)
        {
            Debug.LogWarning("Equipped reel tier " + tier +
                " is missing its Resources prefab; using existing rod reel.");
            return;
        }
        Transform sourceMount = source.transform.Find("ReelMount");
        Transform targetMount = rod.transform.Find("ReelMount");
        if (sourceMount == null || targetMount == null) return;
        foreach (string part in RemoteReelParts)
        {
            Transform from = sourceMount.Find(part), to = targetMount.Find(part);
            if (from == null || to == null) continue;
            MeshFilter fm = from.GetComponent<MeshFilter>(), tm = to.GetComponent<MeshFilter>();
            MeshRenderer fr = from.GetComponent<MeshRenderer>(), tr = to.GetComponent<MeshRenderer>();
            if (fm != null && tm != null) tm.sharedMesh = fm.sharedMesh;
            if (fr != null && tr != null)
            {
                tr.sharedMaterials = fr.sharedMaterials;
                tr.SetPropertyBlock(null);
            }
        }
    }

    private void CreateLeftHandSocket()
    {
        rodSocket = new GameObject("Left Hand Rod Grip").transform;
        rodSocket.SetParent(leftHand, false);
        Transform index = FishingRemoteAvatarAsset.FindBone(leftHand, "Second1.L");
        Transform pinky = FishingRemoteAvatarAsset.FindBone(leftHand, "Pinky1.L");
        if (index == null || pinky == null) return;
        Vector3 indexBase = leftHand.InverseTransformPoint(index.position);
        Vector3 pinkyBase = leftHand.InverseTransformPoint(pinky.position);
        Vector3 palm = (indexBase + pinkyBase) * .5f;
        Vector3 across = (indexBase - pinkyBase).normalized;
        Vector3 normal = Vector3.Cross(across, palm.normalized).normalized;
        rodSocket.localPosition = palm * .8f;
        if (across.sqrMagnitude > .5f && normal.sqrMagnitude > .5f)
            rodSocket.localRotation = Quaternion.LookRotation(normal, across);
    }

    private void UpdateHeldFish(int species, float weight)
    {
        if (species < 0 || rightHand == null)
        {
            ClearHeldFish();
            return;
        }
        if (heldFish != null && heldSpecies == species && heldWeight == weight) return;
        ClearHeldFish();
        heldSpecies = species;
        heldWeight = weight;
        holdFishClock = 0f;
        walkHoldFishClock = 0f;
        heldFishMovementSwing = 0f;
        heldFishSwayPhase = Random.Range(0f, Mathf.PI * 2f);
        heldFish = FishWorldSize.Create("Remote Held Fish", transform, species, weight);
        foreach (Collider collider in heldFish.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (Rigidbody body in heldFish.GetComponentsInChildren<Rigidbody>(true))
        { body.isKinematic = true; body.detectCollisions = false; }
        heldFish.GetComponent<HeroFishAnimator>()?.SetHeld(true);
        heldFish.GetComponent<YellowfinTunaPresentation>()?.SetHeld(true);
        heldFish.GetComponent<GoatfishPresentation>()?.SetHeld(true);
        heldFish.GetComponent<RedSnapperPresentation>()?.SetHeld(true);
        heldFish.GetComponent<MackerelPresentation>()?.SetHeld(true);
        heldFish.GetComponent<YellowtailPresentation>()?.SetHeld(true);
        heldFish.transform.localRotation = Quaternion.AngleAxis(FishCatalog.HeldYaw(heldSpecies), Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
        // Exactly the same authored/bone/geometry mouth lookup used by first person.
        Vector3 mouthWorld = heldFish.transform.TransformPoint(FishingSystem.FindFishMouthLocalPosition(heldFish));
        Transform head = FishingRemoteAvatarAsset.FindBone(heldFish.transform, "Bone");
        heldMouth = new GameObject("Remote Catch Mouth Anchor").transform;
        heldMouth.SetParent(head != null ? head : heldFish.transform, false);
        heldMouth.position = mouthWorld;
        if (heldString == null)
        {
            var lineObject = new GameObject("Right Hand Fish String");
            lineObject.transform.SetParent(transform, false);
            heldString = lineObject.AddComponent<LineRenderer>();
            heldString.useWorldSpace = true;
            heldString.positionCount = 2;
            heldString.startWidth = .0015f;
            heldString.endWidth = .001f;
            heldString.numCapVertices = 4;
            Shader shader = Resources.Load<Shader>("Fishing/FishingUnlit");
            if (shader != null)
            {
                heldStringMaterial = new Material(shader);
                heldStringMaterial.SetColor("_BaseColor", new Color(.8f, .85f, .87f, .9f));
                heldString.sharedMaterial = heldStringMaterial;
            }
        }
        heldString.enabled = true;
        CaughtFishFlop flop = heldFish.GetComponent<CaughtFishFlop>();
        if (flop == null) flop = heldFish.AddComponent<CaughtFishFlop>();
        heldFishFlop = flop;
        flop.ConfigurePlayerHeld();
        flop.PoseUpdated = PinHeldFish;
        PinHeldFish();
    }

    private void LateUpdate()
    {
        UpdateObserverRodBend();
        if(remoteLine!=null)remoteLine.Present(rodEquipped && observerRodView!=null ? observerRodView.RodTip : null);
        PinHeldFish();
    }

    // Called immediately before the local camera renders, after all first-person
    // LateUpdate effects (including lure flex) have finished.
    public void CopyLocalRodBend(FishingRodView source)
    {
        if (localSource == null || observerRodView == null || !rodEquipped ||
            heldRod == null || !heldRod.activeInHierarchy) return;
        observerRodView.CopyPresentationBendFrom(source);
    }

    private void UpdateObserverRodBend()
    {
        // Local third person gets the actual final bend, not the network approximation.
        if (localSource != null || observerRodView == null) return;
        bool shouldBend = rodEquipped && heldRod != null && heldRod.activeInHierarchy &&
            (hasFish || (reeling && lureInWater)) && !casting && castBlend < .1f &&
            Time.realtimeSinceStartup - lastPacket < 1f;
        if (!shouldBend)
        {
            if (observerRodBent) observerRodView.ResetMotion();
            observerRodBent = false;
            return;
        }

        // Approximate the pull ahead and below the angler; no lure position,
        // tension or bend angles need to be synchronized over the network.
        Vector3 pullTarget = heldRod.transform.position + transform.forward * 12f + Vector3.down * 3f;
        float tension = hasFish ? (reeling ? .45f + .10f * Mathf.Sin(Time.unscaledTime * 2.3f) : .60f) : 0f;
        // Lure: base curve. Fish without reeling: medium tension-only curve.
        // Fish with reeling: add the stronger first-person reel-load boost.
        observerRodView.SetLinePull(pullTarget, tension, hasFish && reeling, Time.unscaledDeltaTime);
        observerRodBent = true;
    }

    private void PinHeldFish()
    {
        if (heldFish == null || heldMouth == null || rightHand == null) return;
        if (heldString != null) heldString.enabled = true;
        Vector3 top = rightStringAnchor != null && rightStringAnchor != rightHand
            ? rightStringAnchor.position : rightHand.TransformPoint(rightGripLocal);
        CaughtFishFlop.EvaluateHeldSway(Time.time * 5.8f + heldFishSwayPhase,
            heldFishMovementSwing, heldFishFlop != null ? heldFishFlop.SwingBoost : 0f,
            out Vector3 swayOffset, out Quaternion swayRotation);
        // Preserve the first-person sway pattern, constrained to a 10 cm string.
        Vector3 stringDirection = (Vector3.down * .10f + transform.rotation * swayOffset).normalized;
        Vector3 mouth = top + stringDirection * .10f;
        // Match the local held pose; mouth pinning preserves the existing string endpoints.
        heldFish.transform.rotation = transform.rotation * Quaternion.AngleAxis(FishCatalog.HeldYaw(heldSpecies), Vector3.up) *
            Quaternion.Euler(-90f, 0f, 0f) * swayRotation;
        heldFish.transform.position += mouth - heldMouth.position;
        if (heldString != null)
        {
            heldString.SetPosition(0, top);
            heldString.SetPosition(1, mouth);
        }
    }

    private void ClearHeldFish()
    {
        if (heldFish != null)
        {
            var flop = heldFish.GetComponent<CaughtFishFlop>();
            if (flop != null) flop.PoseUpdated = null;
            heldFish.SetActive(false);
            Destroy(heldFish);
        }
        heldFish = null;
        heldFishFlop = null;
        heldMouth = null;
        heldSpecies = -1;
        if (heldString != null) heldString.enabled = false;
    }

    private void OnDestroy()
    {
        if (heldStringMaterial != null) Destroy(heldStringMaterial);
        if (animationGraph.IsValid())
            animationGraph.Destroy();
    }
}


