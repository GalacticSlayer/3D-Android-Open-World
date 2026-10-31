using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed class ShopWorldHUD : MonoBehaviour
{
    public static bool MenuOpen { get; private set; }
    private const string HabitatAddPrefix="habitat-add:";
    private AudioSource saleAudio;
    private AudioClip saleSound;

    private void PlaySaleSound()
    {
        if (saleAudio == null)
        {
            saleAudio = gameObject.AddComponent<AudioSource>();
            saleAudio.playOnAwake = false;
            saleAudio.spatialBlend = 0f;
            saleSound = Resources.Load<AudioClip>("Fishing/Audio/SoldFishCoins");
        }
        if (saleSound != null) saleAudio.PlayOneShot(saleSound, 0.5f);
    }

    private ShopProgress progress;
    private ShopDimensionManager travel;
    private FishingSystem fishing;
    private FirstPersonController player;
    private FishingHUD fishingHUD;
    private Font font;
    private GameObject root, modal, nearbyButton;
    private GameObject storeHeader, storeBody;
    private Button menuClose, perspectiveToggle;
    private GameObject profileNameRow;
    private InputField profileNameInput;
    private RectTransform list;
    private GameObject personalStatsView;
    private ScrollRect scroll;
    private Text heading, wallet, feedback, nearLabel;
    private string page="travel", message="";
    private int indexBiome;
    private FishIndexView fishIndexView;
    private Texture2D rockyThumbnail,deepThumbnail;
    private bool dirty;
    private string shopSession, bagPicker, habitatPicker;
    private int bagSort, speciesFilter=-1, habitatSort, habitatSpeciesFilter=-1;
    private GameObject menuShortcut, indexShortcut, baitShortcut;
    private RawImage baitPicture;
    private Text baitQuantity, baitName;
    private ShopPreview baitPreview;
    private int shownBait=-1,shownBaitQuantity=-1;
    private Texture2D islandThumbnail;
    private readonly List<GameObject> navigationTabs=new List<GameObject>();
    private static readonly string[] SortNames={"Newest first","Heaviest first","Lightest first","Highest value","Species name"};
    private ShopPreview previews;
    private readonly Color ink=new Color(0.025f,0.055f,0.07f,0.98f);
    private readonly Color teal=new Color(0.04f,0.36f,0.39f,1f);
    private readonly Color gold=new Color(0.89f,0.72f,0.40f,1f);
    private void Start()
    {
        progress=FindFirstObjectByType<ShopProgress>(); travel=ShopDimensionManager.Instance;
        if(progress==null || travel==null) { enabled=false; return; }
        fishing=progress.GetComponent<FishingSystem>(); player=progress.GetComponent<FirstPersonController>();
        fishingHUD=GetComponent<FishingHUD>(); font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        previews=gameObject.AddComponent<ShopPreview>();
        Build(); progress.Changed+=RefreshSoon; message=progress.Notice; MenuOpen=false;
    }
    private void OnDestroy() { if(progress!=null) progress.Changed-=RefreshSoon; MenuOpen=false;if(islandThumbnail!=null)Destroy(islandThumbnail);if(rockyThumbnail!=null)Destroy(rockyThumbnail);if(deepThumbnail!=null)Destroy(deepThumbnail);if(bluewaterThumbnail!=null)Destroy(bluewaterThumbnail);if(snapperThumbnail!=null)Destroy(snapperThumbnail); }
    private void RefreshSoon() => dirty=true;
    private void Update()
    {
        if(root==null) return;
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        { if(MenuOpen) Close(); else Open("travel"); }
        // The animated catch-XP bar temporarily replaces MENU / TRAVEL only.
        // Keep the other shortcuts working and restore the menu button afterward.
        menuShortcut.SetActive(!MenuOpen && !FishingExperienceToast.IsShowingXp);
        indexShortcut.SetActive(!MenuOpen);baitShortcut.SetActive(!MenuOpen);RefreshBaitShortcut();
        bool independentPage=page=="bait" || page.StartsWith(HabitatAddPrefix,StringComparison.Ordinal);
        if(shopSession==null && !independentPage && Keyboard.current!=null && Keyboard.current.tabKey.wasPressedThisFrame) Open("bag");
        string nearest=travel.Nearest();
        nearbyButton.SetActive(!MenuOpen && nearest!=null && !travel.Traveling);
        if(nearest!=null) nearLabel.text=nearest=="gear" ? "OPEN TACKLE STORE" : nearest=="market" ? "SELL FISH" : "VIEW HABITAT";
        if(!MenuOpen && nearest!=null && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame) Open(nearest);

        if(travel.Traveling) { feedback.text="Traveling..."; return; }
        if(travel.TravelError!=null) feedback.text=travel.TravelError;
        if(dirty && MenuOpen) { dirty=false; Refresh(false); }
    }
    public void Open(string destination)
    {
        if(root==null || travel.Traveling) return;
        if(MenuOpen && shopSession!=null && destination!=shopSession && !(shopSession=="market" && destination=="sell-confirm"))return;
        if(MenuOpen && page==destination && !dirty)return;
        if(destination!=page){SaveProfileName();message="";}
        if(!MenuOpen)
        {
            shopSession=destination=="gear" || destination=="market"?destination:null;
            fishing.PrepareForMenu();
        }
        MenuOpen=true; page=destination; modal.SetActive(true); root.transform.SetAsLastSibling();
        player.SetMenuOpen(true); if(fishingHUD!=null) fishingHUD.SetMenuCovered(true);
        Refresh(true);
    }
    public void Close()
    {
        if(travel!=null && travel.Traveling) return;
        SaveProfileName();
        MenuOpen=false;shopSession=null;bagPicker=null;habitatPicker=null;message="";
        if(previews!=null)previews.Suspend();
        if(modal!=null) modal.SetActive(false);
        if(player!=null) player.SetMenuOpen(false);
        if(fishingHUD!=null) fishingHUD.SetMenuCovered(false);

    }
    private void Build()
    {
        root=Panel("TideglassHUD",transform,Color.clear); Full(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().raycastTarget=false;
        Button menu=ButtonAt(root.transform,"MENU / TRAVEL",()=>Open("travel"));
        menuShortcut=menu.gameObject;
        Rect(menu.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),new Vector2(-36,-32),new Vector2(380,92));
        indexShortcut=ButtonAt(root.transform,"ISLAND / FISH INDEX",()=>Open("islands")).gameObject;
        Rect(indexShortcut.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(36,-32),new Vector2(460,92));
        baitShortcut=ButtonAt(root.transform,"",()=>Open("bait")).gameObject;
        Rect(baitShortcut.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(36,-152),new Vector2(200,150));
        baitQuantity=baitShortcut.GetComponentInChildren<Text>();Anchor(baitQuantity.rectTransform,.48f,0,1,.40f,0,8,-12,0);
        baitQuantity.resizeTextMaxSize=30;baitQuantity.fontSize=30;
        baitName=Label(baitShortcut.transform,"WORMS",20,Color.white);baitName.alignment=TextAnchor.MiddleCenter;Anchor(baitName.rectTransform,.34f,.58f,1,1,0,0,-8,-6);
        var picture=new GameObject("EquippedBaitPicture",typeof(RectTransform),typeof(RawImage));picture.transform.SetParent(baitShortcut.transform,false);
        baitPicture=picture.GetComponent<RawImage>();baitPicture.raycastTarget=false;Anchor(baitPicture.rectTransform,0,.05f,.43f,.91f,8,0,0,0);
        FishingHudTheme.Panel(menuShortcut);
        FishingHudTheme.Panel(indexShortcut);
        FishingHudTheme.Panel(baitShortcut);
        baitShortcut.AddComponent<BaitShortcutCard>().Initialize(baitName,baitQuantity,baitPicture);
        StyleHudShortcut(menuShortcut,FishingHudSymbol.Kind.Pin);
        StyleHudShortcut(indexShortcut,FishingHudSymbol.Kind.Island);
        var baitColors=baitShortcut.GetComponent<Button>().colors;
        baitColors.normalColor=Color.white; baitColors.highlightedColor=Color.white;
        baitColors.pressedColor=new Color(.8f,.95f,1f,1f);
        baitShortcut.GetComponent<Button>().colors=baitColors;
        baitPreview=gameObject.AddComponent<ShopPreview>();RefreshBaitShortcut();
        nearbyButton=ButtonAt(root.transform,"OPEN",()=>Open(travel.Nearest()??"travel")).gameObject;
        nearLabel=nearbyButton.GetComponentInChildren<Text>();
        Rect(nearbyButton.GetComponent<RectTransform>(),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0,130),new Vector2(300,58));
        modal=Panel("ShopMenu",root.transform,ink);
        var r=modal.GetComponent<RectTransform>(); r.anchorMin=new Vector2(0.045f,0.045f); r.anchorMax=new Vector2(0.955f,0.955f); r.offsetMin=r.offsetMax=Vector2.zero;
        storeHeader=Panel("StoreGlassHeader",modal.transform,Color.white);
        FishingHudTheme.Panel(storeHeader);storeHeader.GetComponent<Image>().raycastTarget=false;
        Anchor(storeHeader.GetComponent<RectTransform>(),0,1,1,1,0,-130,0,0);
        FishingHudSymbol.Add(storeHeader.transform,FishingHudSymbol.Kind.Cart,new Vector2(0,.5f),new Vector2(68,0),84);
        storeBody=Panel("StoreGlassBody",modal.transform,Color.white);
        FishingHudTheme.Panel(storeBody);storeBody.GetComponent<Image>().raycastTarget=false;
        Anchor(storeBody.GetComponent<RectTransform>(),0,0,1,1,0,60,0,-258);
        storeHeader.SetActive(false);storeBody.SetActive(false);
        heading=Label(modal.transform,"MENU / TRAVEL",36,gold); Anchor(heading.rectTransform,0,1,1,1,24,-62,-150,-12);
        BuildProfileName();
        wallet=Label(modal.transform,"",22,Color.white); Anchor(wallet.rectTransform,0,1,1,1,24,-92,-24,-62);
        Button close=ButtonAt(modal.transform,"CLOSE",Close); menuClose=close; Rect(close.GetComponent<RectTransform>(),Vector2.one,Vector2.one,Vector2.one,new Vector2(-18,-16),new Vector2(160,64));
        perspectiveToggle=ButtonAt(modal.transform,"",()=>
        {
            var view=FishingThirdPersonView.For(player);
            if(view!=null) { view.Toggle(); perspectiveToggle.GetComponent<Image>().color=view.IsThirdPerson?gold:teal; }
        });
        perspectiveToggle.gameObject.name="Toggle Third Person";
        Rect(perspectiveToggle.GetComponent<RectTransform>(),Vector2.one,Vector2.one,Vector2.one,new Vector2(-190,-16),new Vector2(64,64));
        FishingHudSymbol.Add(perspectiveToggle.transform,FishingHudSymbol.Kind.Camera,new Vector2(.5f,.5f),Vector2.zero,48);
        string[] tabs={"travel","bag","equipment"}; string[] names={"TRAVEL","FISH BAG","EQUIPMENT"};
        for(int i=0;i<tabs.Length;i++) { string tab=tabs[i]; var b=ButtonAt(modal.transform,names[i],()=>Open(tab)); navigationTabs.Add(b.gameObject); Anchor(b.GetComponent<RectTransform>(),i/3f,1,(i+1)/3f,1,16,-148,-16,-103); }
        GameObject viewport=Panel("ScrollViewport",modal.transform,new Color(0,0,0,0.1f));
        var vr=viewport.GetComponent<RectTransform>(); Anchor(vr,0,0,1,1,20,70,-30,-164);
        viewport.AddComponent<RectMask2D>(); scroll=viewport.AddComponent<ScrollRect>();
        GameObject content=new GameObject("Rows",typeof(RectTransform)); content.transform.SetParent(viewport.transform,false);
        list=content.GetComponent<RectTransform>(); list.anchorMin=new Vector2(0,1); list.anchorMax=Vector2.one; list.pivot=new Vector2(0.5f,1); list.sizeDelta=Vector2.zero;
        var layout=content.AddComponent<VerticalLayoutGroup>(); layout.spacing=10; layout.childControlHeight=true; layout.childControlWidth=true; layout.childForceExpandHeight=false;
        content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=vr; scroll.content=list; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=42;
        // Visible scrollbar, plus mouse wheel and touch dragging on the full list.
        var track=Panel("Scrollbar",modal.transform,new Color(0.12f,0.19f,0.21f,1)); Anchor(track.GetComponent<RectTransform>(),1,0,1,1,-22,70,-10,-164);
        var handle=Panel("Handle",track.transform,gold); Full(handle.GetComponent<RectTransform>());
        var bar=track.AddComponent<Scrollbar>(); bar.handleRect=handle.GetComponent<RectTransform>(); bar.targetGraphic=handle.GetComponent<Image>(); bar.direction=Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=bar;
        feedback=Label(modal.transform,"",18,gold); Anchor(feedback.rectTransform,0,0,1,0,24,10,-24,60);
        modal.SetActive(false); nearbyButton.SetActive(false);
    }
    private void BuildProfileName()
    {
        profileNameRow=new GameObject("Profile name",typeof(RectTransform));
        profileNameRow.transform.SetParent(modal.transform,false);
        Anchor(profileNameRow.GetComponent<RectTransform>(),.42f,1,1,1,0,-62,-270,-12);
        var caption=Label(profileNameRow.transform,"Name:",27,Color.white);
        Anchor(caption.rectTransform,0,0,0,1,0,0,85,0);
        var field=Panel("Saved player name",profileNameRow.transform,Color.white);
        Anchor(field.GetComponent<RectTransform>(),0,0,1,1,90,0,0,0);
        FishingHudTheme.Panel(field,3);
        profileNameInput=field.AddComponent<InputField>();
        profileNameInput.targetGraphic=field.GetComponent<Image>();
        profileNameInput.characterLimit=20;profileNameInput.lineType=InputField.LineType.SingleLine;
        var text=Label(field.transform,"",26,Color.white);text.supportRichText=false;text.alignment=TextAnchor.MiddleLeft;
        text.resizeTextForBestFit=false;Anchor(text.rectTransform,0,0,1,1,14,4,-14,-4);
        profileNameInput.textComponent=text;
        profileNameInput.SetTextWithoutNotify(ShopProgress.NormalizePlayerName(progress.Data.playerName));
        profileNameInput.onEndEdit.AddListener(_=>SaveProfileName());
    }
    private void OnApplicationPause(bool paused){if(paused)SaveProfileName();}
    private void OnApplicationQuit(){SaveProfileName();}
    private void SaveProfileName()
    {
        if(profileNameInput==null || profileNameRow==null || !profileNameRow.activeInHierarchy)return;
        if(!progress.SetPlayerName(profileNameInput.text))
        {message="Could not save your name. Please try again.";dirty=true;}
        profileNameInput.SetTextWithoutNotify(ShopProgress.NormalizePlayerName(progress.Data.playerName));
    }
    private void Refresh(bool top)
    {
        dirty=false;
        if(fishIndexView!=null)fishIndexView.gameObject.SetActive(false);
        if(personalStatsView!=null){personalStatsView.SetActive(false);Destroy(personalStatsView);}
        float position=scroll.verticalNormalizedPosition;
        for(int i=list.childCount-1;i>=0;i--) { list.GetChild(i).gameObject.SetActive(false); Destroy(list.GetChild(i).gameObject); }
        wallet.text=$"{progress.Data.coins:N0} COINS   /   {progress.Data.bag.Count}/{ShopLedger.BagLimit} FISH IN BAG   /   LOCATION: {(travel.InHome?"HOME":travel.InShop?"TIDEGLASS QUAY":ReefCatalog.Zones[IslandExpansionWorld.FishingBiome(player.transform.position)].name.ToUpperInvariant())}";
        feedback.text=progress.ReadOnly ? progress.Notice : message;
        string habitatAddId=page.StartsWith(HabitatAddPrefix,StringComparison.Ordinal)?page.Substring(HabitatAddPrefix.Length):null;
        heading.text=shopSession=="gear"?"TACKLE STORE":shopSession=="market"?"SELL FISH":"MENU / TRAVEL";
        bool index=page=="islands" || page=="reef-fish";
        if(index)heading.text=page=="islands"?"ISLAND / BIOME INDEX":ReefCatalog.Zones[indexBiome].name.ToUpperInvariant()+" • FISH INDEX";
        if(page=="bait")heading.text="BAIT / LURES";
        if(page=="multiplayer")heading.text="MULTIPLAYER";
        if(page=="stats")heading.text="PERSONAL STATS";
        // Only the gold Personal Stats heading has a crisp, thin black border.
        // Keep the shared heading unchanged on every other shop/menu page.
        Outline statsHeadingOutline=heading.GetComponent<Outline>();
        if(page=="stats" && statsHeadingOutline==null)
            statsHeadingOutline=heading.gameObject.AddComponent<Outline>();
        if(statsHeadingOutline!=null)
        {
            statsHeadingOutline.effectColor=new Color(0f,0f,0f,.92f);
            statsHeadingOutline.effectDistance=new Vector2(1f,-1f);
            statsHeadingOutline.useGraphicAlpha=true;
            statsHeadingOutline.enabled=page=="stats";
        }
        if(habitatAddId!=null)
        {
            var habitat=ShopCatalog.Habitat(habitatAddId);
            heading.text=habitat!=null?"ADD FISH • "+habitat.name.ToUpperInvariant():"ADD FISH";
        }
        bool independent=index || page=="bait" || page=="stats" || habitatAddId!=null;
        SetStorePresentation(shopSession=="gear");
        perspectiveToggle.gameObject.SetActive(page=="travel");
        profileNameRow.SetActive(page=="travel");
        if(page=="travel")
        {
            Anchor(heading.rectTransform,0,1,.42f,1,24,-62,-18,-12);
            if(!profileNameInput.isFocused)profileNameInput.SetTextWithoutNotify(ShopProgress.NormalizePlayerName(progress.Data.playerName));
            profileNameInput.interactable=!progress.ReadOnly;
        }
        bool fishIndex=page=="reef-fish";
        heading.gameObject.SetActive(!fishIndex);wallet.gameObject.SetActive(!fishIndex);
        feedback.gameObject.SetActive(!fishIndex);scroll.gameObject.SetActive(!fishIndex);
        scroll.verticalScrollbar.gameObject.SetActive(!fishIndex);
        foreach(var tab in navigationTabs)tab.SetActive(shopSession==null && !independent);
        if(page=="stats")
        {
            wallet.gameObject.SetActive(false);feedback.gameObject.SetActive(false);
            scroll.gameObject.SetActive(false);scroll.verticalScrollbar.gameObject.SetActive(false);
            personalStatsView=PersonalStatsView.Build(modal.transform,progress.Data.angler,progress);
            FishingHudTheme.Panel(menuClose.gameObject);
        }
        else if(page=="travel") TravelPage(); else if(page=="bag") BagPage(); else if(page=="equipment") EquipmentPage(false);
        else if(page=="gear") EquipmentPage(true); else if(page=="market") MarketPage(); else if(page=="sell-confirm") SellConfirmation();
        else if(page=="bait") BaitPage(false);
        else if(page=="multiplayer") FishingMultiplayerPanel.Build(list,player);
        else if(page=="islands") IslandIndex(); else if(page=="reef-fish") ReefFishIndex();
        else if(habitatAddId!=null) HabitatFishPicker(habitatAddId);
        else HabitatPage(page);
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition=top?1f:position;
    }
    // Layout only. Keep the plain heading text used by existing shop companions.
    private void SetStorePresentation(bool tackle)
    {
        bool market=page=="market" || page=="sell-confirm" || page=="bait";
        bool themed=tackle || market;
        storeHeader.SetActive(themed);storeBody.SetActive(themed);
        // The item surface reaches the frame bottom, with equal 20-unit list insets.
        Anchor(storeBody.GetComponent<RectTransform>(),0,0,1,1,0,0,0,tackle?-258:-158);
        modal.GetComponent<Image>().color=themed?Color.clear:ink;
        var r=modal.GetComponent<RectTransform>();
        bool index=page=="islands" || page=="reef-fish";
        bool sharedFrame=themed || index || page=="stats";
        // Identical size and true screen center for both themed menus.
        r.anchorMin=sharedFrame?new Vector2(.025f,.0275f):new Vector2(.045f,.045f);
        r.anchorMax=sharedFrame?new Vector2(.975f,.9725f):new Vector2(.955f,.955f);
        r.offsetMin=r.offsetMax=Vector2.zero;
        heading.fontSize=themed?52:43;heading.resizeTextMaxSize=themed?52:43;
        heading.fontStyle=themed?FontStyle.Bold:FontStyle.Normal;heading.color=themed?Color.white:gold;
        Anchor(heading.rectTransform,0,1,1,1,themed?132:24,themed?-82:-62,themed?-270:-150,-12);
        wallet.color=themed?new Color(.67f,.93f,1f):Color.white;
        Anchor(wallet.rectTransform,0,1,1,1,themed?132:24,themed?-118:-92,themed?-270:-24,themed?-80:-62);
        Rect(menuClose.GetComponent<RectTransform>(),Vector2.one,Vector2.one,Vector2.one,
            themed?new Vector2(-20,-22):new Vector2(-18,-16),themed?new Vector2(230,84):new Vector2(160,64));
        var closeImage=menuClose.GetComponent<Image>();
        if(themed)FishingHudTheme.Panel(menuClose.gameObject);
        else {closeImage.sprite=null;closeImage.type=Image.Type.Simple;closeImage.color=teal;}
        Anchor(scroll.viewport,0,0,1,1,themed?22:20,themed?20:70,themed?-44:-30,tackle?-278:market?-178:-164);
        var track=scroll.verticalScrollbar.GetComponent<RectTransform>();
        Anchor(track,1,0,1,1,themed?-27:-22,themed?20:70,themed?-13:-10,tackle?-278:market?-178:-164);
        var handle=scroll.verticalScrollbar.targetGraphic as Image;
        if(handle!=null)handle.color=themed?FishingHudTheme.Cyan:gold;
        feedback.color=themed?new Color(.67f,.93f,1f):gold;
        // Keep purchase/sale messages clear of the newly extended item list.
        if(themed)Anchor(feedback.rectTransform,0,1,1,1,24,-150,-24,-130);
        else Anchor(feedback.rectTransform,0,0,1,0,24,10,-24,60);
        if(index)
        {
            // Reserve a header band and a separate right-hand close-button column.
            heading.fontSize=43;heading.resizeTextMaxSize=43;
            Anchor(heading.rectTransform,0,1,1,1,24,-82,-270,-16);
            Anchor(wallet.rectTransform,0,1,1,1,24,-118,-24,-84);
            Rect(menuClose.GetComponent<RectTransform>(),Vector2.one,Vector2.one,Vector2.one,
                new Vector2(-20,-22),new Vector2(230,64));
            // Cards, previews and actions resize together inside the shared frame.
            Anchor(scroll.viewport,0,0,1,1,22,78,-44,-140);
            Anchor(track,1,0,1,1,-27,80,-13,-140);
        }
    }

    private void TravelPage()
    {
        string[] names={"SUNCREST REEF","TIDEGLASS QUAY","HOME","BRINEBREAK ISLE"};
        string[] details={"Beginner island • beach • shallow reef","View habitats • buy upgrades • sell your catch","Your garden • aquarium • pond • fish collection","Rocky island • deeper crossing • restless waters"};
        Color[] colors={new Color(.06f,.30f,.39f),new Color(.29f,.23f,.13f),new Color(.12f,.30f,.22f),new Color(.24f,.28f,.32f)};
        for(int row=0;row<2;row++)
        {
            var cards=Panel("Destinations",list,Color.clear);
            cards.AddComponent<LayoutElement>().preferredHeight=280;
            for(int column=0;column<2;column++)
            {
                int destination=row*2+column;
                bool unlocked=destination!=3 || ReefCatalog.BrinebreakDiscovered;
                bool available=unlocked && (destination!=3 || (IslandExpansionWorld.Active!=null && IslandExpansionWorld.Active.Ready));
                int biome=IslandExpansionWorld.FishingBiome(player.transform.position);
                bool here=destination==3?travel.Destination==0 && biome==1:travel.Destination==destination && (destination!=0 || biome==0);
                bool tournamentAllowed=destination==3?travel.TournamentAllowsIsland(1):
                    destination==0?travel.TournamentAllowsIsland(0):travel.TournamentAllowsDimension(destination);
                string footer=!tournamentAllowed?"LOCKED DURING TOURNAMENT":
                    !unlocked?"LOCKED — discover by landing":here?"YOU ARE HERE • TRAVEL →":"TRAVEL →";
                var button=ButtonAt(cards.transform,names[destination]+"\n\n"+details[destination]+"\n\n"+footer,()=>
                {
                    if(destination==3)travel.TravelIsland(true);
                    else if(destination==0)travel.TravelIsland(false);
                    else travel.Travel(destination);
                });
                Anchor(button.GetComponent<RectTransform>(),column*.5f,0,(column+1)*.5f,1,8,8,-8,-8);
                var caption=button.GetComponentInChildren<Text>();caption.rectTransform.offsetMin=new Vector2(16,16);caption.rectTransform.offsetMax=new Vector2(-16,-16);
                caption.fontSize=34;caption.resizeTextMaxSize=34;button.GetComponent<Image>().color=colors[destination];
                SetAvailability(button,tournamentAllowed && available && (destination==0 || destination==3 || !here));
            }
        }
    }
    public bool SelectIndexBiome(int biome)
    {
        if(biome<0 || biome>=ReefCatalog.Zones.Length || !ReefCatalog.Zones[biome].Unlocked)return false;
        indexBiome=biome;dirty=true;return true;
    }
    private static void SetAvailability(Button button,bool enabled)
    {
        button.interactable=enabled;
        if(enabled)return;
        button.GetComponent<Image>().color=new Color(.23f,.23f,.23f,1);
        foreach(var label in button.GetComponentsInChildren<Text>())label.color=new Color(.60f,.60f,.60f,1);
        foreach(var picture in button.GetComponentsInChildren<RawImage>())picture.color=new Color(.38f,.38f,.38f,1);
    }
    private void IslandIndex()
    {
        foreach(int zoneIndex in ReefCatalog.IslandIndexOrder)
        {
            int selectedBiome=zoneIndex;var entry=ReefCatalog.Zones[zoneIndex];
            var card=ButtonAt(list,entry.name+(entry.Unlocked?"\nUNLOCKED":"\nLOCKED")+" • FISHING AREA\n\n"+entry.description+"\n\nOPEN FISH INDEX →",()=>{if(SelectIndexBiome(selectedBiome))Open("reef-fish");});
            card.gameObject.AddComponent<LayoutElement>().preferredHeight=290;
            card.gameObject.AddComponent<BiomeIndexLink>().Biome=selectedBiome;
            var label=card.GetComponentInChildren<Text>();label.alignment=TextAnchor.MiddleLeft;
            Anchor(label.rectTransform,.29f,0,1,1,20,18,-22,-18);
            var picture=new GameObject("Island preview",typeof(RectTransform),typeof(RawImage));picture.transform.SetParent(card.transform,false);
            Anchor(picture.GetComponent<RectTransform>(),0,0,.29f,1,18,20,0,-20);
            picture.GetComponent<RawImage>().texture=BiomeThumbnail(selectedBiome);picture.GetComponent<RawImage>().raycastTarget=false;
            IslandIndexArtwork.Round(picture.GetComponent<RawImage>());
            SetAvailability(card,entry.Unlocked);
        }


    }
    private void ReefFishIndex()
    {
        previews.Suspend();
        if(fishIndexView==null)
        {
            var view=new GameObject("FishIndexView",typeof(RectTransform),typeof(FishIndexView));
            view.transform.SetParent(modal.transform,false);
            Anchor(view.GetComponent<RectTransform>(),0,0,1,1,22,22,-22,-18);
            fishIndexView=view.GetComponent<FishIndexView>();
        }
        fishIndexView.gameObject.SetActive(true);
        fishIndexView.Show(progress,indexBiome,BiomeThumbnail(indexBiome),()=>
        {
            var safety=GetComponent<FishingMenuSafetyRuntime>();
            if(safety!=null && !MenuOpen)safety.OpenIndexPage("islands");
            else Open("islands");
        });
        // CLOSE stays a real existing button so the non-pausing fishing-menu
        // safety path can keep its own close listener.
        menuClose.transform.SetAsLastSibling();
        Rect(menuClose.GetComponent<RectTransform>(),Vector2.one,Vector2.one,Vector2.one,
            new Vector2(-22,-18),new Vector2(170,54));
        FishingHudTheme.Panel(menuClose.gameObject);
    }
    private Texture2D bluewaterThumbnail,snapperThumbnail;
    private Texture2D BiomeThumbnail(int biome)
    {
        var artwork = IslandIndexArtwork.Load(biome);
        if (artwork != null) return artwork;
        if(biome==0)return IslandThumbnail();
        if(biome==4 && snapperThumbnail!=null)return snapperThumbnail;
        if(biome==3 && bluewaterThumbnail!=null)return bluewaterThumbnail;
        if(biome==1 && rockyThumbnail!=null)return rockyThumbnail;
        if(biome==2 && deepThumbnail!=null)return deepThumbnail;
        var texture=new Texture2D(192,160,TextureFormat.RGBA32,false);var pixels=new Color[192*160];
        for(int y=0;y<160;y++)for(int x=0;x<192;x++)
        {
            float u=(x-96)/78f,v=(y-80)/48f;float q=u*u+v*v;
            Color c=new Color(.035f,.16f,.26f);
            if(biome==4 && q<.83f)c=q>.64f?new Color(.88f,.79f,.56f):Color.Lerp(new Color(.22f,.40f,.22f),new Color(.38f,.53f,.28f),Mathf.PerlinNoise(x*.05f,y*.05f));
            else if(biome==3 && q<.65f)c=q>.43f?new Color(.83f,.77f,.55f):new Color(.23f,.40f,.25f);
            else if(biome==1 && q<1)c=q>.80f?new Color(.65f,.57f,.39f):Color.Lerp(new Color(.22f,.26f,.24f),new Color(.48f,.47f,.4f),Mathf.PerlinNoise(x*.08f,y*.08f));
            else c*=.82f+.18f*Mathf.Sin(y*.30f+x*.05f);
            pixels[y*192+x]=c;
        }
        texture.SetPixels(pixels);texture.Apply(false,true);if(biome==4)snapperThumbnail=texture;else if(biome==1)rockyThumbnail=texture;else if(biome==3)bluewaterThumbnail=texture;else deepThumbnail=texture;return texture;
    }
    private Texture2D IslandThumbnail()
    {
        if(islandThumbnail!=null)return islandThumbnail;
        const int w=192,h=160;islandThumbnail=new Texture2D(w,h,TextureFormat.RGBA32,false);
        var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float u=(x-w*.5f)/(w*.40f),v=(y-h*.5f)/(h*.38f);
            float radius=u*u+v*v+.07f*Mathf.Sin(v*14)*Mathf.Cos(u*9);
            Color c=Color.Lerp(new Color(.025f,.28f,.39f),new Color(.13f,.65f,.65f),Mathf.Clamp01(1.5f-radius));
            if(radius<1)c=new Color(.87f,.77f,.49f);
            if(radius<.64f)c=new Color(.29f,.48f,.19f);
            if(u*u*3+(v+.04f)*(v+.04f)*7<.21f)c=new Color(.12f,.57f,.57f);
            if(radius>.70f && radius<1.35f)c*=.95f+.05f*Mathf.Sin(radius*75);
            if(radius<.60f && radius>.27f && Mathf.Sin(x*.30f)*Mathf.Cos(y*.28f)>.80f)c=new Color(.12f,.32f,.16f);
            pixels[y*w+x]=c;
        }
        islandThumbnail.SetPixels(pixels);islandThumbnail.Apply(false,true);return islandThumbnail;
    }
    private void BagPage()
    {
        var species=progress.Data.bag.Select(f=>f.speciesId).Distinct().OrderBy(id=>FishCatalog.Get(id).Name).ToList();
        if(speciesFilter!=-1 && !species.Contains(speciesFilter))speciesFilter=-1;
        var controls=Panel("Sort and filter",list,Color.clear);controls.AddComponent<LayoutElement>().preferredHeight=64;
        var sort=ButtonAt(controls.transform,"SORT: "+SortNames[bagSort],()=>{bagPicker=bagPicker=="sort"?null:"sort";Refresh(true);});
        Anchor(sort.GetComponent<RectTransform>(),0,0,0.5f,1,0,0,-6,0);
        var filter=ButtonAt(controls.transform,"SPECIES: "+(speciesFilter<0?"All":FishCatalog.Get(speciesFilter).Name),()=>{bagPicker=bagPicker=="species"?null:"species";Refresh(true);});
        Anchor(filter.GetComponent<RectTransform>(),0.5f,0,1,1,6,0,0,0);
        if(bagPicker=="sort")
        {
            for(int i=0;i<SortNames.Length;i++){int choice=i;Row(SortNames[i],"Choose how catches are ordered.",bagSort==i?"SELECTED":"SELECT",()=>{bagSort=choice;bagPicker=null;Refresh(true);});}
            return;
        }
        if(bagPicker=="species")
        {
            Row("All species","Show every catch.","SELECT",()=>{speciesFilter=-1;bagPicker=null;Refresh(true);});
            foreach(int id in species){int choice=id;Row(FishCatalog.Get(id).Name,$"{progress.Data.bag.Count(f=>f.speciesId==id)} in bag","SELECT",()=>{speciesFilter=choice;bagPicker=null;Refresh(true);});}
            return;
        }
        if(progress.Data.bag.Count==0) {Row("No catches yet","Your infinite worms are always available. Travel to the island and cast.","TRAVEL",()=>Open("travel"));return;}
        IEnumerable<CaughtFishRecord> fish=progress.Data.bag.Where(f=>speciesFilter<0 || f.speciesId==speciesFilter);
        switch(bagSort)
        {
            case 1:fish=fish.OrderByDescending(f=>f.weightKg).ThenByDescending(f=>f.caughtUtcTicks);break;
            case 2:fish=fish.OrderBy(f=>f.weightKg).ThenByDescending(f=>f.caughtUtcTicks);break;
            case 3:fish=fish.OrderByDescending(f=>FishCatalog.GetSellValue(f.speciesId,f.weightKg));break;
            case 4:fish=fish.OrderBy(f=>FishCatalog.Get(f.speciesId).Name).ThenByDescending(f=>f.weightKg);break;
            default:fish=fish.OrderByDescending(f=>f.caughtUtcTicks);break;
        }
        foreach(var record in fish)
        {
            var f=record;Row(FishCatalog.Get(f.speciesId).Name,$"{FishCatalog.FormatWeight(f.weightKg)} / {ShopCatalog.FishLength(f.speciesId,f.weightKg):0.00} m / value {FishCatalog.GetSellValue(f.speciesId,f.weightKg)} coins",fishing.IsHolding(f)?"PUT AWAY":"HOLD",()=>{Close();int index=progress.Data.bag.IndexOf(f);if(index>=0)fishing.HoldFish(index);},fish:f);
        }
    }
    private void EquipmentPage(bool shop)
    {
        if(shop && !travel.Near("gear")) { Row("Visit the tackle counter","Return to the counter to make purchases.","CLOSE",Close); return; }
        foreach(GearKind kind in Enum.GetValues(typeof(GearKind)))
        {
            for(int tier=0;tier<4;tier++)
            {
                int t=tier; GearKind k=kind; bool owned=t<=progress.Data.Owned(k), equipped=t==progress.Data.Equipped(k);
                if((!shop && !owned) || (shop && owned)) continue;
                string stats=k==GearKind.Rod?$"{FishingBurstDamageRuntime.NormalMinimumForTier(t)}–{FishingBurstDamageRuntime.NormalMaximumForTier(t)} damage / {FishingBurstDamageRuntime.CriticalChanceForTier(t)*100:0}% critical (2×)":k==GearKind.Reel?$"{(Level2FishingReelRuntime.ReelMultiplier(t)-1f)*100:0}% faster reeling":$"40 m line / 30 m maximum cast; {t*12}% more line tolerance";
                string action=equipped?"EQUIPPED":owned?"EQUIP":$"BUY {ShopCatalog.GearPrice(k,t):N0}";
                bool can=owned?!equipped:t==progress.Data.Owned(k)+1 && progress.Data.coins>=ShopCatalog.GearPrice(k,t);
                Row(ShopCatalog.GearName(k,t),stats+(owned?"":" / Requires previous tier"),action,()=>
                {
                    bool result=owned?(!progress.ReadOnly && progress.Commit(progress.Data.Equip(k,t))):progress.BuyGear(k,t);
                    Result(result,owned?"Equipment updated.":"Purchased and equipped.");
                },can && !progress.ReadOnly,gear:k.ToString());
            }
        }
        if(shop)BaitPage(true);
    }
    private void RefreshBaitShortcut()
    {
        if(baitShortcut==null || progress==null)return;
        int id=progress.Data.baitEquipped;if(id==1)id=0;
        int quantity=id==ShopCatalog.StarterLure?progress.Data.LureQuantity(progress.Data.lureEquipped):ShopCatalog.PermanentBait(id)?-1:progress.Data.bait[id];
        if(shownBait!=id)
        {
            shownBait=id;shownBaitQuantity=int.MinValue;
            baitName.text=ShopCatalog.BaitNames[id].ToUpperInvariant();
            baitPreview.Attach(baitPicture,0,1,"Bait"+id);
        }
        if(shownBaitQuantity!=quantity){shownBaitQuantity=quantity;baitQuantity.text=quantity<0?"∞":quantity.ToString();}
    }
    private void BaitPage(bool shop)
    {
        for(int i=0;i<ShopCatalog.BaitNames.Length;i++)
        {
            if(i==1 || i==ShopCatalog.StarterLure)continue; // Lure variants have their own quantity-aware rows.
            int id=i;if(!shop && !ShopCatalog.PermanentBait(id) && progress.Data.bait[id]<=0)continue;
            string effect=id==ShopCatalog.StarterLure?"Lost if a hooked fish escapes. Rare-fish focus. Hold REEL for bites; longer casts favor larger fish.":id==0?"Infinite worms. Standard catch chances.":id==1?"35% shorter wait; one worm per cast.":id==2?"20% shorter wait; favors snapper and goatfish.":"Favors yellowtail and tuna; one squid per cast.";
            string count=ShopCatalog.PermanentBait(id)?"Unlimited":$"{progress.Data.bait[id]} remaining";
            if(shop && !ShopCatalog.PermanentBait(id)) Row(ShopCatalog.BaitNames[id]+" / PACK OF 10",effect+" "+count,$"BUY {ShopCatalog.BaitPrices[id]}",()=>Result(progress.BuyBait(id),"Bait purchased and selected."),progress.Data.coins>=ShopCatalog.BaitPrices[id] && !progress.ReadOnly,gear:"Bait"+id);
            if(!shop)Row(ShopCatalog.BaitNames[id],count+" / "+effect,progress.Data.baitEquipped==id?"SELECTED":"SELECT",()=>
            {
                if(progress.ReadOnly)return;
                progress.Data.baitEquipped=id;
                progress.Save();
                message="";
                shownBait=-1;
                Refresh(false);
            },progress.Data.baitEquipped!=id && (ShopCatalog.PermanentBait(id) || progress.Data.bait[id]>0) && !progress.ReadOnly,gear:"Bait"+id);
        }
    }
    private void MarketPage()
    {
        if(!travel.Near("market")) { Row("Visit the fish market","Return to the market counter to sell fish.","CLOSE",Close); return; }
        if(progress.Data.bag.Count==0) { Row("Your bag is empty","Bring your next catch to the market.","CLOSE",Close); return; }
        Row("Sell bag contents","Review the total before selling all fish in the bag.","REVIEW ALL",()=>Open("sell-confirm"));
        foreach(var fish in new List<CaughtFishRecord>(progress.Data.bag))
        {
            var f=fish; int value=FishCatalog.GetSellValue(f.speciesId,f.weightKg);
            Row(FishCatalog.Get(f.speciesId).Name,$"{FishCatalog.FormatWeight(f.weightKg)} / {ShopCatalog.FishLength(f.speciesId,f.weightKg):0.00} m",$"SELL {value}",()=>{bool sold=progress.Sell(f);if(sold)PlaySaleSound();Result(sold,$"Sold for {value} coins.");},!progress.ReadOnly,fish:f);
        }
    }
    private void SellConfirmation()
    {
        long total=0; foreach(var f in progress.Data.bag) total+=FishCatalog.GetSellValue(f.speciesId,f.weightKg);
        Row($"Sell {progress.Data.bag.Count} fish for {total:N0} coins?","This empties your fish bag. Habitat residents stay where they are.","CONFIRM SALE",()=>
        {
            if(!travel.Near("market") || progress.ReadOnly) return;
            bool soldAny=false;
            foreach(var f in new List<CaughtFishRecord>(progress.Data.bag))
                soldAny |= progress.Data.Sell(f,FishCatalog.GetSellValue(f.speciesId,f.weightKg));
            progress.Save(); if(soldAny)PlaySaleSound(); message="Fish sold."; Open("market");
        },!progress.ReadOnly && total<=int.MaxValue-progress.Data.coins);
        Row("Keep your catches","Return to individual fish sales.","BACK",()=>Open("market"));
    }
    private void HabitatPage(string id)
    {
        var d=ShopCatalog.Habitat(id); if(d==null) {TravelPage();return;}
        if(!travel.Near(id)) { Row(d.name,"Approach its sign to view this habitat.","TRAVEL",()=>Open("travel"));return; }
        var owned=progress.Data.Habitat(id);
        string capacity=$"{d.width} × {d.depth} × {d.height} m / {d.fishLimit} fish / max {d.maxFishKg} kg each / {d.totalKg} kg total";
        if(travel.InShop)
        {
            int cost=progress.Data.UpgradeCost(id);
            var current=progress.Data.CurrentHabitat(d.pond);
            string detail=owned!=null?"This habitat is installed at Home.":cost>=0?"Upgrades your Home in place. Residents stay with their habitat.":"Purchase the preceding size first. You own one aquarium and one pond.";
            Row(d.name,capacity+"\n"+detail,owned!=null?"OWNED":cost>=0?$"UPGRADE {cost:N0}":"LOCKED",()=>Result(progress.BuyHabitat(id),"Your Home habitat has been upgraded."),cost>=0 && progress.Data.coins>=cost && !progress.ReadOnly);
            return;
        }
        if(owned==null || !travel.InHome)return;
        float kg=0;foreach(var f in owned.fish)kg+=f.weightKg;
        Row(d.name,$"{owned.fish.Count}/{d.fishLimit} fish • {kg:0.00}/{d.totalKg} kg\n"+capacity,"HOME",()=>{},false);
        Row("ADD FISH TO "+d.name.ToUpperInvariant(),$"{progress.Data.bag.Count} fish in your bag. Open the dedicated fish picker to sort, filter and check which catches fit this habitat.",progress.Data.bag.Count>0?"SELECT FISH":"BAG EMPTY",()=>Open(HabitatAddPrefix+id),progress.Data.bag.Count>0 && !progress.ReadOnly);
        foreach(var fish in new List<CaughtFishRecord>(owned.fish))
        {var f=fish;Row(FishCatalog.Get(f.speciesId).Name,$"RESIDENT • {FishCatalog.FormatWeight(f.weightKg)}",progress.Data.BagFull?"BAG FULL":"TO BAG",()=>Result(progress.Withdraw(id,f),"Fish returned to bag."),!progress.ReadOnly && !progress.Data.BagFull,fish:f);}
    }
    private void HabitatFishPicker(string id)
    {
        var d=ShopCatalog.Habitat(id);
        if(d==null){Row("Habitat unavailable","This habitat could not be found.","CLOSE",Close);return;}
        if(!travel.InHome || !travel.Near(id))
        {Row(d.name,"Return to this habitat at Home to add fish.","BACK",()=>Open(id));return;}
        var owned=progress.Data.Habitat(id);
        if(owned==null)
        {Row(d.name,"Purchase this habitat before adding fish.","BACK",()=>Open(id));return;}

        Row("BACK TO "+d.name.ToUpperInvariant(),$"Residents: {owned.fish.Count}/{d.fishLimit}. Select a catch below to move it from your bag into this habitat.","BACK",()=>Open(id));

        var species=progress.Data.bag.Select(f=>f.speciesId).Distinct().OrderBy(speciesId=>FishCatalog.Get(speciesId).Name).ToList();
        if(habitatSpeciesFilter!=-1 && !species.Contains(habitatSpeciesFilter))habitatSpeciesFilter=-1;
        var controls=Panel("Habitat sort and filter",list,Color.clear);controls.AddComponent<LayoutElement>().preferredHeight=64;
        var sort=ButtonAt(controls.transform,"SORT: "+SortNames[habitatSort],()=>{habitatPicker=habitatPicker=="sort"?null:"sort";Refresh(true);});
        Anchor(sort.GetComponent<RectTransform>(),0,0,0.5f,1,0,0,-6,0);
        var filter=ButtonAt(controls.transform,"SPECIES: "+(habitatSpeciesFilter<0?"All":FishCatalog.Get(habitatSpeciesFilter).Name),()=>{habitatPicker=habitatPicker=="species"?null:"species";Refresh(true);});
        Anchor(filter.GetComponent<RectTransform>(),0.5f,0,1,1,6,0,0,0);

        if(habitatPicker=="sort")
        {
            for(int i=0;i<SortNames.Length;i++)
            {
                int choice=i;
                Row(SortNames[i],"Choose how catches are ordered in this habitat picker.",habitatSort==i?"SELECTED":"SELECT",()=>{habitatSort=choice;habitatPicker=null;Refresh(true);});
            }
            return;
        }
        if(habitatPicker=="species")
        {
            Row("All species","Show every catch in your bag.","SELECT",()=>{habitatSpeciesFilter=-1;habitatPicker=null;Refresh(true);});
            foreach(int speciesId in species)
            {
                int choice=speciesId;
                Row(FishCatalog.Get(speciesId).Name,$"{progress.Data.bag.Count(f=>f.speciesId==speciesId)} in bag","SELECT",()=>{habitatSpeciesFilter=choice;habitatPicker=null;Refresh(true);});
            }
            return;
        }

        if(progress.Data.bag.Count==0)
        {Row("No fish in your bag","Catch fish first, then return here to add them to this habitat.","BACK",()=>Open(id));return;}

        IEnumerable<CaughtFishRecord> fish=progress.Data.bag.Where(f=>habitatSpeciesFilter<0 || f.speciesId==habitatSpeciesFilter);
        switch(habitatSort)
        {
            case 1:fish=fish.OrderByDescending(f=>f.weightKg).ThenByDescending(f=>f.caughtUtcTicks);break;
            case 2:fish=fish.OrderBy(f=>f.weightKg).ThenByDescending(f=>f.caughtUtcTicks);break;
            case 3:fish=fish.OrderByDescending(f=>FishCatalog.GetSellValue(f.speciesId,f.weightKg));break;
            case 4:fish=fish.OrderBy(f=>FishCatalog.Get(f.speciesId).Name).ThenByDescending(f=>f.weightKg);break;
            default:fish=fish.OrderByDescending(f=>f.caughtUtcTicks);break;
        }

        foreach(var record in fish)
        {
            var f=record;
            string reason=progress.Data.Admission(id,f);
            string detail=$"{FishCatalog.FormatWeight(f.weightKg)} / {ShopCatalog.FishLength(f.speciesId,f.weightKg):0.00} m / "+(reason??"Fits this habitat");
            Row(FishCatalog.Get(f.speciesId).Name,detail,reason==null?"ADD FISH":"DOESN'T FIT",()=>Result(progress.Deposit(id,f),"Fish added to habitat."),reason==null && !progress.ReadOnly,fish:f);
        }
    }
    private void Result(bool ok,string success) { message=ok?success:"Action unavailable. Check coins, ownership, capacity and distance to the shop."; Refresh(false); }
    private void Row(string title,string detail,string action,Action callback,bool enabled=true,CaughtFishRecord fish=null,string gear=null)
    {
        bool market=page=="market" || page=="sell-confirm" || page=="bait";
        GameObject row=Panel("Item",list,new Color(0.07f,0.12f,0.14f,1));
        row.AddComponent<LayoutElement>().preferredHeight=page=="reef-fish"?340:market?200:184;
        var accent=Panel("Accent",row.transform,gold); Anchor(accent.GetComponent<RectTransform>(),0,0,0,1,0,0,4,0);
        var titleText=Label(row.transform,title,26,Color.white); Anchor(titleText.rectTransform,0,page=="reef-fish"?.72f:.5f,1,1,(fish!=null || gear!=null?170:22),0,-232,-10);
        var detailText=Label(row.transform,detail,21,new Color(0.65f,0.79f,0.8f)); Anchor(detailText.rectTransform,0,0,1,page=="reef-fish"?.75f:.55f,(fish!=null || gear!=null?170:22),10,-232,0);
        var b=ButtonAt(row.transform,action,callback); Rect(b.GetComponent<RectTransform>(),new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(-14,0),new Vector2(205,76));
        SetAvailability(b,enabled);
        if(market)
        {
            accent.SetActive(false);
            titleText.fontSize=34;titleText.resizeTextMaxSize=34;titleText.fontStyle=FontStyle.Bold;
            detailText.fontSize=25;detailText.resizeTextMaxSize=25;
            Anchor(titleText.rectTransform,0,.56f,1,1,(fish!=null || gear!=null)?216:24,0,-286,-18);
            Anchor(detailText.rectTransform,0,0,1,.53f,(fish!=null || gear!=null)?216:24,20,-286,0);
            Rect(b.GetComponent<RectTransform>(),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,0),new Vector2(238,90));
        }
        if(fish!=null || gear!=null)
        {
            var picture=new GameObject("3D item preview",typeof(RectTransform),typeof(RawImage));picture.transform.SetParent(row.transform,false);
            Rect(picture.GetComponent<RectTransform>(),new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(14,0),new Vector2(140,140));
            var image=picture.GetComponent<RawImage>();image.raycastTarget=false;
            if(market)
            {
                var frame=Panel("ItemImageFrame",row.transform,Color.white);
                FishingHudTheme.Panel(frame);frame.GetComponent<Image>().raycastTarget=false;
                Rect(frame.GetComponent<RectTransform>(),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(24,0),new Vector2(164,164));
                picture.transform.SetParent(frame.transform,false);
                Anchor(picture.GetComponent<RectTransform>(),0,0,1,1,10,10,-10,-10);
            }
            previews.Attach(image,fish!=null?fish.speciesId:0,fish!=null?fish.weightKg:1,gear,page=="reef-fish");
        }
    }
    private GameObject Panel(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false); go.GetComponent<Image>().color=color; return go;
    }
    private Text Label(Transform parent,string text,int size,Color color)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=font;t.text=text;size=Mathf.RoundToInt(size*1.2f);t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;t.supportRichText=false;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=22;t.resizeTextMaxSize=size; return t;
    }
    private static void StyleHudShortcut(GameObject shortcut,FishingHudSymbol.Kind symbol)
    {
        FishingHudSymbol.Add(shortcut.transform,symbol,new Vector2(0,.5f),new Vector2(49,0),56);
        var label=shortcut.GetComponentInChildren<Text>();
        label.fontSize=27;label.resizeTextMaxSize=27;label.fontStyle=FontStyle.Bold;
        label.alignment=TextAnchor.MiddleLeft;
        label.rectTransform.offsetMin=new Vector2(105,4);
        label.rectTransform.offsetMax=new Vector2(-18,-4);
    }

    private Button ButtonAt(Transform parent,string text,Action action)
    {
        var go=Panel(text,parent,teal);var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<Image>();b.transition=Selectable.Transition.ColorTint;b.onClick.AddListener(()=>action());
        ColorBlock colors=b.colors;colors.disabledColor=new Color(0.4f,0.4f,0.4f,0.45f);b.colors=colors;
        var label=Label(go.transform,text,23,Color.white);label.alignment=TextAnchor.MiddleCenter;Full(label.rectTransform);return b;
    }
    private static void Full(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
    private static void Rect(RectTransform r,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size) {r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=pos;}
    private static void Anchor(RectTransform r,float x0,float y0,float x1,float y1,float l,float b,float right,float top) { r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(right,top); }
}

