using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Public Home showcase networking is independent of the 10 Hz room protocol.
// PlayerPrefs ownership and ShopProgress saves stay separate; no remote data
// is ever written into a visitor's local aquarium/coin/fish ledger.
[DisallowMultipleComponent]
public sealed class HomeDirectoryClient : MonoBehaviour
{
    [Serializable] public sealed class FishInfo { public int speciesId; public string biomeId; public float weightKg; }
    [Serializable] public sealed class TankInfo
    {
        public string key,id;
        public float x,y,z,yaw;
        public FishInfo[] fish;
    }
    [Serializable] public sealed class HomeInfo
    {
        public string code,name,visibility;
        public long updatedAt;
        public int version,aquariums,fish;
        public FishInfo biggest;
    }
    [Serializable] public sealed class Reply
    {
        public bool ok,more;
        public string error,code,ownerKey,name,visibility;
        public int version,page,total,aquariums,fish;
        public long updatedAt;
        public FishInfo biggest;
        public HomeInfo[] homes;
        public TankInfo[] tanks;
        public TankInfo pond;
    }
    [Serializable] private sealed class Request
    {
        public string code,ownerKey,name,visibility,query,sort,excludeCode;
        public int page;
        public TankInfo[] tanks;
        public TankInfo pond;
    }

    private const string OwnerKeyPref="OpenWorld.Home.OwnerKey.v1";
    private const string CodePref="OpenWorld.Home.Code.v1";
    private const string VisibilityPref="OpenWorld.Home.Visibility.v1";
    public static HomeDirectoryClient Instance { get; private set; }
    public string OwnerCode { get; private set; }
    public string Visibility { get; private set; } = "public";
    public string Status { get; private set; } = "";
    public bool Busy { get; private set; }
    public HomeInfo[] Listings { get; private set; } = new HomeInfo[0];
    public int TotalListings { get; private set; }
    public int ListingPage { get; private set; }
    public bool MoreListings { get; private set; }
    public Reply VisitingHome { get; private set; }
    public bool IsVisiting => VisitingHome!=null;
    // Only used on the owner's own device for the first presence handshake.
    internal string OwnerCredential => ownerKey;
    public string DisplayHomeCode => OwnerCode!=null && OwnerCode.Length==8
        ?OwnerCode.Substring(0,4)+"-"+OwnerCode.Substring(4):"Registering...";
    public event Action Changed;

    private ShopProgress progress;
    private ShopDimensionManager travel;
    private string ownerKey,lastUploaded;
    private bool dirty=true,sending,visitEntered,wasAtOwnHome;
    private NetworkReachability lastReachability;
    private float nextSync, nextVisitPoll, retrySeconds=10f;
    private int activeDownloads;
    private void Awake()
    {
        if(Instance!=null && Instance!=this){Destroy(this);return;}
        Instance=this;
        ownerKey=PlayerPrefs.GetString(OwnerKeyPref,"");
        OwnerCode=PlayerPrefs.GetString(CodePref,"");
        Visibility=PlayerPrefs.GetString(VisibilityPref,"public");
        if(Visibility!="private" && Visibility!="unlisted")Visibility="public";
    }
    private void Start()
    {
        progress=GetComponent<ShopProgress>();
        travel=GetComponent<ShopDimensionManager>();
        if(progress!=null)progress.Changed+=MarkDirty;
        nextSync=Time.unscaledTime+2.5f;
        lastReachability=Application.internetReachability;
    }
    private void OnApplicationPause(bool paused)
    {
        if(!paused && dirty)
            nextSync=Mathf.Min(nextSync,Time.unscaledTime);
    }
    private void OnDestroy()
    {
        if(progress!=null)progress.Changed-=MarkDirty;
        if(Instance==this)Instance=null;
    }
    private void MarkDirty()
    {
        dirty=true;
        nextSync=Time.unscaledTime+
            (LiveGuestsAtMyHome?0.05f:2.5f);
    }

    public void ApplyHeartbeatSnapshot(Reply snapshot)
    {
        // The visitor never writes remote fish or aquarium positions to their
        // own PlayerPrefs. Only apply newer, versioned owner snapshots in RAM.
        if(snapshot==null || !snapshot.ok || VisitingHome==null ||
           snapshot.code!=VisitingHome.code ||
           snapshot.version<=VisitingHome.version)return;
        VisitingHome=snapshot;
        nextVisitPoll=Time.unscaledTime+30f;
        if(travel!=null && travel.InHome && travel.World!=null)
            travel.World.RefreshHomeView();
        Changed?.Invoke();
    }

    private bool LiveGuestsAtMyHome =>
        travel!=null && travel.InHome && !IsVisiting &&
        HomePresenceClient.Instance!=null &&
        HomePresenceClient.Instance.HasOtherPlayersInMyHome;

    // Called ONLY after a successful deposit/withdraw. With an active guest,
    // publish the changed list of fish promptly. Alone, Save() has already
    // scheduled the regular 2.5-second debounced Home update.
    public void NotifyHabitatFishChanged()
    {
        if(progress==null || progress.ReadOnly || !LiveGuestsAtMyHome)return;
        dirty=true;
        nextSync=Time.unscaledTime+.05f;
    }

    public void NotifyPresenceUnavailable()
    {
        // Older Oracle releases might still serve the saved-Home API even if
        // /presence is missing. Continue retrieving the owner's real saved
        // aquarium and fish revisions rather than leaving a guest frozen.
        if(VisitingHome!=null && travel!=null && travel.InHome)
            nextVisitPoll=Mathf.Min(nextVisitPoll,Time.unscaledTime+2f);
    }

    // Placement is visually shared during the drag; commit the new permanent
    // layout quickly when the owner presses PLACE or MOVE TO STORAGE.
    public void NotifyAquariumPositionCommitted()
    {
        if(progress==null || progress.ReadOnly)return;
        dirty=true;
        nextSync=Time.unscaledTime+.35f;
    }

    // Live presence carries only a version number; a full fish/tank snapshot
    // is downloaded once when its owner actually publishes a new revision.
    public void NotifyLiveHomeRevision(int version,bool anotherPlayerPresent)
    {
        // Only a live, shared Home needs the immediate path. Visitors who are
        // alone keep the existing ten-second version-check mechanism.
        if(!anotherPlayerPresent || VisitingHome==null ||
           version<=VisitingHome.version || activeDownloads!=0 ||
           travel==null || !travel.InHome || travel.Traveling)return;
        nextVisitPoll=Time.unscaledTime+10f;
        StartCoroutine(DownloadLiveHomeRevision(version));
    }

    private IEnumerator DownloadLiveHomeRevision(int advertisedVersion)
    {
        var previous=VisitingHome;
        if(previous==null || advertisedVersion<=previous.version)yield break;
        activeDownloads++;
        Reply snapshot=null;
        // Avoid a second /check round trip while someone is physically here.
        // The presence response already contains the authoritative revision.
        yield return Post("/v1/homes/get",
            new Request{code=previous.code},r=>snapshot=r);
        if(VisitingHome==previous && snapshot!=null && snapshot.ok)
            ApplyHeartbeatSnapshot(snapshot);
        activeDownloads--;
    }
    public void SetVisibility(string value)
    {
        if(value!="public" && value!="unlisted" && value!="private")return;
        if(Visibility==value)return;
        Visibility=value;PlayerPrefs.SetString(VisibilityPref,value);PlayerPrefs.Save();
        MarkDirty();Changed?.Invoke();
    }
    private TankInfo[] CurrentTanks()
    {
        var result=new List<TankInfo>();
        if(progress==null || progress.Data==null)return result.ToArray();
        foreach(var own in progress.Data.habitats)
        {
            if(own==null || !own.placed || string.IsNullOrEmpty(own.instanceKey))continue;
            var definition=ShopCatalog.Habitat(own.id);
            if(definition==null || definition.pond)continue;
            var fish=new List<FishInfo>();
            if(own.fish!=null)foreach(var f in own.fish)
                if(f!=null)fish.Add(new FishInfo{speciesId=f.speciesId,biomeId=f.biomeId,
                    weightKg=Mathf.Round(f.weightKg*1000f)/1000f});
            result.Add(new TankInfo{key=own.instanceKey,id=own.id,x=own.localX,
                y=own.localY,z=own.localZ,yaw=own.yaw,fish=fish.ToArray()});
        }
        return result.ToArray();
    }
    private TankInfo CurrentPond()
    {
        if(progress==null || progress.Data==null)return null;
        var owned=progress.Data.CurrentHabitat(true);
        if(owned==null)return null;
        var definition=ShopCatalog.Habitat(owned.id);
        if(definition==null || !definition.pond)return null;
        var fish=new List<FishInfo>();
        if(owned.fish!=null)foreach(var f in owned.fish)
            if(f!=null)fish.Add(new FishInfo{speciesId=f.speciesId,
                biomeId=f.biomeId,weightKg=Mathf.Round(f.weightKg*1000f)/1000f});
        return new TankInfo{id=owned.id,key="pond",fish=fish.ToArray()};
    }
    private Request CurrentUpload() => new Request{
        code=OwnerCode,ownerKey=ownerKey,
        name=progress!=null&&progress.Data!=null
            ?ShopProgress.NormalizePlayerName(progress.Data.playerName):"Angler",
        visibility=Visibility,tanks=CurrentTanks(),pond=CurrentPond()
    };
    private void Update()
    {
        // Every entry to the owner's Home attempts to publish the current
        // layout, regardless of fishing-room state. Save changes remain on
        // disk (ShopProgress/PlayerPrefs) until networking becomes available.
        bool atOwnHome=travel!=null && travel.InHome && !travel.Traveling &&
            !IsVisiting;
        if(atOwnHome && !wasAtOwnHome)
        {
            dirty=true;
            nextSync=Time.unscaledTime;
            retrySeconds=3f;
        }
        wasAtOwnHome=atOwnHome;
        // Do not rely solely on a long exponential retry interval after Wi-Fi
        // returns. This is a wake-up hint, not a hard network-availability gate.
        var reachability=Application.internetReachability;
        if(reachability!=lastReachability)
        {
            lastReachability=reachability;
            if(dirty && reachability!=NetworkReachability.NotReachable)
            {
                nextSync=Time.unscaledTime;
                retrySeconds=3f;
            }
        }
        // A damaged, read-only local save must NEVER overwrite a valid
        // previously published Home with the fallback empty ledger.
        if(progress!=null && !progress.ReadOnly && dirty && !sending &&
           Time.unscaledTime>=nextSync)
            StartCoroutine(Synchronize());
        if(VisitingHome==null || travel==null)return;
        if(travel.InHome && !travel.Traveling)visitEntered=true;
        if(visitEntered && !travel.InHome && !travel.Traveling)
        {
            VisitingHome=null;visitEntered=false;Changed?.Invoke();
            return;
        }
        if(visitEntered && Time.unscaledTime>=nextVisitPoll && activeDownloads==0)
        {
            var presence=HomePresenceClient.Instance;
            bool liveUnavailable=presence!=null &&
                !presence.ConnectionHealthy;
            // The normal heartbeat already sends changed fish and aquarium
            // snapshots. Keep only an occasional 30s verification check while
            // connected, and use 2s fallback checks if the heartbeat fails.
            nextVisitPoll=Time.unscaledTime+(liveUnavailable?2f:30f);
            StartCoroutine(CheckVisitingHome());
        }
    }
    private IEnumerator Synchronize()
    {
        sending=true;
        // Register once per installation. No active multiplayer room required.
        if(string.IsNullOrEmpty(OwnerCode) || string.IsNullOrEmpty(ownerKey))
        {
            Reply registered=null;
            yield return Post("/v1/homes/register",new Request{
                name=progress.Data.playerName},r=>registered=r);
            if(registered==null || !registered.ok || string.IsNullOrEmpty(registered.ownerKey))
            {
                Status=registered!=null?registered.error:"Home registration is unavailable.";
                Retry();yield break;
            }
            ownerKey=registered.ownerKey;
            OwnerCode=registered.code;
            PlayerPrefs.SetString(OwnerKeyPref,ownerKey);
            PlayerPrefs.SetString(CodePref,OwnerCode);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
        var upload=CurrentUpload();
        string fingerprint=JsonUtility.ToJson(upload);
        if(fingerprint==lastUploaded)
        {
            dirty=false;sending=false;yield break;
        }
        Reply answer=null;
        yield return Post("/v1/homes/update",upload,r=>answer=r);
        if(answer==null || !answer.ok)
        {
            Status=answer!=null?answer.error:"Home synchronization unavailable.";
            // A failed Home upload leaves visitors on the previous aquarium
            // layout. Show the real reason in Unity rather than silently
            // pretending a placement/fish change was synchronized.
            Debug.LogWarning("[HOME SAVE] Could not publish aquarium/fish changes: "+Status);
            Retry();yield break;
        }
        lastUploaded=fingerprint;
        // A save during an in-flight upload remains pending.
        dirty=JsonUtility.ToJson(CurrentUpload())!=fingerprint;
        retrySeconds=10f;sending=false;
        Status="Home published • "+DisplayHomeCode;
        Changed?.Invoke();
        if(dirty)nextSync=Time.unscaledTime+
            (LiveGuestsAtMyHome ? 0.05f : 2.5f);
    }
    private void Retry()
    {
        sending=false;dirty=true;
        nextSync=Time.unscaledTime+retrySeconds;
        // When the owner is standing in Home, recover quickly after mobile
        // connectivity returns. Outside Home keep the existing low-traffic
        // background retry behavior.
        retrySeconds=Mathf.Min(wasAtOwnHome?20f:120f,retrySeconds*2f);
        Changed?.Invoke();
    }
    public void Browse(string query,int page,string sort)
    {
        if(Busy)return;
        StartCoroutine(DownloadList(query,page,sort));
    }
    private IEnumerator DownloadList(string query,int page,string sort)
    {
        // A fresh search/sort begins at page zero; subsequent pages append to
        // the existing list so the HUD can scroll continuously without paging UI.
        Busy=true;
        if(page==0)
        {
            Listings=new HomeInfo[0];
            ListingPage=0;
            TotalListings=0;
            MoreListings=false;
        }
        Changed?.Invoke();
        Reply answer=null;
        yield return Post("/v1/homes/list",new Request{query=query,page=page,sort=sort,excludeCode=OwnerCode},r=>answer=r);

        // Exact codes also find Unlisted (PRIVATE ~ CODE ONLY) Homes. They are
        // never exposed through the public listing/search endpoint, so make
        // one read-only lookup only if a normal search returned no matches.
        // Checking the public directory FIRST also avoids confusing an
        // eight-letter player name with a Home code.
        string code=ExtractHomeCode(query);
        if(page==0 && answer!=null && answer.ok && answer.total==0 &&
           code!=null && !string.Equals(code,OwnerCode,StringComparison.OrdinalIgnoreCase))
        {
            Reply byCode=null;
            yield return Post("/v1/homes/get",new Request{code=code},r=>byCode=r);
            if(byCode!=null && byCode.ok)
            {
                answer.homes=new[]{new HomeInfo{
                    code=byCode.code,name=byCode.name,visibility=byCode.visibility,
                    updatedAt=byCode.updatedAt,version=byCode.version,
                    aquariums=byCode.aquariums,fish=byCode.fish,biggest=byCode.biggest}};
                answer.total=1;
                answer.more=false;
            }
        }

        Busy=false;
        if(answer!=null&&answer.ok)
        {
            var incoming=answer.homes??new HomeInfo[0];
            if(page>0)
            {
                var combined=new HomeInfo[Listings.Length+incoming.Length];
                Array.Copy(Listings,combined,Listings.Length);
                Array.Copy(incoming,0,combined,Listings.Length,incoming.Length);
                Listings=combined;
            }
            else Listings=incoming;
            ListingPage=answer.page;TotalListings=answer.total;MoreListings=answer.more;
            Status=TotalListings+" public Homes";
        }
        else
        {
            // Do not automatically repeat a failed page while the user remains
            // scrolled to the bottom; allow another browse/search to retry.
            MoreListings=false;
            Status=answer!=null?answer.error:"Could not load public Homes.";
        }
        Changed?.Invoke();
    }
    // Format matches the server's eight-character unambiguous Home IDs.
    // Users may enter either WQKB-Y5ZF or WQKBY5ZF.
    private static string ExtractHomeCode(string query)
    {
        if(string.IsNullOrEmpty(query))return null;
        string code=query.Replace("-","").Replace(" ","").Trim().ToUpperInvariant();
        if(code.Length!=8)return null;
        foreach(char c in code)
            if(!((c>='A' && c<='H') || (c>='J' && c<='N') ||
                 (c>='P' && c<='Z') || (c>='2' && c<='9')))
                return null;
        return code;
    }

    public void Visit(string code)
    {
        if(Busy || travel==null || travel.Traveling ||
           travel.TournamentTravelRestricted)return;
        string normalized=(code??"").Replace("-","").Replace(" ","").Trim().ToUpperInvariant();
        // A player's own Home is always accessed through ordinary Home travel,
        // never through the read-only Player Aquariums visitor flow.
        if(!string.IsNullOrEmpty(OwnerCode) &&
           string.Equals(normalized,OwnerCode,StringComparison.OrdinalIgnoreCase))
        {
            Status="This is your Home. Use Travel to Home instead.";
            Changed?.Invoke();
            return;
        }
        StartCoroutine(DownloadVisit(normalized));
    }
    private IEnumerator DownloadVisit(string code)
    {
        Busy=true;Changed?.Invoke();
        string normalized=(code??"").Replace("-","").Replace(" ","").Trim().ToUpperInvariant();
        Reply answer=null;
        yield return Post("/v1/homes/get",new Request{code=normalized},r=>answer=r);
        Busy=false;
        // Registration may finish while the request is in flight.
        if(!string.IsNullOrEmpty(OwnerCode) &&
           string.Equals(normalized,OwnerCode,StringComparison.OrdinalIgnoreCase))
        {
            Status="This is your Home. Use Travel to Home instead.";
            Changed?.Invoke();
            yield break;
        }
        if(answer==null||!answer.ok)
        {
            Status=answer!=null?answer.error:"Home is not reachable.";
            Changed?.Invoke();yield break;
        }
        VisitingHome=answer;
        visitEntered=false;
        nextVisitPoll=Time.unscaledTime+10f;
        Status="Visiting "+answer.name+"'s Home";
        Changed?.Invoke();
        if(travel.InHome && travel.World!=null)
        {
            travel.World.RefreshHomeView();
            travel.ReturnToHomeSpawn();
            var hud=FindFirstObjectByType<ShopWorldHUD>();
            if(hud!=null)hud.Close();
        }
        else travel.Travel(2);
    }
    public void ReturnToMyHome()
    {
        if(!IsVisiting)return;
        VisitingHome=null;visitEntered=false;
        if(travel!=null && travel.InHome && travel.World!=null)
        {
            travel.World.RefreshHomeView();
            travel.ReturnToHomeSpawn();
        }
        Changed?.Invoke();
    }
    private IEnumerator CheckVisitingHome()
    {
        var old=VisitingHome;
        if(old==null)yield break;
        activeDownloads++;
        Reply check=null;
        yield return Post("/v1/homes/check",new Request{code=old.code},r=>check=r);
        if(VisitingHome==old && check!=null && !check.ok &&
           check.error=="Home not found or private.")
        {
            ReturnToMyHome();
            Status="The owner has made this Home private.";
        }
        if(VisitingHome==old && check!=null && check.ok && check.version!=old.version)
        {
            Reply updated=null;
            yield return Post("/v1/homes/get",new Request{code=old.code},r=>updated=r);
            if(VisitingHome==old && updated!=null && updated.ok &&
               updated.version>old.version)
            {
                VisitingHome=updated;
                if(travel!=null && travel.InHome && travel.World!=null)
                    travel.World.RefreshHomeView();
                Changed?.Invoke();
            }
        }
        activeDownloads--;
    }
    private IEnumerator Post(string path,object body,Action<Reply> done)
    {
        using(var request=new UnityWebRequest(FishingMultiplayer.Endpoint+path,"POST"))
        {
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler=new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type","application/json");
            request.timeout=path=="/v1/homes/update"?15:8;
            yield return request.SendWebRequest();
            Reply answer=null;
            if(request.downloadHandler!=null&&!string.IsNullOrEmpty(request.downloadHandler.text))
                try{answer=JsonUtility.FromJson<Reply>(request.downloadHandler.text);}
                catch(ArgumentException){}
            if(answer==null || (request.result!=UnityWebRequest.Result.Success && answer.ok))
                answer=new Reply{ok=false,error="Cannot contact the Home directory server."};
            if(!answer.ok && string.IsNullOrEmpty(answer.error))
                answer.error="Home directory request failed.";
            done(answer);
        }
    }
}
