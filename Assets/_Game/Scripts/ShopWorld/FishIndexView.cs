using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Dedicated informational view inside the existing modal/backdrop. It does not
// equip bait, interrupt a cast, change spawn tables, or take ownership of CLOSE.
public sealed class FishIndexView : MonoBehaviour
{
    private ShopProgress progress;
    private Texture island;
    private int biome, selectedFamily=-1, selectedVariant=-1;
    private Font font;
    private ShopPreview snapshots;
    private FishIndexLivePreview live;
    private ScrollRect speciesScroll, detailScroll;
    private RectTransform grid, details;
    private GridLayoutGroup gridLayout;
    private float gridWidth=-1f;
    private bool dirty;
    private UnityEngine.Events.UnityAction back;
    private readonly Dictionary<int,Image> cards=new Dictionary<int,Image>();
    private readonly Color cyan=new Color(.08f,.95f,1f);
    private readonly Color gold=new Color(1f,.86f,.4f);
    private readonly Color muted=new Color(.61f,.72f,.78f);

    public void Show(ShopProgress source,int region,Texture picture,UnityEngine.Events.UnityAction goBack)
    {
        if(progress!=null)progress.Changed-=OnChanged;
        if(biome!=region){selectedFamily=-1;selectedVariant=-1;}
        progress=source;biome=region;island=picture;back=goBack;
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if(snapshots==null)snapshots=gameObject.AddComponent<ShopPreview>();
        if(live==null)live=gameObject.AddComponent<FishIndexLivePreview>();
        progress.Data.EnsureCatchStats();
        progress.Changed+=OnChanged;
        dirty=false;
        Rebuild();
    }

    private void OnChanged(){dirty=true;}
    private void Update()
    {
        if(dirty){dirty=false;Rebuild();}
        ResizeGrid();
    }
    private void ResizeGrid()
    {
        if(grid==null || gridLayout==null)return;
        float width=speciesScroll.viewport.rect.width;
        if(Mathf.Abs(width-gridWidth)<.5f)return;
        gridWidth=width;
        const int columns=2;
        gridLayout.constraintCount=columns;
        gridLayout.cellSize=new Vector2(Mathf.Max(1,(width-20f-(columns-1)*12f)/columns),132f);
        LayoutRebuilder.MarkLayoutForRebuild(grid);
    }

    private void Rebuild()
    {
        float position=speciesScroll!=null?speciesScroll.verticalNormalizedPosition:1f;
        snapshots.Suspend();live.Clear();cards.Clear();
        ClearChildren(transform);gridWidth=-1;
        var families=FishIndexCatalog.RegionSpecies(biome);
        if(!families.Contains(selectedFamily))
        {selectedFamily=families.Count>0?families[0]:-1;selectedVariant=-1;}

        var banner=Panel("RegionBanner",transform);
        Anchor(banner,0,1,.62f,1,0,-184,-12,0);
        var bannerButton=banner.gameObject.AddComponent<Button>();
        bannerButton.targetGraphic=banner.GetComponent<Image>();
        bannerButton.targetGraphic.raycastTarget=true;
        if(back!=null)bannerButton.onClick.AddListener(back);
        var art=Picture(banner,"IslandArtwork");art.texture=island;
        Anchor(art.rectTransform,0,0,.27f,1,12,12,-8,-12);
        IslandIndexArtwork.Round(art);
        // Same texture as the island-index card, with no generated substitute.
        var title=Label(banner,ReefCatalog.Zones[biome].name.ToUpperInvariant(),32,Color.white);
        Anchor(title.rectTransform,.29f,.48f,.78f,1,0,0,-8,-13);
        var total=Label(banner,families.Count+" FISH IN REGION",22,cyan);
        Anchor(total.rectTransform,.29f,0,.78f,.48f,0,15,-8,0);
        int caught=0;
        foreach(int family in families)if(FishIndexCatalog.FoundVariants(progress.Data,family)>0)caught++;
        var discovered=Label(banner,caught+" / "+families.Count+"\nCAUGHT",28,cyan,TextAnchor.MiddleCenter);
        Anchor(discovered.rectTransform,.79f,0,1,1,0,16,-12,-16);

        speciesScroll=MakeScroll(transform,"Species",out grid,true);
        // Move the grid down by the extra banner height and into the old footer.
        Anchor(speciesScroll.GetComponent<RectTransform>(),0,0,.62f,1,0,0,-12,-198);
        gridLayout=grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;gridLayout.constraintCount=2;
        gridLayout.startCorner=GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis=GridLayoutGroup.Axis.Horizontal;
        gridLayout.spacing=new Vector2(12,12);gridLayout.padding=new RectOffset(4,4,4,4);
        foreach(int family in families)BuildCard(family);
        if(families.Count==0)
        {
            var empty=Label(grid,"No species available in this region.",24,muted);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight=132;
        }

        var right=Panel("FishDetails",transform);
        Anchor(right,.62f,0,1,1,0,0,0,-76);
        detailScroll=MakeScroll(right,"Details",out details);
        Anchor(detailScroll.GetComponent<RectTransform>(),0,0,1,1,12,12,-12,-12);

        ShowDetails(selectedFamily,selectedVariant);
        ResizeGrid();
        Canvas.ForceUpdateCanvases();
        speciesScroll.verticalNormalizedPosition=position;
    }

    private void BuildCard(int family)
    {
        int variant=FishIndexCatalog.BestVariant(progress.Data,family);
        bool known=FishIndexCatalog.FoundVariants(progress.Data,family)>0;
        var card=Panel("Species_"+family,grid);
        var button=card.gameObject.AddComponent<Button>();
        button.targetGraphic=card.GetComponent<Image>();
        button.targetGraphic.raycastTarget=true;
        button.onClick.AddListener(()=>ShowDetails(family,-1));
        cards[family]=card.GetComponent<Image>();
        var selected=card.gameObject.AddComponent<Outline>();
        selected.effectColor=new Color(.6f,1f,1f,.9f);selected.effectDistance=new Vector2(2,-2);
        selected.enabled=family==selectedFamily;
        var picture=Picture(card,"SideProfile");
        Anchor(picture.rectTransform,0,.1f,.34f,.9f,12,0,0,0);
        PreserveImageAspect(picture);
        picture.color=known?Color.white:Color.black;
        snapshots.Attach(picture,variant,PreviewWeight(variant),null,true,true,true);
        var title=Label(card,known?FishCatalog.Get(family).Name.ToUpperInvariant():"???",24,Color.white);
        Anchor(title.rectTransform,.36f,.64f,1,1,0,0,-12,-10);
        float best=FishIndexCatalog.Best(progress.Data,variant);
        var specimen=Label(card,known?"Best: "+FishingTuning.SpecimenClass(variant,best):"Not Yet Caught",20,known?gold:muted);
        Anchor(specimen.rectTransform,.36f,.34f,1,.65f,0,0,-12,0);
        var weight=Label(card,known?"PB: "+Measurements(variant,best):"—",19,known?Color.white:muted);
        Anchor(weight.rectTransform,.36f,.06f,1,.35f,0,0,-12,0);
    }

    private void ShowDetails(int family,int variant)
    {
        live.Clear();ClearChildren(details);
        selectedFamily=family;
        foreach(var card in cards)card.Value.GetComponent<Outline>().enabled=card.Key==family;
        if(family<0){TextRow("Select a species to see its details.",26,muted,80);return;}
        if(variant<0 || FishIndexCatalog.Family(variant)!=family)
            variant=FishIndexCatalog.BestVariant(progress.Data,family);
        selectedVariant=variant;
        bool known=FishIndexCatalog.Discovered(progress.Data,variant);
        float best=FishIndexCatalog.Best(progress.Data,variant);
        TextRow(known?FishCatalog.Get(variant).Name.ToUpperInvariant():"???",32,Color.white,62);
        TextRow(known?FishingTuning.SpecimenClass(variant,best).ToUpperInvariant():"NOT YET CAUGHT",25,known?gold:muted,40);

        var imageRow=Row(200);
        var preview=Picture(imageRow,"SelectedFish");
        Anchor(preview.rectTransform,0,0,1,1,4,0,-4,0);PreserveImageAspect(preview);
        if(known)live.Show(preview,variant);
        else
        {
            preview.color=Color.black;
            snapshots.Attach(preview,variant,PreviewWeight(variant),null,true,true,true);
        }
        Divider();
        TextRow("Personal Best",22,cyan,30,TextAnchor.MiddleLeft);
        TextRow(known?Measurements(variant,best):"—",32,Color.white,48);
        TextRow("Caught: "+(known?progress.Data.totalCaught[variant]:0),23,Color.white,38,TextAnchor.MiddleLeft);
        int[] variants=FishIndexCatalog.Variants(family);
        TextRow("Variants Found: "+FishIndexCatalog.FoundVariants(progress.Data,family)+" / "+variants.Length,23,cyan,38,TextAnchor.MiddleLeft);
        if(variants.Length>1)
        {
            foreach(int id in variants)
            {
                int selected=id;
                bool found=FishIndexCatalog.Discovered(progress.Data,id);
                string name=found?FishCatalog.Get(id).Name:"Undiscovered variant";
                var option=Button(details,(id==variant?"•  ":"")+name,()=>ShowDetails(family,selected));
                option.gameObject.AddComponent<LayoutElement>().preferredHeight=44;
            }
        }
        Divider();
        TextRow(known?"Preferred Lures & Baits":"Top 3 Lures & Baits",27,cyan,44,TextAnchor.MiddleLeft);
        bool emperorFamily=family==FishCatalog.EmperorAngelfishId;
        bool makoFamily=family==FishCatalog.MakoSharkId;
        int baitSpecies=emperorFamily?FishCatalog.EmperorAngelfishId:variant;
        if(!known)
            TextRow("Catch and register this fish to reveal its percentages.",21,muted,60);
        else
        {
            if(emperorFamily)
                TextRow("Juveniles are the smallest 30% of the 10–50 cm range (22 cm or less).",18,muted,60,TextAnchor.MiddleLeft);
            if(makoFamily)
                TextRow("Mako sharks: 80% normal, 20% battle-scarred. The overall bite rate stays the same.",18,muted,60,TextAnchor.MiddleLeft);
            TextRow((emperorFamily?"Combined chance for both variants in ":"Chance in ")+ReefCatalog.Zones[biome].name+" when a fish bites.",18,muted,emperorFamily?60:46,TextAnchor.MiddleLeft);
        }
        var options=FishIndexCatalog.PreferredOptions(baitSpecies,biome,known);
        foreach(var bait in options)
        {
            var row=Row(76);
            var icon=Picture(row,"BaitImage");
            Anchor(icon.rectTransform,0,0,.29f,1,0,5,0,-5);
            PreserveImageAspect(icon,bait.PreviewKey!=null && bait.PreviewKey.StartsWith("Bait")?1f:2f);
            snapshots.Attach(icon,0,1f,bait.PreviewKey,false,true);
            var name=Label(row,bait.Name,21,Color.white);
            Anchor(name.rectTransform,.31f,0,.81f,1,0,6,-8,-6);
            float percent=FishIndexCatalog.CatchChance(baitSpecies,bait.Key,biome);
            bool available=percent>=0f;
            var odds=Label(row,FishIndexCatalog.ChanceLabel(percent,known),25,known && available && percent>0?gold:muted,TextAnchor.MiddleRight);
            Anchor(odds.rectTransform,.81f,0,1,1,0,0,-2,0);
        }
        if(!FishingTuning.IsValid)TextRow("Catch percentages are unavailable until the tuning table is valid.",18,muted,60);
        LayoutRebuilder.ForceRebuildLayoutImmediate(details);
        detailScroll.StopMovement();detailScroll.verticalNormalizedPosition=1f;
    }

    private static float PreviewWeight(int species)
    {var fish=FishCatalog.Get(species);return (fish.MinWeightKg+fish.MaxWeightKg)*.5f;}
    private static string Measurements(int species,float kg)
    {return kg.ToString("0.00")+" kg • "+ShopCatalog.FishLength(species,kg).ToString("0.00")+" m";}

    private RectTransform Row(float height)
    {
        var go=new GameObject("DetailRow",typeof(RectTransform),typeof(LayoutElement));
        go.transform.SetParent(details,false);go.GetComponent<LayoutElement>().preferredHeight=height;
        return go.GetComponent<RectTransform>();
    }
    private void TextRow(string value,int size,Color color,float height,TextAnchor alignment=TextAnchor.MiddleCenter)
    {
        var text=Label(Row(height),value,size,color,alignment);
        Anchor(text.rectTransform,0,0,1,1,6,0,-6,0);
    }
    private void Divider()
    {
        var row=Row(8);
        var line=new GameObject("Divider",typeof(RectTransform),typeof(Image));line.transform.SetParent(row,false);
        line.GetComponent<Image>().color=new Color(.1f,.8f,.95f,.65f);line.GetComponent<Image>().raycastTarget=false;
        Anchor(line.GetComponent<RectTransform>(),0,.5f,1,.5f,4,-1,-4,1);
    }

    private ScrollRect MakeScroll(Transform parent,string name,out RectTransform content,bool gridContent=false)
    {
        var box=new GameObject(name+"Scroll",typeof(RectTransform),typeof(ScrollRect));box.transform.SetParent(parent,false);
        var viewport=new GameObject(name+"Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));viewport.transform.SetParent(box.transform,false);
        viewport.GetComponent<Image>().color=Color.clear;
        Anchor(viewport.GetComponent<RectTransform>(),0,0,1,1,0,0,-22,0);
        var rows=new GameObject(name+"Content",typeof(RectTransform),typeof(ContentSizeFitter));rows.transform.SetParent(viewport.transform,false);
        content=rows.GetComponent<RectTransform>();content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;
        content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        if(!gridContent)
        {
            var layout=rows.AddComponent<VerticalLayoutGroup>();layout.spacing=4;layout.padding=new RectOffset(4,4,4,8);
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        }
        rows.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var track=Panel(name+"ScrollTrack",box.transform);FishingHudTheme.Panel(track.gameObject,3);
        Anchor(track,1,0,1,1,-16,4,0,-4);
        var handle=Panel(name+"ScrollHandle",track);FishingHudTheme.Panel(handle.gameObject,1);
        Anchor(handle,0,0,1,1,0,0,0,0);
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();
        bar.targetGraphic.raycastTarget=true;track.GetComponent<Image>().raycastTarget=true;
        bar.direction=Scrollbar.Direction.BottomToTop;
        var scroll=box.GetComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=content;
        scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
        scroll.verticalScrollbar=bar;
        return scroll;
    }

    private RectTransform Panel(string name,Transform parent)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        FishingHudTheme.Panel(go);go.GetComponent<Image>().raycastTarget=false;return go.GetComponent<RectTransform>();
    }
    private Button Button(Transform parent,string value,UnityEngine.Events.UnityAction action)
    {
        var rect=Panel(value,parent);var button=rect.gameObject.AddComponent<Button>();
        button.targetGraphic=rect.GetComponent<Image>();button.targetGraphic.raycastTarget=true;
        if(action!=null)button.onClick.AddListener(action);
        var text=Label(rect,value,23,Color.white,TextAnchor.MiddleCenter);Anchor(text.rectTransform,0,0,1,1,12,5,-12,-5);
        return button;
    }
    private Text Label(Transform parent,string value,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var label=go.GetComponent<Text>();label.font=font;label.text=value;label.fontSize=size;label.color=color;
        label.fontStyle=FontStyle.Bold;label.alignment=alignment;label.raycastTarget=false;
        label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
        label.resizeTextForBestFit=true;label.resizeTextMinSize=14;label.resizeTextMaxSize=size;
        return label;
    }
    private RawImage Picture(Transform parent,string name)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
        var image=go.GetComponent<RawImage>();image.raycastTarget=false;return image;
    }
    private static void PreserveImageAspect(RawImage image,float ratio=2f)
    {
        // Fit inside the assigned slot, not the entire card/detail row.
        var source=image.rectTransform;
        var slot=new GameObject(image.name+"Slot",typeof(RectTransform)).GetComponent<RectTransform>();
        slot.SetParent(source.parent,false);
        slot.anchorMin=source.anchorMin;slot.anchorMax=source.anchorMax;slot.pivot=source.pivot;
        slot.offsetMin=source.offsetMin;slot.offsetMax=source.offsetMax;
        source.SetParent(slot,false);
        var fit=image.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=ratio;
    }
    private static void Anchor(RectTransform rect,float x0,float y0,float x1,float y1,float left,float bottom,float right,float top)
    {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(right,top);}
    private static void ClearChildren(Transform parent)
    {for(int i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}}
    private void OnDisable()
    {
        if(progress!=null)progress.Changed-=OnChanged;
        if(snapshots!=null)snapshots.Suspend();
        if(live!=null)live.Clear();
    }
}

