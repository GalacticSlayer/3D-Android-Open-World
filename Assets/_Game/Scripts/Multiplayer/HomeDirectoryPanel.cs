using System;
using UnityEngine;
using UnityEngine.UI;

// Paginated public aquarium showcase in MENU / TRAVEL -> MULTIPLAYER.
// No room connection is required to browse or visit offline owners.
public sealed class HomeDirectoryPanel : MonoBehaviour
{
    private HomeDirectoryClient directory;
    private FirstPersonController player;
    private Font font;
    private InputField search,homeCode;
    private Text status,myCode,privacy;
    private RectTransform results;
    private string sort="recent";
    private int page;
    private readonly Color foreground=Color.white;
    public static void Build(RectTransform parent,FirstPersonController player)
    {
        var go=new GameObject("Public Home Directory",typeof(RectTransform),
            typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        go.transform.SetParent(parent,false);
        var panel=go.AddComponent<HomeDirectoryPanel>();
        panel.player=player;
        panel.directory=HomeDirectoryClient.Instance;
        panel.BuildUi();
    }
    private void BuildUi()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var group=GetComponent<VerticalLayoutGroup>();
        group.padding=new RectOffset(12,12,12,12);group.spacing=11;
        group.childControlHeight=true;group.childControlWidth=true;
        group.childForceExpandHeight=false;group.childForceExpandWidth=true;
        GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        Heading("PLAYER AQUARIUMS",38);
        Heading("Browse public Homes, including offline players. Aquarium visits are read-only.",22);
        status=Heading("",23);
        Button("BACK TO MULTIPLAYER",()=>Back());
        Heading("MY HOME CODE",26);
        myCode=Heading(directory!=null?directory.DisplayHomeCode:"Offline",30);
        Button("COPY MY HOME CODE",()=>{if(directory!=null&&directory.OwnerCode!=null)
            GUIUtility.systemCopyBuffer=directory.DisplayHomeCode;});
        privacy=Heading("",23);
        Button("PUBLIC • LISTED IN DIRECTORY",()=>directory?.SetVisibility("public"));
        Button("UNLISTED • CODE ONLY",()=>directory?.SetVisibility("unlisted"));
        Button("PRIVATE • ONLY ME",()=>directory?.SetVisibility("private"));
        Heading("FIND A HOME",28);
        homeCode=Field("Enter an 8-character Home code",12);
        Button("VISIT HOME BY CODE",()=>{if(directory!=null)directory.Visit(homeCode.text);});
        Heading("BROWSE PUBLIC HOMES",29);
        search=Field("Search player name or Home code",40);
        Button("SEARCH HOMES",()=>Query(0));
        Button("MOST RECENT",()=>SortBy("recent"));
        Button("MOST FISH",()=>SortBy("fish"));
        Button("BIGGEST DISPLAYED FISH",()=>SortBy("biggest"));
        var go=new GameObject("Home Entries",typeof(RectTransform),
            typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        go.transform.SetParent(transform,false);
        results=go.GetComponent<RectTransform>();
        var rows=go.GetComponent<VerticalLayoutGroup>();
        rows.spacing=12;rows.childControlHeight=true;rows.childControlWidth=true;
        rows.childForceExpandWidth=true;rows.childForceExpandHeight=false;
        go.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        Button("PREVIOUS PAGE",()=>Query(Mathf.Max(0,page-1)));
        Button("NEXT PAGE",()=>{if(directory!=null&&directory.MoreListings)Query(page+1);});
        if(directory!=null)
        {
            directory.Changed+=Refresh;
            directory.Browse("",0,sort);
        }
        Refresh();
    }
    private void OnDestroy(){if(directory!=null)directory.Changed-=Refresh;}
    private void Back()
    {
        var hud=FindFirstObjectByType<ShopWorldHUD>();
        if(hud!=null)hud.Open("multiplayer");
    }
    private void SortBy(string value){sort=value;Query(0);}
    private void Query(int requested)
    {
        if(directory==null)return;
        page=requested;
        directory.Browse(search.text,page,sort);
    }
    private void Refresh()
    {
        if(directory==null || results==null)return;
        status.text=directory.Status+(directory.Busy?"  •  LOADING...":"");
        myCode.text=directory.DisplayHomeCode;
        privacy.text="Visibility: "+directory.Visibility.ToUpperInvariant()+
            " • Public is the default. Private Homes cannot be visited.";
        for(int i=results.childCount-1;i>=0;i--)
        {
            var child=results.GetChild(i).gameObject;
            child.SetActive(false);Destroy(child);
        }
        HeadingAt(results,"PAGE "+(directory.ListingPage+1)+" • "+
            directory.TotalListings+" PUBLIC HOMES",25);
        if(directory.Listings==null || directory.Listings.Length==0)
        {
            HeadingAt(results,"No public Homes found. Try another name or Home code.",23);
            return;
        }
        bool foundOtherHome=false;
        foreach(var home in directory.Listings)
        {
            // Also filter the rendered cards locally in case a directory request
            // finished before this device completed Home registration.
            if(home==null || !string.IsNullOrEmpty(directory.OwnerCode) &&
               string.Equals(home.code,directory.OwnerCode,StringComparison.OrdinalIgnoreCase))
                continue;
            foundOtherHome=true;
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
        if(!foundOtherHome)
            HeadingAt(results,"No other players' public Homes found.",23);
    }
    private Text Heading(string value,int size)=>HeadingAt(transform,value,size);
    private Text HeadingAt(Transform parent,string value,int size)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text),typeof(LayoutElement));
        go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();
        t.font=font;t.fontSize=size;t.color=foreground;
        t.text=value;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;
        t.verticalOverflow=VerticalWrapMode.Overflow;
        go.GetComponent<LayoutElement>().preferredHeight=size<=23?60:72;
        return t;
    }
    private InputField Field(string placeholder,int limit)
    {
        var go=new GameObject("Search Field",typeof(RectTransform),typeof(Image),
            typeof(InputField),typeof(LayoutElement));
        go.transform.SetParent(transform,false);
        FishingHudTheme.Panel(go);
        go.GetComponent<LayoutElement>().preferredHeight=68;
        var field=go.GetComponent<InputField>();
        field.targetGraphic=go.GetComponent<Image>();
        field.characterLimit=limit;
        var text=HeadingAt(go.transform,"",26);
        text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;
        FitChild(text.rectTransform);
        field.textComponent=text;
        var hint=HeadingAt(go.transform,placeholder,22);
        hint.color=new Color(.68f,.77f,.82f);
        FitChild(hint.rectTransform);
        field.placeholder=hint;
        return field;
    }
    private void Button(string text,UnityEngine.Events.UnityAction action)=>ButtonAt(transform,text,action);
    private void ButtonAt(Transform parent,string text,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(text,typeof(RectTransform),typeof(Image),
            typeof(Button),typeof(LayoutElement));
        go.transform.SetParent(parent,false);
        FishingHudTheme.Panel(go,1);
        go.GetComponent<LayoutElement>().preferredHeight=67;
        var b=go.GetComponent<Button>();b.targetGraphic=go.GetComponent<Image>();b.onClick.AddListener(action);
        var label=HeadingAt(go.transform,text,24);
        FitChild(label.rectTransform);
    }
    private static void FitChild(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(16,3);rect.offsetMax=new Vector2(-16,-3);
    }
}
