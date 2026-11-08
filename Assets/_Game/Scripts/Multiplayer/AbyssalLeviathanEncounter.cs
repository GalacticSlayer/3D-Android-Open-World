using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Server-owned in a room; local simulation when playing offline. Ordinary
// fishing still owns the rod, tension, abilities, landing and presentation.
public sealed class AbyssalLeviathanEncounter : MonoBehaviour
{
    [Serializable] public sealed class View
    {
        public string id,phase,owner,lease;
        public int hp,maxHp;
        public float x,y,z,kg,secondsLeft;
        public double serverTime;
    }
    [Serializable] public sealed class Command
    {
        public string op,id,lease,ticket;
        public int cast,damage,balance;
        public int[] caught;
        public float x,y,z;
    }
    [Serializable] private sealed class Request {public FishingMultiplayer.Pose pose;public Command boss;}
    [Serializable] private sealed class ReceiptRequest {public string ticket;}
    [Serializable] private sealed class Reply {public bool ok,accepted,hooked,caught;public string error;public View boss;}
    private FishingMultiplayer session;
    private FishingSystem fishing;
    private ShopProgress progress;
    private OceanWater ocean;
    private View current;
    private AbyssalLeviathanLocalState local;
    private Vector3 localPosition;
    private bool Online => session!=null && session.Connected;
    private string OwnerId => local!=null ? "offline" : session?.LocalId;
    public bool BlocksMultiplayer => local!=null && (Active || HasLease || busy);
    public void BeginMultiplayer()
    {
        // Called only after a successful connection; never import a private boss
        // into another room or let a disconnected room boss become a local one.
        local=null;current=null;announced="";lease="";fightId="";
    }
    private string lease="",fightId="",announced="";
    private int damage;
    private float receivedAt,nextReport,nextRipple,nextRecovery,noticeUntil,nextLocalSnapshot;
    private bool busy,landing;
    private GameObject hudRoot,noticeRoot,abilityGuideButton,abilityGuideBorder,abilityGuidePopup;
    private RawImage abilityGuideArtwork;
    private FishingHUD fishingHud;
    private bool abilityGuideSeen;
    private const string AbilityGuideSeenKey = "AbyssalLeviathan.HardenedScalesGuideSeen";
    private Text status,notice;
    private RawImage awakeningArtwork;
    private bool awakeningNotice;
    private Image health;
    private RectTransform healthFill;
    private Text healthTitle;
    public const float CatchRegionRadius = 80f;
    public bool NavigationArrived { get; private set; }
    public Vector3 RegionCenter => current == null ? Vector3.zero : new Vector3(current.x,current.y,current.z);
    public string Status {get;private set;}="";
    public bool Active => (local!=null || Online) && current!=null && current.phase=="active" && Remaining>0;
    public float Remaining => current==null?0:Mathf.Max(0,current.secondsLeft-(Time.realtimeSinceStartup-receivedAt));
    public bool HasLease => !string.IsNullOrEmpty(lease);
    public int InitialHealth => current!=null?current.hp:220000;
    public float Weight => current!=null?current.kg:420f;

    private void Awake()
    {
        session=GetComponent<FishingMultiplayer>();fishing=GetComponent<FishingSystem>();progress=GetComponent<ShopProgress>();
        abilityGuideSeen=PlayerPrefs.GetInt(AbilityGuideSeenKey,0)!=0;
        BuildHud();
    }
    public bool Unlocked(out int found,out int requiredCount)
    {
        var ids=new HashSet<int>();
        foreach(int island in new[]{0,1,3,4})foreach(int id in FishIndexCatalog.RegionSpecies(island))ids.Add(id);
        found=0;requiredCount=ids.Count;
        foreach(int id in ids)if(FishIndexCatalog.FoundVariants(progress?.Data,id)>0)found++;
        return requiredCount>0 && found==requiredCount;
    }
    public bool CanSummon => (session==null || !session.Busy) && (!Online || session.BossReady) && !busy && !Active && progress!=null && !progress.ReadOnly &&
        string.IsNullOrEmpty(progress.Data.bossSummonTicket) && progress.Data.coins>=10000 && Unlocked(out _,out _);
    public void Summon()
    {
        if(!CanSummon)return;
        if(!BluewaterCayRockRegion.TryBossSpawn(out var point)){ShowNotice("Bluewater Cay rocks are not ready. Try again shortly.");return;}
        if(!Online)
        {
            int previous=progress.Data.coins;
            progress.Data.coins-=10000;
            try{progress.Save();}catch(Exception){progress.Data.coins=previous;ShowNotice("Could not save the summon fee.");return;}
            local=new AbyssalLeviathanLocalState(Time.realtimeSinceStartup,UnityEngine.Random.value);
            localPosition=point;current=null;RefreshLocal();return;
        }
        string ticket=Guid.NewGuid().ToString("N");int balance=progress.Data.coins;
        progress.Data.bossSummonTicket=ticket;progress.Data.coins-=10000;
        try{progress.Save();}catch(Exception){progress.Data.bossSummonTicket=null;progress.Data.coins=balance;ShowNotice("Could not save the summon fee.");return;}
        var caught=new List<int>();
        foreach(int id in FishCatalog.ActiveIds)if(FishIndexCatalog.Discovered(progress.Data,id))caught.Add(id);
        StartCoroutine(SummonRoutine(new Command{op="summon",ticket=ticket,balance=balance,caught=caught.ToArray(),x=point.x,y=point.y,z=point.z}));
    }
    private IEnumerator SummonRoutine(Command command)
    {
        busy=true;Reply result=null;
        yield return Send(command,r=>result=r);
        if(result!=null && result.ok && result.accepted)SettleFee(true);
        else ShowNotice(result?.error??"Checking your summon fee after a connection interruption.");
        busy=false;nextRecovery=0;
    }
    private void SettleFee(bool accepted)
    {
        string ticket=progress.Data.bossSummonTicket;if(string.IsNullOrEmpty(ticket))return;
        int old=progress.Data.coins;
        progress.Data.bossSummonTicket=null;
        if(!accepted)progress.Data.coins=(int)Math.Min(int.MaxValue,(long)old+10000);
        try{progress.Save();if(!accepted)ShowNotice("Summon did not start. Your 10,000 coins were refunded.");}
        catch(Exception){progress.Data.bossSummonTicket=ticket;progress.Data.coins=old;}
    }
    private IEnumerator RecoverFee()
    {
        busy=true;nextRecovery=Time.realtimeSinceStartup+5f;Reply reply=null;
        yield return Post("/v1/boss-receipt",JsonUtility.ToJson(new ReceiptRequest{ticket=progress.Data.bossSummonTicket}),null,r=>reply=r);
        if(reply!=null && reply.ok)SettleFee(reply.accepted);
        busy=false;
    }
    public void Apply(View view)
    {
        if(local!=null || view==null)return;
        ApplyView(view);
    }
    private void ApplyView(View view)
    {
        if(current!=null && view.serverTime<current.serverTime)return;
        if (current == null || current.id != view.id) NavigationArrived = false;
        current=view;receivedAt=Time.realtimeSinceStartup;
        if(view.phase=="active" && announced!=view.id)
        {
            announced=view.id;nextRipple=0;
            ShowNotice("Abyssal Leviathan has awakened on the southern end of Bluewater Cay. Catch it in 15 minutes before it returns to the abyss!");
        }
    }
    public bool TryHook(Vector3 point,int cast)
    {
        if(!Active || !string.IsNullOrEmpty(current.owner) || Vector2.Distance(new Vector2(point.x,point.z),new Vector2(current.x,current.z))>CatchRegionRadius)return false;
        StartCoroutine(Claim(cast));return true;
    }
    private IEnumerator Claim(int cast)
    {
        while(busy)yield return null;
        if(!fishing.BossCastWaiting(cast))yield break;
        busy=true;Reply result=null;
        yield return Send(new Command{op="claim",id=current.id,cast=cast},r=>result=r);
        busy=false;
        if(result!=null && result.ok && result.hooked && result.boss!=null)
        {
            fightId=result.boss.id;lease=result.boss.lease;damage=0;
            if(fishing.BossCastWaiting(cast) && Active && current.owner==OwnerId && current.lease==lease)
                fishing.BeginBossFight(Mathf.Clamp(result.boss.hp,0,result.boss.maxHp>0?result.boss.maxHp:220000),result.boss.kg);
            else Release();
        }
        fishing.BossClaimFinished(cast);
    }
    public void ReportDamage(int amount){if(HasLease)damage=Mathf.Clamp(damage+Mathf.Max(0,amount),0,220000);}
    public void Release()
    {
        if(!HasLease)return;
        var command=FightCommand("release");lease="";fightId="";landing=false;
        StartCoroutine(ReleaseRoutine(command));
    }
    private IEnumerator ReleaseRoutine(Command command)
    {
        while(busy)yield return null;
        busy=true;yield return Send(command,_=>{});busy=false;
    }
    private Command FightCommand(string op)=>new Command{op=op,id=fightId,lease=lease,damage=damage};
    public void Land(Action<bool> done)
    {
        if(landing || !HasLease)return;landing=true;StartCoroutine(LandRoutine(done));
    }
    private IEnumerator LandRoutine(Action<bool> done)
    {
        while(busy)yield return null;
        if(!HasLease){landing=false;done(false);yield break;}
        var command=FightCommand("catch");busy=true;Reply reply=null;
        yield return Send(command,r=>reply=r);
        bool success=reply!=null && reply.ok && reply.caught;
        if(success){lease="";fightId="";}
        busy=false;landing=false;done(success);
    }
    private IEnumerator Report()
    {
        busy=true;Reply reply=null;yield return Send(FightCommand("damage"),r=>reply=r);busy=false;
        if(reply!=null && !reply.ok && HasLease){lease="";fightId="";fishing.AbortBossFight(reply.error);}
    }
    private void Update()
    {
        if(session==null)session=GetComponent<FishingMultiplayer>();
        if(fishing==null)fishing=GetComponent<FishingSystem>();
        if(progress==null)progress=GetComponent<ShopProgress>();
        if(local!=null && Time.realtimeSinceStartup>=nextLocalSnapshot)
        {nextLocalSnapshot=Time.realtimeSinceStartup+.1f;RefreshLocal();}
        bool active=Active;
        // Hide the universal bar only on this player's screen while they fight any fish.
        // The encounter continues to tick and replicate for everyone else.
        var destination = ShopDimensionManager.Instance;
        bool inWorld = destination == null || (!destination.InDimension && !destination.Traveling);
        if (active && inWorld && !NavigationArrived)
        {
            Vector3 offset = transform.position - RegionCenter; offset.y = 0;
            if (offset.sqrMagnitude <= 85f * 85f) NavigationArrived = true;
        }
        bool showBossHealth = active && NavigationArrived && inWorld && !ShopWorldHUD.MenuOpen &&
            (fishing==null || (!fishing.HasHookedFish && !fishing.IsFishPosing));
        hudRoot.SetActive(showBossHealth);
        // Hide the ordinary white casting hint only while this HP bar is visible.
        // FishingHUD combines this with compass coverage so the hint returns
        // correctly regardless of Update/LateUpdate order.
        if(fishingHud==null)fishingHud=FindFirstObjectByType<FishingHUD>();
        if(fishingHud!=null)fishingHud.SetBossHealthCovered(showBossHealth);
        bool guideAvailable=active && inWorld && !ShopWorldHUD.MenuOpen &&
            (fishing==null || !fishing.IsFishPosing);
        if(!active && abilityGuidePopup.activeSelf)abilityGuidePopup.SetActive(false);
        abilityGuideButton.SetActive(guideAvailable && !abilityGuidePopup.activeSelf);
        abilityGuideBorder.SetActive(guideAvailable && !abilityGuideSeen);
        if(active)
        {
            int hp=current.hp;
            if(HasLease && fishing.IsBossFight)hp=fishing.CurrentFishHealth;
            int maximumHp = current.maxHp > 0 ? current.maxHp : 220000;
            float fraction = Mathf.Clamp01(hp / (float)maximumHp);
            healthFill.anchorMax = new Vector2(fraction,1);
            health.gameObject.SetActive(fraction > 0);
            int left=Mathf.CeilToInt(Remaining);
            status.text=$"{hp:N0} / {maximumHp:N0} HP     •     {left/60}:{left%60:00}";
            if(ocean==null)ocean=FindFirstObjectByType<OceanWater>();
            var travel=ShopDimensionManager.Instance;
            if(ocean!=null && (travel==null || travel.Destination==0) && Time.realtimeSinceStartup>=nextRipple)
            {
                nextRipple=Time.realtimeSinceStartup+1.5f;
                AbyssalBloodRipple.Spawn(new Vector3(current.x,current.y,current.z),ocean);
            }
        }
        noticeRoot.SetActive(Time.realtimeSinceStartup<noticeUntil && (fishing==null || !fishing.IsFishPosing));
        // Keep awakening text below the shared compass rather than behind it.
        noticeRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, (active && !NavigationArrived) || WindgroveNavigation.CompassVisible ? -325 : -185);
        if(HasLease && !landing && (!active || current.owner!=OwnerId || current.lease!=lease))
        {lease="";fightId="";fishing.AbortBossFight("The Leviathan is no longer on your line.");}
        if(HasLease && !landing && !busy && Time.realtimeSinceStartup>=nextReport)
        {nextReport=Time.realtimeSinceStartup+.35f;StartCoroutine(Report());}
        if(progress!=null && !progress.ReadOnly && !busy && !HasLease && !string.IsNullOrEmpty(progress.Data.bossSummonTicket) && Time.realtimeSinceStartup>=nextRecovery)
            StartCoroutine(RecoverFee());
    }
    private IEnumerator Send(Command command,Action<Reply> done)
    {
        if(local!=null)
        {
            // Keep the same claim/damage/release/catch flow as multiplayer, but
            // resolve it in memory without contacting or opening a server.
            var result=LocalCommand(command);RefreshLocal();result.boss=current;done(result);yield break;
        }
        Reply reply=null;
        for(int i=0;i<3 && session!=null && session.Connected;i++)
        {
            yield return Post("/v1/boss",JsonUtility.ToJson(new Request{pose=session.LocalPose(),boss=command}),session.SessionToken,r=>reply=r);
            if(reply!=null)break;
        }
        if(reply?.boss!=null)Apply(reply.boss);
        done(reply);
    }
    private void RefreshLocal()
    {
        double now=Time.realtimeSinceStartup;local.Tick(now);
        ApplyView(new View{id=local.Id,phase=local.Phase,owner=string.IsNullOrEmpty(local.Lease)?"":"offline",lease=local.Lease,
            hp=(int)Math.Ceiling(local.Health),maxHp=AbyssalLeviathanLocalState.MaximumHealth,kg=local.Weight,
            x=localPosition.x,y=localPosition.y,z=localPosition.z,secondsLeft=(float)local.SecondsLeft(now),serverTime=now});
    }
    private Reply LocalCommand(Command command)
    {
        double now=Time.realtimeSinceStartup;
        if(command.id!=local.Id)return new Reply{error="This encounter has ended."};
        if(command.op=="claim")return new Reply{ok=true,hooked=local.TryHook(command.cast,UnityEngine.Random.value,now)};
        bool ok=false;
        if(command.op=="damage")ok=local.Damage(command.lease,command.damage,now);
        else if(command.op=="release")ok=local.Release(command.lease,command.damage,now);
        else if(command.op=="catch")ok=local.Catch(command.lease,command.damage,now);
        return new Reply{ok=ok,caught=ok && command.op=="catch",error=ok?null:"The Leviathan is no longer on your line."};
    }
    private static IEnumerator Post(string path,string json,string token,Action<Reply> done)
    {
        using(var request=new UnityWebRequest(FishingMultiplayer.Endpoint+path,"POST"))
        {
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=3;
            request.SetRequestHeader("Content-Type","application/json");if(token!=null)request.SetRequestHeader("Authorization","Bearer "+token);
            yield return request.SendWebRequest();Reply reply=null;
            if(request.responseCode>0 && request.responseCode<500)
                try{reply=JsonUtility.FromJson<Reply>(request.downloadHandler.text);}catch(ArgumentException){}
            done(reply);
        }
    }
    private void ShowNotice(string text)
    {
        Status=text;
        awakeningNotice=text.StartsWith("Abyssal Leviathan has awakened",StringComparison.Ordinal);
        // Use the original text as a fallback until the banner asset is imported.
        bool artworkReady=awakeningNotice && awakeningArtwork!=null && awakeningArtwork.texture!=null;
        if(awakeningArtwork!=null)awakeningArtwork.gameObject.SetActive(artworkReady);
        noticeRoot.GetComponent<Image>().enabled=!artworkReady;
        noticeRoot.GetComponent<RectTransform>().sizeDelta=artworkReady ? new Vector2(1080,215) : new Vector2(980,130);
        notice.gameObject.SetActive(!artworkReady);
        notice.text=text;
        noticeUntil=Time.realtimeSinceStartup+(awakeningNotice?10f:14f);
    }
    private void OpenAbilityGuide()
    {
        // Do not consume the one-time flashing prompt before its artwork exists.
        if(abilityGuideArtwork==null || abilityGuideArtwork.texture==null)
        {
            Debug.LogWarning("Hardened Scales guide image missing: import Assets/Resources/Fishing/HardenedScalesAbilityPopup.png");
            return;
        }
        abilityGuideSeen=true;
        PlayerPrefs.SetInt(AbilityGuideSeenKey,1);
        PlayerPrefs.Save();
        abilityGuideBorder.SetActive(false);
        abilityGuidePopup.SetActive(true);
        abilityGuidePopup.transform.SetAsLastSibling();
        abilityGuideButton.SetActive(false);
    }
    private void CloseAbilityGuide()
    {
        if(abilityGuidePopup!=null)abilityGuidePopup.SetActive(false);
    }
    private void OnDisable()
    {
        if(fishingHud!=null)fishingHud.SetBossHealthCovered(false);
    }
    private static Text Label(Transform parent,int size)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;
        t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new Vector2(16,4);t.rectTransform.offsetMax=new Vector2(-16,-4);return t;
    }
    private static GameObject Panel(Transform parent,string name,Vector2 size,float top,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);r.sizeDelta=size;r.anchoredPosition=new Vector2(0,-top);
        go.GetComponent<Image>().color=color;go.GetComponent<Image>().raycastTarget=false;return go;
    }
    private void BuildHud()
    {
        var go=new GameObject("Leviathan HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=215;
        var s=go.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.matchWidthOrHeight=.5f;
        hudRoot=Panel(go.transform,"Boss health",new Vector2(850,122),26,Color.white);
        hudRoot.GetComponent<Image>().enabled=false; // Keep the title, timer and HP meter without an outer panel.
        healthTitle=Label(hudRoot.transform,40); // 1.6x the former 25-point boss title.
        healthTitle.text="ABYSSAL LEVIATHAN";healthTitle.fontStyle=FontStyle.Bold;
        healthTitle.color=new Color(1f,.22f,.26f);
        var titleOutline=healthTitle.gameObject.AddComponent<Outline>();
        titleOutline.effectColor=Color.black;
        titleOutline.effectDistance=new Vector2(1.5f,-1.5f);
        titleOutline.useGraphicAlpha=true;
        healthTitle.rectTransform.anchorMin=new Vector2(0,.60f);
        var track=Panel(hudRoot.transform,"Health track",new Vector2(806,48),60,Color.white);
        FishingHudTheme.Panel(track,3);
        // A rounded mask keeps the red fill inside the frame, even at low HP.
        var clip=Panel(track.transform,"Health clip",new Vector2(798,40),4,Color.white);
        FishingHudTheme.Fill(clip.GetComponent<Image>());
        clip.AddComponent<Mask>().showMaskGraphic=false;
        var bar=Panel(clip.transform,"Health fill",Vector2.zero,0,Color.white);
        FishingHudTheme.Panel(bar,8);
        health=bar.GetComponent<Image>();healthFill=bar.GetComponent<RectTransform>();
        healthFill.anchorMin=Vector2.zero;healthFill.anchorMax=Vector2.one;
        healthFill.offsetMin=healthFill.offsetMax=Vector2.zero;
        status=Label(track.transform,22);status.fontStyle=FontStyle.Bold;
        noticeRoot=Panel(go.transform,"Awakening notification",new Vector2(980,130),185,new Color(.08f,.02f,.04f,.96f));
        notice=Label(noticeRoot.transform,26);
        var artworkGo=new GameObject("Abyssal Leviathan Awakening Banner",typeof(RectTransform),typeof(RawImage));
        artworkGo.transform.SetParent(noticeRoot.transform,false);
        awakeningArtwork=artworkGo.GetComponent<RawImage>();
        awakeningArtwork.texture=Resources.Load<Texture2D>("Fishing/AbyssalLeviathanAwakeningBanner");
        awakeningArtwork.raycastTarget=false;
        // Crop transparent top/bottom margins without modifying the supplied artwork.
        awakeningArtwork.uvRect=new Rect(0f,152f/682f,1f,385f/682f);
        var artworkRect=awakeningArtwork.rectTransform;
        artworkRect.anchorMin=Vector2.zero;
        artworkRect.anchorMax=Vector2.one;
        artworkRect.offsetMin=artworkRect.offsetMax=Vector2.zero;
        artworkGo.SetActive(false);

        // Show the guide throughout the active summon, not only after
        // the 85 m navigation-to-health-bar transition.
        abilityGuideButton=Panel(go.transform,"Boss Ability Guide",new Vector2(380,76),0,Color.white);
        FishingHudTheme.Panel(abilityGuideButton,1);
        var buttonRect=abilityGuideButton.GetComponent<RectTransform>();
        buttonRect.anchorMin=buttonRect.anchorMax=new Vector2(1,1);
        buttonRect.pivot=new Vector2(1,1);
        buttonRect.anchoredPosition=new Vector2(-36,-246);
        abilityGuideButton.GetComponent<Image>().raycastTarget=true;
        var button=abilityGuideButton.AddComponent<Button>();
        button.targetGraphic=abilityGuideButton.GetComponent<Image>();
        button.onClick.AddListener(OpenAbilityGuide);
        var buttonText=Label(abilityGuideButton.transform,25);
        buttonText.text="BOSS ABILITY GUIDE";
        buttonText.fontStyle=FontStyle.Bold;
        abilityGuideBorder=WindgroveNavigation.AddFlashingBorder(abilityGuideButton.transform);
        abilityGuideBorder.SetActive(false);

        // The popup uses the source aspect ratio and scales only uniformly:
        // no image stretching on tall or wide mobile displays.
        abilityGuidePopup=Panel(go.transform,"Hardened Scales Guide Popup",Vector2.zero,0,new Color(0,.015f,.035f,.78f));
        var overlayRect=abilityGuidePopup.GetComponent<RectTransform>();
        overlayRect.anchorMin=Vector2.zero;
        overlayRect.anchorMax=Vector2.one;
        overlayRect.offsetMin=overlayRect.offsetMax=Vector2.zero;
        abilityGuidePopup.GetComponent<Image>().raycastTarget=true;
        var fitBox=new GameObject("Guide Fit Bounds",typeof(RectTransform));
        fitBox.transform.SetParent(abilityGuidePopup.transform,false);
        var fitRect=fitBox.GetComponent<RectTransform>();
        fitRect.anchorMin=new Vector2(.04f,.03f);
        fitRect.anchorMax=new Vector2(.96f,.97f);
        fitRect.offsetMin=fitRect.offsetMax=Vector2.zero;
        var art=new GameObject("Hardened Scales Ability Artwork",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
        art.transform.SetParent(fitBox.transform,false);
        abilityGuideArtwork=art.GetComponent<RawImage>();
        abilityGuideArtwork.texture=Resources.Load<Texture2D>("Fishing/HardenedScalesAbilityPopup");
        abilityGuideArtwork.raycastTarget=false;
        var artRect=abilityGuideArtwork.rectTransform;
        artRect.anchorMin=artRect.anchorMax=artRect.pivot=new Vector2(.5f,.5f);
        artRect.anchoredPosition=Vector2.zero;
        var aspect=art.GetComponent<AspectRatioFitter>();
        aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio=1447f/1087f;
        // Transparent hit target precisely over the artwork's GOT IT button.
        // Clicks elsewhere are blocked from gameplay but do not dismiss it.
        var gotIt=new GameObject("GOT IT Hit Area",typeof(RectTransform),typeof(Image),typeof(Button));
        gotIt.transform.SetParent(art.transform,false);
        var hitRect=gotIt.GetComponent<RectTransform>();
        hitRect.anchorMin=new Vector2(.33f,.13f);
        hitRect.anchorMax=new Vector2(.67f,.26f);
        hitRect.offsetMin=hitRect.offsetMax=Vector2.zero;
        gotIt.GetComponent<Image>().color=Color.clear;
        gotIt.GetComponent<Image>().raycastTarget=true;
        gotIt.GetComponent<Button>().onClick.AddListener(CloseAbilityGuide);

        hudRoot.SetActive(false);
        noticeRoot.SetActive(false);
        abilityGuideButton.SetActive(false);
        abilityGuidePopup.SetActive(false);
    }
}
