using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reorganizes the generated ShopWorldHUD without requiring a scene/prefab reinstall.
///
/// MENU / TRAVEL keeps dimension travel (Quay/Home) and gains TELEPORT TO BOAT.
/// Island fast travel moves into ISLAND / BIOME INDEX, where every unlocked island
/// has separate TELEPORT and FISH INDEX buttons on the right. Deep Ocean intentionally
/// has no teleport button. Locked entries and unavailable actions are greyed out.
///
/// Boat placement is also remembered while visiting Home/Quay. The original boat
/// system removes its runtime instance during dimension teleports; this component
/// restores the exact placed boat at its saved world pose when the player returns.
///
/// The existing live-fishing safety is preserved: while a cast/fight is active,
/// teleport buttons are disabled but FISH INDEX still opens through the passive
/// FishingMenuSafetyRuntime path without cancelling the fish or releasing REEL.
/// </summary>
[DefaultExecutionOrder(1500)]
public sealed class TravelAndIslandMenuRuntime : MonoBehaviour
{
    private const string MarkerName="__TravelAndIslandMenuRuntime_v2";

    private static readonly BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly FieldInfo PageField=typeof(ShopWorldHUD).GetField("page",PrivateInstance);
    private static readonly FieldInfo ListField=typeof(ShopWorldHUD).GetField("list",PrivateInstance);
    private static readonly MethodInfo BiomeThumbnailMethod=typeof(ShopWorldHUD).GetMethod("BiomeThumbnail",PrivateInstance);
    private static readonly FieldInfo ActiveBoatBackingField=typeof(BoatSystem).GetField("<ActiveBoat>k__BackingField",PrivateInstance);
    private static readonly FieldInfo FishingStateField=typeof(FishingSystem).GetField("state",PrivateInstance);
    private static readonly MethodInfo SafeIndexOpenMethod=typeof(FishingMenuSafetyRuntime).GetMethod("SafeOpenIndex",PrivateInstance);

    private ShopWorldHUD hud;
    private ShopDimensionManager travel;
    private ShopProgress progress;
    private FirstPersonController player;
    private FishingSystem fishing;
    private FishingMenuSafetyRuntime menuSafety;
    private BoatSystem boats;
    private BoatPassenger passenger;
    private Font font;

    private BoatData placedBoatData;
    private Vector3 placedBoatPosition;
    private Quaternion placedBoatRotation;
    private bool hasPlacedBoat;
    private Coroutine boatTeleportRoutine;

    private readonly Color teal=new Color(.04f,.36f,.39f,1f);
    private readonly Color gold=new Color(.89f,.72f,.40f,1f);
    private readonly Color muted=new Color(.23f,.23f,.23f,1f);
    private readonly Color mutedText=new Color(.60f,.60f,.60f,1f);
    private bool previousTournamentLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(ShopWorldHUD existing in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(existing!=null && existing.GetComponent<TravelAndIslandMenuRuntime>()==null)
                existing.gameObject.AddComponent<TravelAndIslandMenuRuntime>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=GetComponent<ShopProgress>();
        player=GetComponent<FirstPersonController>();
        fishing=GetComponent<FishingSystem>();
        menuSafety=GetComponent<FishingMenuSafetyRuntime>();
        boats=GetComponent<BoatSystem>();
        passenger=GetComponent<BoatPassenger>();

        if(progress==null)progress=FindFirstObjectByType<ShopProgress>();
        if(player==null && progress!=null)player=progress.GetComponent<FirstPersonController>();
        if(fishing==null && progress!=null)fishing=progress.GetComponent<FishingSystem>();
        if(menuSafety==null)menuSafety=FindFirstObjectByType<FishingMenuSafetyRuntime>();
        if(boats==null && progress!=null)boats=progress.GetComponent<BoatSystem>();
        if(passenger==null && progress!=null)passenger=progress.GetComponent<BoatPassenger>();

        travel=ShopDimensionManager.Instance;
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if(hud==null || PageField==null || ListField==null)
            enabled=false;
    }

    private void LateUpdate()
    {
        if(!enabled)return;
        if(travel==null)travel=ShopDimensionManager.Instance;
        if(menuSafety==null)menuSafety=GetComponent<FishingMenuSafetyRuntime>();

        CapturePlacedBoat();
        RestorePlacedBoatIfNeeded();

        if(!ShopWorldHUD.MenuOpen)return;
        string page=PageField.GetValue(hud) as string;
        if(page!="travel" && page!="islands")return;

        RectTransform list=ListField.GetValue(hud) as RectTransform;
        if(list==null)return;
        // Refresh an already-open menu when a tournament starts or ends.
        bool restricted=travel!=null && travel.TournamentTravelRestricted;
        if(HasActiveMarker(list) && previousTournamentLock==restricted)return;
        previousTournamentLock=restricted;
        if(page=="travel")BuildTravelPage(list);
        else BuildIslandIndex(list);
    }

    private static bool HasActiveMarker(RectTransform list)
    {
        Transform marker=list.Find(MarkerName);
        return marker!=null && marker.gameObject.activeSelf;
    }

    private void ClearList(RectTransform list)
    {
        for(int i=list.childCount-1;i>=0;i--)
        {
            GameObject child=list.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void AddMarker(RectTransform list)
    {
        GameObject marker=new GameObject(MarkerName,typeof(RectTransform),typeof(LayoutElement));
        marker.transform.SetParent(list,false);
        LayoutElement layout=marker.GetComponent<LayoutElement>();
        layout.ignoreLayout=true;
        marker.GetComponent<RectTransform>().sizeDelta=Vector2.zero;
    }

    private void BuildTravelPage(RectTransform list)
    {
        ClearList(list);
        // One responsive layout slot keeps both cards and the boat strip visible together.
        GameObject surface=Panel("TravelDestinations",list,Color.clear);
        var slot=surface.AddComponent<LayoutElement>();
        slot.preferredHeight=Mathf.Max(560f,((RectTransform)list.parent).rect.height-4f);
        var resize=surface.AddComponent<TravelMenuHeight>();resize.viewport=(RectTransform)list.parent;
        CreateTravelCard(surface.transform,0f,.5f,"HOME",
            "Your garden, aquarium, pond and fish collection.",2,"Home",FishingHudSymbol.Kind.Home);
        CreateTravelCard(surface.transform,.5f,1f,"SHOP",
            "Buy baits, lures and equipment. Upgrade your gear and supplies.",1,"Shop",FishingHudSymbol.Kind.Cart);
        CreateBoatTravelCard(surface.transform);
        AddMarker(list);
    }

    private void CreateTravelCard(Transform parent,float minX,float maxX,string title,string detail,
        int destination,string artwork,FishingHudSymbol.Kind icon)
    {
        bool here=travel!=null && travel.Destination==destination;
        bool allowed=travel==null || travel.TournamentAllowsDimension(destination);
        var card=Panel(title+"TravelCard",parent,Color.white);
        FishingHudTheme.Panel(card);
        Anchor(card.GetComponent<RectTransform>(),minX,.21f,maxX,1f,6,8,-6,0);
        TravelMenuArtwork.Place(card.transform,artwork);
        FishingHudSymbol.Add(card.transform,icon,new Vector2(.10f,.35f),Vector2.zero,78);
        Text name=Label(card.transform,title,40,Color.white,TextAnchor.MiddleLeft);
        name.fontStyle=FontStyle.Bold;
        Anchor(name.rectTransform,.21f,.36f,1f,.47f,0,0,-20,0);
        Text description=Label(card.transform,detail,27,new Color(.70f,.91f,.97f),TextAnchor.UpperLeft);
        description.resizeTextForBestFit=true;description.resizeTextMinSize=18;description.resizeTextMaxSize=27;
        Anchor(description.rectTransform,.21f,.19f,.97f,.36f,0,0,0,0);
        string footer=!allowed?"LOCKED DURING TOURNAMENT":here?"YOU ARE HERE":"TRAVEL TO "+title;
        Button button=ButtonAt(card.transform,footer,()=>
        {
            CapturePlacedBoat();
            if(travel!=null)travel.Travel(destination);
        });
        Anchor(button.GetComponent<RectTransform>(),.12f,.045f,.88f,.175f,0,0,0,0);
        FishingHudTheme.Panel(button.gameObject,1);
        SetButtonAvailable(button,allowed && !here && !IsFishingBusy());
    }

    private void CreateBoatTravelCard(Transform parent)
    {
        bool owns=OwnsAnyBoat();
        bool placed=hasPlacedBoat || (boats!=null && boats.ActiveBoat!=null);
        bool restricted=travel!=null && travel.TournamentTravelRestricted;
        string footer=restricted?"LOCKED DURING TOURNAMENT":!owns?"NO BOAT OWNED":!placed?"BOAT NOT PLACED":"TRAVEL TO BOAT";
        var row=Panel("ReturnToBoat",parent,Color.white);FishingHudTheme.Panel(row);
        Anchor(row.GetComponent<RectTransform>(),0,0,1,.21f,6,0,-6,-8);
        FishingHudSymbol.Add(row.transform,FishingHudSymbol.Kind.Boat,new Vector2(.055f,.5f),Vector2.zero,78);
        Text title=Label(row.transform,"RETURN TO YOUR BOAT",30,Color.white,TextAnchor.MiddleLeft);
        title.fontStyle=FontStyle.Bold;
        Anchor(title.rectTransform,.105f,.5f,.71f,.88f,0,0,-12,0);
        Text detail=Label(row.transform,"Return directly to the boat you placed in the fishing world.",23,new Color(.70f,.91f,.97f),TextAnchor.MiddleLeft);
        detail.resizeTextForBestFit=true;detail.resizeTextMinSize=16;detail.resizeTextMaxSize=23;
        Anchor(detail.rectTransform,.105f,.12f,.71f,.5f,0,0,-12,0);
        Button button=ButtonAt(row.transform,footer,BeginBoatTeleport);
        Anchor(button.GetComponent<RectTransform>(),.73f,.20f,.975f,.80f,0,0,0,0);
        FishingHudTheme.Panel(button.gameObject,1);
        SetButtonAvailable(button,!restricted && owns && placed && travel!=null && !travel.Traveling && !IsFishingBusy());
    }

    private bool OwnsAnyBoat()
    {
        if(progress==null || boats==null || boats.Catalog==null)return false;
        for(int i=0;i<boats.Catalog.Length;i++)
        {
            BoatData data=boats.Catalog[i];
            if(data!=null && progress.Data.OwnsBoat(data.ID))return true;
        }
        return false;
    }

    private void BuildIslandIndex(RectTransform list)
    {
        ClearList(list);
        int currentBiome=player!=null?IslandExpansionWorld.FishingBiome(player.transform.position):0;

        foreach(int biome in ReefCatalog.IslandIndexOrder)
            CreateIslandCard(list,biome,currentBiome);

        AddMarker(list);
    }

    private void CreateIslandCard(RectTransform list,int biome,int currentBiome)
    {
        var entry=ReefCatalog.Zones[biome];
        bool unlocked=entry.Unlocked;
        bool isIsland=biome==0 || biome==1 || biome==3 || biome==4;
        bool here=travel!=null && travel.Destination==0 && currentBiome==biome;
        bool worldReady=biome==4?SnapperIslandRuntime.Ready:biome==0 || biome==2 || (IslandExpansionWorld.Active!=null && IslandExpansionWorld.Active.Ready);
        bool fishingBusy=IsFishingBusy();

        Color cardColor=biome==0?new Color(.045f,.24f,.30f,1f):biome==1?new Color(.20f,.23f,.25f,1f):new Color(.025f,.12f,.20f,1f);
        if(!unlocked)cardColor=muted;

        GameObject card=Panel("Biome_"+biome,list,cardColor);
        card.AddComponent<LayoutElement>().preferredHeight=240f;

        RawImage picture=new GameObject("Preview",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
        picture.transform.SetParent(card.transform,false);
        Anchor(picture.rectTransform,0f,0f,.26f,1f,16,18,-8,-18);
        picture.raycastTarget=false;
        if(BiomeThumbnailMethod!=null)
        {
            object value=BiomeThumbnailMethod.Invoke(hud,new object[]{biome});
            picture.texture=value as Texture;
        }
        picture.color=unlocked?Color.white:new Color(.38f,.38f,.38f,1f);

        Text title=Label(card.transform,entry.name.ToUpperInvariant()+"   •   "+(unlocked?"UNLOCKED":"LOCKED"),28,unlocked?gold:mutedText,TextAnchor.UpperLeft);
        Anchor(title.rectTransform,.27f,.68f,.71f,1f,12,8,-8,-12);
        title.horizontalOverflow=HorizontalWrapMode.Wrap;title.verticalOverflow=VerticalWrapMode.Truncate;

        Text description=Label(card.transform,entry.description,22,unlocked?Color.white:mutedText,TextAnchor.UpperLeft);
        // Dedicated middle column ends before the right-side buttons. Long biome
        // descriptions wrap here instead of running underneath/into the buttons.
        Anchor(description.rectTransform,.27f,.08f,.71f,.70f,12,6,-12,-4);
        description.horizontalOverflow=HorizontalWrapMode.Wrap;
        description.verticalOverflow=VerticalWrapMode.Truncate;
        description.resizeTextForBestFit=true;description.resizeTextMinSize=17;description.resizeTextMaxSize=22;

        if(isIsland)
        {
            bool allowed=travel==null || travel.TournamentAllowsIsland(biome);
            string travelText=!allowed?"TOURNAMENT LOCK":here?"YOU ARE HERE":fishingBusy?"FISHING…":"TELEPORT";
            Button teleport=ButtonAt(card.transform,travelText,()=>TeleportToIsland(biome));
            Anchor(teleport.GetComponent<RectTransform>(),.74f,.54f,.98f,.88f,0,0,0,0);
            SetButtonAvailable(teleport,allowed && unlocked && worldReady && !here && !fishingBusy && travel!=null && !travel.Traveling);
        }

        Button fishIndex=ButtonAt(card.transform,"FISH INDEX",()=>OpenFishIndex(biome));
        if(isIsland)Anchor(fishIndex.GetComponent<RectTransform>(),.74f,.12f,.98f,.46f,0,0,0,0);
        else Anchor(fishIndex.GetComponent<RectTransform>(),.74f,.32f,.98f,.68f,0,0,0,0);
        SetButtonAvailable(fishIndex,unlocked);
    }

    private bool IsFishingBusy()
    {
        if(fishing==null || FishingStateField==null)return false;
        object value=FishingStateField.GetValue(fishing);
        return value!=null && !string.Equals(value.ToString(),"Idle",System.StringComparison.Ordinal);
    }

    private void TeleportToIsland(int biome)
    {
        if(IsFishingBusy())return;
        if(travel==null || travel.Traveling || !travel.TournamentAllowsIsland(biome) || biome<0 || biome>=ReefCatalog.Zones.Length)return;
        if(!ReefCatalog.Zones[biome].Unlocked)return;
        if(biome!=0 && biome!=1 && biome!=3 && biome!=4)return; // Deep Ocean and future non-island biomes have no teleport.
        if((biome==1 || biome==3) && (IslandExpansionWorld.Active==null || !IslandExpansionWorld.Active.Ready))return;

        CapturePlacedBoat();
        travel.TravelIsland(biome);
    }

    private void OpenFishIndex(int biome)
    {
        if(hud==null || biome<0 || biome>=ReefCatalog.Zones.Length || !ReefCatalog.Zones[biome].Unlocked)return;
        if(!hud.SelectIndexBiome(biome))return;

        // During a live cast/fight, route through the safety runtime's existing
        // passive overlay instead of ShopWorldHUD.Open(), which would call
        // PrepareForMenu and cancel the fishing interaction.
        if(IsFishingBusy() && menuSafety!=null && SafeIndexOpenMethod!=null)
        {
            SafeIndexOpenMethod.Invoke(menuSafety,new object[]{"reef-fish"});
            return;
        }

        hud.Open("reef-fish");
    }

    private void CapturePlacedBoat()
    {
        if(boats==null)return;
        BoatController active=boats.ActiveBoat;
        if(active==null || active.Data==null)return;

        placedBoatData=active.Data;
        placedBoatPosition=active.transform.position;
        placedBoatRotation=active.transform.rotation;
        hasPlacedBoat=true;
    }

    private void RestorePlacedBoatIfNeeded()
    {
        if(!hasPlacedBoat || placedBoatData==null || placedBoatData.Prefab==null || boats==null || travel==null)return;
        if(travel.Traveling || travel.Destination!=0 || boats.ActiveBoat!=null)return;
        if(ActiveBoatBackingField==null)return;

        GameObject restored=Instantiate(placedBoatData.Prefab,placedBoatPosition,placedBoatRotation);
        BoatController controller=restored.GetComponent<BoatController>();
        if(controller==null)
        {
            Destroy(restored);
            return;
        }

        controller.Initialize(placedBoatData,boats.Water);
        Rigidbody body=controller.GetComponent<Rigidbody>();
        if(body!=null)
        {
            body.linearVelocity=Vector3.zero;
            body.angularVelocity=Vector3.zero;
        }
        ActiveBoatBackingField.SetValue(boats,controller);
    }

    private void BeginBoatTeleport()
    {
        if(IsFishingBusy())return;
        if(boatTeleportRoutine!=null || travel==null || travel.Traveling || travel.TournamentTravelRestricted)return;
        CapturePlacedBoat();
        if(!hasPlacedBoat)return;
        boatTeleportRoutine=StartCoroutine(TeleportToBoatRoutine());
    }

    private IEnumerator TeleportToBoatRoutine()
    {
        if(travel==null || travel.TournamentTravelRestricted){boatTeleportRoutine=null;yield break;}
        if(travel.Destination!=0)
        {
            travel.Travel(0);
            // TravelRoutine starts on the next coroutine step.
            yield return null;
            while(travel!=null && travel.Traveling)yield return null;
            if(travel==null || travel.Destination!=0)
            {
                boatTeleportRoutine=null;
                yield break;
            }
        }

        if(travel==null || travel.TournamentTravelRestricted){boatTeleportRoutine=null;yield break;}
        RestorePlacedBoatIfNeeded();
        yield return null;
        if(travel==null || travel.TournamentTravelRestricted){boatTeleportRoutine=null;yield break;}
        RestorePlacedBoatIfNeeded();

        BoatController boat=boats!=null?boats.ActiveBoat:null;
        if(boat==null)
        {
            boatTeleportRoutine=null;
            yield break;
        }

        if(travel==null || travel.TournamentTravelRestricted){boatTeleportRoutine=null;yield break;}
        if(fishing!=null)fishing.PrepareForWorldTravel();
        if(passenger!=null)passenger.Detach();
        if(hud!=null && ShopWorldHUD.MenuOpen)hud.Close();

        Transform arrival=boat.DeckExit!=null?boat.DeckExit:boat.transform;
        Vector3 position=arrival.position+Vector3.up*.28f;
        Quaternion rotation=Quaternion.Euler(0f,boat.transform.eulerAngles.y,0f);
        CharacterController capsule=player!=null?player.GetComponent<CharacterController>():null;
        if(capsule!=null)capsule.enabled=false;
        if(player!=null)player.transform.SetPositionAndRotation(position,rotation);
        if(capsule!=null)capsule.enabled=true;
        if(player!=null)
        {
            player.ResetMotion();
            player.ResetViewPitch();
        }
        Physics.SyncTransforms();

        boatTeleportRoutine=null;
    }

    private Button ButtonAt(Transform parent,string text,UnityEngine.Events.UnityAction action)
    {
        GameObject go=new GameObject("Button",typeof(RectTransform),typeof(Image),typeof(Button));
        go.transform.SetParent(parent,false);
        Image image=go.GetComponent<Image>();image.color=teal;
        Button button=go.GetComponent<Button>();
        if(action!=null)button.onClick.AddListener(action);
        Text label=Label(go.transform,text,25,Color.white,TextAnchor.MiddleCenter);
        Full(label.rectTransform,10,6,-10,-6);
        label.horizontalOverflow=HorizontalWrapMode.Wrap;
        label.verticalOverflow=VerticalWrapMode.Truncate;
        label.resizeTextForBestFit=true;label.resizeTextMinSize=17;label.resizeTextMaxSize=25;
        return button;
    }

    private void SetButtonAvailable(Button button,bool available)
    {
        if(button==null)return;
        button.interactable=available;
        if(available)return;
        Image image=button.GetComponent<Image>();if(image!=null)image.color=muted;
        foreach(Text text in button.GetComponentsInChildren<Text>(true))text.color=mutedText;
        foreach(RawImage raw in button.GetComponentsInChildren<RawImage>(true))raw.color=new Color(.38f,.38f,.38f,1f);
    }

    private GameObject Panel(string name,Transform parent,Color color)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);
        Image image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
        return go;
    }

    private Text Label(Transform parent,string value,int size,Color color,TextAnchor alignment)
    {
        GameObject go=new GameObject("Label",typeof(RectTransform),typeof(Text));
        go.transform.SetParent(parent,false);
        Text text=go.GetComponent<Text>();
        text.font=font;
        text.fontSize=size;
        text.text=value;
        text.alignment=alignment;
        text.color=color;
        text.raycastTarget=false;
        return text;
    }

    private static void Full(RectTransform rect,float left=0,float bottom=0,float right=0,float top=0)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(right,top);
    }

    private static void Anchor(RectTransform rect,float minX,float minY,float maxX,float maxY,float left,float bottom,float right,float top)
    {
        rect.anchorMin=new Vector2(minX,minY);rect.anchorMax=new Vector2(maxX,maxY);
        rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(right,top);
    }
}
