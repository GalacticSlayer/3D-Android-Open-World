using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Small, room-independent Home presence channel. Reuses the fishing game's
// existing remote avatar and animations, while fish continue to swim locally.
// A player's transient presence expires on Oracle after 4.5 seconds.
[DisallowMultipleComponent]
public sealed class HomePresenceClient : MonoBehaviour
{
    [Serializable] public sealed class AquariumMove
    {
        public string key;
        public float x,y,z,yaw;
    }
    [Serializable] private sealed class PresenceRequest
    {
        public string code,session,name,ownerKey;
        public bool leave;
        public FishingMultiplayer.Pose pose;
        public AquariumMove move;
    }
    [Serializable] private sealed class PresenceReply
    {
        public bool ok,owner;
        public string code,error;
        public int version;
        // Use the exact same peer model as FishingMultiplayer.Reply; Home
        // presence must not rely on a special subclass or moving tank data.
        public FishingMultiplayer.Peer[] players;
        public AquariumMove moving;
    }

    public static HomePresenceClient Instance { get; private set; }
    private readonly Dictionary<string,FishingRemotePlayer> remotes=
        new Dictionary<string,FishingRemotePlayer>();
    private HomeDirectoryClient directory;
    private ShopDimensionManager travel;
    private HomeAquariumPlacement placement;
    private FishingMultiplayer multiplayer;
    private FirstPersonController player;
    private string currentHome,sessionId;
    private AquariumMove lastMoving;
    private float lastMoveSeen;
    private float nextPoll,lastSuccess;
    private bool pending,authorized,quitting;
    private int otherPlayers;
    private const float ActivePollInterval=.25f;
    private const float EmptyPollInterval=1.5f;
    private const float PresenceTimeout=10f;

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
        lastMoving=null;
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
    private IEnumerator Poll(string code,string id)
    {
        pending=true;
        var request=new PresenceRequest{
            code=code,session=id,
            name=multiplayer!=null?multiplayer.PlayerName:"Angler",
            pose=multiplayer!=null?multiplayer.LocalPose():null
        };
        if(!authorized && directory!=null && !directory.IsVisiting)
            request.ownerKey=directory.OwnerCredential;
        if(directory!=null && !directory.IsVisiting && placement!=null &&
           placement.TryGetMovingAquarium(out string key,out Vector3 position,out float yaw))
            request.move=new AquariumMove{key=key,x=position.x,y=position.y,
                z=position.z,yaw=yaw};

        PresenceReply answer=null;
        yield return Post(request,r=>answer=r);
        // Ignore a delayed response after changing Homes or leaving.
        if(currentHome!=code || sessionId!=id)yield break;
        pending=false;
        if(answer==null || !answer.ok)
        {
            // Reauthenticate after a VM restart or expired presence session.
            authorized=false;
            nextPoll=Time.unscaledTime+2f;
            if(remotes.Count>0 &&
               Time.unscaledTime-lastSuccess>PresenceTimeout)ClearPeers();
            yield break;
        }
        authorized=directory!=null && directory.IsVisiting || answer.owner;
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
        var peers=response.players??new FishingMultiplayer.Peer[0];
        var present=new HashSet<string>();
        // Aquarium drag updates travel separately from avatar presence.
        AquariumMove moving=response.moving;
        foreach(var peer in peers)
        {
            if(peer==null || string.IsNullOrEmpty(peer.id) ||
               peer.id==sessionId || peer.homeCode!=currentHome)continue;
            present.Add(peer.id);
            if(!remotes.TryGetValue(peer.id,out var avatar) || avatar==null)
            {
                avatar=new GameObject("Home Visitor "+peer.name)
                    .AddComponent<FishingRemotePlayer>();
                avatar.Initialize(peer.name,peer.id);
                remotes[peer.id]=avatar;
            }
            avatar.Receive(peer);
            if(!avatar.gameObject.activeSelf)
                avatar.gameObject.SetActive(true);
        }
        otherPlayers=present.Count;
        // When the owner publishes a changed tank/fish snapshot, guests
        // download it immediately. A guest alone keeps the old 10s polling.
        directory?.NotifyLiveHomeRevision(response.version,otherPlayers>0);
        var removed=new List<string>();
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
        else if(lastMoving!=null && Time.unscaledTime-lastMoveSeen<.9f)
            moving=lastMoving;
        else lastMoving=null;
        if(directory!=null && directory.IsVisiting &&
           travel!=null && travel.World!=null)
        {
            if(moving!=null)
                travel.World.SetVisitorLiveAquariumMove(moving.key,
                    new Vector3(moving.x,moving.y,moving.z),moving.yaw);
            else travel.World.SetVisitorLiveAquariumMove(null,Vector3.zero,0);
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
            request.timeout=4;
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
                reply=new PresenceReply{ok=false};
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
