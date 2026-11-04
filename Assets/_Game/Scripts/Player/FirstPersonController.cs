using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private MobileJoystick moveJoystick;
    [SerializeField] private TouchLookArea touchLookArea;
    // Pose's uncluttered overlay supplies the same swipe and pinch gestures
    // while the ordinary gameplay canvas is hidden.
    private TouchLookArea poseLookArea;
    public void SetPoseLookArea(TouchLookArea area)
    {
        if (poseLookArea == area) return;
        if (poseLookArea != null) poseLookArea.ConsumeLookDelta();
        if (touchLookArea != null) touchLookArea.ConsumeLookDelta();
        poseLookArea = area;
    }
    [SerializeField] private MobileActionButton jumpButton;
    [SerializeField] private OceanWater oceanWater;

    [Header("Walking")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 7.5f;
    [SerializeField] private float groundAcceleration = 18f;
    [SerializeField] private float groundDeceleration = 24f;
    [SerializeField] private float airAcceleration = 6f;
    [SerializeField] private float gravity = -22f;
    [SerializeField] private float groundStickForce = 3f;
    [SerializeField] private float jumpHeight = 1.25f;

    [Header("Mobile Sprint")]
    [SerializeField] private float mobileSprintMagnitude = 0.98f;
    [SerializeField] private float mobileSprintForward = 0.88f;
    [SerializeField] private float mobileSprintHoldTime = 0.18f;
    [SerializeField] private float mobileSprintReleaseMagnitude = 0.82f;
    [SerializeField] private float mobileSprintReleaseForward = 0.68f;

    [Header("Swimming")]
    [SerializeField] private float swimSpeed = 3.8f;
    [SerializeField] private float swimAcceleration = 8f;
    [SerializeField] private float swimEnterDepth = 0.45f;
    [SerializeField] private float floatingBodyDepth = 0.55f;
    [SerializeField] private float buoyancyStrength = 1.6f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float touchSensitivity = 0.25f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float sprintFovBoost = 4f;
    [SerializeField] private float fovLerpSpeed = 7f;

    [Header("Boat Camera Rocking")]
    [SerializeField, Range(0f, 1f)] private float boatCameraTiltAmount = 1f;
    [SerializeField, Min(0f)] private float boatCameraTiltFollowSpeed = 10f;

    private BoatPassenger boatPassenger;
    private Quaternion boatCameraTilt = Quaternion.identity;

    private CharacterController controller;
    private Camera playerCamera;
    private FishingThirdPersonView thirdPersonView;

    private Vector3 planarVelocity;
    private Vector3 swimVelocity;

    private float verticalVelocity;
    private float cameraPitch;
    private float baseFov = 72f;

    private bool previousSwimming;
    private float mobileSprintHoldTimer;
    private bool mobileSprintLatched;

    public Camera ViewCamera => playerCamera;
    // Read the existing view mode without changing walking/looking/camera behavior.
    // Motorboat steering uses this only to distinguish its third-person helm.
    public bool IsThirdPersonView
    {
        get
        {
            if (thirdPersonView == null) thirdPersonView = GetComponent<FishingThirdPersonView>();
            return thirdPersonView != null && thirdPersonView.IsThirdPerson;
        }
    }
    public bool IsSwimming { get; private set; }
    public bool IsSprinting { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        boatPassenger = GetComponent<BoatPassenger>();

        if (cameraTransform != null)
        {
            playerCamera = cameraTransform.GetComponent<Camera>();

            if (playerCamera != null)
                baseFov = playerCamera.fieldOfView;
        }

        if (oceanWater == null)
        {
            oceanWater = FindFirstObjectByType<OceanWater>();
        }
    }

    private void Start()
    {
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        previousSwimming = IsSwimming;
    }

    private MobileActionButton swimDownButton;
    private RectTransform swimUpRect;
    private Vector2 landJumpPosition;
    private bool swimLayout;
    private FishingSystem swimmingFishing;
    private void LateUpdate()
    {
        // Apply hull tilt after input and movement, even if a menu is open.
        UpdateBoatCameraRocking();

        // Stow before rendering, even if movement entered swimming after the
        // fishing Update ran this frame. This also restores the swim controls.
        if(IsSwimming)
        {
            if(swimmingFishing==null)swimmingFishing=GetComponent<FishingSystem>();
            if(swimmingFishing!=null)swimmingFishing.OnPlayerSwimming();
        }
        if(jumpButton==null)return;
        if(swimDownButton==null)
        {
            swimUpRect=jumpButton.GetComponent<RectTransform>();landJumpPosition=swimUpRect.anchoredPosition;
            var go=Instantiate(jumpButton.gameObject,jumpButton.transform.parent);go.name="SwimDown";
            swimDownButton=go.GetComponent<MobileActionButton>();
            var button=go.GetComponent<UnityEngine.UI.Button>();if(button!=null)button.onClick.RemoveAllListeners();
            foreach(var label in go.GetComponentsInChildren<UnityEngine.UI.Text>())label.text="▼";
            go.GetComponent<RectTransform>().anchoredPosition=landJumpPosition;
            go.GetComponent<RectTransform>().localRotation=Quaternion.Euler(0,0,180);
            foreach(var label in go.GetComponentsInChildren<UnityEngine.UI.Text>())label.rectTransform.localRotation=Quaternion.Euler(0,0,180);
            go.SetActive(false);
        }
        bool swimming=IsSwimming && !uiBlocked && !castMode;
        swimDownButton.gameObject.SetActive(swimming);
        if(swimLayout!=swimming)
        {
            swimLayout=swimming;
            swimUpRect.anchoredPosition=landJumpPosition+(swimming?Vector2.up*(swimUpRect.sizeDelta.y+14):Vector2.zero);
        }
    }
    private bool castMode;
    public Vector2 JumpRestPosition => swimUpRect!=null?landJumpPosition:JumpControl!=null?JumpControl.anchoredPosition:Vector2.zero;
    public RectTransform JumpControl => jumpButton!=null?jumpButton.GetComponent<RectTransform>():null;
    public void SetCastMode(bool value)
    {
        castMode=value;
        if(jumpButton!=null)jumpButton.gameObject.SetActive(!value);
    }
    private bool menuOpen;
    public void SetMenuOpen(bool open)
    {
        menuOpen=open;
        if(touchLookArea!=null)touchLookArea.ConsumeLookDelta();
        if(!Application.isMobilePlatform){Cursor.lockState=open?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=open;}
    }
    private bool uiBlocked;
    public void SetUIBlocked(bool blocked)
    {
        uiBlocked=blocked;
        ResetMotion();
        if(!Application.isMobilePlatform) { Cursor.lockState=(blocked || menuOpen)?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=blocked || menuOpen; }
    }
    public void ResetViewPitch()
    {
        cameraPitch=0;
        if(cameraTransform!=null) cameraTransform.localRotation=Quaternion.identity;
    }
    public void ResetMotion()
    {
        planarVelocity=Vector3.zero; swimVelocity=Vector3.zero; verticalVelocity=0f;
        IsSwimming=false; previousSwimming=false; IsSprinting=false;
        mobileSprintHoldTimer=0f; mobileSprintLatched=false;
        if(touchLookArea!=null) touchLookArea.ConsumeLookDelta();
    }
    private void Update()
    {
        if(uiBlocked) { if(touchLookArea!=null) touchLookArea.ConsumeLookDelta(); return; }
        HandleMovement();

        if (moveJoystick != null)
        {
            moveJoystick.SetSprinting(IsSprinting);
        }

        if(!menuOpen)HandleLook();
        else if(touchLookArea!=null)touchLookArea.ConsumeLookDelta();
        UpdateCameraFov();
    }

    public void SetOceanWater(OceanWater water)
    {
        oceanWater = water;
    }

    public void SetJumpButton(MobileActionButton button)
    {
        jumpButton = button;
    }

    public bool BoatUIBlocked => uiBlocked || menuOpen;
    public Vector2 BoatInput => BoatUIBlocked?Vector2.zero:Vector2.ClampMagnitude(ReadMovementInput(),1);
    private void HandleMovement()
    {
        var passenger=GetComponent<BoatPassenger>();
        if(passenger!=null && passenger.Seated){IsSwimming=false;IsSprinting=false;return;}

        Vector2 input =
            Vector2.ClampMagnitude(
                ReadMovementInput(),
                1f
            );

        if (thirdPersonView == null) thirdPersonView = GetComponent<FishingThirdPersonView>();
        if (thirdPersonView != null && input.sqrMagnitude > .0001f)
            thirdPersonView.AlignForMovement();

        bool jumpPressed =
            ReadJumpPressed();

        bool swimUpHeld =
            ReadSwimUpHeld();

        float swimDownInput =
            ReadKeyboardSwimDownInput();

        UpdateSwimmingState();
        HandleSwimmingTransition();

        if (IsSwimming)
        {
            IsSprinting = false;

            HandleSwimming(
                input,
                swimUpHeld,
                swimDownInput
            );
        }
        else
        {
            HandleWalking(
                input,
                jumpPressed
            );
        }
    }

    private void HandleWalking(
        Vector2 input,
        bool jumpPressed)
    {
        bool grounded = controller.isGrounded;

        float inputMagnitude =
            Mathf.Clamp01(input.magnitude);

        bool wantsSprint =
            WantsToSprint(input);

        // Sprint represents the player's held input intent, not whether
        // CharacterController happens to report grounded this frame.
        // This keeps sprint stable over bumps and while jumping.
        IsSprinting =
            wantsSprint &&
            input.y > 0.15f &&
            inputMagnitude > 0.15f;

        float targetSpeed =
            IsSprinting
                ? sprintSpeed
                : moveSpeed;

        Vector3 desiredDirection =
            transform.right * input.x +
            transform.forward * input.y;

        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();

            if (grounded &&
                TryGetGroundNormal(
                    out Vector3 groundNormal))
            {
                Vector3 slopeDirection =
                    Vector3.ProjectOnPlane(
                        desiredDirection,
                        groundNormal
                    );

                if (slopeDirection.sqrMagnitude > 0.0001f)
                {
                    desiredDirection =
                        slopeDirection.normalized;
                }
            }
        }

        Vector3 targetVelocity =
            desiredDirection *
            targetSpeed *
            inputMagnitude;

        float acceleration;

        if (!grounded)
        {
            acceleration = airAcceleration;
        }
        else if (inputMagnitude > 0.01f)
        {
            acceleration = groundAcceleration;
        }
        else
        {
            acceleration = groundDeceleration;
        }

        planarVelocity =
            Vector3.MoveTowards(
                planarVelocity,
                targetVelocity,
                acceleration * Time.deltaTime
            );

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity =
                -groundStickForce;
        }

        if (grounded && jumpPressed)
        {
            verticalVelocity =
                Mathf.Sqrt(
                    jumpHeight *
                    -2f *
                    gravity
                );
        }
        else
        {
            verticalVelocity +=
                gravity * Time.deltaTime;
        }

        Vector3 velocity =
            planarVelocity +
            Vector3.up * verticalVelocity;

        controller.Move(
            velocity * Time.deltaTime
        );
    }

    private void HandleSwimming(
        Vector2 input,
        bool swimUpHeld,
        float swimDownInput)
    {
        verticalVelocity = 0f;

        Transform lookTransform =
            cameraTransform != null
                ? cameraTransform
                : transform;

        Vector3 desiredDirection =
            lookTransform.forward * input.y +
            lookTransform.right * input.x;

        if (swimUpHeld)
        {
            desiredDirection += Vector3.up;
        }

        if (swimDownInput > 0f)
        {
            desiredDirection +=
                Vector3.down *
                swimDownInput;
        }

        desiredDirection =
            Vector3.ClampMagnitude(
                desiredDirection,
                1f
            );

        Vector3 targetSwimVelocity =
            desiredDirection * swimSpeed;

        swimVelocity =
            Vector3.MoveTowards(
                swimVelocity,
                targetSwimVelocity,
                swimAcceleration * Time.deltaTime
            );

        if(!ShopWaterVolume.TrySurface(transform.position,oceanWater,out float waterHeight))
        { IsSwimming=false; return; }

        float bodyHeight =
            transform.position.y +
            controller.center.y;

        float targetBodyHeight =
            waterHeight -
            floatingBodyDepth;

        float surfaceAssist =
            Mathf.Clamp(
                (targetBodyHeight - bodyHeight) *
                buoyancyStrength,
                -1.2f,
                1.4f
            );

        bool intentionalVerticalMovement =
            swimUpHeld ||
            swimDownInput > 0.01f ||
            Mathf.Abs(
                lookTransform.forward.y *
                input.y
            ) > 0.18f;

        float assistMultiplier =
            desiredDirection.sqrMagnitude > 0.04f ||
            intentionalVerticalMovement
                ? 0.42f
                : 1f;

        Vector3 velocity =
            swimVelocity +
            Vector3.up *
            surfaceAssist *
            assistMultiplier;

        controller.Move(
            velocity * Time.deltaTime
        );
    }

    private void HandleSwimmingTransition()
    {
        if (IsSwimming == previousSwimming)
            return;

        if (IsSwimming)
        {
            swimVelocity =
                planarVelocity +
                Vector3.up *
                Mathf.Clamp(
                    verticalVelocity,
                    -2f,
                    1f
                );

            planarVelocity =
                Vector3.zero;

            verticalVelocity = 0f;
        }
        else
        {
            planarVelocity =
                Vector3.ProjectOnPlane(
                    swimVelocity,
                    Vector3.up
                );

            swimVelocity =
                Vector3.zero;

            verticalVelocity =
                Mathf.Min(
                    verticalVelocity,
                    -1f
                );
        }

        previousSwimming = IsSwimming;
    }

    private void UpdateSwimmingState()
    {
        if(!ShopWaterVolume.TrySurface(transform.position,oceanWater,out float waterHeight))
        { IsSwimming=false; return; }

        float bodyHeight =
            transform.position.y +
            controller.center.y;

        if (IsSwimming)
        {
            IsSwimming =
                bodyHeight <
                waterHeight + 0.30f;
        }
        else
        {
            IsSwimming =
                bodyHeight <
                waterHeight -
                swimEnterDepth;
        }
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 keyboardInput =
            Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                keyboardInput.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                keyboardInput.y -= 1f;

            if (Keyboard.current.dKey.isPressed)
                keyboardInput.x += 1f;

            if (Keyboard.current.aKey.isPressed)
                keyboardInput.x -= 1f;
        }

        Vector2 mobileInput =
            moveJoystick != null
                ? moveJoystick.Value
                : Vector2.zero;

        return mobileInput.sqrMagnitude >
               keyboardInput.sqrMagnitude
            ? mobileInput
            : keyboardInput;
    }

    private bool ReadJumpPressed()
    {
        if(castMode)return false;
        bool keyboardPressed =
            Keyboard.current != null &&
            Keyboard.current.spaceKey
                .wasPressedThisFrame;

        bool mobilePressed =
            jumpButton != null &&
            jumpButton.ConsumePressed();

        return keyboardPressed ||
               mobilePressed;
    }

    private bool ReadSwimUpHeld()
    {
        if(castMode)return false;
        bool keyboardHeld =
            Keyboard.current != null &&
            Keyboard.current.spaceKey
                .isPressed;

        bool mobileHeld =
            jumpButton != null &&
            jumpButton.IsHeld;

        return keyboardHeld ||
               mobileHeld;
    }

    private float ReadKeyboardSwimDownInput()
    {
        if(swimDownButton!=null && swimDownButton.IsHeld)return 1f;
        if (Keyboard.current == null)
            return 0f;

        return Keyboard.current.leftCtrlKey
            .isPressed
            ? 1f
            : 0f;
    }

    private bool WantsToSprint(
        Vector2 movementInput)
    {
        bool keyboardSprint =
            Keyboard.current != null &&
            Keyboard.current.leftShiftKey
                .isPressed;

        if (keyboardSprint)
            return true;

        if (moveJoystick == null)
        {
            mobileSprintHoldTimer = 0f;
            mobileSprintLatched = false;
            return false;
        }

        Vector2 joystickValue =
            moveJoystick.Value;

        float magnitude =
            joystickValue.magnitude;

        if (mobileSprintLatched)
        {
            if (magnitude <
                    mobileSprintReleaseMagnitude ||
                joystickValue.y <
                    mobileSprintReleaseForward)
            {
                mobileSprintLatched = false;
                mobileSprintHoldTimer = 0f;
            }
        }
        else if (
            magnitude >= mobileSprintMagnitude &&
            joystickValue.y >= mobileSprintForward)
        {
            mobileSprintHoldTimer +=
                Time.deltaTime;

            if (mobileSprintHoldTimer >=
                mobileSprintHoldTime)
            {
                mobileSprintLatched = true;
            }
        }
        else
        {
            mobileSprintHoldTimer = 0f;
        }

        return mobileSprintLatched;
    }

    private bool TryGetGroundNormal(
        out Vector3 normal)
    {
        Vector3 origin =
            transform.position +
            Vector3.up * 0.30f;

        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                0.75f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;
            return true;
        }

        normal = Vector3.up;
        return false;
    }

    private void UpdateBoatCameraRocking()
    {
        if(cameraTransform==null)return;

        Quaternion target=Quaternion.identity;
        if(boatPassenger!=null && boatPassenger.Boat!=null)
        {
            // Only inherit pitch and roll. Leave the player's mouse/touch yaw
            // independent of the boat's heading.
            Vector3 localBoatUp=Quaternion.Inverse(transform.rotation)*
                                boatPassenger.Boat.transform.up;
            Quaternion fullTilt=Quaternion.FromToRotation(Vector3.up,localBoatUp);
            target=Quaternion.Slerp(Quaternion.identity,fullTilt,boatCameraTiltAmount);
        }

        // Smooth the interpolated Rigidbody tilt, then ease back to an upright
        // camera when disembarking. No parenting or positional head bobbing.
        float follow=1f-Mathf.Exp(-boatCameraTiltFollowSpeed*Time.deltaTime);
        boatCameraTilt=Quaternion.Slerp(boatCameraTilt,target,follow);
        ApplyCameraLookRotation();
    }

    private void ApplyCameraLookRotation()
    {
        if(cameraTransform!=null)
            cameraTransform.localRotation=boatCameraTilt*
                Quaternion.Euler(cameraPitch,0f,0f);
    }

    private void UpdateCameraFov()
    {
        if (playerCamera == null)
            return;

        float targetFov =
            baseFov +
            (IsSprinting
                ? sprintFovBoost
                : 0f);

        playerCamera.fieldOfView =
            Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFov,
                1f -
                Mathf.Exp(
                    -fovLerpSpeed *
                    Time.deltaTime
                )
            );
    }

    private void HandleLook()
    {
        Vector2 lookDelta =
            Vector2.zero;

        if (!Application.isMobilePlatform &&
            Mouse.current != null)
        {
            lookDelta +=
                Mouse.current.delta
                    .ReadValue() *
                mouseSensitivity;
        }

        TouchLookArea activeLookArea = poseLookArea != null && poseLookArea.isActiveAndEnabled
            ? poseLookArea : touchLookArea;
        if (activeLookArea != null)
        {
            Vector2 touchDelta =
                activeLookArea
                    .ConsumeLookDelta();

            float dpi =
                Screen.dpi > 0f
                    ? Screen.dpi
                    : 160f;

            float dpiScale =
                Mathf.Max(
                    1f,
                    dpi / 160f
                );

            lookDelta +=
                (touchDelta / dpiScale) *
                touchSensitivity;
        }

        if (thirdPersonView == null) thirdPersonView = GetComponent<FishingThirdPersonView>();
        if (thirdPersonView != null && thirdPersonView.IsThirdPerson)
            thirdPersonView.Orbit(lookDelta.x);
        else
            transform.Rotate(Vector3.up * lookDelta.x);

        cameraPitch -=
            lookDelta.y;

        cameraPitch =
            Mathf.Clamp(
                cameraPitch,
                -maxLookAngle,
                maxLookAngle
            );

        ApplyCameraLookRotation();
    }
}

