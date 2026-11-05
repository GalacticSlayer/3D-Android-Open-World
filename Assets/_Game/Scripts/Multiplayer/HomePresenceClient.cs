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
    [Serializable] public sealed class HomePeer : FishingMultiplayer.Peer
    {
        public AquariumMove move;
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
        public bool ok;
        public string code,error;
        public int version;
        public HomePeer[] players;
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
    private float nextPoll,lastSuccess;
    private bool pending,authorized,quitting;
    private const float PollInterval=.30f;

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
        if(Time.unscaledTime-lastSuccess>3f)ClearPeers();
        if(!pending && Time.unscaledTime>=nextPoll)
        {
            nextPoll=Time.unscaledTime+PollInterval;
            StartCoroutine(Poll(currentHome,sessionId));
        }
    }
    private void ChangeHome(string code)
    {
        string previous=currentHome;
        string previousSession=sessionId;
        currentHome=code;
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
        if(currentHome!=code || sessionId!=id){pending=false;yield break;}
        pending=false;
        if(answer==null || !answer.ok)
        {
            nextPoll=Time.unscaledTime+2f;
            if(Time.unscaledTime-lastSuccess>3f)ClearPeers();
            yield break;
        }
        authorized=true; // Owner credential is needed only on first handshake.
        lastSuccess=Time.unscaledTime;
        nextPoll=Time.unscaledTime+PollInterval;
        Apply(answer);
    }
    private void Apply(PresenceReply response)
    {
        if(response.code!=currentHome)return;
        directory?.NotifyLiveHomeRevision(response.version);
        var peers=response.players??new HomePeer[0];
        var present=new HashSet<string>();
        AquariumMove moving=null;
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
            if(!avatar.gameObject.activeSelf)avatar.gameObject.SetActive(true);
            if(peer.move!=null && moving==null)moving=peer.move;
        }
        var removed=new List<string>();
        foreach(var entry in remotes)
            if(!present.Contains(entry.Key))removed.Add(entry.Key);
        foreach(var id in removed)
        {
            if(remotes[id]!=null)Destroy(remotes[id].gameObject);
            remotes.Remove(id);
        }
        // Only owner-authenticated peers can publish a temporary tank pose.
        // Restore the regular snapshot position as soon as the drag ends.
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
