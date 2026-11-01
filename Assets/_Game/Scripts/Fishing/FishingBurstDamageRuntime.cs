using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns reel bursts, drag modifiers, High-drag passive damage and charged skill strikes.
///
/// The original FishingSystem accumulates tiny frame-by-frame integer damage and the
/// upgraded-rod compatibility layer multiplies those tiny decrements later. That can
/// make a 4-8 damage Level 2 rod visibly show -1/-2/-3, and can mark a tiny popup as
/// critical even though the advertised critical is 2x a full burst.
///
/// Reel-burst damage is earned strictly from ACTUAL REEL HOLD TIME. Every 0.35 seconds of
/// accumulated held REEL time produces one authoritative randomized burst:
///   Woodland: 2-4
///   Level 2:  4-8, 5% critical, critical = 8-16
///   Level 3:  6-12, 8% critical, critical = 12-24
///   Level 4:  10-20, 10% critical, critical = 20-40
///   Level 5:  25-35, 13% critical, critical = 50-70
///
/// Pressing REEL does not deal an instant hit. Releasing and rapidly tapping also
/// does not reset or accelerate the clock: only the sum of time the input is truly
/// held advances reel-burst damage. High drag and charged skills add separate hits.
/// This removes the tap-spam exploit while keeping continuous
/// reeling at the same 0.35-second burst cadence.
///
/// A held pointer is intentionally carried across both fight transitions: permanent
/// lures auto-hook while REEL is held, and consumable bait switches the same held
/// HOOK button into REEL. In both cases the 0.35-second reel timer starts immediately
/// without requiring the player to release and press again.
///
/// IMPORTANT: this compatibility layer can be the code that actually delivers the
/// KO. FishingSystem normally creates the unconscious/dead fish surface visual inside
/// its own zero-health branch. Because this component runs later in the frame, a burst
/// KO can otherwise set fishUnconscious=true after FishingSystem.Update has already
/// passed that branch; on the next frame FishingSystem sees the flag and goes straight
/// to retrieval, leaving only the bobber visible. We explicitly enter the existing
/// FishingSystem unconscious presentation when the authoritative burst reaches zero so
/// the dead fish floats at the surface and the fishing line remains attached to its
/// mouth exactly as the core fishing system intends.
/// </summary>
[DefaultExecutionOrder(2200)]
public sealed class FishingBurstDamageRuntime : MonoBehaviour
{
    private const float SecondsPerBurst = 0.35f;
    private const float SuppressedLegacyDisplayTimer = -1000f;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo StateField = typeof(FishingSystem).GetField("state", Flags);
    private static readonly FieldInfo HpField = typeof(FishingSystem).GetField("fishHealthPoints", Flags);
    private static readonly FieldInfo MaxHpField = typeof(FishingSystem).GetField("fishMaxHealth", Flags);
    private static readonly FieldInfo HealthField = typeof(FishingSystem).GetField("fishHealth", Flags);
    private static readonly FieldInfo PendingDamageField = typeof(FishingSystem).GetField("pendingDamage", Flags);
    private static readonly FieldInfo DamageFractionField = typeof(FishingSystem).GetField("damageFraction", Flags);
    private static readonly FieldInfo DamageDisplayTimerField = typeof(FishingSystem).GetField("damageDisplayTimer", Flags);
    private static readonly FieldInfo UnconsciousField = typeof(FishingSystem).GetField("fishUnconscious", Flags);
    private static readonly FieldInfo BobberField = typeof(FishingSystem).GetField("bobber", Flags);
    private static readonly FieldInfo PlayerCameraField = typeof(FishingSystem).GetField("playerCamera", Flags);
    private static readonly MethodInfo EnsureUnconsciousFishVisualMethod =
        typeof(FishingSystem).GetMethod("EnsureUnconsciousFishVisual", Flags);

    private FishingSystem fishing;
    private FishingHUD hud;
    private ShopProgress progress;
    private Level2FishingRodRuntime legacyUpgradeRuntime;

    private bool inFight;
    private float accumulatedReelSeconds;
    private int authoritativeHp;
    private float passiveSeconds,damageRemainder;
    private float skillCharge;
    private float skillCooldown;
    private const float SkillCooldownSeconds = 2f;
    private bool skillQueued;
    private readonly bool[] abilityUsed = new bool[4];
    private float burnSeconds;
    private int burnTotalDamage;
    private sealed class LightningSequence
    {
        public float age;
        public int total, applied, nextZap;
    }
    private readonly List<LightningSequence> lightningSequences = new List<LightningSequence>();
    private readonly Queue<int> lightningZaps = new Queue<int>();
    // Monotonic observer event ID; independent of the queue consumed by local FX.
    // Keep it across fights so multiplayer never replays old zaps on the next fish.
    private int networkLightningSerial;
    private int networkLightningZap;
    public int NetworkLightningSerial => networkLightningSerial;
    public int NetworkLightningZap => networkLightningZap;
    public float BurningSeconds => burnSeconds;
    public bool TryTakeLightningZap(out int index)
    {
        index = lightningZaps.Count > 0 ? lightningZaps.Dequeue() : -1;
        return index >= 0;
    }
    public float ImmortalitySeconds { get; private set; }
    public bool LineImmortalityActive => inFight && IsFighting() && authoritativeHp > 0 && ImmortalitySeconds > 0f;
    // Passive, once per fight. FishingSystem calls this as soon as line length reaches its limit.
    public bool TryTriggerLineImmortality()
    {
        if (!enabled || !inFight || !IsFighting() || authoritativeHp <= 0 ||
            abilityUsed[2] || fishing == null || fishing.Progression == null ||
            !fishing.Progression.Unlocked(2))
            return false;
        abilityUsed[2] = true;
        ImmortalitySeconds = fishing.Progression.ImmortalityDuration;
        return true;
    }
    public float SirenSeconds { get; private set; }
    // Lightning Strike activates on EVERY charged Skill use once unlocked.
    public bool CanUseAbility(int id) => id >= 0 && id < 4 && id == 3 && inFight && IsFighting() &&
        authoritativeHp > 0 && !abilityUsed[id] && fishing.Progression != null && fishing.Progression.Unlocked(id) &&
        !ShopWorldHUD.MenuOpen && hud != null && hud.CombatInputVisible;
    public bool AbilityUsed(int id) => id >= 0 && id < 4 && abilityUsed[id];
    public void UseAbility(int id)
    {
        if(!CanUseAbility(id))return;
        abilityUsed[id]=true;
        if(id==3)
        {
            SirenSeconds=fishing.Progression.SirenDuration;
            fishing.PlaySirensCallSound();
        }
    }
    private void StartBurn()
    {
        int maximumHp=Mathf.Max(1,(int)MaxHpField.GetValue(fishing));
        burnTotalDamage=Mathf.Min(2000,Mathf.Max(1,
            Mathf.RoundToInt(maximumHp*fishing.Progression.BurningTotalFraction)));
        var sequence = new LightningSequence { total = burnTotalDamage };
        lightningSequences.Add(sequence);
        burnSeconds=4f;
        ApplyLightningZap(sequence, maximumHp);
    }

    private void ApplyLightningZap(LightningSequence sequence, int maxHp)
    {
        int index = sequence.nextZap++;
        // Cumulative rounding keeps all eight integer hits at the exact total:
        // 50%, then seven equal shares of the remaining half.
        int expected = index == 7 ? sequence.total :
            Mathf.RoundToInt(sequence.total * (.5f + index / 14f));
        int amount = expected - sequence.applied;
        sequence.applied = expected;
        lightningZaps.Enqueue(index);
        networkLightningSerial = networkLightningSerial == int.MaxValue ? 1 : networkLightningSerial + 1;
        networkLightningZap = index;
        if (amount > 0) DealDamage(amount, maxHp, false, true);
    }

    private void UpdateLightning(float elapsed, int maxHp)
    {
        burnSeconds=0f;
        for(int i=lightningSequences.Count-1;i>=0;i--)
        {
            var sequence=lightningSequences[i];
            sequence.age+=elapsed;
            while(sequence.nextZap<8 && sequence.age+.00001f>=sequence.nextZap*(4f/7f) && authoritativeHp>0)
                ApplyLightningZap(sequence,maxHp);
            if(sequence.nextZap>=8 || authoritativeHp<=0) lightningSequences.RemoveAt(i);
            else burnSeconds=Mathf.Max(burnSeconds,Mathf.Max(0f,4f-sequence.age));
        }
    }

    private int BoostDamage(int damage, bool critical=false)
    {
        var stats=fishing.Progression;
        return stats==null?damage:Mathf.RoundToInt((damage+stats.Value(0)*(critical?2:1))*stats.Multiplier(1));
    }
    public int SelectedDrag {get;private set;}=FishingDragRules.Medium;

    public void SelectDrag(int mode)
    {
        if(!inFight || !IsFighting() || authoritativeHp<=0)return;
        SelectedDrag=Mathf.Clamp(mode,FishingDragRules.Low,FishingDragRules.High);
    }

    public bool IsSkillReady => inFight && IsFighting() && authoritativeHp > 0 &&
        !skillQueued && skillCooldown <= 0f && skillCharge >= 1f && hud != null &&
        hud.CombatInputVisible && !ShopWorldHUD.MenuOpen;

    public bool TrySkillLook(Vector2 displacement)
    {
        if (!IsSkillReady) return false;
        if(!FishingDragRules.IsLookGesture(displacement.magnitude))return false;
        skillQueued=true;
        skillCharge=0f;
        return true;
    }

    private void ResetDrag()
    {
        SelectedDrag=FishingDragRules.Medium;
        passiveSeconds=damageRemainder=skillCharge=skillCooldown=0f;
        skillQueued=false;
        burnSeconds=ImmortalitySeconds=SirenSeconds=0f;
        burnTotalDamage=0;
        lightningSequences.Clear();
        lightningZaps.Clear();
        Array.Clear(abilityUsed,0,abilityUsed.Length);
    }

    public static int NormalMinimumForTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, ShopCatalog.MaxRodTier);
        if (tier == 5) return 35;
        if (tier == 4) return 25;
        if (tier == 3) return 10;
        return 2 * (tier + 1);
    }

    public static int NormalMaximumForTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, ShopCatalog.MaxRodTier);
        if (tier == 5) return 50;
        if (tier == 4) return 35;
        if (tier == 3) return 20;
        return 4 * (tier + 1);
    }

    public static float CriticalChanceForTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, ShopCatalog.MaxRodTier);
        if (tier >= 4) return 0.13f;
        if (tier == 3) return 0.10f;
        return tier >= 2 ? 0.08f : tier >= 1 ? 0.05f : 0f;
    }

    /// <summary>
    /// Deterministic entry point used by the editor validation pass as well as the
    /// live game. damageRoll01 chooses an integer uniformly across the advertised
    /// inclusive normal range; criticalRoll01 determines whether that whole burst is
    /// doubled. This keeps the UI description and gameplay formula literally shared.
    /// </summary>
    public static int RollBurstForTier(int tier, float damageRoll01, float criticalRoll01, out bool critical)
    {
        int minimum = NormalMinimumForTier(tier);
        int maximum = NormalMaximumForTier(tier);
        float roll = Mathf.Clamp(damageRoll01, 0f, 0.999999f);
        int normal = minimum + Mathf.FloorToInt(roll * (maximum - minimum + 1));
        critical = criticalRoll01 < CriticalChanceForTier(tier);
        return critical ? normal * 2 : normal;
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        if (fishing == null) fishing = FindFirstObjectByType<FishingSystem>();
        progress = fishing != null ? fishing.GetComponent<ShopProgress>() : FindFirstObjectByType<ShopProgress>();
        legacyUpgradeRuntime = fishing != null ? fishing.GetComponent<Level2FishingRodRuntime>() : null;
        hud = FindFirstObjectByType<FishingHUD>();

        if (fishing == null || StateField == null || HpField == null || MaxHpField == null ||
            HealthField == null || PendingDamageField == null || DamageFractionField == null ||
            DamageDisplayTimerField == null || UnconsciousField == null ||
            EnsureUnconsciousFishVisualMethod == null)
        {
            Debug.LogError("FishingBurstDamageRuntime could not bind the fishing damage/presentation fields. Legacy damage was left untouched.");
            enabled = false;
        }
        // This component is often installed by OpenWorldCoreFixBootstrap after
        // scene-load callbacks have already run. Install the accompanying flame
        // presentation on the actual FishingSystem now, not only at scene load.
        if (enabled && fishing != null && fishing.GetComponent<FishingBurnVisuals>() == null)
            fishing.gameObject.AddComponent<FishingBurnVisuals>();
    }

    private void Update()
    {
        if (!enabled || fishing == null) return;
        if (hud == null) hud = FindFirstObjectByType<FishingHUD>();
        if (progress == null) progress = fishing.GetComponent<ShopProgress>();
        if (legacyUpgradeRuntime == null) legacyUpgradeRuntime = fishing.GetComponent<Level2FishingRodRuntime>();

        bool fighting = IsFighting();
        if (!fighting)
        {
            EndFightOwnership();
            return;
        }

        // Match the core fight's menu guard: no passive damage or charge while
        // combat UI is covered and its tension simulation is not advancing.
        if(ShopWorldHUD.MenuOpen || (hud!=null && !hud.FishingUiVisible))
        {
            skillQueued=false;
            return;
        }

        // The older upgraded-rod runtime must not multiply our already-complete
        // bursts. It still runs normally outside fights, so rod model/equipment UI
        // behavior is preserved.
        if (legacyUpgradeRuntime != null && legacyUpgradeRuntime.enabled)
            legacyUpgradeRuntime.enabled = false;

        int maxHp = Mathf.Max(1, (int)MaxHpField.GetValue(fishing));
        if (!inFight)
        {
            inFight = true;
            authoritativeHp = maxHp;
            ResetDrag();
            accumulatedReelSeconds = 0f;
            if(fishing.Progression != null && fishing.Progression.Unlocked(1))
            {
                abilityUsed[1]=true;
                DealDamage(Mathf.Max(1,Mathf.RoundToInt(maxHp*fishing.Progression.HooksetDamageFraction)),maxHp,false);
            }
            // Do not wait for a pointer-up here. The action button deliberately stays
            // held when HOOK changes to REEL (and when a lure auto-hooks), so the
            // existing press should immediately become reel hold time.
        }

        // FishingSystem.Update ran earlier this frame. Restore the authoritative HP
        // before applying our burst so legacy frame ticks cannot leak into health or
        // spawn their old -1/-2/-3 popups.
        WriteAuthoritativeHealth(maxHp);
        SuppressLegacyDamageFields();

        // Only the part of a frame after cooldown expiry may charge the skill.
        float chargeSeconds = Mathf.Max(0f, Time.deltaTime - skillCooldown);
        skillCooldown = Mathf.Max(0f, skillCooldown - Time.deltaTime);
        if (authoritativeHp <= 0)
        {
            // Stop emitting electric ripples immediately if the fish is knocked out.
            burnSeconds=0f;lightningSequences.Clear();
            ImmortalitySeconds=0f;
            EnterUnconsciousPresentation();
            skillCharge=0f;skillQueued=false;
            if(hud!=null)hud.SetDragFightUI(true,false,SelectedDrag,0f,skillCooldown);
            return;
        }

        ImmortalitySeconds=Mathf.Max(0f,ImmortalitySeconds-Time.deltaTime);
        SirenSeconds=Mathf.Max(0f,SirenSeconds-Time.deltaTime);
        UpdateLightning(Time.deltaTime,maxHp);
        if(skillQueued)
        {
            skillQueued=false;
            skillCharge=0f;
            skillCooldown=SkillCooldownSeconds;
            fishing.PlaySkillFeedback();
            // Activate Lightning Strike on the actual successful charged Skill strike.
            // Previously we started it only AFTER the Skill hit and only when
            // the fish survived. On smaller fish the charged hit finished the
            // fight first, making Lightning Strike appear to do nothing.
            if (fishing.Progression != null && fishing.Progression.Unlocked(0))
            {
                abilityUsed[0] = true;
                StartBurn();
            }
            DealDamage(Mathf.RoundToInt(FishingDragRules.SkillDamage(RollNormalDamage()) * (fishing.Progression != null ? fishing.Progression.Multiplier(4) : 1f)),maxHp,true);
            if (authoritativeHp > 0) fishing.ApplySkillStun();
        }
        else if (skillCooldown <= 0f)
            skillCharge=FishingDragRules.Charge(skillCharge,SelectedDrag,chargeSeconds);
        if(SelectedDrag==FishingDragRules.High && authoritativeHp>0)
        {
            passiveSeconds+=Time.deltaTime;
            while(passiveSeconds>=FishingDragRules.PassiveDamageSeconds && authoritativeHp>0)
            {
                passiveSeconds-=FishingDragRules.PassiveDamageSeconds;
                DealDamage(RollNormalDamage(),maxHp,false);
            }
        }

        bool reeling = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
        if (reeling)
        {
            // The only thing that advances damage is real time spent holding REEL.
            // Do NOT award a burst on button-down and do NOT reset this accumulator
            // on release. Therefore 7 x 0.05 s taps equal 0.35 s of reel time, not
            // seven full hits, while a continuous hold behaves identically.
            accumulatedReelSeconds += Time.deltaTime;
            while (accumulatedReelSeconds >= SecondsPerBurst && authoritativeHp > 0)
            {
                accumulatedReelSeconds -= SecondsPerBurst;
                DealBurst(maxHp);
            }
        }

        WriteAuthoritativeHealth(maxHp);
        SuppressLegacyDamageFields();

        if (authoritativeHp <= 0)
            EnterUnconsciousPresentation();
        else
            UnconsciousField.SetValue(fishing, false);
        if(authoritativeHp<=0){skillCharge=0f;skillQueued=false;}
        if(hud!=null)hud.SetDragFightUI(true,authoritativeHp>0,SelectedDrag,skillCharge,skillCooldown);
    }

    private int RollNormalDamage()
    {
        int tier=progress!=null && progress.Data!=null?progress.Data.rodEquipped:0;
        return BoostDamage(RollBurstForTier(tier,UnityEngine.Random.value,1f,out _));
    }

    private void DealBurst(int maxHp)
    {
        int tier = progress != null ? Mathf.Clamp(progress.Data.rodEquipped, 0, ShopCatalog.MaxRodTier) : 0;
        bool critical;
        int rolledDamage = RollBurstForTier(tier, UnityEngine.Random.value, UnityEngine.Random.value, out critical);
        // Carry half points so odd Low-drag rolls deal exactly half over time.
        float scaled=BoostDamage(rolledDamage,critical)*FishingDragRules.DamageMultiplier(SelectedDrag)+damageRemainder;
        int damage=Mathf.FloorToInt(scaled);
        damageRemainder=scaled-damage;
        DealDamage(damage,maxHp,critical);
    }

    private void DealDamage(int rolledDamage,int maxHp,bool critical,bool lightning=false)
    {
        int appliedDamage = Mathf.Min(authoritativeHp, rolledDamage);
        if (appliedDamage <= 0) return;

        authoritativeHp -= appliedDamage;
        WriteAuthoritativeHealth(maxHp);

        if (critical) FishingDamagePresentation.MarkCriticalHit();

        if (hud != null)
        {
            Vector3 screenPoint = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);
            GameObject bobber = BobberField != null ? BobberField.GetValue(fishing) as GameObject : null;
            Camera camera = PlayerCameraField != null ? PlayerCameraField.GetValue(fishing) as Camera : null;
            if (bobber != null && camera != null)
                screenPoint = FishingThirdPersonView.WorldToScreenPoint(camera, bobber.transform.position);

            // Show the rolled attack value, not the remaining-HP-capped value. A
            // finishing critical keeps its rolled attack number even if the fish had
            // less health remaining; only the health bar clamps at zero.
            hud.ShowDamage(rolledDamage, screenPoint, lightning);
        }
    }

    private void EnterUnconsciousPresentation()
    {
        // This must be done as one transition. If we only set fishUnconscious here,
        // FishingSystem skips its own zero-health branch on the following frame and
        // never creates the floating fish. Invoke the existing private presentation
        // method immediately; it creates the correctly-sized species model, freezes
        // its animation, places it on the water surface, hides the bobber renderer,
        // and makes GetLineTargetPosition use the mouth marker.
        WriteAuthoritativeHealth(Mathf.Max(1, (int)MaxHpField.GetValue(fishing)));
        SuppressLegacyDamageFields();
        UnconsciousField.SetValue(fishing, true);

        try
        {
            EnsureUnconsciousFishVisualMethod.Invoke(fishing, null);
        }
        catch (TargetInvocationException e)
        {
            Debug.LogError("FishingBurstDamageRuntime could not create the unconscious fish presentation: " +
                (e.InnerException != null ? e.InnerException.ToString() : e.ToString()));
        }
        catch (Exception e)
        {
            Debug.LogError("FishingBurstDamageRuntime could not create the unconscious fish presentation: " + e);
        }
    }

    private bool IsFighting()
    {
        object state = StateField.GetValue(fishing);
        return state != null && string.Equals(state.ToString(), "Fighting", StringComparison.Ordinal);
    }

    private void WriteAuthoritativeHealth(int maxHp)
    {
        authoritativeHp = Mathf.Clamp(authoritativeHp, 0, maxHp);
        HpField.SetValue(fishing, authoritativeHp);
        HealthField.SetValue(fishing, authoritativeHp / (float)Mathf.Max(1, maxHp));
    }

    private void SuppressLegacyDamageFields()
    {
        PendingDamageField.SetValue(fishing, 0);
        DamageFractionField.SetValue(fishing, 0f);
        DamageDisplayTimerField.SetValue(fishing, SuppressedLegacyDisplayTimer);
    }

    private void EndFightOwnership()
    {
        ResetDrag();
        if(hud!=null)hud.SetDragFightUI(false,false,SelectedDrag,0f);
        if (!inFight)
        {
            if (legacyUpgradeRuntime != null && !legacyUpgradeRuntime.enabled)
                legacyUpgradeRuntime.enabled = true;
            return;
        }

        inFight = false;
        accumulatedReelSeconds = 0f;
        FishingDamagePresentation.ClearPendingCritical();

        if (legacyUpgradeRuntime != null && !legacyUpgradeRuntime.enabled)
            legacyUpgradeRuntime.enabled = true;
    }

    private void OnDisable()
    {
        EndFightOwnership();
        if (legacyUpgradeRuntime != null && !legacyUpgradeRuntime.enabled)
            legacyUpgradeRuntime.enabled = true;
    }
}


