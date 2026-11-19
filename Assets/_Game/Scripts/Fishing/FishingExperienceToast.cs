using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A non-interactive, top-right XP toast that temporarily replaces MENU / TRAVEL. It animates the
/// actual saved catch-XP award through every crossed level threshold, including
/// any XP left over after leveling up. No separate progression is maintained.
/// </summary>
[DisallowMultipleComponent]
public sealed class FishingExperienceToast : MonoBehaviour
{
    private const float Width = 690f;
    private const float Height = 138f;
    private const float RightMargin = 36f;
    // Align XP to the original MENU / TRAVEL button. The second, independent
    // level-up card occupies the XP toast's previous top-right position.
    private const float TopOffset = 32f;
    private const float LevelNoticeTopOffset = 140f;
    public static bool IsShowingXp { get; private set; }
    private const float SlideSeconds = .38f;
    private const float HoldSeconds = 2.25f;

    private static readonly Color Cyan = new Color(.02f, .94f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, .84f, .38f, 1f);

    private struct Award
    {
        public int amount, startLevel, endLevel;
        public double startXp, endXp;
    }

    private enum Phase { Hidden, Entering, Filling, Holding, Exiting }
    private enum NoticePhase { Hidden, Entering, Holding, Exiting }

    private struct LevelReward
    {
        public int level, stat, value;
    }

    private readonly Queue<Award> waiting = new Queue<Award>();
    private readonly Dictionary<int, LevelReward> earnedLevels = new Dictionary<int, LevelReward>();
    private readonly Queue<LevelReward> pendingNotices = new Queue<LevelReward>();
    private RectTransform levelNotice;
    private Text noticeHeading, noticeStat, noticeDetail;
    private NoticePhase noticePhase = NoticePhase.Hidden;
    private float noticeElapsed;
    private RectTransform gemNotice;
    private NoticePhase gemPhase=NoticePhase.Hidden;
    private int pendingGems;
    private float gemElapsed, gemTopOffset, gemVisible, gemHoldSeconds;
    private ShopProgress progress;
    private RectTransform popup;
    private RectTransform meterFill;
    private RectTransform badge;
    private Text levelInBadge, levelTitle, xpValue, earnedValue;
    private Award current;
    private Phase phase = Phase.Hidden;
    private float elapsed;
    private float fillSeconds;
    private float badgePulse;
    private int drawnLevel = -1;

    private void Awake()
    {
        progress = GetComponent<ShopProgress>();
        if (progress != null)
        {
            progress.FishingXpAwarded += OnFishingXpAwarded;
            progress.AbyssalGemAcquired += OnAbyssalGemAcquired;
            progress.FishingLevelAwarded += OnFishingLevelAwarded;
        }
    }

    private void OnDestroy()
    {
        if (progress != null)
        {
            progress.FishingXpAwarded -= OnFishingXpAwarded;
            progress.AbyssalGemAcquired -= OnAbyssalGemAcquired;
            progress.FishingLevelAwarded -= OnFishingLevelAwarded;
        }
        IsShowingXp = false;
    }

    private void OnDisable()
    {
        IsShowingXp = false;
    }

    private void OnAbyssalGemAcquired() => pendingGems++;

    private void OnFishingLevelAwarded(int level, int stat, int value)
    {
        // Keep the stat linked to its actual level, not to a later catch's total.
        earnedLevels[level] = new LevelReward { level = level, stat = stat, value = value };
    }

    private void OnFishingXpAwarded(int amount, int startLevel, double startXp, int endLevel, double endXp)
    {
        if (amount <= 0 || (startLevel == endLevel && Math.Abs(startXp - endXp) < .001d)) return;
        waiting.Enqueue(new Award
        {
            amount = amount,
            startLevel = startLevel,
            startXp = startXp,
            endLevel = endLevel,
            endXp = endXp
        });
    }

    private void Update()
    {
        if (popup == null)
        {
            if ((waiting.Count == 0 && pendingGems == 0) || !BuildIfReady()) return;
        }

        float dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
        if (phase == Phase.Hidden && waiting.Count > 0) NextAward();

        switch (phase)
        {
            case Phase.Entering:
                elapsed += dt;
                SetSlide(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SlideSeconds)));
                if (elapsed >= SlideSeconds) { phase = Phase.Filling; elapsed = 0f; }
                break;

            case Phase.Filling:
                elapsed += dt;
                float fraction = Mathf.Clamp01(elapsed / fillSeconds);
                ShowEarned(current.amount * Mathf.SmoothStep(0f, 1f, fraction));
                if (fraction >= 1f)
                {
                    // Use the saved final snapshot to avoid accumulated float error.
                    ShowLevel(current.endLevel, current.endXp);
                    phase = Phase.Holding;
                    elapsed = 0f;
                }
                break;

            case Phase.Holding:
                elapsed += dt;
                if (elapsed >= HoldSeconds) { phase = Phase.Exiting; elapsed = 0f; }
                break;

            case Phase.Exiting:
                elapsed += dt;
                SetSlide(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SlideSeconds)));
                if (elapsed >= SlideSeconds)
                {
                    phase = Phase.Hidden;
                    popup.gameObject.SetActive(false);
                    if (waiting.Count > 0) NextAward();
                    else IsShowingXp = false;
                }
                break;
        }

        // This card uses the exact slide-in/hold/slide-out timing and easing
        // of the XP bar, but can finish independently without delaying XP.
        UpdateLevelNotice(dt);
        UpdateGemNotice(dt);
        if (badge != null)
        {
            badgePulse = Mathf.MoveTowards(badgePulse, 0f, dt * 2.5f);
            badge.localScale = Vector3.one * (1f + .09f * badgePulse);
        }
    }

    private void UpdateLevelNotice(float dt)
    {
        if (levelNotice == null) return;
        if (noticePhase == NoticePhase.Hidden && pendingNotices.Count > 0)
        {
            LevelReward reward = pendingNotices.Dequeue();
            noticeHeading.text = "LEVEL " + reward.level + "  •  ATTRIBUTE INCREASED";
            if (reward.stat >= 0 && reward.stat < AnglerProgression.Names.Length)
            {
                bool raw = reward.stat == 0;
                noticeStat.text = AnglerProgression.Names[reward.stat].ToUpperInvariant()
                    + "  +1" + (raw ? "" : "%");
                noticeDetail.text = "TOTAL BONUS: +" + reward.value + (raw ? "" : "%");
            }
            else
            {
                noticeStat.text = "ALL FISHING ATTRIBUTES MAXED";
                noticeDetail.text = "ALL PERMANENT BONUSES ARE AT THEIR LIMIT";
            }
            for (int i = 0; i < AnglerProgression.AbilityNames.Length; i++)
            {
                if (reward.level == (i + 1) * 20)
                {
                    noticeDetail.text = "SPECIAL ABILITY UPGRADE READY • PERSONAL STATS";
                    break;
                }
            }
            levelNotice.gameObject.SetActive(true);
            levelNotice.SetAsLastSibling();
            noticePhase = NoticePhase.Entering;
            noticeElapsed = 0f;
            SetNoticeSlide(0f);
        }

        if (noticePhase == NoticePhase.Hidden) return;
        noticeElapsed += dt;
        switch (noticePhase)
        {
            case NoticePhase.Entering:
                SetNoticeSlide(Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(noticeElapsed / SlideSeconds)));
                if (noticeElapsed >= SlideSeconds)
                {
                    noticePhase = NoticePhase.Holding;
                    noticeElapsed = 0f;
                }
                break;
            case NoticePhase.Holding:
                if (noticeElapsed >= HoldSeconds)
                {
                    noticePhase = NoticePhase.Exiting;
                    noticeElapsed = 0f;
                }
                break;
            case NoticePhase.Exiting:
                SetNoticeSlide(1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(noticeElapsed / SlideSeconds)));
                if (noticeElapsed >= SlideSeconds)
                {
                    noticePhase = NoticePhase.Hidden;
                    levelNotice.gameObject.SetActive(false);
                }
                break;
        }
    }

    private void UpdateGemNotice(float dt)
    {
        if(gemNotice==null)return;
        if(gemPhase==NoticePhase.Hidden && pendingGems>0)
        {
            pendingGems--;
            gemElapsed=0f;gemVisible=0f;
            // Extend the complete on-screen lifetime by 1.5x while keeping
            // both slide animations at the XP card's original speed.
            float xpFill=phase!=Phase.Hidden?fillSeconds:0f;
            gemHoldSeconds=(2f*SlideSeconds+xpFill+HoldSeconds)*1.5f-2f*SlideSeconds;
            gemTopOffset=TopOffset+Height+12f;
            gemPhase=NoticePhase.Entering;
            gemNotice.gameObject.SetActive(true);
            gemNotice.SetAsLastSibling();
        }
        if(gemPhase==NoticePhase.Hidden)return;
        // Reserve the level/ability row before the XP fill reaches a new level.
        // Keep that position until this card exits, even if the upper card ends.
        if(noticePhase!=NoticePhase.Hidden || pendingNotices.Count>0 ||
            (phase!=Phase.Hidden && current.endLevel>current.startLevel))
            gemTopOffset=Mathf.Max(gemTopOffset,LevelNoticeTopOffset+104f+12f);
        gemElapsed+=dt;
        switch(gemPhase)
        {
            case NoticePhase.Entering:
                gemVisible=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(gemElapsed/SlideSeconds));
                if(gemElapsed>=SlideSeconds){gemPhase=NoticePhase.Holding;gemElapsed=0f;}
                break;
            case NoticePhase.Holding:
                if(gemElapsed>=gemHoldSeconds){gemPhase=NoticePhase.Exiting;gemElapsed=0f;}
                break;
            case NoticePhase.Exiting:
                gemVisible=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(gemElapsed/SlideSeconds));
                if(gemElapsed>=SlideSeconds){gemPhase=NoticePhase.Hidden;gemNotice.gameObject.SetActive(false);}
                break;
        }
        gemNotice.anchoredPosition=new Vector2(
            Mathf.Lerp(gemNotice.rect.width+45f,-RightMargin,gemVisible),-gemTopOffset);
    }

    private void SetNoticeSlide(float visible)
    {
        if (levelNotice == null) return;
        float outside = levelNotice.rect.width + 45f;
        levelNotice.anchoredPosition = new Vector2(
            Mathf.Lerp(outside, -RightMargin, visible), -LevelNoticeTopOffset);
    }


    private void NextAward()
    {
        current = waiting.Dequeue();
        IsShowingXp = true;
        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling(); // Above the normal HUD, below any subsequently opened modal.
        drawnLevel = -1;
        badgePulse = 0f;
        badge.localScale = Vector3.one;
        earnedValue.text = "+" + current.amount.ToString("N0") + " XP";
        earnedValue.color = current.endLevel > current.startLevel ? Gold : Cyan;
        ShowLevel(current.startLevel, current.startXp);
        // Each crossed level needs enough time to be recognizable.
        fillSeconds = Mathf.Min(5f, 1.3f + Mathf.Max(0, current.endLevel - current.startLevel) * .50f);
        elapsed = 0f;
        phase = Phase.Entering;
        SetSlide(0f);
    }

    private void ShowEarned(double awardedSoFar)
    {
        int level = current.startLevel;
        double xp = current.startXp + awardedSoFar;
        // Process level boundaries individually, rather than interpolating two
        // percentage values (which would incorrectly run backwards on level-up).
        while (level < current.endLevel && level < AnglerProgression.MaxLevel)
        {
            double required = AnglerProgression.RequiredXp(level);
            if (xp + .00001d < required) break;
            xp -= required;
            level++;
        }
        if (level >= AnglerProgression.MaxLevel) xp = 0d;
        ShowLevel(level, xp);
    }

    private void ShowLevel(int level, double xp)
    {
        bool max = level >= AnglerProgression.MaxLevel;
        if (drawnLevel != level)
        {
            if (drawnLevel >= 0 && level > drawnLevel)
            {
                badgePulse = 1f;
                for (int reached = drawnLevel + 1; reached <= level; reached++)
                {
                    LevelReward reward;
                    if (earnedLevels.TryGetValue(reached, out reward))
                    {
                        pendingNotices.Enqueue(reward);
                        earnedLevels.Remove(reached);
                    }
                }
            }
            drawnLevel = level;
            levelInBadge.text = "LV\n" + level;
            levelTitle.text = max ? "MAX LEVEL" : "LEVEL " + level;
        }
        double required = AnglerProgression.RequiredXp(level);
        double fraction = max ? 1d : Math.Max(0d, Math.Min(1d, xp / required));
        xpValue.text = max ? "MAX LEVEL" : xp.ToString("N0") + " / " + required.ToString("N0") + " XP";
        meterFill.anchorMax = new Vector2((float)fraction, 1f);
        meterFill.offsetMin = Vector2.zero;
        meterFill.offsetMax = Vector2.zero;
    }

    private void SetSlide(float visible)
    {
        if (popup == null) return;
        float outside = popup.rect.width + 45f;
        popup.anchoredPosition = new Vector2(
            Mathf.Lerp(outside, -RightMargin, visible), -TopOffset);
    }

    private bool BuildIfReady()
    {
        // ShopWorldHUD creates TideglassHUD in Start, so wait for its real canvas.
        ShopWorldHUD hud = FindFirstObjectByType<ShopWorldHUD>();
        if (hud == null) return false;
        Transform root = hud.transform.Find("TideglassHUD");
        if (root == null || !(root is RectTransform)) return false;

        GameObject box = new GameObject("CaughtFishExperiencePopup",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        box.transform.SetParent(root, false);
        popup = box.GetComponent<RectTransform>();
        popup.anchorMin = popup.anchorMax = popup.pivot = Vector2.one;
        float available = ((RectTransform)root).rect.width;
        popup.sizeDelta = new Vector2(available > 0f ? Mathf.Min(Width, Mathf.Max(420f, available - 72f)) : Width, Height);

        Image background = box.GetComponent<Image>();
        FishingHudTheme.Panel(box);
        background.raycastTarget = false;
        CanvasGroup group = box.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // The existing LEVEL shield is the same artwork used in Personal Stats.
        GameObject shieldObject = ImageAt(box.transform, "LevelShield", .012f, .035f, .205f, .965f);
        Image shieldImage = shieldObject.GetComponent<Image>();
        shieldImage.sprite = PersonalStatsView.Art("LevelShield");
        shieldImage.preserveAspect = true;
        shieldImage.color = Color.white;
        badge = shieldObject.GetComponent<RectTransform>();
        levelInBadge = TextAt(shieldObject.transform, "LevelNumber", "LV\n1", 30,
            .26f, .23f, .74f, .80f, Color.white, TextAnchor.MiddleCenter, true);

        levelTitle = TextAt(box.transform, "LevelTitle", "LEVEL 1", 32,
            .235f, .60f, .70f, .94f, Color.white, TextAnchor.MiddleLeft, true);
        xpValue = TextAt(box.transform, "ExperienceAmount", "0 / 250 XP", 23,
            .235f, .34f, .82f, .60f, Cyan, TextAnchor.MiddleLeft);
        earnedValue = TextAt(box.transform, "CatchXpAward", "", 23,
            .71f, .64f, .965f, .94f, Cyan, TextAnchor.MiddleRight, true);

        GameObject track = ImageAt(box.transform, "ExperienceTrack", .235f, .13f, .965f, .305f);
        FishingHudTheme.Panel(track, 3);
        Image trackImage = track.GetComponent<Image>();
        trackImage.raycastTarget = false;
        GameObject filled = ImageAt(track.transform, "AnimatedExperienceFill", 0f, 0f, 0f, 1f);
        Image fillImage = filled.GetComponent<Image>();
        FishingHudTheme.Fill(fillImage);
        fillImage.color = Cyan;
        fillImage.raycastTarget = false;
        meterFill = filled.GetComponent<RectTransform>();

        // Independent notification at the XP bar's former screen location.
        GameObject notice = new GameObject("FishingStatLevelUpPopup",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        notice.transform.SetParent(root, false);
        levelNotice = notice.GetComponent<RectTransform>();
        levelNotice.anchorMin = levelNotice.anchorMax = levelNotice.pivot = Vector2.one;
        levelNotice.sizeDelta = new Vector2(popup.sizeDelta.x, 104f);
        FishingHudTheme.Panel(notice);
        notice.GetComponent<Image>().raycastTarget = false;
        CanvasGroup noticeGroup = notice.GetComponent<CanvasGroup>();
        noticeGroup.interactable = false;
        noticeGroup.blocksRaycasts = false;
        noticeHeading = TextAt(notice.transform, "LevelUpHeading", "", 23,
            .045f, .63f, .96f, .94f, Gold, TextAnchor.MiddleLeft, true);
        noticeStat = TextAt(notice.transform, "ImprovedAttribute", "", 27,
            .045f, .32f, .96f, .67f, Color.white, TextAnchor.MiddleLeft, true);
        noticeDetail = TextAt(notice.transform, "AttributeBonusOrSpecialUnlock", "", 18,
            .045f, .07f, .96f, .35f, Cyan, TextAnchor.MiddleLeft, true);
        notice.SetActive(false);
        SetNoticeSlide(0f);

        GameObject gemBox=new GameObject("AbyssalGemAcquiredPopup",
            typeof(RectTransform),typeof(Image),typeof(CanvasGroup));
        gemBox.transform.SetParent(root,false);
        gemNotice=gemBox.GetComponent<RectTransform>();
        gemNotice.anchorMin=gemNotice.anchorMax=gemNotice.pivot=Vector2.one;
        gemNotice.sizeDelta=new Vector2(popup.sizeDelta.x,104f);
        FishingHudTheme.Panel(gemBox);
        gemBox.GetComponent<Image>().raycastTarget=false;
        var gemGroup=gemBox.GetComponent<CanvasGroup>();
        gemGroup.interactable=false;gemGroup.blocksRaycasts=false;
        var gemIcon=ImageAt(gemBox.transform,"AbyssalGem",.025f,.06f,.19f,.94f).GetComponent<Image>();
        gemIcon.sprite=AbyssalGemView.Art("AbyssalGem");gemIcon.preserveAspect=true;
        TextAt(gemBox.transform,"GemAcquiredTitle","Abyssal Gem Acquired",30,
            .22f,.14f,.96f,.86f,Gold,TextAnchor.MiddleLeft,true);
        gemBox.SetActive(false);

        popup.gameObject.SetActive(false);
        SetSlide(0f);
        return true;
    }

    private static GameObject ImageAt(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        PersonalStatsView.Place(go.GetComponent<RectTransform>(), x0, y0, x1, y1);
        go.GetComponent<Image>().raycastTarget = false;
        return go;
    }

    private static Text TextAt(Transform parent, string name, string value, int fontSize,
        float x0, float y0, float x1, float y1, Color color,
        TextAnchor alignment, bool bold = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        PersonalStatsView.Place(go.GetComponent<RectTransform>(), x0, y0, x1, y1);
        Text label = go.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = fontSize;
        label.text = value;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        if (bold) label.fontStyle = FontStyle.Bold;
        return label;
    }
}
