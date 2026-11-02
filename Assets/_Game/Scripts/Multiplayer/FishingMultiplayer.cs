using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Player presence and owner-simulated boats; the server owns room membership and seats.
public sealed class FishingMultiplayer : MonoBehaviour
{
    public const string Endpoint = "http://129.153.55.216:3001";
    public const string Protocol = "fishing-walk-v1";
    [Serializable] public class BoatPose { public string key,kind; public float x,y,z,pitch,yaw,roll; }
    [Serializable] public class Pose {
        public BoatPose boat;
        public int lineKind,lureVariant,islandMask=1;
        // Compact replicated FX event; never used to decide damage or fish HP.
        public int lightningSerial,lightningZap;
        // Persisted last successful water landing, independent of transient line state.
        public int castImpactSequence;
        public float castImpactAge=-1f,castImpactX,castImpactY,castImpactZ;
        public float lineX,lineY,lineZ;
        public string rideOwner="",rideKey="";
        public int rideSeat=-1,rideRevision;
        public float rideX,rideY,rideZ,rideYaw,boatInputX,boatInputY,boatLookYaw;
 public float x,y,z,yaw; public int world; public bool swimming, sprinting, rodEquipped, reeling, hasFish, lureInWater; public bool fishPosing; public float reelTime, heldFishWeight; public int heldFishSpecies = -1; public int rodTier, castSequence; public float castAge = -1f, castDuration = .72f; }
    [Serializable] public class Peer : Pose { public string id,name; }
    [Serializable] public class Reply { public bool ok,bossReady; public int maxRodTier; public string error,id,token,code,hostId; public Peer[] players; public FishingTournament.View tournament; public AbyssalLeviathanEncounter.View boss; }
    [Serializable] private class Message
    {
        public string protocol=Protocol, build, code, name;
        public Pose pose;
    }
    public bool BossReady {get;private set;}
    public bool Connected => !string.IsNullOrEmpty(token);
    public bool Busy { get; private set; }
    public string Code { get; private set; } = "";
    public string Status { get; private set; } = "Offline — your game stays local.";
    public int PlayerCount { get; private set; }
    public bool IsHost { get; private set; }
    public string PlayerName
    {
        get
        {
            if(equipment==null)equipment=GetComponent<ShopProgress>();
            return ShopProgress.NormalizePlayerName(equipment?.Data?.playerName ?? PlayerPrefs.GetString("MultiplayerGuestName","Angler"));
        }
    }
    public string LocalId => id;
    internal string SessionToken => token;
    private FishingBoatMultiplayer boats;
    private string token, id;
    private int serverMaximumRodTier=FishingNetworkCompatibility.LegacyMaximumRodTier;
    private bool previousBackground, backgroundOverride;
    private FirstPersonController player;
    private FishingSystem fishing;
    private ShopProgress equipment;
    private Coroutine loop;
    private UnityWebRequest currentRequest;
    private float lastSnapshot;
    private readonly Dictionary<string, FishingRemotePlayer> remotes = new Dictionary<string, FishingRemotePlayer>();

    public static FishingMultiplayer For(FirstPersonController owner)
    {
        if(owner==null)return null;
        var component=owner.GetComponent<FishingMultiplayer>();
        return component!=null?component:owner.gameObject.AddComponent<FishingMultiplayer>();
    }
    private void Awake()
    {
        player=GetComponent<FirstPersonController>();
        if(GetComponent<AbyssalLeviathanEncounter>()==null)gameObject.AddComponent<AbyssalLeviathanEncounter>();
        boats=GetComponent<FishingBoatMultiplayer>();
        if(boats==null)boats=gameObject.AddComponent<FishingBoatMultiplayer>();
    }
    public void OpenServer() { Begin(true,""); }
    public void JoinServer(string code) { Begin(false,code==null?"":code.Trim().ToUpperInvariant()); }
    private void Begin(bool host,string code)
    {
        if(Busy || Connected || player==null)return;
        if(GetComponent<AbyssalLeviathanEncounter>()?.BlocksMultiplayer==true)
        {Status="Finish your offline Leviathan encounter before opening or joining multiplayer.";return;}
        if(!host && code.Length!=4){Status="Enter the host's 4-character room code.";return;}
        var travel=ShopDimensionManager.Instance;
        if(travel!=null && (travel.Traveling || travel.Destination!=0))
        {Status="Return to the fishing islands before opening or joining.";return;}
        previousBackground=Application.runInBackground;Application.runInBackground=true;backgroundOverride=true;
        Busy=true;
        Status=host?"Opening server…":"Joining server…";
        loop=StartCoroutine(ConnectAndPoll(host,code));
    }
    public Pose LocalPose(bool forLocalPresentation = false)
    {
        Vector3 p=transform.position;
        if(fishing==null)fishing=GetComponent<FishingSystem>();
        if(equipment==null)equipment=GetComponent<ShopProgress>();
        var pose = new Pose {x=p.x,y=p.y,z=p.z,yaw=transform.eulerAngles.y,
            world=ShopDimensionManager.Instance!=null?ShopDimensionManager.Instance.Destination:0,
            swimming=player!=null && player.IsSwimming,
            sprinting=player!=null && player.IsSprinting && !player.IsSwimming,
            rodEquipped=fishing!=null && fishing.IsRodEquipped,
            heldFishSpecies=fishing!=null ? fishing.NetworkHeldSpecies : -1,
            heldFishWeight=fishing!=null ? fishing.NetworkHeldWeight : 0f,
            fishPosing=fishing!=null && fishing.IsFishPosing,
            reeling=fishing!=null && fishing.NetworkIsReeling,
            hasFish=fishing!=null && fishing.NetworkHasFish,
            lureInWater=fishing!=null && fishing.NetworkLureInWater,
            reelTime=fishing!=null ? fishing.NetworkReelTime : 0f,
            rodTier=FishingNetworkCompatibility.RodTier(
                equipment!=null && equipment.Data!=null ? equipment.Data.Equipped(GearKind.Rod) : 0,
                // Server compatibility must not downgrade our own third-person model.
                fishing!=null && fishing.IsRodEquipped,
                forLocalPresentation ? ShopCatalog.MaxRodTier : serverMaximumRodTier),
            castSequence=fishing!=null ? fishing.CastSequence : 0,
            castAge=fishing!=null ? fishing.NetworkCastAge : -1f,
            castDuration=fishing!=null ? fishing.NetworkCastDuration : .72f};
        if(fishing!=null && pose.world==0 && fishing.NetworkCastImpactSequence>0)
        {
            Vector3 landing=fishing.NetworkCastImpactPoint;
            pose.castImpactSequence=fishing.NetworkCastImpactSequence;
            pose.castImpactAge=fishing.NetworkCastImpactAge;
            pose.castImpactX=Mathf.Round(landing.x*100f)/100f;
            pose.castImpactY=Mathf.Round(landing.y*100f)/100f;
            pose.castImpactZ=Mathf.Round(landing.z*100f)/100f;
        }
        var lightning=fishing!=null ? fishing.GetComponent<FishingBurstDamageRuntime>() : null;
        if(lightning!=null && pose.world==0)
        {
            pose.lightningSerial=lightning.NetworkLightningSerial;
            pose.lightningZap=lightning.NetworkLightningZap;
        }
        if(fishing!=null && !pose.swimming && pose.world==0)
        {
            pose.lineKind=fishing.NetworkLineKind;
            if(pose.lineKind!=0)
            {
                Vector3 end=fishing.NetworkLineEnd;
                // Centimetre precision bounds payload size; reuse the existing 10 Hz poll.
                pose.lineX=Mathf.Round(end.x*100f)/100f;
                pose.lineY=Mathf.Round(end.y*100f)/100f;
                pose.lineZ=Mathf.Round(end.z*100f)/100f;
                pose.lureVariant=Mathf.Clamp(fishing.NetworkLureVariant,0,ShopCatalog.LureVariantCount-1);
            }
        }
        if(equipment!=null && equipment.Data!=null)
            pose.islandMask=1|4|(equipment.Data.brinebreakDiscovered?2:0)|(equipment.Data.bluewaterDiscovered?8:0)|(equipment.Data.snapperDiscovered?16:0);
        if(boats!=null)boats.Capture(pose);
        return pose;
    }
    private IEnumerator ConnectAndPoll(bool host,string code)
    {
        Reply answer=null;
        yield return Post(host?"/v1/open":"/v1/join",new Message {build=Application.version+"|leviathan-v1",code=code,name=PlayerName,pose=LocalPose()},null,r=>answer=r);
        Busy=false;
        if(answer==null || !answer.ok || string.IsNullOrEmpty(answer.token))
        {ClearSession();Status=answer!=null?answer.error:"Connection failed.";loop=null;yield break;}
        token=answer.token;id=answer.id;Code=answer.code;IsHost=answer.hostId==id;
        GetComponent<AbyssalLeviathanEncounter>()?.BeginMultiplayer();
        Apply(answer);
        int failures=0;
        while(Connected)
        {
            yield return new WaitForSecondsRealtime(.1f);
            answer=null;
            yield return Post("/v1/state",new Message{name=PlayerName,pose=LocalPose()},token,r=>answer=r);
            if(answer==null || !answer.ok)
            {
                failures++;
                if(answer!=null && answer.error!=null && answer.error.StartsWith("Session ended"))failures=3;
                if(failures>=3)
                {
                    ClearSession();
                    Status=answer!=null?answer.error:"Connection lost. Open or join again.";
                    break;
                }
                Status="Connection interrupted — retrying…";
                yield return new WaitForSecondsRealtime(1);
                continue;
            }
            failures=0;Apply(answer);
        }
        loop=null;
    }
    private IEnumerator Post(string path,Message body,string session,Action<Reply> done)
    {
        using(var request=new UnityWebRequest(Endpoint+path,"POST"))
        {
            currentRequest=request;
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler=new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type","application/json");
            if(!string.IsNullOrEmpty(session))request.SetRequestHeader("Authorization","Bearer "+session);
            request.timeout=5;
            yield return request.SendWebRequest();
            Reply reply=null;
            if(request.downloadHandler!=null && !string.IsNullOrEmpty(request.downloadHandler.text))
            {try {reply=JsonUtility.FromJson<Reply>(request.downloadHandler.text);} catch(ArgumentException) {}}
            if(reply==null || (request.result!=UnityWebRequest.Result.Success && reply.ok))
                reply=new Reply{ok=false,error="Cannot reach the multiplayer server. Check your internet connection and try again."};
            if(!reply.ok && string.IsNullOrEmpty(reply.error))reply.error="Server returned an invalid response. Try again.";
            currentRequest=null;
            done(reply);
        }
    }
    private void Apply(Reply reply)
    {
        BossReady=reply.bossReady;
        serverMaximumRodTier=FishingNetworkCompatibility.AdvertisedMaximum(reply.maxRodTier,ShopCatalog.MaxRodTier);
        GetComponent<FishingTournament>()?.Apply(reply.tournament);
        GetComponent<AbyssalLeviathanEncounter>()?.Apply(reply.boss);
        lastSnapshot=Time.realtimeSinceStartup;
        Peer[] peers=reply.players ?? new Peer[0];
        if(boats!=null)boats.Apply(peers);
        PlayerCount=peers.Length;
        Status="Room "+Code+"  •  "+PlayerCount+"/8 players"+(IsHost?"  •  You are the host":"");
        var present=new HashSet<string>();
        foreach(var peer in peers)
        {
            if(string.IsNullOrEmpty(peer.id) || peer.id==id)continue;
            present.Add(peer.id);
            if(!remotes.TryGetValue(peer.id,out var remote))
            {
                remote=new GameObject("RemoteAngler").AddComponent<FishingRemotePlayer>();
                remote.Initialize(peer.name,peer.id);remotes.Add(peer.id,remote);
            }
            remote.Receive(peer);
        }
        var gone=new List<string>();
        foreach(var entry in remotes)if(!present.Contains(entry.Key))gone.Add(entry.Key);
        foreach(string key in gone){Destroy(remotes[key].gameObject);remotes.Remove(key);}
    }
    private void Update()
    {
        var travel=ShopDimensionManager.Instance;
        bool visible=Connected && Time.realtimeSinceStartup-lastSnapshot<3 && (travel==null || (!travel.Traveling && travel.Destination==0));
        foreach(var remote in remotes.Values)if(remote!=null)remote.gameObject.SetActive(visible && remote.World==0);
    }
    public void LeaveServer()
    {
        string oldToken=token;
        if(currentRequest!=null)currentRequest.Abort();
        if(loop!=null){StopCoroutine(loop);loop=null;}
        currentRequest=null;Busy=false;ClearSession();Status="Offline — your game stays local.";
        if(!string.IsNullOrEmpty(oldToken))StartCoroutine(SendLeave(oldToken));
    }
    private IEnumerator SendLeave(string oldToken)
    {
        using(var request=new UnityWebRequest(Endpoint+"/v1/leave","POST"))
        {
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=3;
            request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+oldToken);
            yield return request.SendWebRequest();
        }
    }
    private void ClearSession()
    {
        if(backgroundOverride){Application.runInBackground=previousBackground;backgroundOverride=false;}
        if(boats!=null)boats.Clear();
        token=null;id=null;Code="";PlayerCount=0;IsHost=false;BossReady=false;
        serverMaximumRodTier=FishingNetworkCompatibility.LegacyMaximumRodTier;
        foreach(var remote in remotes.Values)if(remote!=null)Destroy(remote.gameObject);
        remotes.Clear();
    }
    private void OnDestroy()
    {
        if(currentRequest!=null){currentRequest.Abort();currentRequest.Dispose();currentRequest=null;}
        ClearSession();
    }
}


