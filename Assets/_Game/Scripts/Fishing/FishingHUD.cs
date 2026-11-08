using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FishingHUD : MonoBehaviour
{
    private FishingSystem system;

    private GameObject root;
    private GameObject hotbarObject,dragPanel,skillPanel;
    private GameObject abilityBar;
    private readonly Button[] abilityButtons=new Button[4];
    private readonly Text[] abilityLabels=new Text[4];
    private void UpdateAbilityButtons(bool fighting,bool live)
    {
        if(abilityBar==null && fighting)
        {
            abilityBar=new GameObject("FightSpecialAbilities",typeof(RectTransform));abilityBar.transform.SetParent(skillPanel.transform,false);
            // Follow the skill panel: same left edge, half its width, small gap above.
            var abilityRect=abilityBar.GetComponent<RectTransform>();
            abilityRect.anchorMin=new Vector2(0f,1f);
            abilityRect.anchorMax=new Vector2(.5f,1f);
            abilityRect.pivot=new Vector2(0f,0f);
            abilityRect.anchoredPosition=new Vector2(0f,12f);
            for(int i=0;i<4;i++)
            {
                // Lightning Strike, Hookset and Line Immortality are passive.
                // Siren's Call remains the only manually activated special ability.
                if(i!=3)continue;
                int id=i;
                var go=new GameObject("Use_"+AnglerProgression.AbilityAssets[i],typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(abilityBar.transform,false);
                PersonalStatsView.Place(go.GetComponent<RectTransform>(),0,0,1,1);
                go.GetComponent<Image>().sprite=PersonalStatsView.Art(AnglerProgression.AbilityAssets[i]);
                abilityButtons[i]=go.GetComponent<Button>();abilityButtons[i].onClick.AddListener(()=>dragOwner.UseAbility(id));
                abilityLabels[i]=PersonalStatsView.Label(go.transform,"",20,.31f,.12f,.95f,.86f,Color.white,TextAnchor.MiddleCenter);
            }
        }
        if(abilityBar==null)return;
        // Preserve the former 12%-of-HUD height minus its two 4-unit insets.
        abilityBar.GetComponent<RectTransform>().sizeDelta=
            new Vector2(0f,Mathf.Max(1f,root.GetComponent<RectTransform>().rect.height*.12f-8f));
        abilityBar.SetActive(fighting && live);
        if(!fighting || !live)return;
        if(dragOwner==null && system!=null)dragOwner=system.GetComponent<FishingBurstDamageRuntime>();
        for(int i=0;i<4;i++)
        {
            if(abilityButtons[i]==null)continue;
            bool unlocked=system!=null && system.Progression!=null && system.Progression.Unlocked(i);
            abilityButtons[i].gameObject.SetActive(unlocked);
            abilityButtons[i].interactable=dragOwner!=null && dragOwner.CanUseAbility(i);
            float remaining=dragOwner==null?0f:i==3?dragOwner.SirenSeconds:0f;
            abilityLabels[i].text=AnglerProgression.AbilityNames[i]+"\n"+(remaining>0f?remaining.ToString("0.0")+"s":dragOwner!=null && dragOwner.AbilityUsed(i)?"USED":"TAP TO USE");
        }
    }
    private RectTransform skillFill,dragRect,joystickRect;
    private Text skillLabel,dragTitle;
    private Slider dragSlider;
    private GameObject skillReadyArrows;
    private Outline skillReadyOutline;
    private FishingSkillEdgeFlash skillEdgeFlash;

    public void PlaySkillFlash()
    {
        if (root == null) return;
        if (skillEdgeFlash == null)
        {
            var overlay = new GameObject("SkillEdgeFlash", typeof(RectTransform), typeof(FishingSkillEdgeFlash));
            overlay.transform.SetParent(root.transform, false);
            StretchFullScreen(overlay.GetComponent<RectTransform>());
            skillEdgeFlash = overlay.GetComponent<FishingSkillEdgeFlash>();
            skillEdgeFlash.raycastTarget = false;
        }
        skillEdgeFlash.transform.SetAsLastSibling();
        skillEdgeFlash.Play();
    }
    private readonly Vector3[] joystickCorners=new Vector3[4];
    private FishingBurstDamageRuntime dragOwner;
    public bool FishingUiVisible=>root!=null && root.activeInHierarchy;
    public bool CombatInputVisible=>root!=null && root.activeInHierarchy && dragPanel!=null && dragPanel.activeInHierarchy;
    private GameObject actionButtonObject;
    private FishingActionButton actionInput;
    private Text actionLabel;
    private Image actionArtwork;

    private Text statusText;
    private Text fightStatusText, fightHintText;
    private RawImage fightStatusIcon;
    public RawImage CombatFishStatusIcon => fightStatusIcon;
    private readonly Dictionary<string, Texture2D> hudStatusTextures = new Dictionary<string, Texture2D>();
    private string currentStatus = "";
    private int displayDrag = 1;
    private string renderedStatus;
    private int renderedDrag = -1;
    private GameObject fightPanel;
    private Image tensionFill;
    private Image progressFill;
    private Text tensionLabel;
    private Text loosenDragLabel;
    private Outline loosenDragOutline;
    private Text progressLabel;

    private GameObject inventoryPanel;
    private RectTransform inventoryList;
    private Text emptyInventoryText;

    private Image rodSlotImage;
    private Outline rodSlotOutline;

    private GameObject catchPanel;
    private Text catchText, catchSpecimenText, catchSizeText, catchBestText;
    private float catchPanelTimer;

    private GameObject castPanel;
    private CastPowerGauge castGauge;
    private GameObject cancelCast;
    private FirstPersonController castPlayer;
    private Text castCaption;
    private readonly Text[] damageLabels=new Text[8];
    private readonly float[] damageLife=new float[8];
    private int nextDamage;
    private Font font;

    public void SetCastAvailable(bool available)
    {
        if(actionButtonObject==null)return;
        actionButtonObject.GetComponent<Image>().color=available?Color.white:new Color(.40f,.55f,.60f,.85f);
        actionButtonObject.GetComponent<Outline>().effectColor=available?new Color(.4f,1f,.86f):new Color(.25f,.32f,.34f,.65f);
    }
    public void SetActionInteractable(bool value)
    {if(actionInput!=null)actionInput.SetInteractable(value);}
    public void SetCastPower(bool visible,float power,float distance,bool[] validSamples=null,bool rangeAvailable=true)
    {
        if(castPanel==null && visible)
        {
            castPanel=new GameObject("QuarterCircleCastGauge",typeof(RectTransform),typeof(CastPowerGauge));
            castPanel.transform.SetParent(actionButtonObject.transform,false);
            var r=castPanel.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(470,470);
            castGauge=castPanel.GetComponent<CastPowerGauge>();castGauge.raycastTarget=false;
            var captionBox=CreatePanel("GaugeCaption",castPanel.transform,new Color(.02f,.07f,.09f,.96f));
            FishingHudTheme.Panel(captionBox);
            captionBox.GetComponent<Image>().raycastTarget=false;
            var c=captionBox.GetComponent<RectTransform>();c.anchorMin=c.anchorMax=new Vector2(.5f,.5f);c.sizeDelta=new Vector2(330,56);c.anchoredPosition=new Vector2(-95,268);
            castCaption=CreateText("CastDistance",captionBox.transform,"",29,TextAnchor.MiddleCenter);StretchFullScreen(castCaption.rectTransform);
        }
        if(castPanel==null)return;castPanel.SetActive(visible);
        if(!visible)return;
        castGauge.Set(power,validSamples,rangeAvailable);
        castCaption.text=$"{distance:0.0} m"+(!rangeAvailable || !FishingRules.IsCastPowerAvailable(validSamples,power)?" • BLOCKED":"");
    }
    public void SetCastCancel(bool visible,System.Action callback)
    {
        if(system==null)return;
        if(castPlayer==null)castPlayer=system.GetComponent<FirstPersonController>();
        if(castPlayer==null)return;
        var jump=castPlayer.JumpControl;
        if(cancelCast==null && visible && jump!=null)
        {
            cancelCast=CreatePanel("CancelCast",jump.parent,new Color(.72f,.09f,.07f,.96f));
            FishingHudTheme.Panel(cancelCast,5);
            var r=cancelCast.GetComponent<RectTransform>();r.anchorMin=jump.anchorMin;r.anchorMax=jump.anchorMax;r.pivot=jump.pivot;r.sizeDelta=jump.sizeDelta;r.anchoredPosition=castPlayer.JumpRestPosition;
            var text=CreateText("Cancel",cancelCast.transform,"CANCEL\nCAST",26,TextAnchor.MiddleCenter);StretchFullScreen(text.rectTransform);
            cancelCast.AddComponent<Button>();
        }
        if(visible && cancelCast!=null && jump!=null)
        {
            var r=cancelCast.GetComponent<RectTransform>();r.anchorMin=jump.anchorMin;r.anchorMax=jump.anchorMax;r.pivot=jump.pivot;r.sizeDelta=jump.sizeDelta;r.anchoredPosition=castPlayer.JumpRestPosition;
        }
        castPlayer.SetCastMode(visible);
        if(cancelCast!=null)
        {
            cancelCast.SetActive(visible);var b=cancelCast.GetComponent<Button>();b.onClick.RemoveAllListeners();
            if(callback!=null)b.onClick.AddListener(()=>callback());
        }
    }
    public void ShowDamage(int amount,Vector3 screenPoint,bool lightning=false)
    {
        int i=nextDamage++%damageLabels.Length;
        if(damageLabels[i]==null)
        {
            damageLabels[i]=CreateText("ReelDamage",root.transform,"",32,TextAnchor.MiddleCenter);
            var r=damageLabels[i].rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(160,60);
        }
        if(screenPoint.z<=0)screenPoint=new Vector3(Screen.width*.5f,Screen.height*.5f,1);
        var canvas=root.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root.GetComponent<RectTransform>(),screenPoint,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var local);
        damageLabels[i].rectTransform.anchoredPosition=local+new Vector2(Random.Range(-35f,35f),25);
        damageLabels[i].text="−"+amount;damageLabels[i].color=new Color(1,.85f,.35f,1);damageLabels[i].gameObject.SetActive(true);damageLife[i]=.9f;
        FishingDamagePresentation.ConfigureDamageLabel(damageLabels[i],lightning);
    }
    private void UpdateDamage()
    {
        for(int i=0;i<damageLabels.Length;i++)if(damageLife[i]>0 && damageLabels[i]!=null)
        {
            damageLife[i]-=Time.deltaTime;
            damageLabels[i].rectTransform.anchoredPosition+=Vector2.up*(Time.deltaTime*75f);
            var color=damageLabels[i].color;color.a=Mathf.Clamp01(damageLife[i]/.4f);damageLabels[i].color=color;
            if(damageLife[i]<=0)damageLabels[i].gameObject.SetActive(false);
        }
    }

    public FishingActionButton ActionInput => actionInput;

    public void Initialize(FishingSystem owner)
    {
        system = owner;

        if (root != null)
            return;

        font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        BuildUi();
        RefreshInventory();
    }

    private void Update()
    {
        UpdateDamage();
        if (catchPanelTimer <= 0f)
            return;

        catchPanelTimer -= Time.deltaTime;

        if (catchPanelTimer <= 0f &&
            catchPanel != null)
        {
            catchPanel.SetActive(false);
        }
    }

    public void SetDragFightUI(bool fighting,bool live,int mode,float charge,float cooldown=0f)
    {
        if(root==null)return;
        if(fighting && dragPanel==null)BuildDragUI();
        UpdateAbilityButtons(fighting,live);
        if(hotbarObject!=null)hotbarObject.SetActive(!fighting);
        if(dragPanel==null)return;
        dragPanel.SetActive(fighting);
        skillPanel.SetActive(fighting);
        if (skillReadyArrows != null) skillReadyArrows.SetActive(fighting && live && charge >= 1f);
        if (skillReadyOutline != null)
        {
            skillReadyOutline.enabled = fighting && live && charge >= 1f && cooldown <= 0f;
            skillReadyOutline.effectColor = new Color(1f,1f,1f,
                Mathf.Repeat(Time.unscaledTime * 5f,1f) < .5f ? 1f : .18f);
        }
        if(!fighting)return;
        PositionDragAboveJoystick();
        string[] names={"LOW","MEDIUM","HIGH"};
        mode=Mathf.Clamp(mode,0,2);
        displayDrag = mode;
        dragTitle.text="DRAG • <color=#17F5FF>"+names[mode]+"</color>";
        RefreshFightHeader();
        dragSlider.interactable=live;
        dragSlider.SetValueWithoutNotify(mode);
        bool coolingDown = cooldown > 0f;
        skillFill.anchorMax=new Vector2(coolingDown ? 1f : Mathf.Clamp01(charge),1f);
        skillLabel.text=!live?"FISH SUBDUED • REEL IT IN":coolingDown?"SKILL RECOVERING • "+cooldown.ToString("0.0")+"s":charge>=1f?"SKILL READY • SWIPE UP • 20× HIT + STUN":
            "Skill  <color=#17F5FF>"+Mathf.FloorToInt(charge*100f)+"%</color> • "+(mode==FishingDragRules.High?"Charging":"Use high drag to charge");
        skillFill.GetComponent<Image>().color=coolingDown
            ? (Mathf.Repeat(Time.time * 4f, 1f) < .5f ? Color.red : Color.white)
            : charge>=1f?new Color(1f,.76f,.23f,1):new Color(0f,.94f,.84f,1);
    }

    private void BuildDragUI()
    {
        dragOwner=system!=null?system.GetComponent<FishingBurstDamageRuntime>():null;
        dragPanel=CreatePanel("FishingDrag",root.transform,new Color(.025f,.075f,.095f,.95f));
        dragPanel.GetComponent<Image>().raycastTarget=false;
        dragRect=dragPanel.GetComponent<RectTransform>();
        dragRect.anchorMin=dragRect.anchorMax=new Vector2(.5f,.5f);
        dragRect.pivot=new Vector2(.5f,0);dragRect.sizeDelta=new Vector2(340,176);
        FishingHudTheme.Panel(dragPanel);
        FishingHudTheme.Divider(dragPanel.transform,58);
        dragRect.localScale=Vector3.one*1.3f;
        dragTitle=CreateText("DragTitle",dragPanel.transform,"DRAG • MEDIUM",23,TextAnchor.MiddleLeft);
        FishingHudSymbol.Add(dragPanel.transform,FishingHudSymbol.Kind.Gear,new Vector2(0,1),new Vector2(35,-30),32);
        var titleRect=dragTitle.rectTransform;titleRect.anchorMin=titleRect.anchorMax=Vector2.zero;
        titleRect.anchoredPosition=new Vector2(204,146);titleRect.sizeDelta=new Vector2(270,34);
        // A single drag target with a handle and exactly three discrete values.
        var sliderObject=CreatePanel("DragSlider",dragPanel.transform,Color.clear);
        var sliderRect=sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin=sliderRect.anchorMax=Vector2.zero;sliderRect.pivot=Vector2.zero;
        sliderRect.anchoredPosition=new Vector2(26,56);sliderRect.sizeDelta=new Vector2(288,40);
        var rail=CreatePanel("Rail",sliderObject.transform,new Color(.18f,.30f,.34f,1));
        rail.GetComponent<Image>().raycastTarget=false;
        var railRect=rail.GetComponent<RectTransform>();railRect.anchorMin=new Vector2(0,.5f);railRect.anchorMax=new Vector2(1,.5f);
        railRect.offsetMin=new Vector2(28,-12);railRect.offsetMax=new Vector2(-28,12);
        FishingHudTheme.Panel(rail,3);
        string[] names={"LOW","MEDIUM","HIGH"};
        for(int i=0;i<3;i++)
        {
            var tick=CreatePanel("Notch"+i,sliderObject.transform,new Color(.55f,.78f,.77f,1));
            tick.GetComponent<Image>().raycastTarget=false;
            var tickRect=tick.GetComponent<RectTransform>();tickRect.anchorMin=tickRect.anchorMax=new Vector2(0,.5f);
            tickRect.sizeDelta=new Vector2(4,18);tickRect.anchoredPosition=new Vector2(28+i*116,0);
            var label=CreateText("NotchLabel"+i,dragPanel.transform,names[i],18,TextAnchor.MiddleCenter);
            var labelRect=label.rectTransform;labelRect.anchorMin=labelRect.anchorMax=Vector2.zero;
            labelRect.sizeDelta=new Vector2(90,22);labelRect.anchoredPosition=new Vector2(Mathf.Clamp(54+i*116,45,295),13);
        }
        var handleArea=new GameObject("HandleArea",typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform,false);
        var areaRect=handleArea.GetComponent<RectTransform>();StretchFullScreen(areaRect);
        areaRect.offsetMin=new Vector2(28,0);areaRect.offsetMax=new Vector2(-28,0);
        var handle=new GameObject("Handle",typeof(RectTransform),typeof(FishingDragHandle));
        handle.transform.SetParent(handleArea.transform,false);
        var handleGraphic=handle.GetComponent<FishingDragHandle>();
        handleGraphic.color=new Color(.08f,.78f,.66f,1);
        // Keep the enlarged 108-unit height, now a circle with a circular hit area.
        // Slider stretches the handle over the 40-unit area: 40 + 68 = 108.
        var handleRect=handle.GetComponent<RectTransform>();handleRect.sizeDelta=new Vector2(108,68);
        dragSlider=sliderObject.AddComponent<Slider>();
        dragSlider.direction=Slider.Direction.LeftToRight;
        dragSlider.minValue=FishingDragRules.Low;dragSlider.maxValue=FishingDragRules.High;
        dragSlider.wholeNumbers=true;dragSlider.handleRect=handleRect;
        dragSlider.targetGraphic=handleGraphic;
        dragSlider.navigation=new Navigation{mode=Navigation.Mode.None};
        dragSlider.SetValueWithoutNotify(FishingDragRules.Medium);
        dragSlider.onValueChanged.AddListener(value=>
        {
            if(dragOwner==null && system!=null)dragOwner=system.GetComponent<FishingBurstDamageRuntime>();
            if(dragOwner!=null)
            {
                int previous = dragOwner.SelectedDrag;
                dragOwner.SelectDrag(Mathf.RoundToInt(value));
                int notches = Mathf.Abs(dragOwner.SelectedDrag - previous);
                if(notches > 0 && system != null) system.PlayDragNotchSound(notches);
            }
        });
        skillPanel=CreatePanel("FishingSkill",root.transform,new Color(.025f,.075f,.095f,.96f));
        skillPanel.GetComponent<Image>().raycastTarget=false;
        FishingHudTheme.Panel(skillPanel);
        skillReadyOutline = skillPanel.AddComponent<Outline>();
        skillReadyOutline.useGraphicAlpha = false;
        skillReadyOutline.effectDistance = new Vector2(3f,-3f);
        skillReadyOutline.enabled = false;
        FishingHudSymbol.Add(skillPanel.transform,FishingHudSymbol.Kind.Bolt,new Vector2(0,1),new Vector2(43,-36),36);
        var sr=skillPanel.GetComponent<RectTransform>();sr.anchorMin=sr.anchorMax=new Vector2(.5f,0);sr.pivot=new Vector2(.5f,0);
        sr.sizeDelta=new Vector2(670,120);sr.anchoredPosition=new Vector2(0,30);
        var track=CreatePanel("SkillTrack",skillPanel.transform,new Color(.09f,.16f,.19f,1));track.GetComponent<Image>().raycastTarget=false;
        var tr=track.GetComponent<RectTransform>();tr.anchorMin=new Vector2(0,0);tr.anchorMax=new Vector2(1,.42f);tr.offsetMin=new Vector2(34,22);tr.offsetMax=new Vector2(-34,-4);
        FishingHudTheme.Panel(track,3);
        var fill=CreatePanel("SkillFill",track.transform,new Color(.06f,.57f,.53f,1));fill.GetComponent<Image>().raycastTarget=false;
        FishingHudTheme.Fill(fill.GetComponent<Image>());
        skillFill=fill.GetComponent<RectTransform>();skillFill.anchorMin=Vector2.zero;skillFill.anchorMax=new Vector2(0,1);skillFill.offsetMin=skillFill.offsetMax=Vector2.zero;
        skillLabel=CreateText("SkillLabel",skillPanel.transform,"",23,TextAnchor.MiddleLeft);
        skillLabel.resizeTextForBestFit=true;skillLabel.resizeTextMinSize=18;skillLabel.resizeTextMaxSize=23;
        var lr=skillLabel.rectTransform;lr.anchorMin=new Vector2(0,.45f);lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(76,0);lr.offsetMax=new Vector2(-34,-5);
        skillReadyArrows = new GameObject("SkillReadySwipeArrows", typeof(RectTransform), typeof(FishingSkillReadyArrows));
        skillReadyArrows.transform.SetParent(skillPanel.transform, false);
        var arrowsRect = skillReadyArrows.GetComponent<RectTransform>();
        arrowsRect.anchorMin = arrowsRect.anchorMax = new Vector2(1f, .5f);
        arrowsRect.pivot = new Vector2(0f, .5f);
        // The five-arrow mesh occupies 92 units and is centered 2 units above
        // a 108-unit rect's center. Scale the artwork itself, not just its rect.
        // Keep the doubled arrow stack above the bottom screen edge.
        arrowsRect.anchoredPosition = new Vector2(18f, 78f);
        arrowsRect.sizeDelta = new Vector2(45f, 108f);
        arrowsRect.localScale = Vector3.one * 3f;
        skillReadyArrows.GetComponent<FishingSkillReadyArrows>().raycastTarget = false;
        skillReadyArrows.SetActive(false);
    }

    private void PositionDragAboveJoystick()
    {
        if(joystickRect==null)
        {
            var joystick=FindFirstObjectByType<MobileJoystick>();
            if(joystick!=null)joystickRect=joystick.GetComponent<RectTransform>();
        }
        var rootRect=root.GetComponent<RectTransform>();
        Vector2 position=new Vector2(rootRect.rect.xMin+190,rootRect.rect.yMin+320);
        if(joystickRect!=null)
        {
            joystickRect.GetWorldCorners(joystickCorners);
            var source=joystickRect.GetComponentInParent<Canvas>();
            var target=root.GetComponentInParent<Canvas>();
            Camera sourceCamera=source!=null && source.renderMode!=RenderMode.ScreenSpaceOverlay?source.worldCamera:null;
            Camera targetCamera=target!=null && target.renderMode!=RenderMode.ScreenSpaceOverlay?target.worldCamera:null;
            Vector2 screen=RectTransformUtility.WorldToScreenPoint(sourceCamera,(joystickCorners[1]+joystickCorners[2])*.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect,screen,targetCamera,out position);
            position.y+=28f;
        }
        position.x=Mathf.Clamp(position.x,rootRect.rect.xMin+229,rootRect.rect.xMax-229);
        position.y=Mathf.Clamp(position.y,rootRect.rect.yMin+18,rootRect.rect.yMax-250);
        dragRect.localPosition=new Vector3(position.x,position.y,0);
    }

    public void SetRodSelected(bool selected)
    {
        if (rodSlotOutline != null)
        {
            rodSlotOutline.effectColor =
                selected
                    ? new Color(
                        0.24f,
                        0.88f,
                        1f,
                        0.95f
                    )
                    : new Color(
                        0.22f,
                        0.28f,
                        0.31f,
                        0.70f
                    );

            rodSlotOutline.effectDistance =
                selected
                    ? new Vector2(4f, -4f)
                    : new Vector2(2f, -2f);
        }

        if (rodSlotImage != null)
        {
            rodSlotImage.color =
                selected
                    ? new Color(
                        0.08f,
                        0.19f,
                        0.24f,
                        0.88f
                    )
                    : new Color(
                        0.05f,
                        0.07f,
                        0.09f,
                        0.72f
                    );
        }
    }

    public void SetActionVisible(bool visible)
    {
        if (actionButtonObject != null)
            actionButtonObject.SetActive(visible);
    }

    public void SetActionLabel(string text)
    {
        if (actionLabel != null)
            actionLabel.text = text;
        if (actionArtwork != null)
            actionArtwork.sprite = FishingActionArtwork.Get(text == "REEL");
    }

    // The compass occupies the normal top-center status area. Keep updating the
    // hint text while hidden so it restores immediately with the current state.
    public void SetNavigationCovered(bool covered)
    {
        if (statusText != null) statusText.enabled = !covered;
    }

    public void SetStatus(string text)
    {
        currentStatus = text ?? "";
        if (statusText != null) statusText.text = currentStatus;
        RefreshFightHeader();
    }

    public void ShowFightMeters(bool visible)
    {
        if (!visible) SetFragileWarning(false, false);
        if (fightPanel != null) fightPanel.SetActive(visible);
        if (statusText != null) statusText.gameObject.SetActive(!visible);
        RefreshFightHeader();
    }

    public void SetFragileWarning(bool visible, bool flashRed)
    {
        if (loosenDragLabel == null) return;
        loosenDragLabel.gameObject.SetActive(visible);
        loosenDragOutline.effectColor = flashRed ? Color.red : Color.white;
        // Keep HP in its usual place; show the warning below the health bar.
        fightPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, visible ? 214f : 174f);
    }

    public void SetFightMeters(
        float tension,
        float health, int maxHealth=100)
    {
        tension =
            Mathf.Clamp01(tension);

        health =
            Mathf.Clamp01(health);

        if (tensionFill != null)
        {
            SetBarWidth(
                tensionFill.rectTransform,
                tension
            );

            tensionFill.color =
                Color.Lerp(
                    new Color(
                        0.22f,
                        0.78f,
                        0.42f
                    ),
                    new Color(
                        0.95f,
                        0.20f,
                        0.12f
                    ),
                    Mathf.InverseLerp(
                        0.55f,
                        0.95f,
                        tension
                    )
                );
        }

        bool bossHealth = system != null && system.IsBossFight;
        if (progressFill != null)
        {
            SetBarWidth(
                progressFill.rectTransform,
                health
            );
            // Only the Abyssal Leviathan uses red fight HP; reset for ordinary catches.
            progressFill.color = bossHealth ? new Color(.86f,.08f,.14f,1f) : new Color(.22f,.65f,.96f,1f);
        }

        if (tensionLabel != null)
        {
            tensionLabel.text =
                "Tension <color=#17F5FF>" +
                Mathf.RoundToInt(
                    tension * 100f
                ) +
                "%</color>";
        }

        if (progressLabel != null)
        {
            progressLabel.text = bossHealth
                ? $"HP <color=#FF334D>{Mathf.RoundToInt(health*maxHealth)}/{maxHealth}</color>"
                : $"HP <color=#17F5FF>{Mathf.RoundToInt(health*maxHealth)}/{maxHealth}</color>";
        }
    }

    // Hide the temporary center-screen catch card immediately when the rod is re-equipped.
    public void DismissCatchPopup()
    {
        catchPanelTimer = 0f;
        if (catchPanel != null)
            catchPanel.SetActive(false);
    }

    public void ShowCatch(
        CaughtFishRecord record,
        string heading = "CAUGHT!",
        bool personalBest = false)
    {
        if (record == null || catchPanel == null ||
            catchText == null)
        {
            return;
        }

        FishSpeciesDefinition species =
            FishCatalog.Get(record.speciesId);

        catchText.text = species.Name;
        catchSpecimenText.text = FishingTuning.SpecimenClass(record.speciesId,record.weightKg);
        catchSizeText.text = record.weightKg.ToString("0.00") + " kg \u2022 " +
            ShopCatalog.FishLength(record.speciesId,record.weightKg).ToString("0.00") + " m";
        catchBestText.gameObject.SetActive(personalBest);
        catchPanel.GetComponent<RectTransform>().sizeDelta =
            new Vector2(520f,personalBest ? 284f : 240f);

        catchPanel.SetActive(true);
        catchPanelTimer = 3.5f;
    }

    public void RefreshInventory()
    {
        if(GetComponent<ShopWorldHUD>()!=null) return;
        if (inventoryList == null ||
            system == null ||
            system.Inventory == null)
        {
            return;
        }

        for (int i =
                inventoryList.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                inventoryList
                    .GetChild(i)
                    .gameObject
            );
        }

        IReadOnlyList<CaughtFishRecord> fish =
            system.Inventory.Fish;

        if (emptyInventoryText != null)
        {
            emptyInventoryText.gameObject
                .SetActive(
                    fish.Count == 0
                );
        }

        int visibleCount = fish.Count;
        inventoryList.sizeDelta=new Vector2(0f,Mathf.Max(1f,visibleCount*64f));

        for (int row = 0;
             row < visibleCount;
             row++)
        {
            int inventoryIndex =
                fish.Count - 1 - row;

            CaughtFishRecord record =
                fish[inventoryIndex];

            FishSpeciesDefinition species =
                FishCatalog.Get(
                    record.speciesId
                );

            GameObject rowObject =
                CreatePanel(
                    "FishRow_" + inventoryIndex,
                    inventoryList,
                    new Color(
                        0.08f,
                        0.12f,
                        0.14f,
                        0.92f
                    )
                );

            RectTransform rowRect =
                rowObject.GetComponent<RectTransform>();

            rowRect.anchorMin =
                new Vector2(0f, 1f);

            rowRect.anchorMax =
                new Vector2(1f, 1f);

            rowRect.pivot =
                new Vector2(0.5f, 1f);

            rowRect.sizeDelta =
                new Vector2(0f, 58f);

            rowRect.anchoredPosition =
                new Vector2(
                    0f,
                    -row * 64f
                );

            Button button =
                rowObject.AddComponent<Button>();

            int capturedIndex =
                inventoryIndex;

            button.onClick.AddListener(
                () =>
                    system.HoldFish(
                        capturedIndex
                    )
            );

            Text label =
                CreateText(
                    "Label",
                    rowObject.transform,
                    species.Name +
                    "   " +
                    record.weightKg
                        .ToString("0.00") +
                    " kg",
                    24,
                    TextAnchor.MiddleLeft
                );

            RectTransform labelRect =
                label.rectTransform;

            labelRect.anchorMin =
                new Vector2(0f, 0f);

            labelRect.anchorMax =
                new Vector2(1f, 1f);

            labelRect.offsetMin =
                new Vector2(18f, 0f);

            labelRect.offsetMax =
                new Vector2(-12f, 0f);
        }
    }

    public void SetMenuCovered(bool covered)
    {
        if(root!=null) root.SetActive(!covered);
        if(actionInput!=null) actionInput.ResetInput();
    }
    public void SetInventoryOpen(bool open)
    {
        if(open)
        {
            ShopWorldHUD shop=FindFirstObjectByType<ShopWorldHUD>();
            if(shop!=null) { shop.Open("bag"); return; }
        }
        if (inventoryPanel == null)
            return;

        if (open)
            RefreshInventory();

        inventoryPanel.SetActive(open);
    }

    private void BuildUi()
    {
        root =
            new GameObject(
                "FishingHUDRoot",
                typeof(RectTransform)
            );

        root.transform.SetParent(
            transform,
            false
        );

        RectTransform rootRect =
            root.GetComponent<RectTransform>();

        StretchFullScreen(rootRect);

        BuildHotbar();
        BuildActionButton();
        BuildStatus();
        BuildFightPanel();
        BuildInventory();
        BuildCatchPanel();
        root.transform.SetAsLastSibling();
    }

    private void BuildHotbar()
    {
        GameObject hotbar =
            new GameObject(
                "Hotbar",
                typeof(RectTransform)
            );

        hotbarObject=hotbar;
        hotbar.transform.SetParent(
            root.transform,
            false
        );

        RectTransform hotbarRect =
            hotbar.GetComponent<RectTransform>();

        hotbarRect.anchorMin =
            new Vector2(0.5f, 0f);

        hotbarRect.anchorMax =
            new Vector2(0.5f, 0f);

        hotbarRect.pivot =
            new Vector2(0.5f, 0f);

        hotbarRect.sizeDelta =
            new Vector2(610f, 120f);

        hotbarRect.anchoredPosition =
            new Vector2(0f, 24f);

        for (int i = 0; i < 5; i++)
        {
            GameObject slot =
                CreatePanel(
                    "Slot_" + (i + 1),
                    hotbar.transform,
                    new Color(
                        0.05f,
                        0.07f,
                        0.09f,
                        0.72f
                    )
                );

            RectTransform slotRect =
                slot.GetComponent<RectTransform>();

            slotRect.anchorMin =
                new Vector2(0f, 0.5f);

            slotRect.anchorMax =
                new Vector2(0f, 0.5f);

            slotRect.pivot =
                new Vector2(0f, 0.5f);

            slotRect.sizeDelta =
                new Vector2(110f, 110f);

            slotRect.anchoredPosition =
                new Vector2(
                    i * 122f,
                    0f
                );

            Outline outline =
                slot.AddComponent<Outline>();

            outline.effectColor =
                new Color(
                    0.22f,
                    0.28f,
                    0.31f,
                    0.70f
                );

            outline.effectDistance =
                new Vector2(2f, -2f);

            Text number =
                CreateText(
                    "Number",
                    slot.transform,
                    (i + 1).ToString(),
                    16,
                    TextAnchor.UpperLeft
                );

            number.color =
                new Color(
                    0.68f,
                    0.75f,
                    0.78f,
                    0.92f
                );

            RectTransform numberRect =
                number.rectTransform;

            numberRect.anchorMin =
                new Vector2(0f, 0f);

            numberRect.anchorMax =
                new Vector2(1f, 1f);

            numberRect.offsetMin =
                new Vector2(7f, 3f);

            numberRect.offsetMax =
                new Vector2(-4f, -3f);

            if(i==1)system.GetComponent<BoatSystem>()?.BindSlot(slot);
            if (i == 0)
            {
                rodSlotImage =
                    slot.GetComponent<Image>();

                rodSlotOutline = outline;

                Button button =
                    slot.AddComponent<Button>();

                button.onClick.AddListener(
                    system.ToggleRod
                );

                CreateRodIcon(slot.transform);
            }
        }
    }

    private void CreateRodIcon(Transform parent)
    {
        var icon=new GameObject("AuthoredRodIcon",typeof(RectTransform),typeof(RawImage));
        icon.transform.SetParent(parent,false);
        var image=icon.GetComponent<RawImage>();image.raycastTarget=false;
        image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;
        image.rectTransform.offsetMin=new Vector2(5,5);image.rectTransform.offsetMax=new Vector2(-5,-5);
        // The hotbar is the sole transparent-artwork variant.
        // It follows the equipped tier when players switch rods.
        icon.AddComponent<FishingHotbarRodArtwork>().Initialize(image);
    }

    private void BuildActionButton()
    {
        actionButtonObject =
            CreatePanel(
                "FishingActionButton",
                root.transform,
                new Color(
                    0.05f,
                    0.30f,
                    0.42f,
                    0.88f
                )
            );

        RectTransform rect =
            actionButtonObject
                .GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(1f, 0f);

        rect.anchorMax =
            new Vector2(1f, 0f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.sizeDelta =
            new Vector2(280f, 216f);

        rect.anchoredPosition =
            new Vector2(
                -190f,
                440f
            );

        Outline outline =
            actionButtonObject
                .AddComponent<Outline>();

        outline.effectColor =
            new Color(
                0.36f,
                0.90f,
                1f,
                0.85f
            );

        outline.effectDistance =
            new Vector2(4f, -4f);

        FishingHudTheme.Panel(actionButtonObject,1);
        outline.effectDistance = new Vector2(2f,-2f);
        var artworkObject = new GameObject("ActionArtwork",typeof(RectTransform),typeof(Image));
        artworkObject.transform.SetParent(actionButtonObject.transform,false);
        actionArtwork = artworkObject.GetComponent<Image>();
        actionArtwork.raycastTarget = false;
        actionArtwork.preserveAspect = true;
        actionArtwork.sprite = FishingActionArtwork.Get(false);
        var artworkRect = actionArtwork.rectTransform;
        artworkRect.anchorMin = artworkRect.anchorMax = new Vector2(.5f,1f);
        artworkRect.sizeDelta = new Vector2(104f,104f);
        artworkRect.anchoredPosition = new Vector2(0,-70f);
        actionInput =
            actionButtonObject
                .AddComponent<FishingActionButton>();

        actionLabel =
            CreateText(
                "Label",
                actionButtonObject.transform,
                "CAST",
                42,
                TextAnchor.MiddleCenter
            );
        actionLabel.fontStyle = FontStyle.Bold;
        var labelRect = actionLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = new Vector2(1,.42f);
        labelRect.offsetMin = new Vector2(8,12); labelRect.offsetMax = new Vector2(-8,0);
    }

    private void BuildStatus()
    {
        statusText =
            CreateText(
                "FishingStatus",
                root.transform,
                string.Empty,
                27,
                TextAnchor.MiddleCenter
            );

        RectTransform rect =
            statusText.rectTransform;

        rect.anchorMin =
            new Vector2(0.5f, 1f);

        rect.anchorMax =
            new Vector2(0.5f, 1f);

        rect.pivot =
            new Vector2(0.5f, 1f);

        rect.sizeDelta =
            new Vector2(700f, 70f);

        rect.anchoredPosition =
            new Vector2(0f, -24f);

        statusText.color =
            new Color(
                0.93f,
                0.98f,
                1f,
                0.96f
            );
    }

    private void BuildFightPanel()
    {
        fightPanel =
            CreatePanel(
                "FightPanel",
                root.transform,
                new Color(
                    0.02f,
                    0.05f,
                    0.07f,
                    0.78f
                )
            );

        RectTransform panelRect =
            fightPanel.GetComponent<RectTransform>();

        panelRect.anchorMin =
            new Vector2(0.5f, 1f);

        panelRect.anchorMax =
            new Vector2(0.5f, 1f);

        panelRect.pivot =
            new Vector2(0.5f, 1f);

        panelRect.sizeDelta =
            new Vector2(720f, 174f);
        // Scale the entire status panel, including labels, meters and warnings.
        panelRect.localScale = Vector3.one * 1.1f;

        panelRect.anchoredPosition =
            new Vector2(0f, -32f);

        FishingHudTheme.Panel(fightPanel);
        FishingHudTheme.Divider(fightPanel.transform,72);
        fightPanel.GetComponent<Image>().raycastTarget = false;
        fightStatusText = CreateText("FightState",fightPanel.transform,"",28,TextAnchor.MiddleLeft);
        fightStatusText.fontStyle = FontStyle.Bold;
        PlaceHeader(fightStatusText.rectTransform,new Vector2(90,-17),new Vector2(330,46));
        fightHintText = CreateText("FightHint",fightPanel.transform,"",23,TextAnchor.MiddleLeft);
        fightHintText.resizeTextForBestFit = true; fightHintText.resizeTextMinSize = 18; fightHintText.resizeTextMaxSize = 23;
        PlaceHeader(fightHintText.rectTransform,new Vector2(425,-17),new Vector2(275,46));
        var icon = new GameObject("FightStateIcon",typeof(RectTransform),typeof(RawImage));
        icon.transform.SetParent(fightPanel.transform,false);
        fightStatusIcon = icon.GetComponent<RawImage>(); fightStatusIcon.raycastTarget = false;
        PlaceHeader(fightStatusIcon.rectTransform,new Vector2(25,-13),new Vector2(54,54));
        CreateBar(
            fightPanel.transform,
            "TENSION",
            new Vector2(0f, -82f),
            new Color(
                0.22f,
                0.78f,
                0.42f
            ),
            out tensionFill,
            out tensionLabel
        );

        CreateBar(
            fightPanel.transform,
            "HEALTH",
            new Vector2(0f, -124f),
            new Color(
                0.22f,
                0.65f,
                0.96f
            ),
            out progressFill,
            out progressLabel
        );

        loosenDragLabel = CreateText("LoosenDragWarning", fightPanel.transform,
            "LOOSEN DRAG", 30, TextAnchor.MiddleCenter);
        loosenDragLabel.color = Color.red;
        loosenDragLabel.fontStyle = FontStyle.Bold;
        loosenDragLabel.raycastTarget = false;
        var warningRect = loosenDragLabel.rectTransform;
        warningRect.anchorMin = warningRect.anchorMax = new Vector2(0f, 1f);
        warningRect.pivot = new Vector2(0f, 1f);
        warningRect.anchoredPosition = new Vector2(250f, -167f);
        warningRect.sizeDelta = new Vector2(435f, 42f);
        loosenDragOutline = loosenDragLabel.gameObject.AddComponent<Outline>();
        loosenDragOutline.effectColor = Color.white;
        loosenDragOutline.effectDistance = new Vector2(2f, -2f);
        loosenDragLabel.gameObject.SetActive(false);
        fightPanel.SetActive(false);
    }

    private static void PlaceHeader(RectTransform r, Vector2 position, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1);
        r.anchoredPosition = position; r.sizeDelta = size;
    }

    private void RefreshFightHeader()
    {
        if (fightStatusText == null || (renderedStatus == currentStatus && renderedDrag == displayDrag)) return;
        renderedStatus = currentStatus; renderedDrag = displayDrag;
        string state = currentStatus.Split(new[] { " - " }, System.StringSplitOptions.None)[0];
        string key = state.Contains("STUNNED") ? "Stunned" : state.Contains("UNCONSCIOUS") ? "Fainted" :
            state.Contains("CONTROLLED") || state.Contains("SIREN'S CALL") ? "SirensCall" :
            state.Contains("FRAGILE") ? "LookingForCover" : state.Contains("ANGRY") ? "Angry" :
            state.Contains("IRRITATED") ? "Irritated" : "Calm";
        string title = key == "SirensCall" ? "Controlled" : key == "LookingForCover" ? "Fragile" : key == "Fainted" ? "Subdued" : key;
        string[] dragNames = { "Low", "Medium", "High" };
        fightStatusText.text = title + " • " + dragNames[displayDrag] + " Drag";
        int split = currentStatus.IndexOf(" - ", System.StringComparison.Ordinal);
        string hint = split >= 0 ? currentStatus.Substring(split + 3) : currentStatus;
        hint = hint.Replace("HIGH DRAG: ", "");
        if (hint.Length > 0) hint = char.ToUpperInvariant(hint[0]) + hint.Substring(1);
        fightHintText.text = hint;
        if (!hudStatusTextures.TryGetValue(key, out var texture))
        {
            texture = Resources.Load<Texture2D>("Fishing/StatusIcons/" + key);
            hudStatusTextures[key] = texture;
        }
        fightStatusIcon.texture = texture;
        fightStatusIcon.enabled = texture != null;
    }

    private void CreateBar(
        Transform parent,
        string label,
        Vector2 position,
        Color fillColor,
        out Image fill,
        out Text labelText)
    {
        labelText =
            CreateText(
                label + "Label",
                parent,
                label + " 0%",
                24,
                TextAnchor.MiddleLeft
            );

        labelText.resizeTextForBestFit = true;
        labelText.resizeTextMinSize = 19; labelText.resizeTextMaxSize = 24;
        Text text = labelText;

        RectTransform textRect =
            text.rectTransform;

        textRect.anchorMin =
            new Vector2(0f, 1f);

        textRect.anchorMax =
            new Vector2(0f, 1f);

        textRect.pivot =
            new Vector2(0f, 1f);

        textRect.sizeDelta =
            new Vector2(222f, 34f);

        textRect.anchoredPosition =
            new Vector2(
                18f,
                position.y
            );

        GameObject background =
            CreatePanel(
                label + "Background",
                parent,
                new Color(
                    0.14f,
                    0.16f,
                    0.17f,
                    0.96f
                )
            );

        FishingHudTheme.Panel(background,3);
        background.GetComponent<Image>().raycastTarget = false;
        RectTransform bgRect =
            background.GetComponent<RectTransform>();

        bgRect.anchorMin =
            new Vector2(0f, 1f);

        bgRect.anchorMax =
            new Vector2(0f, 1f);

        bgRect.pivot =
            new Vector2(0f, 1f);

        bgRect.sizeDelta =
            new Vector2(420f, 26f);

        bgRect.anchoredPosition =
            new Vector2(
                270f,
                position.y - 3f
            );

        GameObject fillObject =
            CreatePanel(
                label + "Fill",
                background.transform,
                fillColor
            );

        fill =
            fillObject.GetComponent<Image>();

        FishingHudTheme.Fill(fill);
        fill.type = Image.Type.Sliced;
        fill.raycastTarget = false;

        RectTransform fillRect =
            fill.rectTransform;

        fillRect.anchorMin =
            new Vector2(0f, 0f);

        fillRect.anchorMax =
            new Vector2(0f, 1f);

        fillRect.pivot =
            new Vector2(0f, 0.5f);

        fillRect.offsetMin =
            Vector2.zero;

        fillRect.offsetMax =
            Vector2.zero;
    }

    private static void SetBarWidth(
        RectTransform rect,
        float amount)
    {
        Vector2 min =
            rect.anchorMin;

        Vector2 max =
            rect.anchorMax;

        min.x = 0f;
        max.x = Mathf.Clamp01(amount);

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void BuildInventory()
    {
        GameObject inventoryButton =
            CreatePanel(
                "InventoryButton",
                root.transform,
                new Color(
                    0.07f,
                    0.10f,
                    0.12f,
                    0.88f
                )
            );

        RectTransform buttonRect =
            inventoryButton
                .GetComponent<RectTransform>();

        buttonRect.anchorMin =
            new Vector2(1f, 1f);

        buttonRect.anchorMax =
            new Vector2(1f, 1f);

        buttonRect.pivot =
            new Vector2(1f, 1f);

        buttonRect.sizeDelta =
            new Vector2(150f, 58f);

        buttonRect.anchoredPosition =
            new Vector2(-28f, -28f);

        Button button =
            inventoryButton.AddComponent<Button>();

        button.onClick.AddListener(
            () =>
                SetInventoryOpen(
                    !inventoryPanel.activeSelf
                )
        );

        if(GetComponent<ShopWorldHUD>()!=null) inventoryButton.SetActive(false);

        Text inventoryButtonText =
            CreateText(
                "Label",
                inventoryButton.transform,
                "FISH",
                24,
                TextAnchor.MiddleCenter
            );

        StretchFullScreen(
            inventoryButtonText.rectTransform
        );

        inventoryPanel =
            CreatePanel(
                "InventoryPanel",
                root.transform,
                new Color(
                    0.025f,
                    0.045f,
                    0.055f,
                    0.96f
                )
            );

        RectTransform panelRect =
            inventoryPanel
                .GetComponent<RectTransform>();

        panelRect.anchorMin =
            new Vector2(1f, 0.5f);

        panelRect.anchorMax =
            new Vector2(1f, 0.5f);

        panelRect.pivot =
            new Vector2(1f, 0.5f);

        panelRect.sizeDelta =
            new Vector2(520f, 720f);

        panelRect.anchoredPosition =
            new Vector2(-28f, 0f);

        Text title =
            CreateText(
                "Title",
                inventoryPanel.transform,
                "CAUGHT FISH",
                34,
                TextAnchor.MiddleLeft
            );

        RectTransform titleRect =
            title.rectTransform;

        titleRect.anchorMin =
            new Vector2(0f, 1f);

        titleRect.anchorMax =
            new Vector2(1f, 1f);

        titleRect.pivot =
            new Vector2(0.5f, 1f);

        titleRect.sizeDelta =
            new Vector2(0f, 70f);

        titleRect.offsetMin =
            new Vector2(22f, -70f);

        titleRect.offsetMax =
            new Vector2(-90f, 0f);

        GameObject closeObject =
            CreatePanel(
                "Close",
                inventoryPanel.transform,
                new Color(
                    0.30f,
                    0.10f,
                    0.10f,
                    0.92f
                )
            );

        RectTransform closeRect =
            closeObject
                .GetComponent<RectTransform>();

        closeRect.anchorMin =
            new Vector2(1f, 1f);

        closeRect.anchorMax =
            new Vector2(1f, 1f);

        closeRect.pivot =
            new Vector2(1f, 1f);

        closeRect.sizeDelta =
            new Vector2(64f, 52f);

        closeRect.anchoredPosition =
            new Vector2(-14f, -14f);

        Button closeButton =
            closeObject.AddComponent<Button>();

        closeButton.onClick.AddListener(
            () =>
                SetInventoryOpen(false)
        );

        Text closeText =
            CreateText(
                "Label",
                closeObject.transform,
                "X",
                26,
                TextAnchor.MiddleCenter
            );

        StretchFullScreen(
            closeText.rectTransform
        );

        GameObject viewport = new GameObject("FishViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
        viewport.transform.SetParent(inventoryPanel.transform,false);
        RectTransform viewportRect=viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin=Vector2.zero; viewportRect.anchorMax=Vector2.one;
        viewportRect.offsetMin=new Vector2(20f,24f); viewportRect.offsetMax=new Vector2(-20f,-88f);
        viewport.GetComponent<Image>().color=new Color(0,0,0,0.01f);
        GameObject listObject=new GameObject("FishList",typeof(RectTransform));
        listObject.transform.SetParent(viewport.transform,false);
        inventoryList=listObject.GetComponent<RectTransform>();
        inventoryList.anchorMin=new Vector2(0,1); inventoryList.anchorMax=Vector2.one;
        inventoryList.pivot=new Vector2(0.5f,1); inventoryList.sizeDelta=Vector2.zero;
        ScrollRect scroll=viewport.GetComponent<ScrollRect>();
        scroll.viewport=viewportRect; scroll.content=inventoryList; scroll.horizontal=false;
        scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35f;

        emptyInventoryText =
            CreateText(
                "Empty",
                inventoryPanel.transform,
                "No catches yet.\nEquip the rod and cast into the ocean.",
                24,
                TextAnchor.MiddleCenter
            );

        RectTransform emptyRect =
            emptyInventoryText.rectTransform;

        emptyRect.anchorMin =
            new Vector2(0f, 0f);

        emptyRect.anchorMax =
            new Vector2(1f, 1f);

        emptyRect.offsetMin =
            new Vector2(30f, 30f);

        emptyRect.offsetMax =
            new Vector2(-30f, -90f);

        emptyInventoryText.color =
            new Color(
                0.64f,
                0.73f,
                0.76f,
                0.94f
            );

        inventoryPanel.SetActive(false);
    }

    private void BuildCatchPanel()
    {
        catchPanel = CreatePanel("CatchPanel",root.transform,Color.white);
        FishingHudTheme.Panel(catchPanel);
        catchPanel.GetComponent<Image>().raycastTarget=false;
        var rect=catchPanel.GetComponent<RectTransform>();
        // Keep the top edge fixed when omitting the personal-best row.
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.pivot=new Vector2(.5f,1f);
        rect.sizeDelta=new Vector2(520f,240f);
        rect.anchoredPosition=new Vector2(0f,220f);

        catchText=CreateCatchRow("CatchText",string.Empty,36,24f,54f,Color.white);
        catchSpecimenText=CreateCatchRow("SpecimenClass",string.Empty,28,82f,42f,
            new Color(1f,.87f,.45f));
        FishingHudTheme.Divider(catchPanel.transform,151f);
        var divider=catchPanel.transform.Find("GlassDivider").GetComponent<Image>();
        divider.color=new Color(.1f,.88f,1f,.65f);
        divider.rectTransform.sizeDelta=new Vector2(-76f,2f);
        catchSizeText=CreateCatchRow("CatchMeasurements",string.Empty,30,170f,44f,Color.white);
        catchBestText=CreateCatchRow("PersonalBest","Personal Best",26,222f,38f,FishingHudTheme.Cyan);
        catchBestText.gameObject.SetActive(false);
        catchPanel.SetActive(false);
    }

    private Text CreateCatchRow(string name,string value,int size,float top,float height,Color color)
    {
        var text=CreateText(name,catchPanel.transform,value,size,TextAnchor.MiddleCenter);
        text.fontStyle=FontStyle.Bold;
        text.color=color;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;
        text.verticalOverflow=VerticalWrapMode.Truncate;
        text.resizeTextForBestFit=true;
        text.resizeTextMinSize=18;
        text.resizeTextMaxSize=size;
        var rect=text.rectTransform;
        rect.anchorMin=new Vector2(0f,1f);
        rect.anchorMax=Vector2.one;
        rect.pivot=new Vector2(.5f,1f);
        rect.sizeDelta=new Vector2(-48f,height);
        rect.anchoredPosition=new Vector2(0f,-top);
        return text;
    }

    private GameObject CreatePanel(
        string name,
        Transform parent,
        Color color)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        gameObject.transform.SetParent(
            parent,
            false
        );

        Image image =
            gameObject.GetComponent<Image>();

        image.color = color;

        return gameObject;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );

        gameObject.transform.SetParent(
            parent,
            false
        );

        Text text =
            gameObject.GetComponent<Text>();

        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private static void StretchFullScreen(
        RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}



