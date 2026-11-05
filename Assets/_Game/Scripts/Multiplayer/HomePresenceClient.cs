using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Small, room-independent Home presence channel. Reuses the fishing game's
// existing remote avatar and animations, while fish continue to swim locally.
// A player's idle presence lasts through short mobile network pauses.
[DisallowMultipleComponent]
public sealed class HomePresenceClient : MonoBehaviour
{
    [Serializable] public sealed class AquariumMove
    {
        public string key;
        public float x,y,z,yaw;
    }
    // A compact independent Home pose. Avoid serializing the full fishing-room
    // state (boats, line effects and other optional network fields) into a Home
    // heartbeat; entry and avatar visibility must work without opening a room.
    [Serializable] private sealed class HomePose
    {
        public int world=2,heldFishSpecies=-1,rodTier,reelTier,castSequence;
        public float x,y,z,yaw,heldFishWeight,reelTime;
        public bool swimming,sprinting,rodEquipped,reeling,hasFish,lureInWater,fishPosing;
    }
    // Explicit flat wire DTO. JsonUtility must not deserialize through
    // FishingMultiplayer.Peer inheritance to decide whether someone exists.
    [Serializable] private sealed class HomePeer
    {
        public string id,name,homeCode;
        public float x,y,z,yaw,heldFishWeight,reelTime;
        public int world,heldFishSpecies=-1,rodTier,reelTier,castSequence;
        public bool swimming,sprinting,rodEquipped,reeling,hasFish;
        public bool lureInWater,fishPosing;
    }
    [Serializable] private sealed class PresenceRequest
    {
        public string code,session,name,ownerKey;
        public bool leave;
        public HomePose pose;
        public bool wantSnapshot;
        public int knownVersion=-1;
        public AquariumMove move,placed;
    }
    [Serializable] private sealed class PresenceReply
    {
        public bool ok,owner;
        public string code,error;
        public int version,otherCount;
        public HomePeer[] players;
        public HomeDirectoryClient.Reply snapshot;
        public AquariumMove moving,placed;
    }

    public static HomePresenceClient Instance { get; private set; }
    private readonly Dictionary<string,FishingRemotePlayer> remotes=
        new Dictionary<string,FishingRemotePlayer>();
    private HomeDirectoryClient directory;
    private ShopDimensionManager travel;
    private HomeAquariumPlacement placement;
    private FishingMultiplayer multiplayer;
    private FishingSystem fishing;
    private ShopProgress equipment;
    private FirstPersonController player;
    private string currentHome,sessionId;
    private AquariumMove lastMoving,recentlyPlaced;
    private float lastMoveSeen,placedUntil;
    private float nextPoll,lastSuccess;
    private bool pending,authorized,quitting;
    private int otherPlayers;
    private string lastError="";
    private float lastErrorLog;
    public string LiveConnectionError => lastError;
    // An actual successful independent Home heartbeat, not a fishing room.
    public bool ConnectionHealthy =>
        !string.IsNullOrEmpty(currentHome) && string.IsNullOrEmpty(lastError) &&
        lastSuccess>0f && Time.unscaledTime-lastSuccess<PresenceTimeout;
    private const float ActivePollInterval=.25f;
    private const float EmptyPollInterval=1.5f;
    private const float PresenceTimeout=10f;
    // Repeat a tiny commit notification while the server saves the owner's
    // permanent snapshot. The visitor pins it until that revision arrives.
    private const float PlacementAnnouncementSeconds=12f;

    public void AnnounceAquariumPlacement(string key,Vector3 position,float yaw)
    {
        if(directory==null || directory.IsVisiting ||
           travel==null || !travel.InHome || string.IsNullOrEmpty(key))return;
        recentlyPlaced=new AquariumMove{key=key,x=position.x,y=position.y,
            z=position.z,yaw=yaw};
        placedUntil=Time.unscaledTime+PlacementAnnouncementSeconds;
        nextPoll=Time.unscaledTime;
    }

    // The owner's aquarium fish changes use the fast path ONLY while at
    // least one actual guest is present in this same Home.
    public bool HasOtherPlayersInMyHome =>
        directory!=null && !directory.IsVisiting &&
        !string.IsNullOrEmpty(currentHome) &&
        currentHome==directory.OwnerCode &&
        otherPlayers>0 && Time.unscaledTime-lastSuccess<PresenceTimeout;

    // The fishing-room presentation falls back to its own peers if the
    // independent Home-presence endpoint temporarily becomes unavailable.
    public bool HasLivePresence =>
        !string.IsNullOrEmpty(currentHome) &&
        otherPlayers>0 && Time.unscaledTime-lastSuccess<PresenceTimeout;

    private void Awake()
    {
        if(Instance!=null && Instance!=this){Destroy(this);return;}
        Instance=this;
        sessionId=Guid.NewGuid().ToString("N");
    }
    private void Start()
    {
        directory=HomeDirectoryClient.Instance;
        travel=GetComponent<ShopDimensionManager>();
        placement=GetComponent<HomeAquariumPlacement>();
        player=GetComponent<FirstPersonController>();
        fishing=GetComponent<FishingSystem>();
        equipment=GetComponent<ShopProgress>();
        multiplayer=FishingMultiplayer.For(player);
    }
    private string HomeCode
    {
        get
        {
            if(travel==null || !travel.InHome || travel.Traveling ||
               directory==null)return null;
            return directory.IsVisiting
                ?directory.VisitingHome.code:directory.OwnerCode;
        }
    }
    private void Update()
    {
        if(quitting)return;
        string code=HomeCode;
        if(string.IsNullOrEmpty(code) || code.Length!=8)
        {
            if(currentHome!=null)ChangeHome(null);
            return;
        }
        if(currentHome!=code)ChangeHome(code);
        // Never hide an idle avatar just because there is no active tank
        // drag. A brief mobile HTTP hiccup also must not destroy/recreate it.
        if(remotes.Count>0 && Time.unscaledTime-lastSuccess>PresenceTimeout)
            ClearPeers();
        if(!pending && Time.unscaledTime>=nextPoll)
        {
            nextPoll=Time.unscaledTime+
                (otherPlayers>0?ActivePollInterval:EmptyPollInterval);
            StartCoroutine(Poll(currentHome,sessionId));
        }
    }
    private void ChangeHome(string code)
    {
        string previous=currentHome;
        string previousSession=sessionId;
        currentHome=code;
        otherPlayers=0;
        lastError="";
        lastMoving=null;
        recentlyPlaced=null;
        placedUntil=0f;
        sessionId=Guid.NewGuid().ToString("N");
        authorized=false;
        lastSuccess=0f;pending=false;
        nextPoll=Time.unscaledTime;
        ClearPeers();
        if(travel!=null && travel.World!=null)
            travel.World.SetVisitorLiveAquariumMove(null,Vector3.zero,0);
        // Leaving is best-effort; expired sessions are removed server-side.
        if(!string.IsNullOrEmpty(previous))
            StartCoroutine(SendLeave(previous,previousSession));
    }
    private HomePose LocalHomePose()
    {
        // Only data required to show a player's real-time movements, rods,
        // held fish and pose. No fishing-room connection or token is used.
        var position=transform.position;
        bool equipped=fishing!=null && fishing.IsRodEquipped;
        float weight=fishing!=null?fishing.NetworkHeldWeight:0f;
        if(float.IsNaN(weight)||float.IsInfinity(weight))weight=0f;
        float reel=fishing!=null?fishing.NetworkReelTime:0f;
        if(float.IsNaN(reel)||float.IsInfinity(reel))reel=0f;
        return new HomePose{
            world=2,
            x=position.x,y=position.y,z=position.z,yaw=transform.eulerAngles.y,
            swimming=player!=null && player.IsSwimming,
            sprinting=player!=null && player.IsSprinting && !player.IsSwimming,
            rodEquipped=equipped,
            heldFishSpecies=fishing!=null?fishing.NetworkHeldSpecies:-1,
            heldFishWeight=weight,
            fishPosing=fishing!=null && fishing.IsFishPosing,
            reeling=fishing!=null && fishing.NetworkIsReeling,
            hasFish=fishing!=null && fishing.NetworkHasFish,
            lureInWater=fishing!=null && fishing.NetworkLureInWater,
            reelTime=reel,
            rodTier=FishingNetworkCompatibility.RodTier(
                equipment!=null&&equipment.Data!=null
                    ?equipment.Data.Equipped(GearKind.Rod):0,
                equipped,ShopCatalog.MaxRodTier),
            reelTier=equipment!=null&&equipment.Data!=null
                ?Mathf.Clamp(equipment.Data.Equipped(GearKind.Reel),
                    0,ShopCatalog.MaxReelTier):0,
            castSequence=fishing!=null?fishing.CastSequence:0
        };
    }

    private IEnumerator Poll(string code,string id)
    {
        pending=true;
        var visiting=directory!=null && directory.IsVisiting
            ?directory.VisitingHome:null;
        var request=new PresenceRequest{
            code=code,session=id,
            name=multiplayer!=null?multiplayer.PlayerName:"Angler",
            pose=LocalHomePose(),
            // Guests automatically receive aquarium positions and fish through
            // this SAME heartbeat whenever the published revision changes.
            wantSnapshot=visiting!=null,
            knownVersion=visiting!=null?visiting.version:-1
        };
        if(!authorized && directory!=null && !directory.IsVisiting)
            request.ownerKey=directory.OwnerCredential;
        if(directory!=null && !directory.IsVisiting && placement!=null &&
           placement.TryGetMovingAquarium(out string key,out Vector3 position,out float yaw))
        {
            request.move=new AquariumMove{key=key,x=position.x,y=position.y,
                z=position.z,yaw=yaw};
            // Beginning another drag supersedes any announcement about this
            // same aquarium's previous placement.
            if(recentlyPlaced!=null && recentlyPlaced.key==key)
                recentlyPlaced=null;
        }
        if(recentlyPlaced!=null && Time.unscaledTime<placedUntil)
            request.placed=recentlyPlaced;
        else recentlyPlaced=null;

        PresenceReply answer=null;
        yield return Post(request,r=>answer=r);
        // Ignore a delayed response after changing Homes or leaving.
        if(currentHome!=code || sessionId!=id)yield break;
        pending=false;
        if(answer==null || !answer.ok)
        {
            // Reauthenticate after a VM restart or expired presence session.
            authorized=false;
            var reason=answer!=null && !string.IsNullOrEmpty(answer.error)
                ?answer.error:"Home presence request failed.";
            if(reason!=lastError || Time.unscaledTime-lastErrorLog>=20f)
            {
                Debug.LogWarning("[HOME LIVE] "+reason+
                    " Avatars and instant aquarium changes require the current Oracle Home API.");
                lastErrorLog=Time.unscaledTime;
            }
            lastError=reason;
            directory?.NotifyPresenceUnavailable();
            nextPoll=Time.unscaledTime+2f;
            if(remotes.Count>0 &&
               Time.unscaledTime-lastSuccess>PresenceTimeout)ClearPeers();
            yield break;
        }
        authorized=directory!=null && directory.IsVisiting || answer.owner;
        lastError="";
        // The owner key is sent on first handshake (or after an expiration),
        // never continuously with normal movement updates.
        lastSuccess=Time.unscaledTime;
        Apply(answer);
        // Fast movement only while multiple players share a Home. Alone,
        // reduce requests substantially without making entry take very long.
        nextPoll=Time.unscaledTime+
            (otherPlayers>0?ActivePollInterval:EmptyPollInterval);
    }
    private void Apply(PresenceReply response)
    {
        if(response.code!=currentHome)return;
        if(directory!=null && directory.IsVisiting)
        {
            if(response.snapshot!=null && response.snapshot.ok)
                directory.ApplyHeartbeatSnapshot(response.snapshot);
            else if(directory.VisitingHome!=null &&
                    response.version>directory.VisitingHome.version)
                // Backward-compatible fallback for old Oracle deployments.
                directory.NotifyLiveHomeRevision(response.version,true);
        }
        var peers=response.players??new HomePeer[0];
        var present=new HashSet<string>();
        // Aquarium drag updates travel separately from avatar presence.
        AquariumMove moving=response.moving;
        foreach(var peer in peers)
        {
            // The server already scopes this response to currentHome.
            // Do not require a second HomeCode field on the avatar: Unity's
            // nested JSON inheritance can omit that optional field and
            // previously caused stationary players to be filtered out.
            if(peer==null || string.IsNullOrEmpty(peer.id) ||
               peer.id==sessionId || float.IsNaN(peer.x) ||
               float.IsNaN(peer.y) || float.IsNaN(peer.z))continue;
            present.Add(peer.id);
            if(!remotes.TryGetValue(peer.id,out var avatar) || avatar==null)
            {
                avatar=new GameObject("Home Visitor "+peer.name)
                    .AddComponent<FishingRemotePlayer>();
                avatar.Initialize(peer.name,peer.id);
                remotes[peer.id]=avatar;
            }
            // Construct the regular animation pose after reading the simple
            // wire format. This includes idle states: tank dragging is never
            // a prerequisite for the avatar to be instantiated or shown.
            var pose=new FishingMultiplayer.Peer {
                id=peer.id,name=peer.name,
                homeCode=currentHome,world=2,
                x=peer.x,y=peer.y,z=peer.z,yaw=peer.yaw,
                swimming=peer.swimming,sprinting=peer.sprinting,
                rodEquipped=peer.rodEquipped,reeling=peer.reeling,
                hasFish=peer.hasFish,lureInWater=peer.lureInWater,
                fishPosing=peer.fishPosing,
                heldFishSpecies=peer.heldFishSpecies,
                heldFishWeight=peer.heldFishWeight,
                rodTier=peer.rodTier,reelTier=peer.reelTier,
                reelTime=peer.reelTime,castSequence=peer.castSequence
            };
            avatar.Receive(pose);
            if(!avatar.gameObject.activeSelf)
                avatar.gameObject.SetActive(true);
        }
        // The server's authoritative count drives sync; avatar decoding must
        // never be able to disable live fish/aquarium updates.
        otherPlayers=Mathf.Max(present.Count,response.otherCount);
        // When the owner publishes a changed tank/fish snapshot, guests
        // download it immediately. A guest alone keeps the old 10s polling.
        directory?.NotifyLiveHomeRevision(response.version,otherPlayers>0);
        var removed=new List<string>();
        // Guard against a malformed/partially decoded peer array: don't hide
        // otherwise valid avatars while the backend reports peers are here.
        if(present.Count>0 || response.otherCount==0)
            foreach(var entry in remotes)
                if(!present.Contains(entry.Key))removed.Add(entry.Key);
        foreach(var id in removed)
        {
            if(remotes[id]!=null)Destroy(remotes[id].gameObject);
            remotes.Remove(id);
        }
        // A completed drag is published through the owner's persistent Home
        // snapshot shortly afterward. Hold the last transient pose briefly
        // so the tank doesn't flicker back to its old position between them.
        if(moving!=null)
        {
            lastMoving=moving;
            lastMoveSeen=Time.unscaledTime;
        }
        // Give a completed placement announcement time to arrive after the
        // last drag packet. This is only a brief in-flight grace period;
        // confirmed placements remain pinned until saved on the server.
        else if(lastMoving!=null && Time.unscaledTime-lastMoveSeen<2f)
            moving=lastMoving;
        else lastMoving=null;
        if(directory!=null && directory.IsVisiting &&
           travel!=null && travel.World!=null)
        {
            var world=travel.World;
            if(response.placed!=null)
            {
                var placed=response.placed;
                world.CommitVisitorLiveAquariumPlacement(placed.key,
                    new Vector3(placed.x,placed.y,placed.z),placed.yaw);
                if(lastMoving!=null && lastMoving.key==placed.key)
                {
                    lastMoving=null;
                    moving=null; // A confirmed placement ends that drag.
                }
            }
            if(moving!=null)
                world.SetVisitorLiveAquariumMove(moving.key,
                    new Vector3(moving.x,moving.y,moving.z),moving.yaw);
            else world.SetVisitorLiveAquariumMove(null,Vector3.zero,0);
        }
    }
    private void ClearPeers()
    {
        otherPlayers=0;
        lastMoving=null;
        foreach(var peer in remotes.Values)
            if(peer!=null)Destroy(peer.gameObject);
        remotes.Clear();
        if(directory!=null && directory.IsVisiting &&
           travel!=null && travel.World!=null)
            travel.World.SetVisitorLiveAquariumMove(null,Vector3.zero,0);
    }
    private IEnumerator SendLeave(string code,string id)
    {
        yield return Post(new PresenceRequest{code=code,session=id,leave=true},
            _=>{});
    }
    private IEnumerator Post(PresenceRequest body,Action<PresenceReply> done)
    {
        using(var request=new UnityWebRequest(
            FishingMultiplayer.Endpoint+"/v1/homes/presence","POST"))
        {
            request.uploadHandler=new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler=new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type","application/json");
            // A changed Home can include multiple resident fish records.
            // Give the first larger response room on slower mobile Wi-Fi.
            request.timeout=8;
            yield return request.SendWebRequest();
            PresenceReply reply=null;
            if(request.downloadHandler!=null &&
               !string.IsNullOrEmpty(request.downloadHandler.text))
            {
                try{reply=JsonUtility.FromJson<PresenceReply>(
                    request.downloadHandler.text);}
                catch(ArgumentException){}
            }
            if(reply==null || request.result!=UnityWebRequest.Result.Success)
            {
                string message=reply!=null && !string.IsNullOrEmpty(reply.error)
                    ?reply.error:"No valid Home presence response";
                if(request.result!=UnityWebRequest.Result.Success)
                    message+=" (HTTP "+request.responseCode+": "+request.error+")";
                reply=new PresenceReply{ok=false,error=message};
            }
            done(reply);
        }
    }
    private void OnApplicationQuit(){quitting=true;}
    private void OnDestroy()
    {
        ClearPeers();
        if(Instance==this)Instance=null;
    }
}
