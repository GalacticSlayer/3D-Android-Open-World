using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FishingSystem : MonoBehaviour
{
    private enum FishingState
    {
        Idle,
        Charging,
        Casting,
        Waiting,
        Bite,
        Fighting
    }

    private enum FishTemperament
    {
        Calm,
        Irritated,
        Angry,
        Fragile,
        // Actual timed combat temperament: Irritated mechanics, inward swimming.
        SirensCall = 5
    }

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private OceanWater oceanWater;
    [SerializeField] private FishingInventory inventory;
    [SerializeField] private Canvas gameplayCanvas;

    [Header("Casting")]
    [SerializeField] private float preferredCastDistance = 16f;
    [SerializeField] private float maximumCastDistance = 30f;
    [SerializeField] private float castDuration = 0.72f;

    [Header("Line + Valid Water")]
    [SerializeField] private float maximumLineDistance = 40f;
    [SerializeField] private float minimumFishingDepth = 0.85f;
    [SerializeField] private float deepWaterSafetyRadius = 0.75f;

    [Header("Bites")]
    [SerializeField] private float minimumBiteDelay = 2.5f;
    [SerializeField] private float maximumBiteDelay = 6.0f;
    [SerializeField] private float hookWindow = 1.35f;

    private FishingHUD hud;
    private FishingBurstDamageRuntime dragDamage;
    private int CurrentDrag => dragDamage!=null && dragDamage.enabled?dragDamage.SelectedDrag:FishingDragRules.Medium;
    private FishingState state;

    private GameObject rodRoot;
    private Transform rodTip;
    private FishingRodView rodView;
    private LineRenderer fishingLine;
    public LineRenderer PresentationLine => fishingLine;
    public OceanWater PresentationWater => oceanWater;
    public Camera PresentationCamera => playerCamera;
    public RawImage PresentationFishStatusIcon => bobberStatusIcon;
    public FishingRodView PresentationRod => rodView;
    private GameObject bobber;

    private GameObject bobberIndicatorRoot;
    private RectTransform bobberIndicatorRect;
    private RawImage bobberStatusIcon;
    private Texture2D[] fishStatusTextures;
    private Texture2D stunnedStatusTexture;
    private float skillStunUntil;
    public bool IsSkillStunned => state == FishingState.Fighting && !fishUnconscious && Time.time < skillStunUntil;

    public void ApplySkillStun()
    {
        if (state != FishingState.Fighting || fishUnconscious) return;
        skillStunUntil = Time.time + FishingDragRules.SkillStunSeconds;
        surgeAmount = 0f;
        fragileBreakTimer = 0f;
        lineBreakTimer = 0f;
        StopLinePullSound();
    }
    private Text bobberIndicatorText;

    private Transform heldFishAnchor;
    private GameObject heldFishVisual;
    private GameObject heldHookVisual;
    private Transform heldHookPoint;
    private Transform heldFishMouthMarker;
    private LineRenderer heldCatchLine;
    private float heldCatchLineTopViewportX = 0.75f;
    private Vector3 heldMouthRestLocalPosition;
    private Quaternion heldFishBaseRotation =
        Quaternion.identity;
    private float heldFishFlopOffset;
    private CharacterController heldFishPlayerController;
    private float heldFishMovementSwing;

    private GameObject unconsciousFishVisual;
    private Transform unconsciousFishMouthMarker;

    private bool rodEquipped = true;
    // Presentation state only; observers never control the local fishing simulation.
    public bool IsRodEquipped => rodEquipped && !shopMode && isActiveAndEnabled;
    public int CastSequence { get; private set; }
    private float networkCastStarted;
    // Observer-only successful water-impact event. Keep its ID across casts:
    // cast releases that hit land must never produce multiplayer splashes.
    public int NetworkCastImpactSequence { get; private set; }
    public Vector3 NetworkCastImpactPoint { get; private set; }
    private float networkCastImpactTime = -1f;
    public float NetworkCastImpactAge => networkCastImpactTime < 0f ? -1f :
        Mathf.Min(60f, Mathf.Max(0f, Time.time - networkCastImpactTime));
    public bool NetworkHasFish => IsRodEquipped && state == FishingState.Fighting;
    public bool NetworkLureInWater => IsRodEquipped && state == FishingState.Waiting && activeBait == ShopCatalog.StarterLure;
    public bool NetworkIsReeling => (NetworkHasFish || NetworkLureInWater) && !(ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen) &&
        hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
    // 0 hidden, 1 retrieving lure; 2 bait and 3 lure-hooked fish use line endpoints only.
    public int NetworkLineKind => !IsRodEquipped || fishingLine==null || !fishingLine.enabled ||
        bobber==null || !bobber.activeSelf || (state!=FishingState.Waiting && state!=FishingState.Fighting)
        ? 0 : activeBait==ShopCatalog.StarterLure ? (state==FishingState.Fighting?3:1) : 2;
    public int NetworkLureVariant => shopProgress!=null ? shopProgress.Data.lureEquipped : 0;
    // Includes final lure line-tie and unconscious fish-mouth presentation adjustments.
    public Vector3 NetworkLineEnd => fishingLine!=null && fishingLine.positionCount>=2
        ? fishingLine.GetPosition(fishingLine.positionCount-1) : GetLineTargetPosition();
    public float NetworkReelTime { get; private set; }
    public float NetworkCastDuration => Mathf.Max(.1f, castDuration);
    public float NetworkCastAge => state == FishingState.Casting
        ? Mathf.Max(0f, Time.time - networkCastStarted) : -1f;

    private Vector3 castPoint;
    private bool pondCast;
    private int castBiome;
    private FishingHotspotManager hotspots;
    private float castHotspotMultiplier = 1f;
    private Vector3 castLandingPoint;
    public int CastBiome => pondCast ? 0 : castBiome;
    public Vector3 CastLandingPoint => castLandingPoint;
    /// <summary>Frozen on successful water landing; never changes as lure or player moves.</summary>
    public bool CastHitHotspot => !pondCast && castHotspotMultiplier > 1.0001f;
    private float chargeStarted,castHintTimer,castDepth;
    private bool castRangeAvailable;
    private float minimumCastPower;
    private readonly bool[] castSamples=new bool[51];
    private int fishMaxHealth=1,fishHealthPoints=1,pendingDamage;
    private float damageFraction,damageDisplayTimer;

    private float stateTimer;
    private float fightTension;
    private float fishHealth;
    private float lineBreakTimer;
    private float fightTime;
    private float surgeAmount;
    private float surgeTimer;
    private float temperamentTimer;
    private float fragileBreakTimer;
    private FishingStatusOutline fragileOutline;
    private bool FragileWarningActive => isActiveAndEnabled && rodEquipped && !shopMode &&
        !(ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen) && state == FishingState.Fighting && !fishUnconscious && !IsSkillStunned &&
        ActiveTemperament == FishTemperament.Fragile && CurrentDrag != FishingDragRules.Low;
    private bool FragileWarningFlashing => FragileWarningActive && fragileBreakTimer >= FishingDragRules.FragileWarningSeconds;
    private bool FragileFlashOn => ((int)(Time.unscaledTime * 16f) & 1) != 0;
    private float initialEscapeBurstTimer;
    private bool fishUnconscious;
    private bool fishOnShore;

    private const float CatchDistance = 1.15f;

    private int hookedSpeciesId;
    private float hookedWeightKg;
    private FishTemperament hookedTemperament;

    private Vector3 fightTravelDirection;
    private float fightMovePhase;

    private static readonly float[] FightMoveAngles =
    {
        0f,
        18f,
        -18f,
        36f,
        -36f,
        54f,
        -54f,
        72f,
        -72f,
        86f,
        -86f
    };

    private static readonly float[] FightMoveStepScales =
    {
        1f,
        0.60f,
        0.32f,
        0.16f
    };

    private const string YellowfinPreviewGrantKey =
        "OpenWorld.YellowfinTunaPreviewGrant.v2";

    private const string GoatfishPreviewGrantKey =
        "OpenWorld.GoatfishPreviewGrant.v1";

    private const string RedSnapperPreviewGrantKey =
        "OpenWorld.RedSnapperPreviewGrant.v1";

    public FishingInventory Inventory => inventory;

    private ShopProgress shopProgress;
    private bool shopMode;
    public AnglerProgression Progression => shopProgress != null ? shopProgress.Data?.angler : null;
    private float ProgressReel => Progression != null ? Progression.Multiplier(2) : 1f;
    public bool IsSirenCalling => dragDamage != null && dragDamage.SirenSeconds > 0f;
    // Siren's Call is a genuine temporary fish temperament, not an image
    // painted over whichever random status happens to be underneath.
    // The underlying temperament is preserved until Siren's Call expires.
    private FishTemperament ActiveTemperament => state == FishingState.Fighting &&
        !fishUnconscious && IsSirenCalling ? FishTemperament.SirensCall : hookedTemperament;
    public float ProgressDifficulty => GetEffectiveFightDifficulty(FishCatalog.Get(hookedSpeciesId), hookedWeightKg);
    private float rodPower=1f, reelPower=1f, lineGuard=1f;
    private float baseCastRange, basePreferredRange, baseLineRange;
    private int activeBait;
    private float originalCastDistance,lureLength,lureRetrieved;
    private Vector3 castOrigin,lureStart,lureEnd;
    private void ApplyShopGear()
    {
        if(shopProgress==null) return;
        var gear=shopProgress.Data;
        rodPower=1f+gear.rodEquipped*0.18f;
        reelPower=Level2FishingReelRuntime.ReelMultiplier(gear.reelEquipped);
        lineGuard=1f+gear.lineEquipped*0.12f;
        float extension=0f; // Cast envelope stays 5–30 m and line stays 40 m for every loadout.
        maximumCastDistance=baseCastRange+extension;
        preferredCastDistance=basePreferredRange+extension*0.6f;
        maximumLineDistance=baseLineRange+extension;
        if(rodRoot!=null)
        {
            foreach(var renderer in rodRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.name=="ReelSeatCollar") continue;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                Color tint=renderer.name=="RodBlank" ? Color.Lerp(Color.white,new Color(0.2f,0.3f,0.33f),gear.rodEquipped/3f) : Color.Lerp(Color.white,new Color(0.94f,0.78f,0.42f),gear.reelEquipped/3f);
                block.SetColor("_BaseColor",tint);renderer.SetPropertyBlock(block);
            }
        }
    }
    private CaughtFishRecord heldRecord;
    public int NetworkHeldSpecies => heldRecord != null && heldFishVisual != null && !rodEquipped && !shopMode
        ? heldRecord.speciesId : -1;
    public float NetworkHeldWeight => NetworkHeldSpecies >= 0 ? heldRecord.weightKg : 0f;
    public bool IsHolding(CaughtFishRecord record) => heldRecord==record && heldFishVisual!=null;
    public void PrepareForMenu()
    {
        if(state!=FishingState.Idle) {CancelFishing();RefreshHandHud();}
    }
    private void RefreshHandHud()
    {
        if(hud==null)return;
        bool ready=rodEquipped && !shopMode;
        hud.SetRodSelected(ready);hud.SetActionVisible(ready);hud.SetActionLabel("CAST");
        hud.SetStatus(ready?"Aim toward open water and cast.":heldRecord!=null?FishCatalog.Get(heldRecord.speciesId).Name+"  "+FishCatalog.FormatWeight(heldRecord.weightKg)+" / "+ShopCatalog.FishLength(heldRecord.speciesId,heldRecord.weightKg).ToString("0.00")+" m":"");
    }
    public void ToggleRod()
    {
        if(shopMode)return;
        if(rodEquipped)UnequipHands();else EquipRod();
    }
    public void UnequipHands()
    {
        CancelFishing();ClearHeldFish();rodEquipped=false;
        if(rodRoot!=null)rodRoot.SetActive(false);
        RefreshHandHud();
    }
    public void PrepareForWorldTravel()
    {
        CancelFishing(); ClearHeldFish();
        if(hud!=null) hud.SetInventoryOpen(false);
    }
    public void SetShopWorld(bool shop)
    {
        shopMode=shop;
        if(shop) { UnequipHands(); }
        else EquipRod();
        RefreshHandHud();
    }
    private void Start()
    {
        InitializeFishingAudio();
        shopProgress=GetComponent<ShopProgress>();
        baseCastRange=30f; basePreferredRange=20f; baseLineRange=40f; maximumCastDistance=baseCastRange; maximumLineDistance=baseLineRange;
        if(shopProgress!=null) { shopProgress.Changed+=ApplyShopGear; ApplyShopGear(); }
        ResolveReferences();

        if (playerCamera == null ||
            oceanWater == null ||
            inventory == null ||
            gameplayCanvas == null)
        {
            Debug.LogWarning(
                "FishingSystem could not resolve all required references."
            );
            enabled = false;
            return;
        }

        hotspots = GetComponent<FishingHotspotManager>();
        if (hotspots == null) hotspots = gameObject.AddComponent<FishingHotspotManager>();

        minimumFishingDepth = 0.6f;
        deepWaterSafetyRadius = 0.75f;

        GrantYellowfinPreviewOnce();
        GrantGoatfishPreviewOnce();
        GrantRedSnapperPreviewOnce();

        CreateRodAndLine();
        ApplyShopGear();
        CreateHeldFishAnchor();

        hud =
            gameplayCanvas
                .gameObject
                .GetComponent<FishingHUD>();

        if (hud == null)
        {
            hud =
                gameplayCanvas
                    .gameObject
                    .AddComponent<FishingHUD>();
        }

        hud.Initialize(this);
        if (hud.ActionInput != null) hud.ActionInput.Released += StopReelSound;
        if (hud.ActionInput != null) hud.ActionInput.Pressed += StopLinePullSound;

        inventory.Changed +=
            hud.RefreshInventory;

        EquipRod();
    }

    private void OnDisable()
    {
        CancelFishing();
    }

    private void OnDestroy()
    {
        if (hud != null && hud.ActionInput != null) hud.ActionInput.Released -= StopReelSound;
        if (hud != null && hud.ActionInput != null) hud.ActionInput.Pressed -= StopLinePullSound;
        if(shopProgress!=null) shopProgress.Changed-=ApplyShopGear;
        if (inventory != null &&
            hud != null)
        {
            inventory.Changed -=
                hud.RefreshInventory;
        }
    }

    private void Update()
    {
        fishPulledLineThisFrame = false;
        if(heldRecord!=null && inventory!=null && !System.Linq.Enumerable.Contains(inventory.Fish,heldRecord))
        {ClearHeldFish();RefreshHandHud();}
        UpdateHeldFishAnimation();
        if(rodEquipped && MustStowForSwimming())
        {UnequipHands();return;}

        if(shopMode || (ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen)) return;
        if (hud == null)
            return;

        if(rodEquipped && (state==FishingState.Idle || state==FishingState.Charging))
        {
            if((castHintTimer-=Time.deltaTime)<=0){castHintTimer=.25f;RefreshCastRange();}
            bool charging=state==FishingState.Charging;
            // Grey idle CAST never queues input. In mode, grey sectors may be
            // selected to cancel immediately with feedback, without rod animation.
            hud.SetActionInteractable(charging || castRangeAvailable);
            if(charging)
            {
                float power=FishingRules.CastPower(Time.time-chargeStarted);
                hud.SetCastPower(true,power,FishingRules.CastDistance(power,maximumCastDistance),castSamples,castRangeAvailable);
                hud.SetCastAvailable(castRangeAvailable && FishingRules.IsCastPowerAvailable(castSamples,power));
            }
            else hud.SetCastAvailable(castRangeAvailable);
        }
        FishingActionButton action =
            hud.ActionInput;

        if (action != null &&
            action.ConsumePressed())
        {
            HandleActionPressed();
        }

        switch (state)
        {
            case FishingState.Waiting:
                UpdateWaiting(action!=null && action.IsHeld);
                break;

            case FishingState.Bite:
                UpdateBite();
                break;

            case FishingState.Fighting:
                UpdateFight(
                    action != null &&
                    action.IsHeld
                );
                break;
        }

        if (NetworkIsReeling) NetworkReelTime += Time.deltaTime;

        if (rodView != null)
        {
            bool fighting =
                rodEquipped &&
                state ==
                    FishingState.Fighting &&
                bobber != null &&
                bobber.activeSelf;

            bool reeling =
                (fighting || (state==FishingState.Waiting && activeBait==ShopCatalog.StarterLure)) &&
                action != null &&
                action.IsHeld;

            if (fighting)
            {
                rodView.SetLinePull(
                    GetLineTargetPosition(),
                    fightTension,
                    reeling,
                    Time.deltaTime
                );
            }
            else
            {
                rodView.RelaxLinePull(
                    Time.deltaTime
                );
            }

            rodView.TickReel(
                reeling,
                Time.deltaTime * reelPower
            );
        }

        UpdateLineAndBobber();
    }

    public void Configure(
        Camera camera,
        OceanWater water,
        FishingInventory fishingInventory,
        Canvas canvas)
    {
        playerCamera = camera;
        oceanWater = water;
        inventory = fishingInventory;
        gameplayCanvas = canvas;
    }

    public void OnPlayerSwimming()
    {if(rodEquipped)UnequipHands();}
    private bool MustStowForSwimming()
    {
        var controller=GetComponent<FirstPersonController>();
        return (controller!=null && controller.IsSwimming) ||
            (playerCamera!=null && oceanWater!=null && oceanWater.IsPointUnderwater(playerCamera.transform.position));
    }
    public void EquipRod()
    {
        var boatPassenger=GetComponent<BoatPassenger>();
        if(boatPassenger!=null && boatPassenger.Seated)return;
        GetComponent<BoatSystem>()?.CancelPlacement();

        if(shopMode)return;

        if(MustStowForSwimming()){UnequipHands();return;}

        CancelFishing();

        rodEquipped = true;

        if (rodRoot != null)
            rodRoot.SetActive(true);

        ClearHeldFish();

        if (hud != null)
        {
            hud.SetRodSelected(true);
            hud.DismissCatchPopup();
            hud.SetActionVisible(true);
            hud.SetActionLabel("CAST");
            hud.SetStatus(
                "Aim toward open water and cast."
            );
            hud.ShowFightMeters(false);
            hud.SetInventoryOpen(false);
        }
    }

    public void HoldFish(int inventoryIndex)
    {
        GetComponent<BoatSystem>()?.CancelPlacement();

        if (inventory == null ||
            inventoryIndex < 0 ||
            inventoryIndex >=
            inventory.Fish.Count)
        {
            return;
        }

        if(IsHolding(inventory.Fish[inventoryIndex])) {UnequipHands();return;}
        CancelFishing();

        rodEquipped = false;

        if (rodRoot != null)
            rodRoot.SetActive(false);

        ClearHeldFish();

        CaughtFishRecord record =
            inventory.Fish[inventoryIndex];

        heldRecord=record;
        heldFishVisual =
            FishWorldSize.Create(
                "Held_" +
                FishCatalog
                    .Get(record.speciesId)
                    .Name,
                heldFishAnchor,
                record.speciesId,
                record.weightKg
            );

        HeroFishAnimator heroAnimator =
            heldFishVisual.GetComponent<HeroFishAnimator>();

        if (heroAnimator != null)
        {
            heroAnimator.SetHeld(true);
        }

        YellowfinTunaPresentation tunaPresentation =
            heldFishVisual.GetComponent<YellowfinTunaPresentation>();

        if (tunaPresentation != null)
        {
            tunaPresentation.SetHeld(true);
        }

        GoatfishPresentation goatfishPresentation =
            heldFishVisual.GetComponent<GoatfishPresentation>();

        if (goatfishPresentation != null)
        {
            goatfishPresentation.SetHeld(true);
        }

        RedSnapperPresentation redSnapperPresentation =
            heldFishVisual.GetComponent<RedSnapperPresentation>();

        if (redSnapperPresentation != null)
        {
            redSnapperPresentation.SetHeld(true);
        }

        MackerelPresentation mackerelPresentation =
            heldFishVisual.GetComponent<MackerelPresentation>();

        if (mackerelPresentation != null)
        {
            mackerelPresentation.SetHeld(true);
        }

        YellowtailPresentation yellowtailPresentation =
            heldFishVisual.GetComponent<YellowtailPresentation>();

        if (yellowtailPresentation != null)
        {
            yellowtailPresentation.SetHeld(true);
        }

        SetupHookedCatchPresentation();

        heldFishFlopOffset =
            Random.Range(
                0f,
                10f
            );

        if (hud != null)
        {
            hud.SetRodSelected(false);
            hud.SetActionVisible(false);
            hud.SetStatus(
                FishCatalog
                    .Get(record.speciesId)
                    .Name +
                "  " +
                FishCatalog.FormatWeight(record.weightKg) +
                " / " + ShopCatalog.FishLength(record.speciesId,record.weightKg).ToString("0.00") + " m"
            );
            hud.ShowFightMeters(false);
            hud.SetInventoryOpen(false);
        }
    }

    private void GrantYellowfinPreviewOnce()
    {
        if (inventory == null)
            return;

        if (PlayerPrefs.GetInt(
                YellowfinPreviewGrantKey,
                0) != 0)
        {
            return;
        }

        inventory.AddFish(
            FishCatalog.YellowfinTunaId,
            14.50f
        );

        PlayerPrefs.SetInt(
            YellowfinPreviewGrantKey,
            1
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Granted one temporary 14.50 kg Yellowfin Tuna preview catch. This one-time development grant will be removed after the imported fish is approved."
        );
    }

    private void GrantGoatfishPreviewOnce()
    {
        if (inventory == null)
            return;

        if (PlayerPrefs.GetInt(
                GoatfishPreviewGrantKey,
                0) != 0)
        {
            return;
        }

        inventory.AddFish(
            FishCatalog.YellowGoatfishId,
            1.25f
        );

        inventory.AddFish(
            FishCatalog.BlackSpotGoatfishId,
            1.10f
        );

        PlayerPrefs.SetInt(
            GoatfishPreviewGrantKey,
            1
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Granted one temporary Yellow Goatfish and one temporary Black Spot Goatfish for animation testing."
        );
    }

    private void GrantRedSnapperPreviewOnce()
    {
        if (inventory == null)
            return;

        if (PlayerPrefs.GetInt(
                RedSnapperPreviewGrantKey,
                0) != 0)
        {
            return;
        }

        inventory.AddFish(
            FishCatalog.RedSnapperId,
            1.75f
        );

        PlayerPrefs.SetInt(
            RedSnapperPreviewGrantKey,
            1
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Granted one temporary 1.75 kg Red Snapper for imported-model testing."
        );
    }

    private void ResolveReferences()
    {
        if (playerCamera == null)
        {
            playerCamera =
                GetComponentInChildren<Camera>();

            if (playerCamera == null)
            {
                playerCamera =
                    FindFirstObjectByType<Camera>();
            }
        }

        if (oceanWater == null)
        {
            oceanWater =
                FindFirstObjectByType<OceanWater>();
        }

        if (inventory == null)
        {
            inventory =
                GetComponent<FishingInventory>();

            if (inventory == null)
            {
                inventory =
                    gameObject.AddComponent<FishingInventory>();
            }
        }

        if (gameplayCanvas == null)
        {
            gameplayCanvas =
                FindFirstObjectByType<Canvas>();
        }
    }

    private AudioSource fishingAudio;
    private FishingLoopAudio reelAudio;
    private FishingLoopAudio linePullAudio, calmLinePullAudio, irritatedLinePullAudio, angryLinePullAudio;
    private AudioClip waterLandingSound;
    private AudioClip castingSound, lineSnapSound, skillSound, sirensCallSound;
    private AudioClip[] decentCatchSounds, largeCatchSounds;
    private bool fishPulledLineThisFrame;
    private int linePullSoundStatus = -1;
    private bool audioFocused = true, audioPaused;
    private AudioClip dragClickSound, biteSplashSound;

    private void InitializeFishingAudio()
    {
        fishingAudio = gameObject.AddComponent<AudioSource>();
        fishingAudio.playOnAwake = false;
        fishingAudio.loop = false;
        fishingAudio.spatialBlend = 0f;
        dragClickSound = Resources.Load<AudioClip>("Fishing/Audio/DragClick");
        biteSplashSound = Resources.Load<AudioClip>("Fishing/Audio/BiteSplash");
        castingSound = Resources.Load<AudioClip>("Fishing/Audio/RodCast");
        skillSound = Resources.Load<AudioClip>("Fishing/Audio/SkillYank");
        sirensCallSound = Resources.Load<AudioClip>("Fishing/Audio/SirensCall");
        lineSnapSound = Resources.Load<AudioClip>("Fishing/Audio/LineSnap");
        decentCatchSounds = new[]
        {
            Resources.Load<AudioClip>("Fishing/Audio/DecentCatch1"),
            Resources.Load<AudioClip>("Fishing/Audio/DecentCatch2"),
            Resources.Load<AudioClip>("Fishing/Audio/DecentCatch3")
        };
        largeCatchSounds = new[]
        {
            Resources.Load<AudioClip>("Fishing/Audio/LargeCatch1"),
            Resources.Load<AudioClip>("Fishing/Audio/LargeCatch2")
        };
        reelAudio = new FishingLoopAudio(gameObject,
            "Fishing/Audio/ReelLeadIn", "Fishing/Audio/ReelLoop");
        waterLandingSound = Resources.Load<AudioClip>("Fishing/Audio/WaterLanding");
        linePullAudio = new FishingLoopAudio(gameObject,
            "Fishing/Audio/LinePullLeadIn", "Fishing/Audio/LinePullLoop", 2f);
        calmLinePullAudio = new FishingLoopAudio(gameObject,
            "Fishing/Audio/LinePullCalmLeadIn", "Fishing/Audio/LinePullCalmLoop", 1.5f);
        irritatedLinePullAudio = new FishingLoopAudio(gameObject,
            "Fishing/Audio/LinePullIrritatedLeadIn", "Fishing/Audio/LinePullIrritatedLoop", 1.5f);
        angryLinePullAudio = new FishingLoopAudio(gameObject,
            "Fishing/Audio/LinePullAngryLeadIn", "Fishing/Audio/LinePullAngryLoop", 1.5f);
    }

    private void StartReelSound()
    {
        StopLinePullSound();
        if (reelAudio == null || !audioFocused || audioPaused ||
            hud == null || hud.ActionInput == null || !hud.ActionInput.IsHeld) return;
        reelAudio.Play();
    }

    private void StopReelSound()
    {
        if (reelAudio != null) reelAudio.Stop();
    }

    private void StopLinePullSound()
    {
        linePullSoundStatus = -1;
        if (linePullAudio != null) linePullAudio.Stop();
        if (calmLinePullAudio != null) calmLinePullAudio.Stop();
        if (irritatedLinePullAudio != null) irritatedLinePullAudio.Stop();
        if (angryLinePullAudio != null) angryLinePullAudio.Stop();
    }

    private void UpdateLinePullSound()
    {
        bool pulling = audioFocused && !audioPaused && rodEquipped && !shopMode &&
            !(ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen) && state == FishingState.Fighting &&
            !fishUnconscious && !IsSkillStunned &&
            hud != null && hud.ActionInput != null && !hud.ActionInput.IsHeld;
        if (!pulling)
        {
            StopLinePullSound();
            return;
        }
        FishTemperament temperament = ActiveTemperament;
        int status = (int)temperament;
        bool statusChanged = linePullSoundStatus != status;
        if (statusChanged) StopLinePullSound();
        // SirensCall shares the Irritated sound profile exactly.
        FishingLoopAudio active = (temperament == FishTemperament.Irritated || temperament == FishTemperament.SirensCall)
            ? irritatedLinePullAudio : temperament == FishTemperament.Angry
            ? angryLinePullAudio : (temperament == FishTemperament.Calm || temperament == FishTemperament.Fragile)
            ? calmLinePullAudio : linePullAudio;
        if (active != null)
        {
            active.SetPitch(1f);
            // Mood recordings use 1.5x gain and their original playback speed.
            // Restart immediately on status changes and stop when reeling resumes.
            if (!active.IsPlaying) active.Play(0d);
            linePullSoundStatus = status;
        }
    }

    private void CheckReelSound()
    {
        // A press during Casting is consumed before the lure reaches Waiting.
        // Reconcile the held input after the cast coroutine/state transitions so
        // that hold starts audio as soon as reeling becomes valid, without a new tap.
        bool canReel = state == FishingState.Fighting || state == FishingState.Bite ||
            (state == FishingState.Waiting && activeBait == ShopCatalog.StarterLure);
        if (!audioFocused || audioPaused || !rodEquipped || shopMode || (ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen) || !canReel ||
            hud == null || hud.ActionInput == null || !hud.ActionInput.IsHeld)
            StopReelSound();
        else if (reelAudio != null && !reelAudio.IsPlaying)
            StartReelSound();
    }

    private void OnApplicationFocus(bool focused)
    {
        audioFocused = focused;
        if (!focused) { StopReelSound(); StopLinePullSound(); }
    }

    private void OnApplicationPause(bool paused)
    {
        audioPaused = paused;
        if (paused) { StopReelSound(); StopLinePullSound(); }
    }

    private void PlayFishingSound(AudioClip clip, float volumeScale = 1f)
    {
        if (fishingAudio != null && clip != null) fishingAudio.PlayOneShot(clip, volumeScale);
    }

    public void PlaySkillFeedback()
    {
        PlayFishingSound(skillSound);
        if (hud != null) hud.PlaySkillFlash();
    }

    public void PlaySirensCallSound()
    {
        // Trigger only on a successful Siren's Call activation (once per fight).
        PlayFishingSound(sirensCallSound);
    }

    private void PlayCatchSound(float weightKg)
    {
        // Exactly 10 kg joins the large-fish group; every landing gets one clip.
        AudioClip[] clips = weightKg < 10f ? decentCatchSounds : largeCatchSounds;
        if (clips != null && clips.Length > 0)
            PlayFishingSound(clips[Random.Range(0, clips.Length)]);
    }

    public void PlayDragNotchSound(int notches)
    {
        StartCoroutine(PlayDragNotches(Mathf.Clamp(notches, 1, 2)));
    }

    private System.Collections.IEnumerator PlayDragNotches(int notches)
    {
        for (int i = 0; i < notches; i++)
        {
            PlayFishingSound(dragClickSound);
            if (i + 1 < notches) yield return new WaitForSecondsRealtime(0.11f);
        }
    }

    private void HandleActionPressed()
    {
        if (!rodEquipped)return;
        if(MustStowForSwimming()){UnequipHands();return;}

        if (state == FishingState.Fighting || state == FishingState.Bite ||
            (state == FishingState.Waiting && activeBait == ShopCatalog.StarterLure))
            StartReelSound();

        switch (state)
        {
            case FishingState.Idle:
                if(inventory.IsFull){hud.SetStatus("FISH BAG FULL (50).");return;}
                RefreshCastRange();if(!castRangeAvailable)return;
                state=FishingState.Charging;chargeStarted=Time.time;
                hud.SetCastCancel(true,CancelCastMode);
                hud.SetStatus("Tap CAST to release. Red casts furthest; grey distances are blocked.");
                break;
            case FishingState.Charging:
                TryCast();
                break;

            case FishingState.Bite:
                StartFight();
                break;
        }
    }

    private void TryCast()
    {
        if(inventory.IsFull) { hud.SetStatus("FISH BAG FULL (50). Sell fish or move them into your Home habitat."); return; }
        if(shopMode) return;
        float power=FishingRules.CastPower(Time.time-chargeStarted);
        RefreshCastRange();
        Vector3 target=ProjectCastPoint(power);
        if(!castRangeAvailable || !FishingRules.IsCastPowerAvailable(castSamples,power) || !CanLandCast(target,true))
        {FailFishing("That cast distance is blocked. Cast cancelled — no bait used.");return;}
        hud.SetCastCancel(false,null);
        hud.SetActionInteractable(true);
        hud.SetCastPower(false,0,0);
        castOrigin=playerCamera.transform.position;
        // Freeze the player's zone when the cast is released, before any movement.
        castBiome=SnapperIslandGeometry.ResolveBiome(transform.position);
        castLandingPoint=target;
        castHotspotMultiplier=1f;
        originalCastDistance=Vector3.ProjectOnPlane(target-castOrigin,Vector3.up).magnitude;
        activeBait=0; // Bait is committed only after a successful water landing.
        StartCoroutine(
            CastRoutine(target)
        );
    }

    private IEnumerator CastRoutine(
        Vector3 target)
    {
        state = FishingState.Casting;
        CastSequence = CastSequence == int.MaxValue ? 1 : CastSequence + 1;
        networkCastStarted = Time.time;
        NetworkReelTime = 0f;
        hud.SetActionLabel("...");
        hud.SetStatus("Casting...");

        bobber.SetActive(true);
        SetBobberVisible(true);
        fishingLine.enabled = true;

        Vector3 start = rodTip.position;
        bool released = false;
        const float releaseProgress = 0.38f;
        float duration = Mathf.Max(0.1f, castDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            if (rodView != null) rodView.SetCastPose(t);
            if (rodView != null && !released && t < releaseProgress)
            {
                bobber.transform.position = rodTip.position;
                SetLinePositions();
                yield return null;
                continue;
            }
            if (!released)
            {
                start = rodTip.position;
                released = true;
                PlayFishingSound(castingSound);
            }
            float flightProgress = rodView != null
                ? Mathf.InverseLerp(releaseProgress, 1f, t)
                : t;

            Vector3 point =
                Vector3.Lerp(
                    start,
                    target,
                    flightProgress
                );

            point.y +=
                Mathf.Sin(
                    flightProgress * Mathf.PI
                ) *
                4.0f;

            Vector3 previousPoint=bobber.transform.position;
            Vector3 segment=point-previousPoint;
            if(segment.sqrMagnitude>.000001f && Physics.Raycast(previousPoint,segment.normalized,segment.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                FailFishing("Cast hit land or an obstacle. Retracted - no bait used.");
                yield break;
            }
            bobber.transform.position = point;

            SetLinePositions();

            yield return null;
        }

        if(!CanLandCast(target))
        {
            FailFishing("Cast landed on land, shallow or locked water. Retracted - no bait used.");
            yield break;
        }
        pondCast=PondWater.Active!=null && PondWater.Active.Contains(target);
        // Capture eligibility once on a successful landing. Retrieving through a
        // hotspot later or swimming across zones must never reroll this catch.
        castHotspotMultiplier=!pondCast && hotspots!=null ? hotspots.LandingMultiplier(target) : 1f;
        TryGetTerrainWaterDepth(target,out _,out _,out castDepth);
        activeBait=shopProgress!=null ? shopProgress.TakeBait() : 0;
        if (rodView != null) rodView.SetCastPose(1f);
        castPoint = target;
        SnapBobberToSurface();
        PlayFishingSound(waterLandingSound, 1.3f);
        // Play the supplied 20-frame animation only for a successful water landing.
        FishingCastSplashAnimation.Spawn(castPoint, oceanWater, playerCamera);
        // A separate, horizontal ripple expands out to a one-metre radius.
        FishingCastRippleEffect.Spawn(castPoint, oceanWater);
        // Publish only after successful water validation, using the identical
        // position as both locally rendered effects. The server simply relays it.
        NetworkCastImpactPoint = castPoint;
        NetworkCastImpactSequence = NetworkCastImpactSequence == int.MaxValue
            ? 1 : NetworkCastImpactSequence + 1;
        networkCastImpactTime = Time.time;

        state = FishingState.Waiting;
        stateTimer =
            Random.Range(
                minimumBiteDelay,
                maximumBiteDelay
            ) * (activeBait==1 ? 0.65f : activeBait==2 ? 0.8f : 1f);

        if(activeBait==ShopCatalog.StarterLure)
        {
            BeginLureRetrieve();hud.SetActionLabel("REEL");
            hud.SetStatus("Hold REEL to work the lure. No bites while resting." + HotspotStatus());yield break;
        }
        hud.SetActionLabel("WAIT");
        hud.SetStatus(
            "Waiting for a bite..." + HotspotStatus()
        );
    }

    private string HotspotStatus() => castHotspotMultiplier > 1f
        ? "  HOTSPOT: +" + Mathf.RoundToInt((castHotspotMultiplier - 1f) * 100f) + "% fish size" : "";

    private int fightBiome;
    private int RollBaitSpecies() => ReefCatalog.Roll(Random.value,activeBait,fightBiome);

    private void BeginLureRetrieve()
    {
        lureStart=castPoint;lureEnd=castPoint;lureRetrieved=0;
        Vector3 end=castOrigin;end.y=lureStart.y;
        // Stop at the first shoreline. Normalize bite opportunity over the
        // usable water retrieve so a full 30 m cast still has a 50% chance.
        for(int i=1;i<=120;i++)
        {
            Vector3 probe=Vector3.Lerp(lureStart,end,i/120f);
            TryGetTerrainWaterDepth(probe,out _,out _,out float depth);
            if(depth<.08f)break;
            lureEnd=probe;
        }
        lureLength=Mathf.Max(.01f,Vector3.ProjectOnPlane(lureStart-lureEnd,Vector3.up).magnitude);
    }
    private void UpdateWaiting(bool reeling)
    {
        SnapBobberToSurface();
        if(activeBait!=ShopCatalog.StarterLure)
        {
            stateTimer-=Time.deltaTime;if(stateTimer<=0)BeginBite();return;
        }
        if(!reeling)return;
        float step=Mathf.Min(lureLength-lureRetrieved,2.4f*reelPower*ProgressReel*Time.deltaTime);
        if(step<=0){FailFishing("Lure retrieved. Cast again.");return;}
        lureRetrieved+=step;
        castPoint=Vector3.Lerp(lureStart,lureEnd,lureRetrieved/lureLength);
        SnapBobberToSurface();
        if(Random.value<FishingRules.LureBiteChance(originalCastDistance,step/lureLength))
        {BeginBite();return;}
        if(lureRetrieved>=lureLength)FailFishing("Lure retrieved. No bite this time — cast again.");
    }
    private void BeginBite()
    {
            PlayFishingSound(biteSplashSound);
            // Species and weight are rolled once from the cast's original zone.
            fightBiome=CastBiome;
            float offshore=IslandExpansionWorld.Active!=null && IslandExpansionWorld.Active.Ready
                ? (fightBiome==2?IslandExpansionWorld.Active.OffshoreAt(castOrigin):0f) : FishingRules.OffshoreFactor;
            hookedSpeciesId =
                RollBaitSpecies();

            hookedWeightKg =
                activeBait==ShopCatalog.StarterLure
                    ? FishingRules.WeightAtCastDistance(hookedSpeciesId,originalCastDistance,Random.value,offshore,fightBiome)
                    : FishingRules.WeightAtDepth(hookedSpeciesId,castDepth,Random.value,offshore,fightBiome);

            if(fightBiome==1)
            {
                var range=FishCatalog.Get(hookedSpeciesId);
                hookedWeightKg=Mathf.Lerp(ReefCatalog.MinimumWeight(hookedSpeciesId,1),ReefCatalog.MaximumWeight(hookedSpeciesId,1),Mathf.InverseLerp(range.MinWeightKg,range.MaxWeightKg,hookedWeightKg));
            }
            if(pondCast)
            {
                var species=FishCatalog.Get(hookedSpeciesId);
                float size=activeBait==ShopCatalog.StarterLure?Mathf.InverseLerp(species.MinWeightKg,species.MaxWeightKg,hookedWeightKg):Random.value;
                hookedWeightKg=FishSizeTable.WeightForLength(hookedSpeciesId,Mathf.Lerp(.05f,.12f,size));
            }

            // One multiplier on the original species' weight, before temperament/HP
            // and the existing depth-quality pass. It may exceed the normal zone cap.
            if(!pondCast) hookedWeightKg*=castHotspotMultiplier;
            hookedWeightKg *= Progression != null ? Progression.Multiplier(5) : 1f;

            hookedSpeciesId=FishCatalog.ResolveSizeVariant(hookedSpeciesId,hookedWeightKg);

            fishUnconscious = false;
            fishOnShore = false;
            fishHealth = 1f;
            lineBreakTimer = 0f;

            hookedTemperament =
                RollTemperament(
                    hookedSpeciesId,
                    hookedWeightKg
                );

            if(activeBait==ShopCatalog.StarterLure)
            {
                // Keep the existing pointer held: reveal the fight meters now,
                // and let the same REEL press continue into the fight.
                StartFight();
                return;
            }

            state =
                FishingState.Bite;

            stateTimer = hookWindow;

            hud.SetActionLabel("HOOK!");
            hud.SetStatus(
                "BITE! Tap HOOK!"
            );
    }

    private void UpdateBite()
    {
        stateTimer -= Time.deltaTime;

        float surface =
            oceanWater.GetSurfaceHeight(
                castPoint
            );

        Vector3 position =
            castPoint;

        position.y =
            surface -
            0.22f +
            Mathf.Sin(
                Time.time * 17f
            ) *
            0.06f;

        bobber.transform.position =
            position;

        if (stateTimer <= 0f)
        {
            FailFishing(
                "Too slow - the fish got away."
            );
        }
    }

    private void StartFight()
    {
        state = FishingState.Fighting;

        fightTension = 0.24f;
        fragileBreakTimer=0f;
        fishMaxHealth=Mathf.RoundToInt(FishingRules.MaxHealth(hookedSpeciesId,hookedWeightKg)*ReefCatalog.HealthMultiplier(fightBiome));
        fishHealthPoints=fishMaxHealth;fishHealth=1f;
        damageFraction=0;pendingDamage=0;damageDisplayTimer=0;
        lineBreakTimer = 0f;
        fightTime = 0f;
        surgeAmount = 0f;
        fishUnconscious = false;
        fishOnShore = false;

        temperamentTimer =
            Random.Range(
                1.6f,
                3.4f
            );

        if(hookedTemperament==FishTemperament.Fragile)temperamentTimer=Random.Range(3.5f,5f);
        initialEscapeBurstTimer =
            1.15f;

        surgeTimer =
            Random.Range(
                1.2f,
                2.3f
            );

        fightMovePhase =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        Vector3 away =
            bobber.transform.position -
            transform.position;

        away.y = 0f;

        if (away.sqrMagnitude <
            0.001f)
        {
            away =
                Vector3.ProjectOnPlane(
                    playerCamera.transform.forward,
                    Vector3.up
                );
        }

        fightTravelDirection =
            away.sqrMagnitude >
                0.001f
                ? away.normalized
                : Vector3.forward;

        hud.SetActionLabel("REEL");
        hud.SetStatus(
            GetTemperamentName() +
            " fish - wear down its health."
        );
        hud.ShowFightMeters(true);
        hud.SetFightMeters(
            fightTension,
            fishHealth, fishMaxHealth
        );
    }

    private void UpdateFight(bool reeling)
    {
        if(dragDamage==null)dragDamage=GetComponent<FishingBurstDamageRuntime>();
        // Activate protection before any reeling/fish resistance is calculated when
        // the hooked fish begins this frame at the equipped line limit.
        if (dragDamage != null && GetCurrentLineDistance() >= maximumLineDistance)
            dragDamage.TryTriggerLineImmortality();
        FishSpeciesDefinition species =
            FishCatalog.Get(
                hookedSpeciesId
            );

        float effectiveDifficulty =
            GetEffectiveFightDifficulty(
                species,
                hookedWeightKg
            );

        float tensionBefore = fightTension;
        fightTime += Time.deltaTime;

        if (IsSkillStunned)
        {
            // Suspend escape, mood timers and tension generation during the stun.
            surgeAmount = 0f;
            fragileBreakTimer = 0f;
            lineBreakTimer = 0f;
            fightTension = Mathf.MoveTowards(fightTension, 0.05f, Time.deltaTime * 0.8f);
            UpdateStunnedBobber(reeling);
        }
        else if (!fishUnconscious)
        {
            initialEscapeBurstTimer =
                Mathf.Max(
                    0f,
                    initialEscapeBurstTimer -
                    Time.deltaTime
                );

            // Freeze the ordinary temperament countdown while Siren's Call is
            // the active status; resume the previous status when its timer ends.
            if (!IsSirenCalling) temperamentTimer -= Time.deltaTime;

            if (temperamentTimer <= 0f && !IsSirenCalling)
            {
                hookedTemperament =
                    RollDifferentTemperament(
                        hookedSpeciesId,
                        hookedWeightKg,
                        hookedTemperament
                    );

                temperamentTimer =
                    Random.Range(
                        1.45f,
                        3.65f
                    );
                fragileBreakTimer=0f;
                if(hookedTemperament==FishTemperament.Fragile)temperamentTimer=Random.Range(3.5f,5f);
            }

            // Two seconds to loosen drag, then actual tension rises instead of
            // an unconditional timer snap. Low clears this hazard immediately.
            float previousFragileTime = fragileBreakTimer;
            fragileBreakTimer=FishingDragRules.FragileTimer(fragileBreakTimer,
                ActiveTemperament==FishTemperament.Fragile,CurrentDrag,Time.deltaTime);
            float fragileTensionGain = FishingDragRules.FragileTensionGain(previousFragileTime, fragileBreakTimer);

            surgeTimer -=
                Time.deltaTime;

            float temperamentTension =
                GetTemperamentTensionMultiplier();

            float temperamentResistance =
                GetTemperamentResistanceMultiplier();

            if (surgeTimer <= 0f)
            {
                surgeAmount =
                    Random.Range(
                        0.42f,
                        0.88f
                    ) *
                    Mathf.Lerp(
                        0.82f,
                        1.34f,
                        effectiveDifficulty
                    ) *
                    temperamentResistance;

                surgeTimer =
                    Random.Range(
                        1.5f,
                        2.9f
                    );
            }

            surgeAmount =
                Mathf.MoveTowards(
                    surgeAmount,
                    0f,
                    Time.deltaTime * 0.55f
                );

            float naturalResistance =
                (
                    0.23f +
                    Mathf.Sin(
                        fightTime *
                        2.2f +
                        hookedSpeciesId
                    ) *
                    0.10f +
                    effectiveDifficulty *
                    0.28f +
                    surgeAmount *
                    0.30f
                ) *
                temperamentResistance;

            if (reeling)
            {
                fightTension +=
                    Time.deltaTime *
                    (
                        0.18f +
                        naturalResistance *
                        0.34f
                    ) *
                    (dragDamage != null && dragDamage.ImmortalitySeconds > 0f ? 0f : 1f) *
                    FishingDragRules.ReelingTensionMultiplier(CurrentDrag) * 1.12f * (fightBiome==1?1.15f:fightBiome==2?1.25f:1f) * temperamentTension / (rodPower * lineGuard);

                float moodHealthFactor =
                    ActiveTemperament ==
                        FishTemperament.Calm
                        ? 1.12f
                        : ActiveTemperament ==
                            FishTemperament.Angry
                            ? 0.78f
                            : 0.96f;

                // The burst owner also handles passive drag and skill damage. Do not
                // let legacy ticks trigger a premature KO before it runs this frame.
                if(dragDamage==null || !dragDamage.enabled)
                {
                    damageFraction+=Time.deltaTime*8.5f*moodHealthFactor*reelPower;
                    int damage=Mathf.Min(fishHealthPoints,Mathf.FloorToInt(damageFraction));
                    damageFraction-=damage;fishHealthPoints-=damage;pendingDamage+=damage;
                    fishHealth=fishHealthPoints/(float)fishMaxHealth;
                }
            }
            else if(CurrentDrag!=FishingDragRules.High && fragileBreakTimer<=FishingDragRules.FragileWarningSeconds)
            {
                fightTension -=
                    Time.deltaTime *
                    Mathf.Lerp(
                        0.52f,
                        0.32f,
                        effectiveDifficulty
                    ) * FishingDragRules.RecoveryMultiplier(CurrentDrag);
            }

            fightTension +=
                naturalResistance *
                0.032f *
                Time.deltaTime * FishingDragRules.TensionMultiplier(CurrentDrag);
            if(CurrentDrag==FishingDragRules.High)
                fightTension+=Time.deltaTime*FishingDragRules.HighTensionPerSecond;
            fightTension += fragileTensionGain;

            fishHealth =
                Mathf.Clamp01(
                    fishHealth
                );

            damageDisplayTimer+=Time.deltaTime;
            if(pendingDamage>0 && (damageDisplayTimer>=.35f || fishHealthPoints==0))
            {
                hud.ShowDamage(pendingDamage,FishingThirdPersonView.WorldToScreenPoint(playerCamera, bobber.transform.position));
                pendingDamage=0;damageDisplayTimer=0;
            }
            if (fishHealth <= 0f)
            {
                fishHealth = 0f;
                fishUnconscious = true;
                surgeAmount = 0f;
                lineBreakTimer = 0f;

                EnsureUnconsciousFishVisual();
                fightTension =
                    Mathf.Min(
                        fightTension,
                        0.18f
                    );

                hud.SetStatus(
                    "UNCONSCIOUS - reel it in."
                );
            }
            else
            {
                UpdateFightBobber(
                    effectiveDifficulty,
                    reeling
                );
            }
        }
        else
        {
            surgeAmount = 0f;

            fightTension =
                Mathf.MoveTowards(
                    fightTension,
                    0.05f,
                    Time.deltaTime * 0.80f
                );

            if (reeling)
            {
                UpdateUnconsciousBobber();
            }
            else
            {
                SnapCurrentBobberToSurface();
            }

            UpdateUnconsciousFishVisual();
        }

        // +100% control halves positive tension gain; resting recovery is unchanged.
        if(fightTension > tensionBefore && Progression != null)
            fightTension = tensionBefore + (fightTension - tensionBefore) / Progression.Multiplier(3);
        fightTension =
            Mathf.Clamp01(
                fightTension
            );

        float lineDistance =
            GetCurrentLineDistance();

        // A fish can cross the limit during UpdateFightBobber this same frame.
        // Activate before the distance snap and undo that frame's tension gain.
        if (lineDistance >= maximumLineDistance && dragDamage != null)
            dragDamage.TryTriggerLineImmortality();
        bool lineProtected = dragDamage != null && dragDamage.LineImmortalityActive;
        if (lineProtected)
        {
            fightTension = Mathf.Min(fightTension, tensionBefore);
            lineBreakTimer = 0f;
        }

        if (lineDistance >=
            maximumLineDistance && !lineProtected)
        {
            fightTension = 1f;

            hud.SetFightMeters(
                fightTension,
                fishHealth, fishMaxHealth
            );

            UpdateBobberIndicator();

            SnapLine(
                "SNAP! Maximum line distance reached."
            );

            return;
        }

        if (!lineProtected && !fishUnconscious && !IsSkillStunned &&
            fightTension >= 0.985f)
        {
            lineBreakTimer +=
                Time.deltaTime;
        }
        else
        {
            lineBreakTimer =
                Mathf.Max(
                    0f,
                    lineBreakTimer -
                    Time.deltaTime * 2f
                );
        }

        hud.SetFightMeters(
            fightTension,
            fishHealth, fishMaxHealth
        );

        if (IsSkillStunned)
        {
            hud.SetStatus("STUNNED - reel it in!");
        }
        else if (!fishUnconscious)
        {
            hud.SetStatus(
                reeling
                    ? GetTemperamentName() +
                      " - reducing fish health."
                    : GetTemperamentName() +
                      (CurrentDrag==FishingDragRules.High?" - HIGH DRAG: tension rising.":" - resting the line.")
            );
        }
        else
        {
            hud.SetStatus(
                reeling
                    ? "UNCONSCIOUS - pulling it toward you."
                    : "UNCONSCIOUS - hold REEL to retrieve it."
            );
        }

        if (!lineProtected && lineBreakTimer >= 0.55f)
        {
            SnapLine(
                "SNAP! Too much tension."
            );

            return;
        }

        if (fishUnconscious &&
            fishOnShore)
        {
            CatchFish(
                true
            );

            return;
        }

        if (fishUnconscious &&
            GetRemainingCatchDistance() <=
                0.01f)
        {
            CatchFish(
                false
            );
        }
    }

    private void CatchFish(
        bool shoreCatch)
    {
        // AddFish records the new best, so capture the comparison beforehand.
        // The saved ledger retains records even after a fish is sold or housed.
        bool personalBest=false;
        if(shopProgress!=null && shopProgress.Data!=null)
        {
            var data=shopProgress.Data;
            data.EnsureCatchStats();
            int species=FishCatalog.CanonicalId(hookedSpeciesId);
            personalBest=species>=0 && species<data.personalBestKg.Length &&
                hookedWeightKg>data.personalBestKg[species];
        }

        int newIndex =
            inventory.AddFish(
                hookedSpeciesId,
                hookedWeightKg, true
            );

        if(newIndex<0) { FailFishing("FISH BAG FULL (50). Fish released. Sell or house fish to make room."); return; }

        CaughtFishRecord record =
            inventory.Fish[newIndex];
        if(!pondCast)GetComponent<FishingTournament>()?.ReportCatch(record,CastBiome,CastSequence);

        int oldLevel = Progression != null ? Progression.level : 1;
        int earnedXp = shopProgress != null ? shopProgress.AwardFishingXp(fishMaxHealth, ProgressDifficulty) : 0;
        string xpCaption = earnedXp > 0 ? "  +" + earnedXp + " XP" : "";
        if(Progression != null && Progression.level > oldLevel) xpCaption += "  LEVEL " + Progression.level + "!";
        ResetLine();

        state = FishingState.Idle;

        hud.ShowFightMeters(false);
        hud.ShowCatch(
            record,
            shoreCatch
                ? "LANDED ON SHORE!" + xpCaption
                : "CAUGHT!" + xpCaption,
            personalBest
        );

        HoldFish(newIndex);
        PlayCatchSound(record.weightKg);
    }

    private void SnapLine(string message)
    {
        PlayFishingSound(lineSnapSound);
        FailFishing(message);
    }

    private void FailFishing(string message)
    {
        ResetLine();

        state = FishingState.Idle;

        hud.ShowFightMeters(false);
        hud.SetActionLabel("CAST");
        hud.SetStatus(message);
    }

    private void CancelFishing()
    {
        StopReelSound();
        StopLinePullSound();
        StopAllCoroutines();
        if (rodView != null) rodView.ResetMotion();
        if (hud != null && hud.ActionInput != null) hud.ActionInput.ResetInput();

        state = FishingState.Idle;

        ResetLine();

        if (hud != null)
        {
            hud.ShowFightMeters(false);
        }
    }

    public void CancelCastMode()
    {
        if(state!=FishingState.Charging)return;
        FailFishing("Cast cancelled — no bait used.");
    }
    private void RefreshCastRange()
    {
        for(int i=0;i<castSamples.Length;i++)
            castSamples[i]=CanLandCast(ProjectCastPoint(i/(float)(castSamples.Length-1)),true);
        castRangeAvailable=FishingRules.ContinuousCastRange(castSamples,out minimumCastPower);
    }
    private Vector3 ProjectCastPoint(float power)
    {
        Vector3 direction=Vector3.ProjectOnPlane(playerCamera.transform.forward,Vector3.up);
        if(direction.sqrMagnitude<.001f)direction=transform.forward;
        Vector3 target=playerCamera.transform.position+direction.normalized*FishingRules.CastDistance(power,maximumCastDistance);
        TryGetTerrainWaterDepth(target,out float ground,out float water,out _);
        target.y=Mathf.Max(ground+.08f,water+.06f);
        return target;
    }
    private bool CanLandCast(Vector3 target,bool checkFlight=false)
    {
        if((ReefZone.Active!=null && !ReefZone.Active.Contains(target)) || !IsValidFishingWater(target))return false;
        if(checkFlight && rodTip!=null)
        {
            Vector3 start=rodTip.position,previous=start;
            for(int i=1;i<=8;i++)
            {
                float t=i/8f;Vector3 point=Vector3.Lerp(start,target,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*4f);
                if(Physics.Linecast(previous,point,~0,QueryTriggerInteraction.Ignore))return false;
                previous=point;
            }
        }
        return true;
    }

    private bool IsValidFishingWater(
        Vector3 worldPosition)
    {
        if (!SnapperRockFishObstacles.SurfacePointClear(worldPosition,.40f) ||
            !BrinebreakRockRegion.SurfacePointClear(worldPosition,.40f) ||
            !BluewaterCayRockCollision.SurfacePointClear(worldPosition,.40f) ||
            !SuncrestMainRockCollision.SurfacePointClear(worldPosition,.40f) ||
            !HasFishingDepth(
                worldPosition,
                minimumFishingDepth))
        {
            return false;
        }

        float radius =
            Mathf.Max(
                0f,
                deepWaterSafetyRadius
            );

        if (radius <= 0f)
            return true;

        Vector3 right =
            Vector3.right *
            radius;

        Vector3 forward =
            Vector3.forward *
            radius;

        return
            HasFishingDepth(
                worldPosition + right,
                minimumFishingDepth * 0.85f
            ) &&
            HasFishingDepth(
                worldPosition - right,
                minimumFishingDepth * 0.85f
            ) &&
            HasFishingDepth(
                worldPosition + forward,
                minimumFishingDepth * 0.85f
            ) &&
            HasFishingDepth(
                worldPosition - forward,
                minimumFishingDepth * 0.85f
            );
    }

    private bool HasFishingDepth(
        Vector3 worldPosition,
        float requiredDepth)
    {
        if (!WorldTerrain.TryGetHeight(worldPosition, out float ground))
            return true;

        float water =
            oceanWater.GetSurfaceHeight(
                worldPosition
            );

        return
            water -
            ground >=
            Mathf.Max(
                0.25f,
                requiredDepth
            );
    }

    private float GetEffectiveFightDifficulty(
        FishSpeciesDefinition species,
        float weightKg)
    {
        var castQuality = GetComponent<FishingCastQualityRuntime>();
        float fightQuality = castQuality != null ? castQuality.DifficultyMultiplier : 1f;
        if(castQuality != null && castQuality.OriginalWeight > 0f)
            weightKg = castQuality.OriginalWeight;
        float sizeDifficulty =
            Mathf.InverseLerp(
                species.MinWeightKg,
                species.MaxWeightKg,
                weightKg
            );

        // Match Red Snapper at the same size percentile, then multiply AFTER
        // its normal clamp. Raising the base stat alone caps large groupers.
        if(hookedSpeciesId==FishCatalog.PantherGrouperId)
            return Mathf.Clamp01(FishCatalog.Get(FishCatalog.RedSnapperId).Difficulty *
                (pondCast ? .20f : .74f) + sizeDifficulty * .26f) * fightQuality * 2.5f;

        if(hookedSpeciesId==FishCatalog.CoralGrouperId)
            return Mathf.Clamp01(FishCatalog.Get(FishCatalog.RedSnapperId).Difficulty *
                (pondCast ? .20f : .74f) + sizeDifficulty * .26f) * fightQuality * 3f;

        return
            Mathf.Clamp01(
                species.Difficulty *
                (pondCast ? 0.20f : 0.74f) +
                sizeDifficulty *
                0.26f
            ) * fightQuality * (hookedSpeciesId==FishCatalog.BattleScarredMakoSharkId?1.5f:1f);
    }

    private FishTemperament RollTemperament(
        int speciesId,
        float weightKg)
    {
        if(Random.value<.15f)return FishTemperament.Fragile;
        GetTemperamentWeights(
            speciesId,
            weightKg,
            out float calmWeight,
            out float irritatedWeight,
            out float angryWeight
        );

        float total =
            calmWeight +
            irritatedWeight +
            angryWeight;

        float roll =
            Random.value *
            total;

        if (roll <
            calmWeight)
        {
            return
                FishTemperament.Calm;
        }

        roll -=
            calmWeight;

        if (roll <
            irritatedWeight)
        {
            return
                FishTemperament.Irritated;
        }

        return
            FishTemperament.Angry;
    }

    private FishTemperament RollDifferentTemperament(
        int speciesId,
        float weightKg,
        FishTemperament current)
    {
        for (int i = 0;
             i < 5;
             i++)
        {
            FishTemperament next =
                RollTemperament(
                    speciesId,
                    weightKg
                );

            if (next != current)
            {
                return next;
            }
        }

        // Guarantee a visible mood change even after repeated weighted rolls.
        if (current ==
            FishTemperament.Calm)
        {
            return
                FishTemperament.Irritated;
        }

        if (current ==
            FishTemperament.Angry)
        {
            return
                FishTemperament.Irritated;
        }

        return
            Random.value < 0.5f
                ? FishTemperament.Calm
                : FishTemperament.Angry;
    }

    private void GetTemperamentWeights(
        int speciesId,
        float weightKg,
        out float calm,
        out float irritated,
        out float angry)
    {
        // Species signatures. These are intentionally different even before
        // size is considered, so each fish family has a recognizable fight.
        switch (speciesId)
        {
            case FishCatalog.RedSnapperId:
                calm = 0.22f;
                irritated = 0.48f;
                angry = 0.30f;
                break;

            case FishCatalog.YellowfinTunaId:
                calm = 0.08f;
                irritated = 0.28f;
                angry = 0.64f;
                break;

            case FishCatalog.YellowGoatfishId:
                calm = 0.64f;
                irritated = 0.33f;
                angry = 0.03f;
                break;

            case FishCatalog.BlackSpotGoatfishId:
                calm = 0.68f;
                irritated = 0.29f;
                angry = 0.03f;
                break;

            case 3: // Yellowtail
                calm = 0.28f;
                irritated = 0.45f;
                angry = 0.27f;
                break;

            case 2: // Sea Bass
                calm = 0.42f;
                irritated = 0.43f;
                angry = 0.15f;
                break;

            default: // Blue Mackerel / fallback
                calm = 0.70f;
                irritated = 0.27f;
                angry = 0.03f;
                break;
        }

        FishSpeciesDefinition species =
            FishCatalog.Get(
                speciesId
            );

        float size =
            Mathf.InverseLerp(
                species.MinWeightKg,
                species.MaxWeightKg,
                weightKg
            );

        // Large individuals spend more of the fight angry, but every species
        // keeps a non-zero chance to switch through all three moods.
        float angryShift =
            size *
            Mathf.Lerp(
                0.08f,
                0.24f,
                species.Difficulty
            );

        calm =
            Mathf.Max(
                0.05f,
                calm -
                angryShift *
                0.72f
            );

        irritated =
            Mathf.Max(
                0.08f,
                irritated -
                angryShift *
                0.28f
            );

        angry =
            Mathf.Max(
                0.02f,
                angry +
                angryShift
            );
    }

    private float GetTemperamentTensionMultiplier()
    {
        switch (ActiveTemperament)
        {
            case FishTemperament.Calm:
                return 0.55f;

            case FishTemperament.Angry:
                return 2.0f;

            default:
                return 1f;
        }
    }

    private float GetTemperamentResistanceMultiplier()
    {
        switch (ActiveTemperament)
        {
            case FishTemperament.Calm:
                return 0.82f;

            case FishTemperament.Angry:
                return 1.28f;

            default:
                return 1f;
        }
    }

    private string GetTemperamentName()
    {
        if (fishUnconscious)
            return "UNCONSCIOUS";

        switch (ActiveTemperament)
        {
            case FishTemperament.SirensCall:
                return "CONTROLLED";
            case FishTemperament.Fragile:
                return "FRAGILE • LOW DRAG";
            case FishTemperament.Calm:
                return "CALM";

            case FishTemperament.Angry:
                return "ANGRY";

            default:
                return "IRRITATED";
        }
    }

    private Color GetTemperamentColor()
    {
        if (fishUnconscious)
        {
            return
                new Color(
                    0.34f,
                    0.56f,
                    0.72f,
                    0.94f
                );
        }

        switch (ActiveTemperament)
        {
            case FishTemperament.Fragile:
                return new Color(.55f,.16f,.78f,.96f);
            case FishTemperament.Calm:
                return
                    new Color(
                        0.18f,
                        0.78f,
                        0.30f,
                        0.92f
                    );

            case FishTemperament.Angry:
                return
                    new Color(
                        0.94f,
                        0.16f,
                        0.10f,
                        0.94f
                    );

            default:
                return
                    new Color(
                        0.62f,
                        0.43f,
                        0.035f,
                        0.94f
                    );
        }
    }

    private void UpdateFightBobber(
        float effectiveDifficulty,
        bool reeling)
    {
        if (bobber == null ||
            !bobber.activeSelf)
        {
            return;
        }

        Vector3 current =
            bobber.transform.position;

        Vector3 away =
            current -
            transform.position;

        away.y = 0f;

        if (away.sqrMagnitude <
            0.001f)
        {
            away =
                fightTravelDirection;
        }

        if (away.sqrMagnitude <
            0.001f)
        {
            away =
                Vector3.ProjectOnPlane(
                    playerCamera.transform.forward,
                    Vector3.up
                );
        }

        away =
            away.sqrMagnitude >
                0.001f
                ? away.normalized
                : Vector3.forward;

        Vector3 side =
            Vector3.Cross(
                Vector3.up,
                away
            ).normalized;

        float weave =
            Mathf.Sin(
                fightTime *
                Mathf.Lerp(
                    0.85f,
                    1.45f,
                    effectiveDifficulty
                ) +
                fightMovePhase
            );

        float surgeWeave =
            Mathf.Sin(
                fightTime * 2.15f +
                fightMovePhase * 0.63f
            ) *
            surgeAmount *
            0.34f;

        Vector3 outwardDirection =
            (
                away +
                side *
                (
                    weave * 0.72f +
                    surgeWeave
                )
            ).normalized;

        float moodSwimMultiplier;

        switch (ActiveTemperament)
        {
            case FishTemperament.Calm:
                moodSwimMultiplier = FishingTuning.GreenToYellowSpeed;
                break;

            case FishTemperament.Angry:
                moodSwimMultiplier = FishingTuning.RedToYellowSpeed;
                break;

            default:
                moodSwimMultiplier = 1f;
                break;
        }

        float initialBurstMultiplier =
            initialEscapeBurstTimer > 0f
                ? Mathf.Lerp(
                    1f,
                    1.34f,
                    initialEscapeBurstTimer /
                    1.15f
                )
                : 1f;

        float outwardSpeed =
            FishingTuning.YellowSpeed(hookedSpeciesId,effectiveDifficulty) *
            FishingDragRules.EscapeMultiplier(CurrentDrag) *
            moodSwimMultiplier *
            initialBurstMultiplier *
            (
                1f +
                surgeAmount * 0.28f
            );

        // Purple status has a fixed fraction of base speed, independent of drag,
        // surges or the initial escape burst.
        if(ActiveTemperament==FishTemperament.Fragile)
            outwardSpeed=FishingTuning.YellowSpeed(hookedSpeciesId,effectiveDifficulty)*.2f;
        Vector3 velocity =
            outwardDirection *
            outwardSpeed;

        if (reeling)
        {
            Vector3 toPlayer =
                transform.position -
                current;

            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude >
                0.001f)
            {
                toPlayer.Normalize();

                float inwardGain;

                switch (ActiveTemperament)
                {
                    case FishTemperament.Calm:
                        inwardGain = 1.05f;
                        break;

                    case FishTemperament.Angry:
                        inwardGain = 0.48f;
                        break;

                    default:
                        inwardGain = 0.72f;
                        break;
                }

                // Cancel only the actual outward radial component,
                // not the fish's lateral/weaving motion. This gives a stable
                // slow retrieval rate while the fish still visibly swims.
                float radialOutwardSpeed =
                    Mathf.Max(
                        0f,
                        Vector3.Dot(
                            velocity,
                            away
                        )
                    );

                float reelPullSpeed =
                    radialOutwardSpeed +
                    inwardGain * reelPower * ProgressReel;

                velocity +=
                    toPlayer *
                    reelPullSpeed;
            }
        }

        if (IsSirenCalling)
        {
            // Siren's Call overrides escape + reel velocity with a FIXED 2 m/s
            // straight toward the angler for its timed duration. Keep the
            // existing valid-water and shoreline collision movement below.
            Vector3 toPlayer = transform.position - current;
            toPlayer.y = 0f;
            velocity = toPlayer.sqrMagnitude > .0001f
                ? toPlayer.normalized * 2f : Vector3.zero;
        }
        if (velocity.sqrMagnitude <
            0.001f)
        {
            SnapCurrentBobberToSurface();
            return;
        }

        Vector3 movementDirection =
            velocity.normalized;

        float movementDistance =
            velocity.magnitude *
            Time.deltaTime;

        bool requireAway =
            !reeling && !IsSirenCalling;

        if (TryMoveBobberInValidWater(
                current,
                movementDirection,
                away,
                movementDistance,
                requireAway,
                out Vector3 next))
        {
            bobber.transform.position =
                next;

            // Compare before/after fish movement against the same player position:
            // walking away or wave bobbing must not count as a fish taking line.
            Vector3 before = current - transform.position;
            Vector3 after = next - transform.position;
            before.y = after.y = 0f;
            fishPulledLineThisFrame = !reeling && after.sqrMagnitude > before.sqrMagnitude + 0.000001f;

            fightTravelDirection =
                outwardDirection;
        }
        else
        {
            current.y =
                oceanWater.GetSurfaceHeight(
                    current
                ) -
                0.08f;

            bobber.transform.position =
                current;
        }

        castPoint =
            bobber.transform.position;
    }

    private bool TryMoveBobberInValidWater(
        Vector3 current,
        Vector3 desiredDirection,
        Vector3 away,
        float distance,
        bool requireAway,
        out Vector3 next)
    {
        desiredDirection.y = 0f;
        away.y = 0f;

        if (desiredDirection.sqrMagnitude <
            0.001f)
        {
            desiredDirection =
                away;
        }

        if (away.sqrMagnitude <
            0.001f)
        {
            away =
                desiredDirection;
        }

        desiredDirection.Normalize();
        away.Normalize();

        float requestedDistance =
            Mathf.Max(
                0f,
                distance
            );

        // First try the desired swimming direction with progressively smaller
        // steps. Fast fish therefore slide along a water boundary instead of
        // freezing because one large frame step crossed into shallow water.
        for (int scaleIndex = 0;
             scaleIndex <
                 FightMoveStepScales.Length;
             scaleIndex++)
        {
            float stepDistance =
                requestedDistance *
                FightMoveStepScales[
                    scaleIndex
                ];

            for (int i = 0;
                 i < FightMoveAngles.Length;
                 i++)
            {
                Vector3 direction =
                    Quaternion.AngleAxis(
                        FightMoveAngles[i],
                        Vector3.up
                    ) *
                    desiredDirection;

                if (TryValidateFightMove(
                        current,
                        direction,
                        away,
                        stepDistance,
                        requireAway,
                        out next))
                {
                    return true;
                }
            }
        }

        // If the weave direction is boxed in near shore, explicitly search
        // around the true away-from-player vector. This keeps an angry fish
        // moving immediately rather than waiting for the sine weave to point
        // somewhere valid again.
        if (requireAway)
        {
            for (int scaleIndex = 0;
                 scaleIndex <
                     FightMoveStepScales.Length;
                 scaleIndex++)
            {
                float stepDistance =
                    requestedDistance *
                    FightMoveStepScales[
                        scaleIndex
                    ];

                for (int i = 0;
                     i < FightMoveAngles.Length;
                     i++)
                {
                    Vector3 direction =
                        Quaternion.AngleAxis(
                            FightMoveAngles[i],
                            Vector3.up
                        ) *
                        away;

                    if (TryValidateFightMove(
                            current,
                            direction,
                            away,
                            stepDistance,
                            true,
                            out next))
                    {
                        return true;
                    }
                }
            }
        }

        next = current;
        return false;
    }

    private bool TryValidateFightMove(
        Vector3 current,
        Vector3 direction,
        Vector3 away,
        float distance,
        bool requireAway,
        out Vector3 next)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <
            0.001f ||
            distance <= 0f)
        {
            next = current;
            return false;
        }

        direction.Normalize();

        // Allow almost-tangential motion so fish can slide along shore/depth
        // boundaries, but never let a free-swimming fish move back at player.
        if (requireAway &&
            Vector3.Dot(
                direction,
                away
            ) <
            -0.02f)
        {
            next = current;
            return false;
        }

        Vector3 candidate =
            current +
            direction *
            distance;

        candidate.y =
            oceanWater.GetSurfaceHeight(
                candidate
            ) -
            0.08f;

        if (!WorldTerrain.SurfaceSegmentClear(current,candidate,oceanWater,.40f) ||
            !MarinaDockExtensionRuntime.WaterSegmentClear(current,candidate,.25f) ||
            !SnapperRockFishObstacles.SurfaceSegmentClear(current,candidate,.40f) ||
            !BrinebreakRockRegion.SurfaceSegmentClear(current,candidate,.40f) ||
            !BluewaterCayRockCollision.SurfaceSegmentClear(current,candidate,.40f) ||
            !SuncrestMainRockCollision.SurfaceSegmentClear(current,candidate,.40f) ||
            !IsValidFishingWater(candidate))
        {
            next = current;
            return false;
        }

        next = candidate;
        return true;
    }

    private float GetCurrentLineDistance()
    {
        if (rodTip == null ||
            bobber == null)
        {
            return 0f;
        }

        return
            Vector3.Distance(
                rodTip.position,
                GetLineTargetPosition()
            );
    }

    private float GetRemainingCatchDistance()
    {
        if (bobber == null)
            return 0f;

        Vector3 delta =
            GetLineTargetPosition() -
            transform.position;

        delta.y = 0f;

        return
            Mathf.Max(
                0f,
                delta.magnitude -
                CatchDistance
            );
    }

    private void UpdateUnconsciousBobber()
    {
        if (bobber == null ||
            !bobber.activeSelf)
        {
            return;
        }

        Vector3 current =
            bobber.transform.position;

        Vector3 mouthPosition =
            GetLineTargetPosition();

        Vector3 toPlayer =
            transform.position -
            mouthPosition;

        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        Vector3 direction =
            toPlayer.normalized;

        Vector3 candidate =
            current +
            direction *
            (
                3.70f * reelPower * ProgressReel *
                Time.deltaTime
            );

        // Treat a subdued fish reaching any authored island rock exactly like reaching
        // shallow terrain: stop at the obstacle and complete the existing catch.
        if (!WorldTerrain.SurfaceSegmentClear(current,candidate,oceanWater,.40f) ||
            !SnapperRockFishObstacles.SurfaceSegmentClear(current,candidate,.40f) ||
            !BrinebreakRockRegion.SurfaceSegmentClear(current,candidate,.40f) ||
            !BluewaterCayRockCollision.SurfaceSegmentClear(current,candidate,.40f) ||
            !SuncrestMainRockCollision.SurfaceSegmentClear(current,candidate,.40f))
        {
            fishOnShore = true;
            return;
        }
        if (TryGetTerrainWaterDepth(
                candidate,
                out float ground,
                out float water,
                out float depth))
        {
            if (depth <= 0.08f)
            {
                candidate.y =
                    ground +
                    0.07f;

                bobber.transform.position =
                    candidate;

                castPoint =
                    candidate;

                fishOnShore = true;

                hud.SetStatus(
                    "UNCONSCIOUS - landed on shore!"
                );

                return;
            }

            candidate.y =
                water +
                0.03f;
        }
        else
        {
            candidate.y =
                oceanWater.GetSurfaceHeight(
                    candidate
                ) +
                0.03f;
        }

        bobber.transform.position =
            candidate;

        castPoint =
            candidate;
    }

    private void SnapCurrentBobberToSurface()
    {
        if (bobber == null ||
            !bobber.activeSelf ||
            fishOnShore)
        {
            return;
        }

        Vector3 position =
            bobber.transform.position;

        position.y =
            oceanWater.GetSurfaceHeight(
                position
            ) +
            0.03f;

        bobber.transform.position =
            position;

        castPoint =
            position;
    }

    private void UpdateStunnedBobber(bool reeling)
    {
        if (bobber == null || !bobber.activeSelf) return;
        if (reeling)
        {
            Vector3 current = bobber.transform.position;
            Vector3 toPlayer = transform.position - current;
            toPlayer.y = 0f;
            float step = Mathf.Min(0.72f * reelPower * ProgressReel * Time.deltaTime, Mathf.Max(0f, toPlayer.magnitude - CatchDistance));
            if (TryMoveBobberInValidWater(current, toPlayer.normalized, -toPlayer.normalized, step, false, out Vector3 next))
                bobber.transform.position = next;
        }
        SnapCurrentBobberToSurface();
    }

    private bool TryGetTerrainWaterDepth(
        Vector3 worldPosition,
        out float ground,
        out float water,
        out float depth)
    {
        water =
            oceanWater.GetSurfaceHeight(
                worldPosition
            );

        ground =
            water -
            1000f;

        depth = 1000f;

        if (!WorldTerrain.TryGetHeight(worldPosition, out float sampledGround))
            return false;
        ground = sampledGround;

        depth =
            water -
            ground;

        return true;
    }

    private void SnapBobberToSurface()
    {
        if (bobber == null ||
            !bobber.activeSelf)
        {
            return;
        }

        Vector3 position =
            castPoint;

        position.y =
            oceanWater.GetSurfaceHeight(
                castPoint
            ) +
            0.06f;

        position.y +=
            Mathf.Sin(
                Time.time * 2.5f
            ) *
            0.035f;

        bobber.transform.position =
            position;
    }

    private void UpdateLineAndBobber()
    {
        if (fishingLine == null ||
            !fishingLine.enabled ||
            bobber == null ||
            !bobber.activeSelf)
        {
            if (bobberIndicatorRoot != null)
            {
                bobberIndicatorRoot.SetActive(
                    false
                );
            }

            return;
        }

        SetLinePositions();
        UpdateBobberIndicator();
    }

    private void CreateBobberIndicator()
    {
        if (bobberIndicatorRoot != null) Destroy(bobberIndicatorRoot);
        if (fishStatusTextures == null)
        {
            stunnedStatusTexture = Resources.Load<Texture2D>("Fishing/StatusIcons/Stunned");
            fishStatusTextures = new[]
            {
                Resources.Load<Texture2D>("Fishing/StatusIcons/Calm"),
                Resources.Load<Texture2D>("Fishing/StatusIcons/Irritated"),
                Resources.Load<Texture2D>("Fishing/StatusIcons/Angry"),
                Resources.Load<Texture2D>("Fishing/StatusIcons/LookingForCover"),
                Resources.Load<Texture2D>("Fishing/StatusIcons/Fainted"),
                Resources.Load<Texture2D>("Fishing/StatusIcons/SirensCall")
            };
        }

        bobberIndicatorRoot = new GameObject("BobberStatusIndicator", typeof(RectTransform));
        bobberIndicatorRoot.transform.SetParent(gameplayCanvas.transform, false);
        bobberIndicatorRect = bobberIndicatorRoot.GetComponent<RectTransform>();
        bobberIndicatorRect.anchorMin = bobberIndicatorRect.anchorMax = new Vector2(.5f, .5f);
        bobberIndicatorRect.pivot = new Vector2(.5f, .5f);
        // Fixed canvas size keeps the icon readable at any fishing distance.
        bobberIndicatorRect.sizeDelta = new Vector2(128f, 128f);

        var icon = new GameObject("FishStatusIcon", typeof(RectTransform), typeof(RawImage));
        icon.transform.SetParent(bobberIndicatorRoot.transform, false);
        bobberStatusIcon = icon.GetComponent<RawImage>();
        bobberStatusIcon.raycastTarget = false;
        var iconRect = bobberStatusIcon.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;

        var label = new GameObject("Distance", typeof(RectTransform), typeof(Text), typeof(Outline));
        label.transform.SetParent(bobberIndicatorRoot.transform, false);
        bobberIndicatorText = label.GetComponent<Text>();
        bobberIndicatorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bobberIndicatorText.fontSize = 26;
        bobberIndicatorText.fontStyle = FontStyle.Bold;
        bobberIndicatorText.alignment = TextAnchor.MiddleLeft;
        bobberIndicatorText.color = Color.white;
        bobberIndicatorText.raycastTarget = false;
        var textRect = bobberIndicatorText.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, .5f);
        textRect.pivot = new Vector2(0f, .5f);
        textRect.anchoredPosition = new Vector2(72f, 0f);
        textRect.sizeDelta = new Vector2(220f, 80f);
        var textOutline = label.GetComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2f, -2f);
        textOutline.useGraphicAlpha = true;

        FishingHudTheme.Distance(bobberIndicatorText);

        var warning = new GameObject("FragileBreakOutline", typeof(RectTransform), typeof(FishingStatusOutline));
        warning.transform.SetParent(icon.transform, false);
        var warningRect = warning.GetComponent<RectTransform>();
        warningRect.anchorMin = Vector2.zero;
        warningRect.anchorMax = Vector2.one;
        warningRect.offsetMin = warningRect.offsetMax = Vector2.zero;
        fragileOutline = warning.GetComponent<FishingStatusOutline>();
        fragileOutline.raycastTarget = false;
        fragileOutline.color = new Color(1f, .04f, .025f, 1f);
        bobberIndicatorRoot.SetActive(false);
    }

    private void UpdateBobberIndicator()
    {
        if (bobberIndicatorRoot == null ||
            bobberIndicatorRect == null ||
            bobberIndicatorText == null ||
            bobberStatusIcon == null ||
            bobber == null ||
            !bobber.activeSelf ||
            state ==
                FishingState.Casting ||
            state ==
                FishingState.Idle)
        {
            if (bobberIndicatorRoot != null)
            {
                bobberIndicatorRoot.SetActive(
                    false
                );
            }

            return;
        }

        Vector3 screenPoint =
            FishingThirdPersonView.WorldToScreenPoint(playerCamera,
                GetLineTargetPosition() +
                Vector3.up * 0.52f
            );

        if (screenPoint.z <= 0f)
        {
            bobberIndicatorRoot.SetActive(
                false
            );

            return;
        }

        RectTransform canvasRect =
            gameplayCanvas.transform
                as RectTransform;

        Camera uiCamera =
            gameplayCanvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : gameplayCanvas.worldCamera;

        if (canvasRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            bobberIndicatorRoot.SetActive(
                false
            );

            return;
        }

        bobberIndicatorRoot.SetActive(
            true
        );

        bobberIndicatorRect.anchoredPosition = localPoint + new Vector2(0f, 72f);

        bool hasFish = state == FishingState.Bite || state == FishingState.Fighting;
        bool showStatus = hasFish;
        bobberStatusIcon.gameObject.SetActive(showStatus);
        if (showStatus)
            bobberStatusIcon.texture = IsSkillStunned && stunnedStatusTexture != null
                ? stunnedStatusTexture
                : fishStatusTextures[fishUnconscious ? 4 : (int)ActiveTemperament];

        if (fragileOutline != null)
        {
            fragileOutline.Progress = IsSkillStunned ? 1f : FragileWarningActive
                ? Mathf.Clamp01(fragileBreakTimer / FishingDragRules.FragileWarningSeconds) : 0f;
            if (IsSkillStunned)
            {
                float elapsed = Mathf.Clamp(FishingDragRules.SkillStunSeconds - (skillStunUntil - Time.time), 0f, FishingDragRules.SkillStunSeconds);
                // Integrated frequency rises smoothly from 2 Hz to 10 Hz.
                float cycles = 2f * elapsed + 4f * elapsed * elapsed / FishingDragRules.SkillStunSeconds;
                float alpha = Mathf.Sin(cycles * Mathf.PI * 2f) >= 0f ? 1f : 0.15f;
                fragileOutline.color = new Color(1f, 1f, 1f, alpha);
            }
            else fragileOutline.color = FragileWarningFlashing && FragileFlashOn
                ? Color.white : new Color(1f, .04f, .025f, 1f);
        }

        float lineDistance = GetCurrentLineDistance();
        bobberIndicatorText.text = lineDistance.ToString("0.0") + " m";
        bool nearLineLimit = hasFish && lineDistance >= maximumLineDistance - 10f;
        bool lineProtected = hasFish && dragDamage != null && dragDamage.LineImmortalityActive;
        bobberIndicatorText.color = lineProtected
            ? Color.Lerp(new Color(.65f, .29f, 1f), new Color(.88f, .64f, 1f), FishingEscapeVignette.ImmortalityPulse)
            : nearLineLimit
                ? Color.Lerp(Color.white, Color.red, FishingEscapeVignette.WarningPulse)
                : Color.white;
        bobberIndicatorText.rectTransform.anchoredPosition =
            new Vector2(showStatus ? 72f : 0f, 0f);
    }

    private void SetLinePositions()
    {
        fishingLine.SetPosition(
            0,
            rodTip.position
        );

        fishingLine.SetPosition(
            1,
            GetLineTargetPosition()
        );
    }

    private void ResetLine()
    {
        skillStunUntil = 0f;
        StopReelSound();
        StopLinePullSound();
        fishPulledLineThisFrame = false;
        if(hud!=null){hud.SetCastPower(false,0,0);hud.SetCastCancel(false,null);hud.SetActionInteractable(true);hud.SetCastAvailable(false);}
        castHintTimer=0;
        if(rodView!=null)rodView.ResetMotion();
        if (fishingLine != null)
            fishingLine.enabled = false;

        if (bobber != null)
            bobber.SetActive(false);

        if (bobberIndicatorRoot != null)
            bobberIndicatorRoot.SetActive(false);

        ClearUnconsciousFishVisual();

        if (rodView != null)
        {
            rodView.RelaxLinePull(
                Time.deltaTime
            );
        }
    }

    // Keep old scenes playable before the one-click asset installation has run.
    private void CreatePlaceholderRod()
    {
        rodRoot =
            new GameObject(
                "FishingRodViewModel"
            );

        rodRoot.transform.SetParent(
            playerCamera.transform,
            false
        );

        rodRoot.transform.localPosition =
            new Vector3(
                0.44f,
                -0.43f,
                0.78f
            );

        rodRoot.transform.localRotation =
            Quaternion.Euler(
                67f,
                -6f,
                11f
            );

        Material rodMaterial =
            CreateMaterial(
                new Color(
                    0.25f,
                    0.16f,
                    0.07f
                ),
                0.22f
            );

        Material reelMaterial =
            CreateMaterial(
                new Color(
                    0.16f,
                    0.36f,
                    0.42f
                ),
                0.52f
            );

        GameObject shaft =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        shaft.name = "Rod";
        shaft.transform.SetParent(
            rodRoot.transform,
            false
        );

        shaft.transform.localPosition =
            new Vector3(
                0f,
                0.60f,
                0f
            );

        shaft.transform.localScale =
            new Vector3(
                0.018f,
                0.60f,
                0.018f
            );

        shaft.GetComponent<Renderer>()
            .sharedMaterial =
            rodMaterial;

        Destroy(
            shaft.GetComponent<Collider>()
        );

        GameObject reel =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        reel.name = "Reel";
        reel.transform.SetParent(
            rodRoot.transform,
            false
        );

        reel.transform.localPosition =
            new Vector3(
                0.06f,
                0.18f,
                0f
            );

        reel.transform.localRotation =
            Quaternion.Euler(
                90f,
                0f,
                0f
            );

        reel.transform.localScale =
            new Vector3(
                0.10f,
                0.035f,
                0.10f
            );

        reel.GetComponent<Renderer>()
            .sharedMaterial =
            reelMaterial;

        Destroy(
            reel.GetComponent<Collider>()
        );

        GameObject tip =
            new GameObject("RodTip");

        tip.transform.SetParent(
            rodRoot.transform,
            false
        );

        tip.transform.localPosition =
            new Vector3(
                0f,
                1.22f,
                0f
            );

        rodTip = tip.transform;

    }

    private bool rodFitPending;
    private void FitRodToControls()
    {
        Canvas.ForceUpdateCanvases();
        var mount=rodRoot.transform.Find("ReelMount");if(mount==null)return;
        float target=Screen.height*0.22f;
        var joystick=FindFirstObjectByType<MobileJoystick>();
        if(joystick!=null)
        {
            var rect=joystick.GetComponent<RectTransform>();var canvas=joystick.GetComponentInParent<Canvas>();
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var camera=canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            target=Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera,corners[1]),RectTransformUtility.WorldToScreenPoint(camera,corners[0]));
        }
        var renderers=mount.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
        for(int pass=0;pass<4;pass++)
        {
            Bounds b=renderers[0].bounds;foreach(var renderer in renderers)b.Encapsulate(renderer.bounds);
            rodRoot.transform.position+=playerCamera.ViewportToWorldPoint(new Vector3(0.77f,0.19f,Mathf.Max(0.65f,playerCamera.nearClipPlane + 0.35f)))-b.center;
            b=renderers[0].bounds;foreach(var renderer in renderers)b.Encapsulate(renderer.bounds);
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            for(int i=0;i<8;i++)
            {
                var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                float y=playerCamera.WorldToScreenPoint(corner).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);
            }
            float size=Mathf.Clamp(rodRoot.transform.localScale.x*target/Mathf.Max(1,high-low),0.35f,4f);
            rodRoot.transform.localScale=Vector3.one*size;
        }
        // Scaling moves the mount around the rod pivot: pin it once more after
        // the last fit so the reel stays beside, and above, the right control.
        Bounds finalBounds=renderers[0].bounds;
        foreach(var renderer in renderers)finalBounds.Encapsulate(renderer.bounds);
        rodRoot.transform.position+=playerCamera.ViewportToWorldPoint(
            new Vector3(0.77f,0.19f,Mathf.Max(0.65f,playerCamera.nearClipPlane + 0.35f)))-finalBounds.center;
    }

    private void CreateRodAndLine()
    {
        GameObject prefab = Resources.Load<GameObject>("Fishing/FishingRodReel");
        FishingRodView prefabView = prefab != null ? prefab.GetComponent<FishingRodView>() : null;
        if (prefabView != null && prefabView.RodTip != null)
        {
            rodRoot = Instantiate(prefab, playerCamera.transform, false);
            rodRoot.name = "FishingRodViewModel";
            rodRoot.transform.localPosition = new Vector3(0.38f, -0.18f, 0.48f);
            rodRoot.transform.localRotation = Quaternion.Euler(40f, -6f, -4f);
            rodRoot.transform.localScale=Vector3.one*2f;
            rodFitPending=true;
            rodView = rodRoot.GetComponent<FishingRodView>();
            rodTip = rodView.RodTip;
            rodView.InitializePose();
        }
        else
        {
            Debug.LogWarning("Rod/reel prefab not installed. Run Tools > Open World > Install Fishing Rod + Animated Reel (One Click).");
            CreatePlaceholderRod();
        }

        fishingLine =
            rodRoot.AddComponent<LineRenderer>();

        fishingLine.useWorldSpace = true;
        fishingLine.positionCount = 2;
        fishingLine.startWidth = 0.008f;
        fishingLine.endWidth = 0.004f;

        Shader lineShader =
            Resources.Load<Shader>(
                "Fishing/FishingUnlit"
            );

        if (lineShader == null)
        {
            Debug.LogError(
                "Missing Resources/Fishing/FishingUnlit shader."
            );

            enabled = false;
            return;
        }

        Material lineMaterial =
            new Material(lineShader);

        // The ocean and FishingUnlit both default to Transparent (3000).
        // Sorting them by renderer distance can draw water over the line as
        // the camera moves. Draw this line after water, retaining depth tests
        // against opaque scenery and leaving other FishingUnlit users alone.
        lineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;

        if (lineMaterial.HasProperty("_BaseColor"))
        {
            lineMaterial.SetColor(
                "_BaseColor",
                new Color(
                    0.88f,
                    0.94f,
                    0.96f,
                    0.92f
                )
            );
        }
        else if (lineMaterial.HasProperty("_Color"))
        {
            lineMaterial.color =
                new Color(
                    0.88f,
                    0.94f,
                    0.96f,
                    0.92f
                );
        }

        fishingLine.material =
            lineMaterial;

        fishingLine.enabled = false;

        bobber =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        bobber.name = "FishingBobber";

        bobber.transform.localScale =
            Vector3.one * 0.16f;

        bobber.GetComponent<Renderer>()
            .sharedMaterial =
            CreateMaterial(
                new Color(
                    0.88f,
                    0.10f,
                    0.08f
                ),
                0.35f
            );

        Collider bobberCollider =
            bobber.GetComponent<Collider>();

        if (bobberCollider != null)
            Destroy(bobberCollider);

        bobber.SetActive(false);

        CreateBobberIndicator();
    }

    private void CreateHeldFishAnchor()
    {
        GameObject anchor =
            new GameObject(
                "HeldFishAnchor"
            );

        anchor.transform.SetParent(
            playerCamera.transform,
            false
        );

        anchor.transform.localPosition =
            new Vector3(
                0.58f,
                0.50f,
                0.65f
            );

        heldFishAnchor =
            anchor.transform;
        heldFishPlayerController = playerCamera.GetComponentInParent<CharacterController>();
    }

    private void UpdateHeldFishAnimation()
    {
        if (heldFishVisual == null)
            return;

        float time =
            Time.time * 5.8f +
            heldFishFlopOffset;

        var struggle=heldFishVisual.GetComponent<CaughtFishFlop>();
        Vector3 movement = heldFishPlayerController != null
            ? heldFishPlayerController.velocity : Vector3.zero;
        // Ground-stick/gravity must not look like walking while standing still.
        movement.y = 0f;
        heldFishMovementSwing = CaughtFishFlop.SmoothHeldMovement(
            heldFishMovementSwing, movement.magnitude, Time.deltaTime);
        CaughtFishFlop.EvaluateHeldSway(time, heldFishMovementSwing,
            struggle != null ? struggle.SwingBoost : 0f,
            out Vector3 swayOffset, out Quaternion swayRotation);
        if (heldHookPoint != null)
            heldHookPoint.localPosition = heldMouthRestLocalPosition + swayOffset;
        heldFishVisual.transform.localRotation = heldFishBaseRotation * swayRotation;

        if (heldHookPoint != null &&
            heldFishMouthMarker != null)
        {
            Vector3 correction =
                heldHookPoint.position -
                heldFishMouthMarker.position;

            heldFishVisual.transform.position +=
                correction;


        }
    }

    private void LateUpdate()
    {
        // The skill damage owner runs after Update; reflect its stun/KO this frame.
        UpdateBobberIndicator();
        if (hud != null) hud.SetFragileWarning(FragileWarningActive, FragileWarningFlashing && FragileFlashOn);
        CheckReelSound();
        UpdateLinePullSound();
        if(rodFitPending){rodFitPending=false;FitRodToControls();}
        UpdateHeldCatchLine();
    }

    private void UpdateHeldCatchLine()
    {
        if (heldCatchLine == null ||
            heldFishMouthMarker == null)
        {
            return;
        }

        Vector3 top =
            GetHeldCatchLineTop();

        Vector3 mouth =
            heldFishMouthMarker.position;

        // Follow the animated mouth, with a short overlap to hide the lip gap.
        Vector3 lineEnd = mouth + (mouth - top).normalized * 0.025f;

        heldCatchLine.SetPosition(
            0,
            top
        );

        heldCatchLine.SetPosition(
            1,
            lineEnd
        );
    }

    private void SetupHookedCatchPresentation()
    {
        if (heldFishVisual == null)
            return;

        // Remove legacy hook presentation completely.
        if (heldHookVisual != null)
        {
            Destroy(
                heldHookVisual
            );

            heldHookVisual = null;
        }

        if (heldCatchLine != null)
        {
            Destroy(
                heldCatchLine.gameObject
            );

            heldCatchLine = null;
        }

        if (heldHookPoint != null &&
            heldHookPoint.parent ==
                heldFishAnchor)
        {
            Destroy(
                heldHookPoint.gameObject
            );

            heldHookPoint = null;
        }

        GameObject mouthTargetObject =
            new GameObject(
                "HeldCatchMouthPoint"
            );

        mouthTargetObject.transform.SetParent(
            heldFishAnchor,
            false
        );

        mouthTargetObject.transform.localPosition =
            new Vector3(
                0f,
                0.035f,
                0f
            );

        heldHookPoint =
            mouthTargetObject.transform;

        GameObject lineObject =
            new GameObject(
                "HeldCatchLine"
            );

        lineObject.transform.SetParent(
            heldFishAnchor,
            false
        );

        heldCatchLine =
            lineObject.AddComponent<LineRenderer>();

        heldCatchLine.useWorldSpace = true;
        heldCatchLine.positionCount = 2;
        heldCatchLine.startWidth = 0.0015f;
        heldCatchLine.endWidth = 0.0010f;
        heldCatchLine.numCapVertices = 6;

        if (fishingLine != null &&
            fishingLine.sharedMaterial != null)
        {
            heldCatchLine.sharedMaterial =
                fishingLine.sharedMaterial;
        }

        heldFishVisual.transform.localPosition =
            Vector3.zero;

        // Keep the fish vertical while presenting almost its full side.
        // Species-specific yaw only; stingrays add 10 degrees clockwise.
        // The existing mouth correction below keeps the line attachment fixed.
        heldFishBaseRotation =
            Quaternion.AngleAxis(
                heldRecord != null ? FishCatalog.HeldYaw(heldRecord.speciesId) : 86f,
                Vector3.up
            ) *
            Quaternion.Euler(
                -90f,
                0f,
                0f
            );

        heldFishVisual.transform.localRotation =
            heldFishBaseRotation;

        Vector3 mouthLocal =
            FindFishMouthLocalPosition(
                heldFishVisual
            );

        Vector3 mouthWorld =
            heldFishVisual.transform
                .TransformPoint(
                    mouthLocal
                );

        Transform mouthParent =
            FindDeepChildByName(
                heldFishVisual.transform,
                "Bone"
            );

        GameObject marker =
            new GameObject(
                "CatchMouthAnchor"
            );

        marker.transform.SetParent(
            mouthParent != null
                ? mouthParent
                : heldFishVisual.transform,
            false
        );

        marker.transform.position =
            mouthWorld;

        heldFishMouthMarker =
            marker.transform;

        Vector3 desiredMouthWorld =
            GetHeldCatchMouthTargetWorld();

        heldHookPoint.position =
            desiredMouthWorld;

        heldMouthRestLocalPosition =
            heldHookPoint.localPosition;

        heldFishVisual.transform.position +=
            desiredMouthWorld -
            heldFishMouthMarker.position;

        // Keep the line's ceiling attachment fixed over the common held-fish
        // presentation point. The fish can still swing underneath it.
        heldCatchLineTopViewportX =
            0.72f;

        UpdateHeldCatchLine();

        CaughtFishFlop flop =
            heldFishVisual
                .GetComponent<CaughtFishFlop>();

        if (flop == null)
        {
            flop =
                heldFishVisual
                    .AddComponent<CaughtFishFlop>();
        }

        flop.ConfigurePlayerHeld();
        flop.PoseUpdated=()=>
        {
            if(heldFishVisual!=null && heldHookPoint!=null && heldFishMouthMarker!=null)
                heldFishVisual.transform.position+=heldHookPoint.position-heldFishMouthMarker.position;
            UpdateHeldCatchLine();
        };
    }

    private Vector3 GetHeldCatchMouthTargetWorld()
    {
        if (playerCamera == null)
        {
            return
                heldFishAnchor != null
                    ? heldFishAnchor.position
                    : transform.position;
        }

        const float targetViewportX = 0.72f;
        const float targetViewportY = 0.82f;

        float depth =
            0.65f;

        if (heldFishAnchor != null)
        {
            Vector3 anchorViewport =
                playerCamera.WorldToViewportPoint(
                    heldFishAnchor.position
                );

            if (anchorViewport.z >
                playerCamera.nearClipPlane)
            {
                depth =
                    anchorViewport.z;
            }
        }

        return
            playerCamera.ViewportToWorldPoint(
                new Vector3(
                    targetViewportX,
                    targetViewportY,
                    depth
                )
            );
    }

    private Vector3 GetHeldCatchLineTop()
    {
        if (playerCamera == null)
        {
            return
                heldHookPoint != null
                    ? heldHookPoint.position +
                      Vector3.up * 2f
                    : transform.position +
                      Vector3.up * 2f;
        }

        Vector3 mouthPosition =
            heldFishMouthMarker != null
                ? heldFishMouthMarker.position
                : heldHookPoint != null
                    ? heldHookPoint.position
                    : heldFishAnchor.position;

        Vector3 viewport =
            playerCamera.WorldToViewportPoint(
                mouthPosition
            );

        float depth =
            Mathf.Max(
                playerCamera.nearClipPlane +
                0.05f,
                viewport.z
            );

        // The top attachment is fixed on-screen. Only the mouth moves, so the
        // line naturally tilts and swings as the fish dangles below it.
        return
            playerCamera.ViewportToWorldPoint(
                new Vector3(
                    heldCatchLineTopViewportX,
                    1.03f,
                    depth
                )
            );
    }

    public static Vector3 FindFishMouthLocalPosition(
        GameObject fish)
    {
        var authoredMouth=FindDeepChildByName(fish.transform,"AuthoredMouthAnchor");
        if(authoredMouth!=null)return fish.transform.InverseTransformPoint(authoredMouth.position);
        bool useBoneMouth =
            fish.GetComponent<RedSnapperPresentation>() != null ||
            fish.GetComponent<GoatfishPresentation>() != null ||
            fish.GetComponent<MackerelPresentation>() != null ||
            fish.GetComponent<YellowtailPresentation>() != null;

        if (useBoneMouth)
        {
            Transform headBone =
                FindDeepChildByName(
                    fish.transform,
                    "Bone"
                );

            Transform nextBone =
                FindDeepChildByName(
                    fish.transform,
                    "Bone.001"
                );

            if (headBone != null &&
                nextBone != null)
            {
                Vector3 headDirection =
                    headBone.position -
                    nextBone.position;

                float segmentLength =
                    headDirection.magnitude;

                if (segmentLength >
                    0.0001f)
                {
                    bool isMackerel =
                        fish.GetComponent<MackerelPresentation>() != null;

                    bool isYellowtail =
                        fish.GetComponent<YellowtailPresentation>() != null;

                    bool useTightMouthProjection =
                        isMackerel ||
                        isYellowtail;

                    float projectionFraction =
                        useTightMouthProjection
                            ? 0.16f
                            : 0.42f;

                    float projectionMinimum =
                        useTightMouthProjection
                            ? 0.006f
                            : 0.018f;

                    float projectionMaximum =
                        useTightMouthProjection
                            ? 0.040f
                            : 0.11f;

                    Vector3 mouthWorld =
                        headBone.position +
                        headDirection.normalized *
                        Mathf.Clamp(
                            segmentLength *
                            projectionFraction,
                            projectionMinimum,
                            projectionMaximum
                        );

                    return
                        fish.transform
                            .InverseTransformPoint(
                                mouthWorld
                            );
                }
            }
        }

        // Yellowfin Tuna and non-standard rigs retain the geometry-based
        // mouth estimate that already gives the desired held presentation.
        float frontZ =
            FindFrontmostLocalZ(
                fish
            );

        Transform fallbackHead =
            FindDeepChildByName(
                fish.transform,
                "Bone"
            );

        float mouthInset =
            GetFishMouthInset(
                fish
            );

        if (fallbackHead != null)
        {
            Vector3 headLocal =
                fish.transform
                    .InverseTransformPoint(
                        fallbackHead.position
                    );

            return
                new Vector3(
                    headLocal.x,
                    headLocal.y,
                    frontZ -
                    mouthInset
                );
        }

        return
            new Vector3(
                0f,
                0f,
                frontZ -
                mouthInset
            );
    }

    private static float GetFishMouthInset(
        GameObject fish)
    {
        Renderer[] renderers =
            fish.GetComponentsInChildren<Renderer>(
                true
            );

        if (renderers.Length == 0)
            return 0.035f;

        Bounds combined =
            renderers[0].bounds;

        for (int i = 1;
             i < renderers.Length;
             i++)
        {
            combined.Encapsulate(
                renderers[i].bounds
            );
        }

        Vector3 localFront =
            fish.transform
                .InverseTransformPoint(
                    combined.center +
                    fish.transform.forward *
                    combined.extents.magnitude
                );

        Vector3 localBack =
            fish.transform
                .InverseTransformPoint(
                    combined.center -
                    fish.transform.forward *
                    combined.extents.magnitude
                );

        float approximateLength =
            Mathf.Abs(
                localFront.z -
                localBack.z
            );

        return
            Mathf.Clamp(
                approximateLength *
                0.030f,
                0.015f,
                0.045f
            );
    }

    private Vector3 GetLineTargetPosition()
    {
        if (fishUnconscious &&
            unconsciousFishMouthMarker != null)
        {
            return
                unconsciousFishMouthMarker.position;
        }

        if (bobber != null)
        {
            return
                bobber.transform.position;
        }

        return
            rodTip != null
                ? rodTip.position
                : transform.position;
    }

    private void SetBobberVisible(
        bool visible)
    {
        if (bobber == null)
            return;

        Renderer[] renderers =
            bobber.GetComponentsInChildren<Renderer>(
                true
            );

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            renderers[i].enabled =
                visible;
        }
    }

    private static float FindFrontmostLocalZ(
        GameObject fish)
    {
        Renderer[] renderers =
            fish.GetComponentsInChildren<Renderer>(
                true
            );

        float best =
            float.NegativeInfinity;

        for (int r = 0;
             r < renderers.Length;
             r++)
        {
            Renderer renderer =
                renderers[r];

            Bounds localBounds;
            Transform boundsTransform;

            SkinnedMeshRenderer skinned =
                renderer as SkinnedMeshRenderer;

            if (skinned != null)
            {
                localBounds =
                    skinned.localBounds;

                boundsTransform =
                    skinned.transform;
            }
            else
            {
                MeshFilter filter =
                    renderer.GetComponent<MeshFilter>();

                if (filter == null ||
                    filter.sharedMesh == null)
                {
                    continue;
                }

                localBounds =
                    filter.sharedMesh.bounds;

                boundsTransform =
                    filter.transform;
            }

            Vector3 c =
                localBounds.center;

            Vector3 e =
                localBounds.extents;

            for (int x = -1;
                 x <= 1;
                 x += 2)
            {
                for (int y = -1;
                     y <= 1;
                     y += 2)
                {
                    for (int z = -1;
                         z <= 1;
                         z += 2)
                    {
                        Vector3 localCorner =
                            c +
                            Vector3.Scale(
                                e,
                                new Vector3(
                                    x,
                                    y,
                                    z
                                )
                            );

                        Vector3 worldCorner =
                            boundsTransform.TransformPoint(
                                localCorner
                            );

                        float fishLocalZ =
                            fish.transform
                                .InverseTransformPoint(
                                    worldCorner
                                )
                                .z;

                        best =
                            Mathf.Max(
                                best,
                                fishLocalZ
                            );
                    }
                }
            }
        }

        if (float.IsNegativeInfinity(
                best))
        {
            return 0.46f;
        }

        return best;
    }

    private static Transform FindDeepChildByName(
        Transform root,
        string childName)
    {
        if (root == null)
            return null;

        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(
                true
            );

        for (int i = 0;
             i < transforms.Length;
             i++)
        {
            if (transforms[i].name ==
                childName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private void ClearHeldFish()
    {
        heldRecord=null;
        if (heldFishVisual != null)
        {
            Destroy(
                heldFishVisual
            );

            heldFishVisual = null;
        }

        if (heldHookVisual != null)
        {
            Destroy(
                heldHookVisual
            );

            heldHookVisual = null;
        }

        if (heldCatchLine != null)
        {
            Destroy(
                heldCatchLine.gameObject
            );

            heldCatchLine = null;
        }

        if (heldHookPoint != null &&
            heldHookPoint.parent ==
                heldFishAnchor)
        {
            Destroy(
                heldHookPoint.gameObject
            );
        }

        heldHookPoint = null;
        heldFishMouthMarker = null;
    }

    private void EnsureUnconsciousFishVisual()
    {
        if (unconsciousFishVisual != null)
            return;

        unconsciousFishVisual =
            FishWorldSize.Create(
                "Unconscious_" +
                FishCatalog
                    .Get(
                        hookedSpeciesId
                    )
                    .Name,
                null,
                hookedSpeciesId,
                hookedWeightKg
            );

        if (unconsciousFishVisual == null)
            return;

        foreach (Animator animator
                 in unconsciousFishVisual
                    .GetComponentsInChildren<Animator>(
                        true
                    ))
        {
            animator.enabled = false;
        }

        HeroFishAnimator hero =
            unconsciousFishVisual
                .GetComponent<HeroFishAnimator>();

        if (hero != null)
            hero.enabled = false;

        YellowfinTunaPresentation tuna =
            unconsciousFishVisual
                .GetComponent<YellowfinTunaPresentation>();

        if (tuna != null)
            tuna.enabled = false;

        GoatfishPresentation goatfish =
            unconsciousFishVisual
                .GetComponent<GoatfishPresentation>();

        if (goatfish != null)
            goatfish.enabled = false;

        RedSnapperPresentation snapper =
            unconsciousFishVisual
                .GetComponent<RedSnapperPresentation>();

        if (snapper != null)
            snapper.enabled = false;

        MackerelPresentation mackerel =
            unconsciousFishVisual
                .GetComponent<MackerelPresentation>();

        if (mackerel != null)
            mackerel.enabled = false;

        YellowtailPresentation yellowtail =
            unconsciousFishVisual
                .GetComponent<YellowtailPresentation>();

        if (yellowtail != null)
            yellowtail.enabled = false;

        GameObject mouthMarker =
            new GameObject(
                "UnconsciousMouthAnchor"
            );

        mouthMarker.transform.SetParent(
            unconsciousFishVisual.transform,
            false
        );

        mouthMarker.transform.localPosition =
            FindFishMouthLocalPosition(
                unconsciousFishVisual
            );

        unconsciousFishMouthMarker =
            mouthMarker.transform;

        SetBobberVisible(false);

        UpdateUnconsciousFishVisual();
    }

    private void UpdateUnconsciousFishVisual()
    {
        if (unconsciousFishVisual == null ||
            bobber == null)
        {
            return;
        }

        Vector3 position =
            bobber.transform.position;

        float sample =
            0.35f;

        float left =
            oceanWater.GetSurfaceHeight(
                position -
                Vector3.right * sample
            );

        float right =
            oceanWater.GetSurfaceHeight(
                position +
                Vector3.right * sample
            );

        float back =
            oceanWater.GetSurfaceHeight(
                position -
                Vector3.forward * sample
            );

        float front =
            oceanWater.GetSurfaceHeight(
                position +
                Vector3.forward * sample
            );

        Vector3 tangentX =
            new Vector3(
                sample * 2f,
                right - left,
                0f
            );

        Vector3 tangentZ =
            new Vector3(
                0f,
                front - back,
                sample * 2f
            );

        Vector3 surfaceNormal =
            Vector3.Cross(
                tangentZ,
                tangentX
            );

        if (surfaceNormal.y < 0f)
        {
            surfaceNormal =
                -surfaceNormal;
        }

        if (surfaceNormal.sqrMagnitude <
            0.0001f)
        {
            surfaceNormal =
                Vector3.up;
        }

        surfaceNormal.Normalize();

        Vector3 forward =
            Vector3.ProjectOnPlane(
                fightTravelDirection,
                surfaceNormal
            );

        if (forward.sqrMagnitude <
            0.0001f)
        {
            forward =
                Vector3.ProjectOnPlane(
                    playerCamera.transform.forward,
                    surfaceNormal
                );
        }

        if (forward.sqrMagnitude <
            0.0001f)
        {
            forward =
                Vector3.forward;
        }

        forward.Normalize();

        unconsciousFishVisual.transform.position =
            position +
            surfaceNormal * 0.025f;

        unconsciousFishVisual.transform.rotation =
            Quaternion.LookRotation(
                forward,
                surfaceNormal
            ) *
            Quaternion.Euler(
                0f,
                0f,
                90f
            );
    }

    private void ClearUnconsciousFishVisual()
    {
        if (unconsciousFishVisual == null)
            return;

        Destroy(
            unconsciousFishVisual
        );

        unconsciousFishVisual = null;
        unconsciousFishMouthMarker = null;
    }

    private Material CreateMaterial(
        Color color,
        float smoothness)
    {
        Shader shader =
            Resources.Load<Shader>(
                "Fishing/FishingLit"
            );

        if (shader == null)
        {
            Debug.LogError(
                "Missing Resources/Fishing/FishingLit shader."
            );

            return null;
        }

        Material material =
            new Material(shader);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor(
                "_Color",
                color
            );
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat(
                "_Smoothness",
                smoothness
            );
        }

        return material;
    }
}







