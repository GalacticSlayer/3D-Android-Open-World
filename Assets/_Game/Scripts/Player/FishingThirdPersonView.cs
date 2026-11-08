using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Render-only perspective: gameplay always retains its original eye/aim transform.
[DefaultExecutionOrder(-10000)]
public sealed class FishingThirdPersonView : MonoBehaviour
{
    public bool IsThirdPerson { get; private set; }
    private Camera view;
    private FirstPersonController player;
    private float movementHeldTime, returnYawVelocity;
    private float orbitYaw;
    private const float MinZoomDistance = 1.2f, MaxZoomDistance = 3.4f;
    private float zoomDistance = MaxZoomDistance;
    private FishingSystem fishing;
    private FishingRemotePlayer avatar;
    private Renderer[] avatarRenderers;
    private bool avatarRenderVisible = true;
    private bool rendering;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 savedLineStart;
    private LineRenderer renderLine;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly RaycastHit[] obstacles = new RaycastHit[64];

    public static FishingThirdPersonView For(FirstPersonController player)
    {
        if (player == null) return null;
        var component = player.GetComponent<FishingThirdPersonView>();
        return component != null ? component : player.gameObject.AddComponent<FishingThirdPersonView>();
    }

    private void Awake()
    {
        player = GetComponent<FirstPersonController>();
        view = player != null ? player.ViewCamera : null;
        fishing = GetComponent<FishingSystem>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginSRP;
        RenderPipelineManager.endCameraRendering += EndSRP;
        Camera.onPreCull += BeginBuiltin;
        Camera.onPostRender += EndBuiltin;
    }

    public void Toggle()
    {
        if (view == null) return;
        RestoreRenderPose();
        IsThirdPerson = !IsThirdPerson;
        movementHeldTime = returnYawVelocity = 0f;
        // The visible motorboat driver faces the bow, not the player controller's
        // last independent look yaw. Enter third person directly behind her.
        if (IsThirdPerson)
        {
            BoatPassenger passenger = player != null ? player.GetComponent<BoatPassenger>() : null;
            orbitYaw = passenger != null && passenger.Seated &&
                passenger.Boat != null && passenger.Boat.IsMotorboat
                ? passenger.Boat.transform.eulerAngles.y
                : transform.eulerAngles.y;
        }
        SyncLocalAvatar();
    }

    // Enter pose with the camera in front of the angler, preserving the existing zoom.
    public void FacePlayerForPose()
    {
        if (!IsThirdPerson) return;
        RestoreRenderPose();
        orbitYaw = Mathf.Repeat(transform.eulerAngles.y + 180f, 360f);
        movementHeldTime = returnYawVelocity = 0f;
    }

    private bool IsSeatedMotorboat()
    {
        BoatPassenger passenger = player != null ? player.GetComponent<BoatPassenger>() : null;
        return passenger != null && passenger.Seated &&
            passenger.Boat != null && passenger.Boat.IsMotorboat;
    }

    private void SyncLocalAvatar()
    {
        // A lightweight, visually hidden copy of the existing third-person rig
        // supplies the real animated head location while driving in first person.
        // Never touch the boat, driver anchor, controller or aiming camera.
        bool needed = IsThirdPerson || IsSeatedMotorboat();
        if (!needed)
        {
            if (avatar != null && avatar.gameObject.activeSelf)
                avatar.gameObject.SetActive(false);
            return;
        }
        if (avatar == null)
        {
            avatar = new GameObject("Local Third Person Angler").AddComponent<FishingRemotePlayer>();
            avatar.Initialize("", "local", false);
            avatar.FollowLocal(FishingMultiplayer.For(player));
            avatarRenderers = avatar.GetComponentsInChildren<Renderer>(true);
        }
        if (!avatar.gameObject.activeSelf) avatar.gameObject.SetActive(true);
        if (avatarRenderVisible != IsThirdPerson)
        {
            // The character is still animated so we can track her head, but her
            // face/hair must not be drawn in front of the first-person camera.
            if (avatarRenderers != null)
                foreach (Renderer renderer in avatarRenderers)
                    if (renderer != null) renderer.forceRenderingOff = !IsThirdPerson;
            avatarRenderVisible = IsThirdPerson;
        }
    }

    private bool TryGetFirstPersonSeatedHead(out Vector3 head)
    {
        head = Vector3.zero;
        return !IsThirdPerson && IsSeatedMotorboat() && avatar != null &&
            avatar.isActiveAndEnabled && avatar.TryGetMotorboatSeatedHead(out head);
    }

    // A render interrupted by scene changes must never leave gameplay at the boom position.
    private void Update()
    {
        RestoreRenderPose();
        SyncLocalAvatar();
    }

    private void LateUpdate()
    {
        // Releasing the stick or opening UI restarts the one-second grace period.
        // Keyboard movement uses the same input, so desktop behaves consistently.
        bool moving = IsThirdPerson && player != null && player.isActiveAndEnabled
            && player.BoatInput.sqrMagnitude > .0025f;
        if(!moving)
        {
            movementHeldTime = returnYawVelocity = 0f;
            return;
        }
        movementHeldTime += Time.deltaTime;
        if(movementHeldTime <= 1f) return;
        // The driving motorboat avatar faces the bow, whereas the real player
        // controller keeps an independent yaw for steering/look. Recentering to
        // that controller yaw would incorrectly show the character from the side
        // or front. Other modes retain their original character-relative target.
        BoatPassenger passenger = player != null ? player.GetComponent<BoatPassenger>() : null;
        float behindYaw = passenger != null && passenger.Seated &&
            passenger.Boat != null && passenger.Boat.IsMotorboat
            ? passenger.Boat.transform.eulerAngles.y
            : transform.eulerAngles.y;
        orbitYaw = Mathf.SmoothDampAngle(orbitYaw, behindYaw,
            ref returnYawVelocity, .35f, 240f, Time.deltaTime);
    }

    // Fingers spreading apart move closer; bringing them together zooms out.
    public void PinchZoom(float previousSeparation, float separation)
    {
        if (!IsThirdPerson || previousSeparation < 1f || separation < 1f) return;
        zoomDistance = Mathf.Clamp(zoomDistance * previousSeparation / separation,
            ClosestFullBodyDistance(), MaxZoomDistance);
    }

    public void Orbit(float yawDelta)
    {
        orbitYaw = Mathf.Repeat(orbitYaw + yawDelta, 360f);
    }

    // Rear half (including either 90-degree side view): restore the original
    // camera-relative movement. Front half: retain character-relative movement,
    // so forward walks toward a camera looking at the character's face.
    public void AlignForMovement()
    {
        if (!IsThirdPerson) return;
        float relativeYaw = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, orbitYaw));
        if (relativeYaw <= 90f)
        {
            transform.rotation = Quaternion.Euler(0f, orbitYaw, 0f);
            returnYawVelocity = 0f;
        }
    }

    private float ClosestFullBodyDistance()
    {
        if (view == null || avatar == null) return MinZoomDistance;
        // Keep body framing with a slightly tighter close-up margin.
        float halfFov = Mathf.Clamp(view.fieldOfView, 10f, 150f) * .5f * Mathf.Deg2Rad;
        float fitDistance = avatar.BodyHeight * .5f * 1.2f / Mathf.Tan(halfFov) + .15f;
        return Mathf.Clamp(fitDistance, MinZoomDistance, MaxZoomDistance);
    }

    private void CameraPose(out Vector3 position, out Quaternion rotation)
    {
        rotation = Quaternion.AngleAxis(
            Mathf.DeltaAngle(transform.eulerAngles.y, orbitYaw), Vector3.up) * view.transform.rotation;
        float closest = ClosestFullBodyDistance();
        float framedDistance = Mathf.Clamp(zoomDistance, closest, MaxZoomDistance);
        float closeBlend = 1f - Mathf.InverseLerp(closest, MaxZoomDistance, framedDistance);
        Vector3 farPivot = view.transform.position + Vector3.up * .65f;
        // Bring the close-up focus slightly above body centre, toward the chest.
        Vector3 pivot = avatar != null ? Vector3.Lerp(farPivot, avatar.BodyCenter + Vector3.up * .18f, closeBlend) : farPivot;
        Vector3 offset = rotation * new Vector3(.35f * framedDistance / MaxZoomDistance, 0f, -framedDistance);
        float distance = offset.magnitude;
        int count = Physics.SphereCastNonAlloc(pivot, .22f, offset / distance, obstacles,
            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        BoatPassenger passenger = GetComponent<BoatPassenger>();
        Transform ownBoat = passenger != null && passenger.Boat != null && passenger.Seated
            ? passenger.Boat.transform : null;
        for (int i = 0; i < count; i++)
        {
            Transform hit = obstacles[i].transform;
            if (hit == null || hit.IsChildOf(transform) ||
                (avatar != null && hit.IsChildOf(avatar.transform)) ||
                (ownBoat != null && hit.IsChildOf(ownBoat))) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, obstacles[i].distance - .08f));
        }
        position = pivot + offset.normalized * distance;
    }

    // Camera effects must sample the displayed eye even if their render callback
    // was subscribed before this component. BeginRender is idempotent per render.
    public static void PrepareViewingCamera(Camera camera)
    {
        var perspective = camera != null ? camera.GetComponentInParent<FishingThirdPersonView>() : null;
        if (perspective != null && perspective.isActiveAndEnabled) perspective.BeginRender(camera);
    }

    private void BeginSRP(ScriptableRenderContext context, Camera camera) => BeginRender(camera);
    private void EndSRP(ScriptableRenderContext context, Camera camera) { if (camera == view) RestoreRenderPose(); }
    private void BeginBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) BeginRender(camera); }
    private void EndBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null && camera == view) RestoreRenderPose(); }

    private void BeginRender(Camera camera)
    {
        if (camera != view || rendering) return;
        if (!IsThirdPerson)
        {
            if (!TryGetFirstPersonSeatedHead(out Vector3 head)) return;
            // Rendering-only eye shift: player aiming, touch look, fishing,
            // physics and network poses keep the original camera transform.
            savedPosition = view.transform.localPosition;
            savedRotation = view.transform.localRotation;
            rendering = true;
            view.transform.position = head;
            return;
        }
        // Read the final rod flex before moving the eye for rendering. This includes
        // current tension, fish modes, reel load, lure flex and release/relaxation.
        if (avatar != null && fishing != null) avatar.CopyLocalRodBend(fishing.PresentationRod);
        CameraPose(out Vector3 position, out Quaternion rotation);
        savedPosition = view.transform.localPosition;
        savedRotation = view.transform.localRotation;
        rendering = true;
        // Hide only the first-person presentation, keeping its simulation running.
        renderLine = fishing != null ? fishing.PresentationLine : null;
        view.GetComponentsInChildren<Renderer>(true, renderers);
        hidden.Clear();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == renderLine || renderer.forceRenderingOff) continue;
            renderer.forceRenderingOff = true;
            hidden.Add(renderer);
        }
        if (renderLine != null && renderLine.positionCount > 0)
        {
            savedLineStart = renderLine.GetPosition(0);
            if (avatar != null && avatar.RodTip != null)
                renderLine.SetPosition(0, avatar.RodTip.position);
        }
        view.transform.SetPositionAndRotation(position, rotation);
    }

    private void RestoreRenderPose()
    {
        if (!rendering) return;
        if (view != null)
        {
            view.transform.localPosition = savedPosition;
            view.transform.localRotation = savedRotation;
        }
        foreach (Renderer renderer in hidden)
            if (renderer != null) renderer.forceRenderingOff = false;
        hidden.Clear();
        if (renderLine != null && renderLine.positionCount > 0) renderLine.SetPosition(0, savedLineStart);
        renderLine = null;
        rendering = false;
    }

    public static Quaternion ViewingRotation(Camera camera)
    {
        if (camera == null) return Quaternion.identity;
        var perspective = camera.GetComponentInParent<FishingThirdPersonView>();
        if (perspective != null && perspective.IsThirdPerson && !perspective.rendering)
        {
            perspective.CameraPose(out _, out Quaternion rotation);
            return rotation;
        }
        return camera.transform.rotation;
    }

    // World-attached HUD labels must use the displayed perspective, not the aiming eye.
    public static Vector3 WorldToScreenPoint(Camera camera, Vector3 point)
    {
        var perspective = camera != null ? camera.GetComponentInParent<FishingThirdPersonView>() : null;
        if (perspective == null || perspective.rendering)
            return camera != null ? camera.WorldToScreenPoint(point) : Vector3.zero;
        Vector3 position;
        Quaternion rotation;
        if (perspective.IsThirdPerson)
            perspective.CameraPose(out position, out rotation);
        else if (perspective.TryGetFirstPersonSeatedHead(out position))
            rotation = camera.transform.rotation;
        else
            return camera.WorldToScreenPoint(point);
        Vector3 local = Quaternion.Inverse(rotation) * (point - position);
        Vector4 clip = camera.projectionMatrix * new Vector4(local.x, local.y, -local.z, 1f);
        if (Mathf.Abs(clip.w) < .00001f) return new Vector3(0f, 0f, local.z);
        Rect pixels = camera.pixelRect;
        return new Vector3(pixels.x + (clip.x / clip.w + 1f) * .5f * pixels.width,
            pixels.y + (clip.y / clip.w + 1f) * .5f * pixels.height, local.z);
    }

    private void OnDisable()
    {
        RestoreRenderPose();
        RenderPipelineManager.beginCameraRendering -= BeginSRP;
        RenderPipelineManager.endCameraRendering -= EndSRP;
        Camera.onPreCull -= BeginBuiltin;
        Camera.onPostRender -= EndBuiltin;
        IsThirdPerson = false;
        movementHeldTime = returnYawVelocity = 0f;
        if (avatar != null) avatar.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreRenderPose();
        if (avatar != null) Destroy(avatar.gameObject);
    }
}
