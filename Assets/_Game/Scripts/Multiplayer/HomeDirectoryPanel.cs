using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Public Homes are the primary content of this page. Search, sort and the
// player's own Home settings open as temporary overlays instead of taking up
// permanent space above the aquarium listings.
public sealed class HomeDirectoryPanel : MonoBehaviour
{
    private enum PopupMode { None, Settings, Search, Sort }
    private HomeDirectoryClient directory;
    private Font font;
    private ScrollRect scroll;
    private Transform overlayParent;
    private RectTransform results;
    private Text status, settingsCode, settingsVisibility;
    private Button settingsButton,sortButton;
    private InputField searchField;
    private GameObject popupRoot, popupBackdrop, emptyNotice;
    private Transform popupContent;
    private PopupMode popupMode;
    private readonly Text[] sortChoiceLabels=new Text[3];
    private static readonly string[] SortKeys={"recent","fish","biggest"};
    private static readonly string[] SortLabels={"MOST RECENT","MOST FISH","BIGGEST DISPLAYED FISH"};
    private string sort="recent",pendingSort="recent",searchQuery="";
    private int renderedCount;
    private readonly Color foreground=Color.white;

    public static void Build(RectTransform parent,FirstPersonController player)
    {
        var go=new GameObject("Public Home Directory",typeof(RectTransform),
            typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        go.transform.SetParent(parent,false);
        var panel=go.AddComponent<HomeDirectoryPanel>();
        panel.directory=HomeDirectoryClient.Instance;
        panel.scroll=parent.GetComponentInParent<ScrollRect>();
        // Overlay the entire HUD canvas (not just its inset content frame)
        // so tapping ANYWHERE outside the popup dismisses it.
        panel.overlayParent=panel.scroll!=null && panel.scroll.transform.parent!=null
            ?panel.scroll.transform.parent.parent:parent;
        panel.BuildUi();
    }

    private void BuildUi()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var group=GetComponent<VerticalLayoutGroup>();
        group.padding=new RectOffset(12,12,10,12);
        group.spacing=12;
        group.childControlHeight=true;group.childControlWidth=true;
        group.childForceExpandHeight=false;group.childForceExpandWidth=true;
        GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;

        // The shared HUD already displays PLAYER AQUARIUMS and the top-right BACK.
        settingsButton=ButtonAt(transform,"AQUARIUM SETTINGS: PUBLIC",()=>OpenPopup(PopupMode.Settings));

        var controls=new GameObject("Search and Sort Controls",
            typeof(RectTransform),typeof(LayoutElement));
        controls.transform.SetParent(transform,false);
        controls.GetComponent<LayoutElement>().preferredHeight=74;
        var search=ButtonAt(controls.transform,"SEARCH HOMES",()=>OpenPopup(PopupMode.Search));
        HalfWidth(search.GetComponent<RectTransform>(),0f,.5f);
        sortButton=ButtonAt(controls.transform,"SORT HOMES: MOST RECENT",()=>OpenPopup(PopupMode.Sort));
        HalfWidth(sortButton.GetComponent<RectTransform>(),.5f,1f);
        // The longest current sort label should fit even on narrow displays.
        var sortCaption=sortButton.GetComponentInChildren<Text>();
        sortCaption.resizeTextForBestFit=true;
        sortCaption.resizeTextMinSize=14;
        sortCaption.resizeTextMaxSize=24;

        status=HeadingAt(transform,"",21);
        status.GetComponent<LayoutElement>().preferredHeight=42;
        status.gameObject.SetActive(false);

        var go=new GameObject("Public Home Entries",typeof(RectTransform),
            typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        go.transform.SetParent(transform,false);
        results=go.GetComponent<RectTransform>();
        var rows=go.GetComponent<VerticalLayoutGroup>();
        rows.spacing=12;rows.childControlHeight=true;rows.childControlWidth=true;
        rows.childForceExpandWidth=true;rows.childForceExpandHeight=false;
        go.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;

        if(directory!=null)
        {
            directory.Changed+=Refresh;
            directory.Browse("",0,sort);
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if(directory!=null)directory.Changed-=Refresh;
        ClosePopup();
    }

    private void LateUpdate()
    {
        // Request 20 more only when the player approaches the bottom. A busy
        // request cannot overlap the next one, and cached rows stay mounted.
        if(directory==null || scroll==null || popupMode!=PopupMode.None ||
           directory.Busy || !directory.MoreListings || !isActiveAndEnabled)return;
        if(scroll.verticalNormalizedPosition<=.12f)
            directory.Browse(searchQuery,directory.ListingPage+1,sort);
    }

    private void Query(string requested)
    {
        if(directory==null || directory.Busy)return;
        searchQuery=(requested??"").Trim();
        ResetResults();
        if(scroll!=null)scroll.verticalNormalizedPosition=1f;
        directory.Browse(searchQuery,0,sort);
    }

    private void ResetResults()
    {
        renderedCount=0;
        if(results==null)return;
        for(int i=results.childCount-1;i>=0;i--)
        {
            var child=results.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        emptyNotice=null;
    }

    private void Refresh()
    {
        if(directory==null || results==null)return;
        // Only show request progress or errors, not general sync metadata.
        string note=directory.Status??"";
        bool error=!directory.Busy &&
            (note.Contains("Could not") || note.Contains("Cannot contact") ||
             note.Contains("unavailable") || note.Contains("Home not found") ||
             note.Contains("This is your Home"));
        status.text=directory.Busy?"LOADING HOMES...":error?note:"";
        status.gameObject.SetActive(directory.Busy || error);
        UpdateSettingsLabels();
        UpdateMainButtonLabels();
        AppendResults();
    }

    private void AppendResults()
    {
        var homes=directory.Listings??new HomeDirectoryClient.HomeInfo[0];
        if(renderedCount>homes.Length)ResetResults();
        if(homes.Length>0 && emptyNotice!=null)
        {
            emptyNotice.SetActive(false);
            Destroy(emptyNotice);
            emptyNotice=null;
        }
        for(int i=renderedCount;i<homes.Length;i++)
        {
            var home=homes[i];
            // Additional guard for cached results fetched before local Home
            // registration completes. The backend also excludes before paging.
            if(home==null || !string.IsNullOrEmpty(directory.OwnerCode) &&
               string.Equals(home.code,directory.OwnerCode,StringComparison.OrdinalIgnoreCase))
                continue;
            MakeHomeCard(home);
        }
        renderedCount=homes.Length;
        if(homes.Length==0 && !directory.Busy && emptyNotice==null)
            emptyNotice=HeadingAt(results.transform,
                "No other players' public Homes found.",23).gameObject;
    }

    private void MakeHomeCard(HomeDirectoryClient.HomeInfo home)
    {
        var card=new GameObject("Home "+home.code,typeof(RectTransform),
            typeof(Image),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        card.transform.SetParent(results,false);
        FishingHudTheme.Panel(card);
        var group=card.GetComponent<VerticalLayoutGroup>();
        group.padding=new RectOffset(14,14,10,10);group.spacing=6;
        group.childControlWidth=true;group.childControlHeight=true;
        group.childForceExpandHeight=false;group.childForceExpandWidth=true;
        card.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        HeadingAt(card.transform,home.name.ToUpperInvariant()+" • "+home.code,28);
        string biggest="None";
        if(home.biggest!=null)
        {
            string fishName="Fish";
            try{fishName=FishCatalog.Get(home.biggest.speciesId).Name;}catch{}
            biggest=fishName+" ("+home.biggest.weightKg.ToString("0.00")+" kg)";
        }
        HeadingAt(card.transform,home.aquariums+" aquariums • "+
            home.fish+" fish • Biggest: "+biggest,22);
        string date=home.updatedAt>0?DateTimeOffset.FromUnixTimeMilliseconds(home.updatedAt)
            .ToLocalTime().ToString("yyyy-MM-dd HH:mm"):"Not yet updated";
        HeadingAt(card.transform,"Last updated: "+date,20);
        string code=home.code;
        ButtonAt(card.transform,"VISIT HOME",()=>directory.Visit(code));
    }

    private void OpenPopup(PopupMode kind)
    {
        if(overlayParent==null)return;
        ClosePopup();
        popupMode=kind;

        // A full-screen click catcher dismisses the popup when the user taps
        // outside it, without triggering any buttons on the underlying menu.
        popupBackdrop=new GameObject("Aquarium Popup Dismiss Backdrop",
            typeof(RectTransform),typeof(Image),typeof(Button));
        popupBackdrop.transform.SetParent(overlayParent,false);
        var backdropRect=popupBackdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin=Vector2.zero;
        backdropRect.anchorMax=Vector2.one;
        backdropRect.offsetMin=backdropRect.offsetMax=Vector2.zero;
        var dim=popupBackdrop.GetComponent<Image>();
        dim.color=new Color(0f,.012f,.025f,.48f);
        var dismiss=popupBackdrop.GetComponent<Button>();
        dismiss.targetGraphic=dim;
        dismiss.onClick.AddListener(ClosePopup);

        popupRoot=new GameObject("Aquarium "+kind+" Popup",
            typeof(RectTransform),typeof(Image),typeof(Button));
        popupRoot.transform.SetParent(popupBackdrop.transform,false);
        var rect=popupRoot.GetComponent<RectTransform>();
        rect.anchorMin=new Vector2(.17f,.14f);
        rect.anchorMax=new Vector2(.83f,.87f);
        rect.offsetMin=rect.offsetMax=Vector2.zero;

        // The solid backing makes this window FULLY OPAQUE. The transparent
        // Tideglass gradient graphic is only an overlay on the solid backing.
        var background=popupRoot.GetComponent<Image>();
        background.sprite=null;
        background.color=new Color(.012f,.08f,.125f,1f);
        var absorb=popupRoot.GetComponent<Button>();
        absorb.targetGraphic=background;
        absorb.onClick.AddListener(()=>{}); // Tapping inside must not dismiss it.
        var glass=new GameObject("Opaque Panel Gradient",typeof(RectTransform),
            typeof(Image));
        glass.transform.SetParent(popupRoot.transform,false);
        var glassRect=glass.GetComponent<RectTransform>();
        glassRect.anchorMin=Vector2.zero;
        glassRect.anchorMax=Vector2.one;
        glassRect.offsetMin=glassRect.offsetMax=Vector2.zero;
        FishingHudTheme.Panel(glass);
        glass.GetComponent<Image>().raycastTarget=false;

        // Keep Home settings and search/sort choices scrollable on Android.
        // Reserve a top-right close slot for Settings/Search, or a bottom
        // action bar for the Sort picker.
        var viewport=new GameObject("Popup Scroll View",typeof(RectTransform),
            typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
        viewport.transform.SetParent(popupRoot.transform,false);
        var vr=viewport.GetComponent<RectTransform>();
        vr.anchorMin=Vector2.zero;
        vr.anchorMax=Vector2.one;
        vr.offsetMin=new Vector2(18,kind==PopupMode.Sort?105:15);
        vr.offsetMax=new Vector2(-18,kind==PopupMode.Sort?-15:-88);
        var viewportImage=viewport.GetComponent<Image>();
        viewportImage.color=Color.clear;
        viewportImage.raycastTarget=true;
        var content=new GameObject("Popup Rows",typeof(RectTransform),
            typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform,false);
        popupContent=content.transform;
        var cr=content.GetComponent<RectTransform>();
        cr.anchorMin=new Vector2(0,1);
        cr.anchorMax=Vector2.one;
        cr.pivot=new Vector2(.5f,1);
        cr.sizeDelta=Vector2.zero;
        var group=content.GetComponent<VerticalLayoutGroup>();
        group.padding=new RectOffset(6,6,6,6);
        group.spacing=7;
        group.childControlHeight=true;
        group.childControlWidth=true;
        group.childForceExpandHeight=false;
        group.childForceExpandWidth=true;
        content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var popupScroll=viewport.GetComponent<ScrollRect>();
        popupScroll.viewport=vr;
        popupScroll.content=cr;
        popupScroll.horizontal=false;
        popupScroll.scrollSensitivity=38;
        popupScroll.movementType=ScrollRect.MovementType.Clamped;

        switch(kind)
        {
            case PopupMode.Settings: BuildSettingsPopup();break;
            case PopupMode.Search: BuildSearchPopup();break;
            case PopupMode.Sort: BuildSortPopup();break;
        }
        popupBackdrop.transform.SetAsLastSibling();
    }

    private void ClosePopup()
    {
        popupMode=PopupMode.None;
        popupContent=null;
        searchField=null;
        settingsCode=null;
        settingsVisibility=null;
        for(int i=0;i<sortChoiceLabels.Length;i++)sortChoiceLabels[i]=null;
        var backdrop=popupBackdrop;
        popupBackdrop=null;
        popupRoot=null;
        if(backdrop!=null)
        {
            backdrop.SetActive(false);
            Destroy(backdrop);
        }
    }

    private void TopRightClose()
    {
        var button=ButtonAt(popupRoot.transform,"CLOSE",ClosePopup,8);
        var rect=button.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=new Vector2(1f,1f);
        rect.pivot=new Vector2(1f,1f);
        rect.anchoredPosition=new Vector2(-17f,-15f);
        rect.sizeDelta=new Vector2(185f,64f);
    }

    private Button BottomAction(string caption,bool left,
        UnityEngine.Events.UnityAction action,int style)
    {
        var button=ButtonAt(popupRoot.transform,caption,action,style);
        var rect=button.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=new Vector2(left?0f:1f,0f);
        rect.pivot=new Vector2(left?0f:1f,0f);
        rect.anchoredPosition=new Vector2(left?24f:-24f,20f);
        rect.sizeDelta=new Vector2(230f,68f);
        return button;
    }

    private void BuildSettingsPopup()
    {
        var target=popupContent;
        HeadingAt(target,"AQUARIUM SETTINGS",32);
        HeadingAt(target,"MY HOME CODE",25);
        settingsCode=HeadingAt(target,"",28);
        ButtonAt(target,"COPY MY HOME CODE",()=>
        {
            if(directory!=null && !string.IsNullOrEmpty(directory.OwnerCode))
                GUIUtility.systemCopyBuffer=directory.DisplayHomeCode;
        });
        settingsVisibility=HeadingAt(target,"",21);
        ButtonAt(target,"PUBLIC • LISTED IN DIRECTORY",()=>directory?.SetVisibility("public"));
        ButtonAt(target,"PRIVATE ~ CODE ONLY",()=>directory?.SetVisibility("unlisted"));
        UpdateSettingsLabels();
        TopRightClose();
    }

    private string VisibilityLabel()
    {
        if(directory==null)return "PUBLIC";
        if(directory.Visibility=="unlisted")return "PRIVATE ~ CODE ONLY";
        if(directory.Visibility=="private")return "PRIVATE ~ HIDDEN"; // Legacy setting
        return "PUBLIC";
    }

    private void UpdateMainButtonLabels()
    {
        if(settingsButton!=null)
        {
            var caption=settingsButton.GetComponentInChildren<Text>();
            if(caption!=null)caption.text="AQUARIUM SETTINGS: "+VisibilityLabel();
        }
        if(sortButton!=null)
        {
            var caption=sortButton.GetComponentInChildren<Text>();
            int index=Array.IndexOf(SortKeys,sort);
            if(caption!=null)caption.text="SORT HOMES: "+
                SortLabels[index<0?0:index];
        }
    }

    private void UpdateSettingsLabels()
    {
        if(settingsCode!=null)
            settingsCode.text=directory!=null?directory.DisplayHomeCode:"Registering...";
        if(settingsVisibility!=null && directory!=null)
            settingsVisibility.text="VISIBILITY: "+VisibilityLabel()+
                " • Public is the default. Code-only Homes can be visited with their code.";
    }

    private void BuildSearchPopup()
    {
        var target=popupContent;
        HeadingAt(target,"SEARCH PLAYER HOMES",32);
        searchField=Field(target,"Player name or Home code",40);
        searchField.text=searchQuery;
        searchField.lineType=InputField.LineType.SingleLine;
        // Pressing Enter on a keyboard searches immediately; tapping ENTER /
        // SEARCH does the same on a touchscreen.
        searchField.onEndEdit.AddListener(value=>
        {
            var keyboard=Keyboard.current;
            if(popupMode==PopupMode.Search && keyboard!=null &&
               (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame))
                ConfirmSearch(value);
        });
        ButtonAt(target,"SEARCH",()=>ConfirmSearch(searchField!=null?searchField.text:""));
        // The same search field handles names and Home codes, including
        // code-only Homes. There is no separate Visit by Code action.
        var cancel=ButtonAt(popupRoot.transform,"CANCEL",ClosePopup,8);
        var rect=cancel.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=new Vector2(1f,1f);
        rect.pivot=new Vector2(1f,1f);
        rect.anchoredPosition=new Vector2(-17f,-15f);
        rect.sizeDelta=new Vector2(185f,64f);
        searchField.ActivateInputField();
    }

    private void ConfirmSearch(string value)
    {
        if(popupMode!=PopupMode.Search)return;
        if(directory!=null && directory.Busy)return;
        ClosePopup();
        Query(value);
    }

    private void BuildSortPopup()
    {
        var target=popupContent;
        pendingSort=sort;
        HeadingAt(target,"SORT PLAYER HOMES",32);
        for(int i=0;i<SortKeys.Length;i++)
        {
            int choice=i;
            var option=ButtonAt(target,"",()=>
            {
                pendingSort=SortKeys[choice];
                UpdateSortLabels();
            });
            sortChoiceLabels[i]=option.GetComponentInChildren<Text>();
        }
        UpdateSortLabels();
        BottomAction("APPLY SORT",true,()=>
        {
            if(directory==null || directory.Busy)return;
            sort=pendingSort;
            ClosePopup();
            UpdateMainButtonLabels();
            Query(searchQuery);
        },7);
        BottomAction("CANCEL",false,ClosePopup,8);
    }

    private void UpdateSortLabels()
    {
        for(int i=0;i<sortChoiceLabels.Length;i++)
            if(sortChoiceLabels[i]!=null)
                sortChoiceLabels[i].text=(pendingSort==SortKeys[i]?"✓  ":"")+
                    SortLabels[i];
    }

    private Text HeadingAt(Transform parent,string value,int size)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text),typeof(LayoutElement));
        go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();
        t.font=font;t.fontSize=size;t.color=foreground;
        t.text=value;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;
        t.verticalOverflow=VerticalWrapMode.Overflow;
        go.GetComponent<LayoutElement>().preferredHeight=size<=23?53:62;
        return t;
    }

    private InputField Field(Transform parent,string placeholder,int limit)
    {
        var go=new GameObject("Search Field",typeof(RectTransform),typeof(Image),
            typeof(InputField),typeof(LayoutElement));
        go.transform.SetParent(parent,false);
        FishingHudTheme.Panel(go);
        go.GetComponent<LayoutElement>().preferredHeight=66;
        var field=go.GetComponent<InputField>();
        field.targetGraphic=go.GetComponent<Image>();
        field.characterLimit=limit;
        var text=HeadingAt(go.transform,"",26);
        text.alignment=TextAnchor.MiddleLeft;
        FitChild(text.rectTransform);
        field.textComponent=text;
        var hint=HeadingAt(go.transform,placeholder,22);
        hint.color=new Color(.68f,.77f,.82f);
        FitChild(hint.rectTransform);
        field.placeholder=hint;
        return field;
    }

    private Button ButtonAt(Transform parent,string caption,UnityEngine.Events.UnityAction action,
        int style=1)
    {
        var go=new GameObject(caption,typeof(RectTransform),typeof(Image),
            typeof(Button),typeof(LayoutElement));
        go.transform.SetParent(parent,false);
        FishingHudTheme.Panel(go,style);
        go.GetComponent<LayoutElement>().preferredHeight=64;
        var button=go.GetComponent<Button>();
        button.targetGraphic=go.GetComponent<Image>();
        button.onClick.AddListener(action);
        var label=HeadingAt(go.transform,caption,24);
        FitChild(label.rectTransform);
        return button;
    }

    private static void FitChild(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(16,3);rect.offsetMax=new Vector2(-16,-3);
    }

    private static void HalfWidth(RectTransform rect,float min,float max)
    {
        rect.anchorMin=new Vector2(min,0);
        rect.anchorMax=new Vector2(max,1);
        rect.pivot=new Vector2(.5f,.5f);
        rect.offsetMin=new Vector2(7,2);
        rect.offsetMax=new Vector2(-7,-2);
    }
}
