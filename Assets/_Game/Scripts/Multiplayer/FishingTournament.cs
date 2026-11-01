using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Tournament state is server-owned; this component owns UI and saved local coin escrow.
public sealed partial class FishingTournament : MonoBehaviour
{
    [Serializable] public sealed class Entry
    {
        public string id,name,status;
        public int points,catches,biggestSpecies;
        public float biggestKg,totalKg;
    }
    [Serializable] public sealed class View
    {
        public string id,phase,islandName,speciesName,winnerId,winnerName,biggestName,reason,mode;
        public int island,wager,pool,species,biggestSpecies;
        public float inviteSeconds,startSeconds,secondsLeft,biggestKg;
        public double serverTime;
        public Entry[] entries;
    }
    [Serializable] public sealed class Command
    {
        public string op,id,ticket,mode;
        public int island,wager,balance,species,cast;
        public float kg;
    }
    [Serializable] private sealed class Request {public FishingMultiplayer.Pose pose;public Command tournament;}
    [Serializable] private sealed class ReceiptRequest {public string ticket;public bool cancelMissing=true;}
    [Serializable] private sealed class CatchListRequest {public string id,player;}
    [Serializable] private sealed class CatchDetail {public int species;public string name;public float kg;}
    [Serializable] private sealed class Response {public bool ok,ready,refund;public string error,player;public int payout;public float totalKg;public CatchDetail[] catches;public View tournament;}
    public static bool ModalOpen {get;private set;}
    private FishingMultiplayer session;
    private FirstPersonController controls;
    private ShopProgress progress;
    private TournamentStand stand;
    private View current;
    private float receivedAt,nextRecovery;
    private string shownInvite="",shownResult="",announcement="",notice="";
    private Canvas canvas;
    private GameObject modal,shade;
    private Text title,body,hud,noticeText,toast;
    private float toastUntil,nextTextRefresh;
    private Button interact;
    private InputField wagerInput;
    private readonly List<GameObject> modalControls=new List<GameObject>();
    private readonly Queue<Command> commands=new Queue<Command>();
    private bool sending,recovering,wasConnected;
    private string inFlightTicket="",screen="",selectedMode="species",teleportedTournamentId="";
    private int selectedIsland;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ModalOpen=false;
        foreach(var player in FindObjectsByType<BoatSystem>(FindObjectsSortMode.None))
            if(player.GetComponent<FishingTournament>()==null)player.gameObject.AddComponent<FishingTournament>();
    }
    private void Start()
    {
        controls=GetComponent<FirstPersonController>();progress=GetComponent<ShopProgress>();
        session=FishingMultiplayer.For(controls);
        if(progress!=null && progress.Data.tournamentWagers==null)progress.Data.tournamentWagers=new List<TournamentWager>();
        stand=gameObject.AddComponent<TournamentStand>();
        BuildUI();
    }
    public bool NearStand
    {
        get
        {
            var travel=ShopDimensionManager.Instance;
            var passenger=GetComponent<BoatPassenger>();
            return stand!=null && stand.Counter!=null && controls!=null && !controls.IsSwimming &&
                (passenger==null || passenger.Boat==null) && Vector3.Distance(transform.position,stand.Counter.position)<3f &&
                (travel==null || (!travel.Traveling && travel.Destination==0));
        }
    }
    public void Apply(View view)
    {
        if(view==null || string.IsNullOrEmpty(view.id))return;
        if(current!=null && view.serverTime<current.serverTime)return;
        current=view;receivedAt=Time.realtimeSinceStartup;
        if(canvas==null)return;
        var me=Mine();
        // The server owns the start; all joined total-weight entrants arrive at
        // the existing Suncrest dock before fishing begins. Retry if travel was busy.
        if(view.mode=="weight" && (view.phase=="countdown" || view.phase=="running") &&
           me?.status=="joined" && teleportedTournamentId!=view.id)
        {
            var travel=ShopDimensionManager.Instance;
            if(travel!=null && !travel.Traveling)
            {
                travel.TravelIsland(0);
                teleportedTournamentId=view.id;
            }
        }
        if(view.phase=="inviting" && shownInvite!=view.id && me!=null && me.status!="joined")
        {shownInvite=view.id;ShowInvitation();}
        if(view.phase=="countdown" && announcement!=view.id)
        {announcement=view.id;Open("announcement",view.mode=="weight"?"TOTAL WEIGHT TOURNAMENT":"TOURNAMENT TARGET");}
        if(view.phase=="running" && (screen=="announcement"||screen=="invitation"||screen=="lobby"))Close();
        if((view.phase=="ended"||view.phase=="cancelled") && shownResult!=view.id)
        {shownResult=view.id;ShowResults();nextRecovery=0;}
        if(screen=="invitation" && me!=null && me.status!="pending" && me.status!="locked")ShowLobby();
        if(view.phase=="inviting" && screen=="info" && me?.status=="joined")ShowLobby();
        RefreshText();
    }
    // Active only for entrants in the map-wide total-weight format. Other
    // players and all classic tournament travel rules remain unchanged.
    // The travel service uses this to block destinations even when invoked
    // outside the visible MENU / TRAVEL buttons.
    public bool SuncrestTravelRestricted => session!=null && session.Connected &&
        current!=null && current.mode=="weight" &&
        (current.phase=="countdown" || current.phase=="running") &&
        Mine()?.status=="joined";
    private Entry Mine()=>current?.entries==null || session==null?null:Array.Find(current.entries,e=>e.id==session.LocalId);
    private bool Unlocked(int island)=>progress!=null && progress.Data!=null && (island==0 ||
        island==1&&progress.Data.brinebreakDiscovered || island==2 || island==3&&progress.Data.bluewaterDiscovered || island==4&&progress.Data.snapperDiscovered);
    private void Update()
    {
        if(canvas==null || session==null)return;
        bool connected=session.Connected;
        if(wasConnected && !connected)
        {
            current=null;shownInvite=shownResult=announcement=teleportedTournamentId="";Close();
            Notice("Disconnected. Saved wagers will settle or be refunded by the server.");nextRecovery=0;
        }
        wasConnected=connected;
        bool near=NearStand;
        interact.gameObject.SetActive(near && !ModalOpen && !ShopWorldHUD.MenuOpen && !controls.BoatUIBlocked);
        if(interact.gameObject.activeSelf && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)OpenStand();
        float elapsed=Time.realtimeSinceStartup-receivedAt;
        if(current!=null && (current.phase=="running" || current.phase=="countdown"))
        {
            float left=Mathf.Max(0,current.secondsLeft-elapsed);
            hud.text=(current.mode=="weight"?"TOTAL WEIGHT":current.speciesName)+" • "+current.islandName+"\n"+
                (current.phase=="countdown"?"Starts in "+Mathf.CeilToInt(Mathf.Max(0,current.startSeconds-elapsed))+"s":
                current.mode=="weight"
                  ?string.Format("{0}:{1:00} remaining • {2:0.00} kg • Pool {3:N0}",(int)left/60,(int)left%60,Mine()?.totalKg??0,current.pool)
                  :string.Format("{0}:{1:00} remaining • {2} points • Pool {3:N0}",(int)left/60,(int)left%60,Mine()?.points??0,current.pool));
            // Never let a missed polling response leave the start announcement blocking controls.
            if(screen=="announcement" && elapsed>=current.startSeconds)Close();
        }
        else hud.text=current!=null && current.phase=="inviting"?"TOURNAMENT INVITATIONS • "+Mathf.CeilToInt(Mathf.Max(0,current.inviteSeconds-elapsed))+"s":"";
        if(Time.realtimeSinceStartup>toastUntil)toast.text="";
        if(modal.activeSelf && Time.realtimeSinceStartup>=nextTextRefresh){nextTextRefresh=Time.realtimeSinceStartup+.25f;RefreshText();}
        if(!sending && commands.Count>0)StartCoroutine(SendCommands());
        if(!recovering && !sending && commands.Count==0 && Time.realtimeSinceStartup>=nextRecovery &&
            progress!=null && !progress.ReadOnly && progress.Data.tournamentWagers.Count>0)StartCoroutine(RecoverWagers());
    }
    public void ReportCatch(CaughtFishRecord fish,int island,int cast)
    {
        if(current==null || current.phase!="running" || Mine()?.status!="joined" || !session.Connected ||
            (current.mode!="weight" && (island!=current.island || FishCatalog.TournamentSpeciesId(fish.speciesId)!=FishCatalog.TournamentSpeciesId(current.species))))return;
        commands.Enqueue(new Command{op="catch",id=current.id,island=island,species=FishCatalog.CanonicalId(fish.speciesId),cast=cast,kg=fish.weightKg});
    }
    private bool Reserve(string id,int amount,out TournamentWager entry)
    {
        entry=null;
        if(progress==null || progress.ReadOnly){Notice("Your save is read-only; wagering is unavailable.");return false;}
        if(progress.Data.tournamentWagers.Count>0){Notice("Wait for your previous wager to settle before joining another tournament.");return false;}
        if(amount<1 || amount>1000000 || progress.Data.coins<amount){Notice("Not enough coins. Wagers must be 1–1,000,000 coins.");return false;}
        entry=new TournamentWager{id=id,ticket=Guid.NewGuid().ToString("N"),amount=amount,balance=progress.Data.coins};
        progress.Data.coins-=amount;progress.Data.tournamentWagers.Add(entry);
        try{progress.Save();}
        catch(Exception){progress.Data.coins+=amount;progress.Data.tournamentWagers.Remove(entry);Notice("Could not save the entry fee. Try again.");return false;}
        return true;
    }
    private IEnumerator SendCommands()
    {
        sending=true;
        while(commands.Count>0)
        {
            Command command=commands.Dequeue();inFlightTicket=command.ticket??"";
            Response response=null;
            for(int attempt=0;attempt<3 && session.Connected;attempt++)
            {
                yield return Post("/v1/tournament",JsonUtility.ToJson(new Request{pose=session.LocalPose(),tournament=command}),session.SessionToken,r=>response=r);
                if(response!=null)break;
                yield return new WaitForSecondsRealtime(.5f);
            }
            if(response!=null && response.ok)Apply(response.tournament);
            else Notice(response?.error??"Connection interrupted. Your saved entry fee is being checked.");
            inFlightTicket="";nextRecovery=0;
        }
        sending=false;
    }
    private IEnumerator RecoverWagers()
    {
        recovering=true;nextRecovery=Time.realtimeSinceStartup+5f;
        foreach(var entry in new List<TournamentWager>(progress.Data.tournamentWagers))
        {
            if(entry.ticket==inFlightTicket)continue;
            Response reply=null;
            yield return Post("/v1/tournament-receipt",JsonUtility.ToJson(new ReceiptRequest{ticket=entry.ticket}),null,r=>reply=r);
            if(reply==null || !reply.ok || !reply.ready || !progress.Data.tournamentWagers.Contains(entry))continue;
            int credit=reply.refund?entry.amount:reply.payout;
            if(credit<0 || credit>8000000)continue;
            int previous=progress.Data.coins;
            progress.Data.coins=(int)Math.Min(int.MaxValue,(long)previous+credit);
            progress.Data.tournamentWagers.Remove(entry);
            try{progress.Save();Notice(reply.refund?"Tournament entry fee refunded: "+credit.ToString("N0")+" coins.":credit>0?"Tournament winnings: "+credit.ToString("N0")+" coins!":"Tournament wager settled.");}
            catch(Exception){progress.Data.coins=previous;progress.Data.tournamentWagers.Add(entry);}
        }
        recovering=false;
    }
    private static IEnumerator Post(string path,string json,string token,Action<Response> done)
    {
        using(var request=new UnityWebRequest(FishingMultiplayer.Endpoint+path,"POST"))
        {
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=5;
            request.SetRequestHeader("Content-Type","application/json");if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);
            yield return request.SendWebRequest();
            Response reply=null;
            if(request.responseCode>0 && request.responseCode<500)
                try{reply=JsonUtility.FromJson<Response>(request.downloadHandler.text);}catch(ArgumentException){}
            done(reply);
        }
    }
    private void OpenStand()
    {
        Open("stand","TOURNAMENTS & ABYSSAL LEVIATHAN");
        var boss=GetComponent<AbyssalLeviathanEncounter>();
        int found=0,required=0;
        bool unlocked=boss!=null && boss.Unlocked(out found,out required);
        body.text="Abyssal Leviathan: catch every species across all four islands. Variants count as one species.\n"+
            found+" / "+required+" species recorded. Summoning costs 10,000 coins.\n"+
            (!session.Connected?"Open or join a multiplayer server first.":!session.BossReady?"The multiplayer server needs the Leviathan update.":boss!=null && boss.Active?"The Leviathan is already awake.":unlocked?"Summoning unlocked.":"Summoning locked until the island collection is complete.");
        ActionButton("FISHING TOURNAMENTS",-20,OpenTournamentModes);
        ActionButton("SUMMON ABYSSAL LEVIATHAN • 10,000 COINS",-120,()=>{boss.Summon();Close();},boss!=null && boss.CanSummon);
    }
    private void OpenTournamentModes()
    {
        if(!session.Connected){Open("info","FISHING TOURNAMENTS");body.text="Open or join a multiplayer server from MENU / TRAVEL first.";return;}
        if(current!=null && (current.phase=="inviting" || current.phase=="countdown" || current.phase=="running")){ShowLobby();return;}
        if(!session.IsHost){Open("info","FISHING TOURNAMENTS");body.text="Only the server host needs to visit this stand.\nThey choose the island and entry fee; everyone receives an invitation.";return;}
        Open("modes","CHOOSE TOURNAMENT FORMAT");
        body.text="Choose a classic target-species tournament in any unlocked fishing biome, or a map-wide total-weight tournament. All weight-tournament players start at Suncrest.";
        ActionButton("TARGET SPECIES • CHOOSE BIOME",15,OpenSpeciesIslands);
        ActionButton("MAP-WIDE • TOTAL FISH WEIGHT",-85,()=>ChooseWager(0,"weight"));
    }
    private void OpenSpeciesIslands()
    {
        Open("islands","CATCH A RANDOM SPECIES");
        body.text="Choose the tournament biome.\nThe server reveals one random species after invitations close.";
        int row=0;
        foreach(int island in new[]{0,4,1,3,2})
        {
            int choice=island;bool unlocked=Unlocked(choice);
            ActionButton(ReefCatalog.Zones[choice].name+(unlocked?"":" — LOCKED"),80-row*80,()=>ChooseWager(choice),unlocked);row++;
        }
    }
    private void ChooseWager(int island,string mode="species")
    {
        selectedIsland=island;selectedMode=mode;Open("wager",mode=="weight"?"ENTRY FEE • MAP-WIDE TOTAL WEIGHT":"ENTRY FEE • "+ReefCatalog.Zones[island].name.ToUpperInvariant());
        body.text=mode=="weight"
            ?"All participating anglers teleport to Suncrest Beach at the start.\nFish caught anywhere in the map count; most TOTAL KG after six minutes wins.\nTies: biggest catch, then earliest score. Winner takes all.\nYour balance: "+progress.Data.coins.ToString("N0")+" coins"
            :"100 points per kg of the target species. Six minutes.\nWinner takes all. Ties: biggest catch, then first to reach the score.\nNo qualifying catches or cancelled match: full refunds.\nYour balance: "+progress.Data.coins.ToString("N0")+" coins";
        var input=Panel("Wager",modal.transform,new Vector2(0,10),new Vector2(500,65),new Color(.95f,.95f,.9f));FishingHudTheme.Panel(input,3);modalControls.Add(input);
        wagerInput=input.AddComponent<InputField>();var text=Label(input.transform,"",26,Vector2.zero,new Vector2(470,60));text.color=Color.white;text.alignment=TextAnchor.MiddleLeft;
        wagerInput.textComponent=text;wagerInput.contentType=InputField.ContentType.IntegerNumber;wagerInput.characterLimit=7;wagerInput.text="100";
        ActionButton("WAGER & INVITE EVERYONE",-110,CreateTournament);
    }
    private void CreateTournament()
    {
        if(!session.IsHost || !Unlocked(selectedIsland) || !int.TryParse(wagerInput.text,out int amount)){Notice("Enter a whole coin amount.");return;}
        string id=Guid.NewGuid().ToString("N");
        if(!Reserve(id,amount,out var entry))return;
        commands.Enqueue(new Command{op="create",id=id,ticket=entry.ticket,balance=entry.balance,island=selectedIsland,wager=amount,mode=selectedMode});
        Open("info","SENDING INVITATIONS");body.text="Reserving your entry and inviting the room…";
    }
    private void ShowInvitation()
    {
        Open("invitation","TOURNAMENT INVITATION");
        bool can=Unlocked(current.island) && Mine()?.status=="pending" && !progress.ReadOnly && progress.Data.coins>=current.wager;
        ActionButton("JOIN • "+current.wager.ToString("N0")+" COINS",-145,Join,can);
        ActionButton("DECLINE",-235,Decline);
    }
    private void Join()
    {
        if(current==null || !Unlocked(current.island) || !Reserve(current.id,current.wager,out var entry))return;
        commands.Enqueue(new Command{op="join",id=current.id,ticket=entry.ticket,balance=entry.balance});ShowLobby();
    }
    private void Decline(){if(current!=null)commands.Enqueue(new Command{op="decline",id=current.id});Close();}
    private void ShowLobby()
    {
        Open("lobby","TOURNAMENT LOBBY");
        if(session.IsHost && current?.phase=="inviting")ActionButton("CANCEL & REFUND ENTRIES",-260,()=>{commands.Enqueue(new Command{op="cancel",id=current.id});Close();});
    }
    private void ShowResults(){Open("results",current.phase=="ended"?"TOURNAMENT COMPLETE":"TOURNAMENT CANCELLED");BuildResults();RefreshText();}
    private void RefreshText()
    {
        if(current==null || body==null)return;
        if(screen=="invitation")body.text=current.islandName+" • "+current.wager.ToString("N0")+" coins to join\nSix minutes • "+(current.mode=="weight"?"MOST TOTAL FISH WEIGHT":"100 points per target-species kg")+" • Winner takes all\n\n"+
            (!Unlocked(current.island)?"You have not unlocked this island and cannot join.":progress.Data.coins<current.wager?"You do not have enough coins to join.":current.mode=="weight"?"Join to compete, or decline.\nJoined players start at Suncrest Beach, but fish from any biome count.":"Join to wager the entry fee, or decline.\nThe target species is revealed after everyone responds.");
        else if(screen=="lobby")
        {
            body.rectTransform.anchoredPosition=new Vector2(0,40);body.rectTransform.sizeDelta=new Vector2(850,430);
            var text=new StringBuilder((current.mode=="weight"?"TOTAL WEIGHT • ":"")+current.islandName+" • Pool "+current.pool.ToString("N0")+" coins\n\n");
            foreach(var e in current.entries??new Entry[0])text.AppendLine(e.name+" — "+e.status.ToUpperInvariant());
            text.Append("\nUnanswered invitations decline after 45 seconds.");body.text=text.ToString();
        }
        else if(screen=="announcement")body.text=(current.mode=="weight"
            ?"CATCH FISH FROM ANY BIOME\n\nAll competitors start at Suncrest Beach.\nMost total kilograms caught in six minutes wins. Only NEW casts during the tournament count, across the entire fishing map."
            :"CATCH "+current.speciesName.ToUpperInvariant()+"\n\n"+current.islandName+" • Six minutes • 100 points per kg\nOnly new casts in these island waters score. Normal and variant forms score equally. Other species do not score.")
            +"\n\nStarts in "+Mathf.CeilToInt(Mathf.Max(0,current.startSeconds-(Time.realtimeSinceStartup-receivedAt)))+" seconds";
        else if(screen=="results")
        {
            RefreshResults();
        }
    }
    private void Open(string page,string heading)
    {
        ClearResults();
        foreach(var go in modalControls){go.SetActive(false);Destroy(go);}modalControls.Clear();wagerInput=null;
        screen=page;LayoutModal(page=="results");body.gameObject.SetActive(page!="results");body.rectTransform.anchoredPosition=new Vector2(0,45);body.rectTransform.sizeDelta=new Vector2(850,450);notice="";noticeText.text="";title.text=heading;body.text="";shade.SetActive(true);modal.SetActive(true);ModalOpen=true;controls.SetMenuOpen(true);
    }
    private void Close(){ClearResults();if(shade!=null)shade.SetActive(false);if(modal!=null)modal.SetActive(false);screen="";ModalOpen=false;if(controls!=null)controls.SetMenuOpen(ShopWorldHUD.MenuOpen);}
    private void Notice(string text){notice=text;if(toast!=null){toast.text=text;toastUntil=Time.realtimeSinceStartup+7f;}if(noticeText!=null)noticeText.text=text;Debug.Log("[Tournament] "+text);}
    private void BuildUI()
    {
        var go=new GameObject("Tournament HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=220;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        hud=Label(go.transform,"",22,new Vector2(0,390),new Vector2(850,75));hud.color=new Color(1,.88f,.5f);
        toast=Label(go.transform,"",20,new Vector2(0,-400),new Vector2(1000,65));toast.color=new Color(1,.85f,.42f);
        interact=Button(go.transform,"TOURNAMENT STAND",new Vector2(0,-300),new Vector2(360,85),OpenStand);
        shade=Panel("Tournament shade",go.transform,Vector2.zero,Vector2.zero,new Color(0,0,0,.55f));
        var cover=shade.GetComponent<RectTransform>();cover.anchorMin=Vector2.zero;cover.anchorMax=Vector2.one;cover.offsetMin=cover.offsetMax=Vector2.zero;
        modal=Panel("Tournament",shade.transform,Vector2.zero,new Vector2(940,760),new Color(.025f,.09f,.12f,.99f));
        title=Label(modal.transform,"",30,new Vector2(-20,315),new Vector2(790,75));title.color=Color.white;title.fontStyle=FontStyle.Bold;
        body=Label(modal.transform,"",25,new Vector2(0,45),new Vector2(850,450));body.resizeTextForBestFit=true;body.resizeTextMinSize=17;body.resizeTextMaxSize=25;
        closeButton=Button(modal.transform,"CLOSE",new Vector2(420,325),new Vector2(65,60),()=>{if(screen=="invitation")Decline();else Close();});
        noticeText=Label(modal.transform,"",20,new Vector2(0,-325),new Vector2(860,65));noticeText.color=new Color(1,.75f,.4f);
        LargeShopBackdropTheme.Apply(modal.GetComponent<Image>());
        headerTrophy=Artwork(modal.transform,"Trophy",Vector2.zero,new Vector2(64,70));
        Close();
    }
    private void ActionButton(string text,float y,UnityEngine.Events.UnityAction action,bool enabled=true)
    {
        var b=Button(modal.transform,text,new Vector2(0,y),new Vector2(620,62),action);b.interactable=enabled;modalControls.Add(b.gameObject);
        body.rectTransform.anchoredPosition=new Vector2(0,180);body.rectTransform.sizeDelta=new Vector2(850,200);
    }
    private static GameObject Panel(string name,Transform parent,Vector2 at,Vector2 size,Color color)
    {
        var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.sizeDelta=size;r.anchoredPosition=at;g.GetComponent<Image>().color=color;return g;
    }
    private static Text Label(Transform parent,string text,int size,Vector2 at,Vector2 dimensions)
    {
        var g=new GameObject("Text",typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);var t=g.GetComponent<Text>();
        t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.supportRichText=false;
        t.rectTransform.sizeDelta=dimensions;t.rectTransform.anchoredPosition=at;return t;
    }
    private static Button Button(Transform parent,string label,Vector2 at,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var g=Panel(label,parent,at,size,new Color(.06f,.34f,.37f,1));Label(g.transform,label,24,Vector2.zero,size-new Vector2(16,8));FishingHudTheme.Panel(g,label=="DECLINE" || label.StartsWith("CANCEL")?5:1);var b=g.AddComponent<Button>();b.onClick.AddListener(action);return b;
    }
    private void OnDestroy(){if(modal!=null && modal.activeSelf)Close();}
}
