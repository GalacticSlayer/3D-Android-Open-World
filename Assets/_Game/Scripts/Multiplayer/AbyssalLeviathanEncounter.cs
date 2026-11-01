using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// One server-owned encounter per room. The ordinary fishing system still owns
// the angler's fight, rod, tension, abilities, landing and inventory presentation.
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
    private string lease="",fightId="",announced="";
    private int damage;
    private float receivedAt,nextReport,nextRipple,nextRecovery,noticeUntil;
    private bool busy,landing;
    private GameObject hudRoot,noticeRoot;
    private Text status,notice;
    private Image health;
    public string Status {get;private set;}="";
    public bool Active => session!=null && session.Connected && current!=null && current.phase=="active" && Remaining>0;
    public float Remaining => current==null?0:Mathf.Max(0,current.secondsLeft-(Time.realtimeSinceStartup-receivedAt));
    public bool HasLease => !string.IsNullOrEmpty(lease);
    public int InitialHealth => current!=null?current.hp:150000;
    public float Weight => current!=null?current.kg:420f;

    private void Awake()
    {
        session=GetComponent<FishingMultiplayer>();fishing=GetComponent<FishingSystem>();progress=GetComponent<ShopProgress>();
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
    public bool CanSummon => session!=null && session.Connected && session.BossReady && !busy && !Active && progress!=null && !progress.ReadOnly &&
        string.IsNullOrEmpty(progress.Data.bossSummonTicket) && progress.Data.coins>=10000 && Unlocked(out _,out _);
    public void Summon()
    {
        if(!CanSummon)return;
        if(!BluewaterCayRockRegion.TryBossSpawn(out var point)){ShowNotice("Bluewater Cay rocks are not ready. Try again shortly.");return;}
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
        if(view==null)return;
        if(current!=null && view.serverTime<current.serverTime)return;
        current=view;receivedAt=Time.realtimeSinceStartup;
        if(view.phase=="active" && announced!=view.id)
        {
            announced=view.id;nextRipple=0;
            ShowNotice("Abyssal Leviathan has awakened on the southern end of Bluewater Cay. Catch it in 15 minutes before it returns to the abyss!");
        }
    }
    public bool TryHook(Vector3 point,int cast)
    {
        if(!Active || !string.IsNullOrEmpty(current.owner) || Vector2.Distance(new Vector2(point.x,point.z),new Vector2(current.x,current.z))>80f)return false;
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
            if(fishing.BossCastWaiting(cast) && Active && current.owner==session.LocalId && current.lease==lease)
                fishing.BeginBossFight(InitialHealth,Weight);
            else Release();
        }
        fishing.BossClaimFinished(cast);
    }
    public void ReportDamage(int amount){if(HasLease)damage=Mathf.Clamp(damage+Mathf.Max(0,amount),0,150000);}
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
        bool active=Active;
        hudRoot.SetActive(active);
        if(active)
        {
            int hp=current.hp;
            if(HasLease && fishing.IsBossFight)hp=fishing.CurrentFishHealth;
            health.fillAmount=hp/150000f;
            int left=Mathf.CeilToInt(Remaining);
            status.text=$"ABYSSAL LEVIATHAN   {hp:N0} / 150,000 HP   {left/60}:{left%60:00}";
            if(ocean==null)ocean=FindFirstObjectByType<OceanWater>();
            var travel=ShopDimensionManager.Instance;
            if(ocean!=null && (travel==null || travel.Destination==0) && Time.realtimeSinceStartup>=nextRipple)
            {
                nextRipple=Time.realtimeSinceStartup+1.5f;
                AbyssalBloodRipple.Spawn(new Vector3(current.x,current.y,current.z),ocean);
            }
        }
        noticeRoot.SetActive(Time.realtimeSinceStartup<noticeUntil);
        if(HasLease && !landing && (!active || current.owner!=session.LocalId || current.lease!=lease))
        {lease="";fightId="";fishing.AbortBossFight("The Leviathan is no longer on your line.");}
        if(HasLease && !landing && !busy && Time.realtimeSinceStartup>=nextReport)
        {nextReport=Time.realtimeSinceStartup+.35f;StartCoroutine(Report());}
        if(progress!=null && !progress.ReadOnly && !busy && !HasLease && !string.IsNullOrEmpty(progress.Data.bossSummonTicket) && Time.realtimeSinceStartup>=nextRecovery)
            StartCoroutine(RecoverFee());
    }
    private IEnumerator Send(Command command,Action<Reply> done)
    {
        Reply reply=null;
        for(int i=0;i<3 && session!=null && session.Connected;i++)
        {
            yield return Post("/v1/boss",JsonUtility.ToJson(new Request{pose=session.LocalPose(),boss=command}),session.SessionToken,r=>reply=r);
            if(reply!=null)break;
        }
        if(reply?.boss!=null)Apply(reply.boss);
        done(reply);
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
    private void ShowNotice(string text){Status=text;notice.text=text;noticeUntil=Time.realtimeSinceStartup+14f;}
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
        var go=new GameObject("Leviathan HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
        var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=215;
        var s=go.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.matchWidthOrHeight=.5f;
        hudRoot=Panel(go.transform,"Boss health",new Vector2(850,62),18,new Color(.05f,.02f,.04f,.92f));
        var bar=Panel(hudRoot.transform,"Health fill",new Vector2(842,54),4,new Color(.65f,.03f,.09f));
        health=bar.GetComponent<Image>();health.sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f));
        health.type=Image.Type.Filled;health.fillMethod=Image.FillMethod.Horizontal;status=Label(hudRoot.transform,25);
        noticeRoot=Panel(go.transform,"Awakening notification",new Vector2(980,130),185,new Color(.08f,.02f,.04f,.96f));notice=Label(noticeRoot.transform,26);
        hudRoot.SetActive(false);noticeRoot.SetActive(false);
    }
}
