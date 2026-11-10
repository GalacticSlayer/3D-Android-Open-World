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
        [NonSerialized] public bool transportUnavailable;
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
    // Shown separately from directory search status. A failed owner upload
    // must remain visible when someone browses public Homes.
    public string OwnerSyncStatus { get; private set; } = "Checking saved Home...";
    public int OwnerSyncedVersion { get; private set; } = -1;
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
    private float retryNotBefore;
    private string lastOwnerFailure;
    // Aquarium/fish refresh must work independently of the optional live
    // avatar heartbeat. 2s /check requests are tiny; /get is only fetched
    // when the owner's published version actually changes.
    private const float VisitorCheckInterval=2f;
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
        RestoreVerifiedHomeIdentity();
        if(progress!=null)progress.Changed+=MarkDirty;
        nextSync=Time.unscaledTime+2.5f;
        lastReachability=Application.internetReachability;
    }
    private static bool ValidOwnerCredential(string credential)
    {
        if(string.IsNullOrEmpty(credential)||credential.Length!=64)return false;
        foreach(char c in credential)
            if(!((c>='a' && c<='f') || (c>='0' && c<='9')))return false;
        return true;
    }
    private static bool ValidHomeIdentity(string code,string credential)
    {
        if(string.IsNullOrEmpty(code)||code.Length!=8 ||
           string.IsNullOrEmpty(credential)||credential.Length!=64)return false;
        foreach(char c in code)
            if(!((c>='A' && c<='H') || (c>='J' && c<='N') ||
                 (c>='P' && c<='Z') || (c>='2' && c<='9')))return false;
        foreach(char c in credential)
            if(!((c>='a' && c<='f') || (c>='0' && c<='9')))return false;
        return true;
    }
    private void RestoreVerifiedHomeIdentity()
    {
        if(progress==null || progress.ReadOnly || progress.Data==null)return;
        var save=progress.Data;
        // ShopLedger backup is written ONLY after the server has actually
        // accepted an owner-authenticated update. If independent PlayerPrefs
        // keys were wiped or split during a build, the verified pair wins.
        if(!ValidHomeIdentity(save.verifiedHomeCode,save.verifiedHomeOwnerKey))return;
        if(OwnerCode==save.verifiedHomeCode && ownerKey==save.verifiedHomeOwnerKey)return;
        OwnerCode=save.verifiedHomeCode;
        ownerKey=save.verifiedHomeOwnerKey;
        PlayerPrefs.SetString(OwnerKeyPref,ownerKey);
        PlayerPrefs.SetString(CodePref,OwnerCode);
        PlayerPrefs.Save();
        OwnerSyncStatus="Restored verified Home "+DisplayHomeCode+" from local save.";
        Debug.LogWarning("[HOME OWNER] Restored previously verified Home code "+
            DisplayHomeCode+". No new public Home was created.");
        Changed?.Invoke();
    }
    private void BackUpVerifiedHomeIdentity()
    {
        if(progress==null || progress.ReadOnly || progress.Data==null ||
           !ValidHomeIdentity(OwnerCode,ownerKey))return;
        var save=progress.Data;
        if(save.verifiedHomeCode==OwnerCode && save.verifiedHomeOwnerKey==ownerKey)
            return;
        save.verifiedHomeCode=OwnerCode;
        save.verifiedHomeOwnerKey=ownerKey;
        // Save() serializes the entire ledger; fish and aquarium positions are
        // left untouched. Never publish the owner credential in a Home snapshot.
        progress.Save();
    }
    private IEnumerator ResolveOwnerHomeCode(Action<bool> done)
    {
        // The server compares SHA-256 owner hashes. It cannot reveal a Home
        // from a public player name or an unauthenticated directory listing.
        // Recover ONLY when we still hold a complete private owner key.
        Reply resolved=null;
        yield return Post("/v1/homes/resolve-owner",
            new Request{ownerKey=ownerKey},r=>resolved=r);
        if(resolved!=null && resolved.ok &&
           ValidHomeIdentity(resolved.code,ownerKey))
        {
            string previous=OwnerCode;
            OwnerCode=resolved.code;
            PlayerPrefs.SetString(CodePref,OwnerCode);
            PlayerPrefs.SetString(OwnerKeyPref,ownerKey);
            PlayerPrefs.Save();
            lastUploaded=null;
            OwnerSyncStatus="Recovered verified Home "+DisplayHomeCode+
                " from Oracle; publishing local aquarium changes.";
            if(previous!=OwnerCode)
                Debug.LogWarning("[HOME OWNER] Restored Home code "+
                    DisplayHomeCode+" using the saved owner credential. "+
                    "The Home presence session will rejoin the correct code.");
            Changed?.Invoke();
            done(true);
        }
        else
        {
            OwnerSyncStatus="Could not recover a Home from the saved owner key. "+
                (resolved!=null?resolved.error:"Oracle unavailable.");
            LogOwnerFailure(resolved!=null && resolved.transportUnavailable);
            Changed?.Invoke();
            done(false);
        }
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
        // A Home edit should be uploaded promptly even if live presence
        // fails to discover a visitor. Keep the old debounce elsewhere.
        nextSync=Time.unscaledTime+
            (travel!=null && travel.InHome && !IsVisiting?0.1f:2.5f);
    }

    public void ApplyHeartbeatSnapshot(Reply snapshot)
    {
        // The visitor never writes remote fish or aquarium positions to their
        // own PlayerPrefs. Only apply newer, versioned owner snapshots in RAM.
        if(snapshot==null || !snapshot.ok || VisitingHome==null ||
           snapshot.code!=VisitingHome.code ||
           snapshot.version<=VisitingHome.version)return;
        VisitingHome=snapshot;
        Debug.Log("[HOME SNAPSHOT] Updated "+snapshot.code+
            " to version "+snapshot.version+
            " with "+(snapshot.tanks!=null?snapshot.tanks.Length:0)+
            " aquariums and "+snapshot.fish+" fish.");
        nextVisitPoll=Time.unscaledTime+VisitorCheckInterval;
        if(travel!=null && travel.InHome && travel.World!=null)
            travel.World.RefreshHomeView();
        Changed?.Invoke();
    }

    // A Home presence response can succeed for a visitor even when the
    // owner's saved credential is invalid. Surface that distinction rather
    // than claiming the owner has synchronized successfully.
    public void ReportOwnerPresenceUnauthorized()
    {
        if(IsVisiting)return;
        string status="OWNER NOT VERIFIED • "+DisplayHomeCode+
            ". Check this code against the visitor's Home listing.";
        if(OwnerSyncStatus==status)return;
        OwnerSyncStatus=status;
        Changed?.Invoke();
    }

    // Publish a completed fish transfer immediately while at Home. Never
    // require the separate player-presence channel to be healthy first.
    public void NotifyHabitatFishChanged()
    {
        if(progress==null || progress.ReadOnly || IsVisiting)return;
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
        nextSync=Time.unscaledTime+.05f;
    }

    // Optional faster notice from live player presence. The independent
    // visitor polling loop works even when this notification never arrives.
    public void NotifyLiveHomeRevision(int version,bool anotherPlayerPresent)
    {
        // Never let a failed or delayed live-notice download stall the
        // normal 2-second revision checks.
        if(!anotherPlayerPresent || VisitingHome==null ||
           version<=VisitingHome.version || activeDownloads!=0 ||
           travel==null || !travel.InHome || travel.Traveling)return;
        nextVisitPoll=Time.unscaledTime+VisitorCheckInterval;
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
            // Do not trust a fingerprint cached before travel or a server
            // restart. Re-publish the authoritative local Home on each entry;
            // the server handles identical uploads without bumping version.
            lastUploaded=null;
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
                retryNotBefore=0f;
                retrySeconds=3f;
            }
        }
        // A damaged, read-only local save must NEVER overwrite a valid
        // previously published Home with the fallback empty ledger.
        if(progress!=null && !progress.ReadOnly && dirty && !sending &&
           Time.unscaledTime>=nextSync && Time.unscaledTime>=retryNotBefore)
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
            // Always verify published aquarium and fish revisions, even when
            // avatar presence reports success or fails to initialize. The
            // independent saved-Home API is the source of truth.
            nextVisitPoll=Time.unscaledTime+VisitorCheckInterval;
            StartCoroutine(CheckVisitingHome());
        }
    }
    private IEnumerator Synchronize()
    {
        sending=true;
        // Register once per installation. No active multiplayer room required.
        // Never silently replace an existing Home with a freshly registered
        // eight-character code if only its key/code was lost. Doing so strands
        // visitors in the old listing and makes both players and tanks stale.
        bool hasCode=!string.IsNullOrEmpty(OwnerCode);
        bool hasKey=!string.IsNullOrEmpty(ownerKey);
        if(hasCode && !hasKey || hasKey && !ValidOwnerCredential(ownerKey))
        {
            OwnerSyncStatus="HOME OWNER KEY MISSING/INVALID for "+DisplayHomeCode+
                ". Existing Home preserved; cloud upload blocked.";
            Status=OwnerSyncStatus;
            LogOwnerFailure(false);
            Retry();yield break;
        }
        if(hasKey && !ValidHomeIdentity(OwnerCode,ownerKey))
        {
            bool recovered=false;
            yield return ResolveOwnerHomeCode(ok=>recovered=ok);
            if(!recovered){Retry();yield break;}
            hasCode=true;
        }
        if(!hasCode)
        {
            Reply registered=null;
            yield return Post("/v1/homes/register",new Request{
                name=progress.Data.playerName},r=>registered=r);
            if(registered==null || !registered.ok ||
               !ValidHomeIdentity(registered.code,registered.ownerKey))
            {
                Status=registered!=null?registered.error:"Home registration is unavailable.";
                OwnerSyncStatus="HOME REGISTRATION FAILED: "+Status;
                LogOwnerFailure(registered!=null && registered.transportUnavailable);
                Retry();yield break;
            }
            ownerKey=registered.ownerKey;
            OwnerCode=registered.code;
            PlayerPrefs.SetString(OwnerKeyPref,ownerKey);
            PlayerPrefs.SetString(CodePref,OwnerCode);
            PlayerPrefs.Save();
            OwnerSyncStatus="Registered "+DisplayHomeCode+"; publishing local Home...";
            Debug.Log("[HOME OWNER] Registered new Home "+DisplayHomeCode+
                ". Visitors must use this exact code.");
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
            // A valid key may belong to the owner's ORIGINAL Home even if
            // an old reinstall/partial preferences loss changed the code.
            // Resolve by authenticated owner hash before treating it as a
            // permanent upload error; never select a Home by player name.
            if(ValidOwnerCredential(ownerKey) &&
               (Status=="Home ownership could not be verified." ||
                Status=="Home not found or private."))
            {
                string attempted=OwnerCode;
                bool recovered=false;
                yield return ResolveOwnerHomeCode(ok=>recovered=ok);
                if(recovered && OwnerCode!=attempted)
                {
                    sending=false;dirty=true;
                    nextSync=Time.unscaledTime;yield break;
                }
            }
            OwnerSyncStatus="UPLOAD FAILED for "+DisplayHomeCode+": "+Status;
            // A stale Home can still be visited even though owner-only
            // writes fail. Log code and error, never the private owner key.
            LogOwnerFailure(answer!=null && answer.transportUnavailable);
            Retry();yield break;
        }
        lastUploaded=fingerprint;
        OwnerSyncedVersion=answer.version;
        OwnerSyncStatus="SYNCED "+DisplayHomeCode+" • version "+
            answer.version+" • "+upload.tanks.Length+" aquariums";
        Debug.Log("[HOME SAVE] Published "+OwnerCode+
            " revision "+answer.version+" ("+
            upload.tanks.Length+" aquariums).");
        BackUpVerifiedHomeIdentity();
        // A save during an in-flight upload remains pending.
        dirty=JsonUtility.ToJson(CurrentUpload())!=fingerprint;
        retrySeconds=10f;retryNotBefore=0f;lastOwnerFailure=null;sending=false;
        Status="Home published • "+DisplayHomeCode;
        Changed?.Invoke();
        if(dirty)nextSync=Time.unscaledTime+
            (travel!=null && travel.InHome && !IsVisiting?0.1f:2.5f);
    }
    private void LogOwnerFailure(bool transportUnavailable)
    {
        // A temporary outage is expected during offline/mobile play. Keep the
        // cloud status visible, but do not reopen the development error console
        // on every automatic retry. Genuine server rejections stay errors.
        string failure=transportUnavailable?"transport unavailable":OwnerSyncStatus;
        if(lastOwnerFailure==failure)return;
        lastOwnerFailure=failure;
        string message="[HOME OWNER] "+OwnerSyncStatus+
            ". Local fish and placement changes are still saved; upload will retry automatically.";
        if(transportUnavailable)Debug.LogWarning(message);
        else Debug.LogError(message);
    }
    private void Retry()
    {
        sending=false;dirty=true;
        retryNotBefore=Time.unscaledTime+retrySeconds;
        nextSync=retryNotBefore;
        // Save events may debounce nextSync, but cannot bypass this retry floor.
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
        nextVisitPoll=Time.unscaledTime+VisitorCheckInterval;
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
        if(VisitingHome==old && check!=null && check.ok && check.version>old.version)
        {
            Reply updated=null;
            yield return Post("/v1/homes/get",new Request{code=old.code},r=>updated=r);
            if(VisitingHome==old && updated!=null && updated.ok)
                ApplyHeartbeatSnapshot(updated);
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
                answer=new Reply{ok=false,transportUnavailable=true,
                    error="Cannot contact the Home directory server ("+request.result+
                        (request.responseCode>0?", HTTP "+request.responseCode:"")+")."};
            if(!answer.ok && string.IsNullOrEmpty(answer.error))
                answer.error="Home directory request failed.";
            done(answer);
        }
    }
}
