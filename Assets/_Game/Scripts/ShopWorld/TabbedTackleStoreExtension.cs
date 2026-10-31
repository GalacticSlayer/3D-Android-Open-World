using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replaces the flat Tackle Store list with five focused tabs while it is open:
/// Rods, Reels, Lines, Lures and Bait.
///
/// Every durable item stays visible and uses one consistent action state:
/// BUY -> EQUIP -> EQUIPPED. Consumable bait mirrors that presentation by treating
/// a positive stack as owned. The underlying ShopLedger remains authoritative.
///
/// This is a presentation companion rather than a replacement for ShopWorldHUD, so
/// travel, inventory, the standalone BAIT / LURES page and shop proximity rules keep
/// their existing behavior.
/// </summary>
[DefaultExecutionOrder(5000)]
public sealed class TabbedTackleStoreExtension : MonoBehaviour
{
    private enum TackleTab { Rods, Reels, Lines, Lures, Bait }

    private static readonly Color Ink=new Color(.07f,.12f,.14f,1f);
    private static readonly Color Teal=new Color(.04f,.36f,.39f,1f);
    private static readonly Color Gold=new Color(.89f,.72f,.40f,1f);
    private static readonly Color Detail=new Color(.65f,.79f,.8f,1f);
    private static readonly Color Disabled=new Color(.17f,.23f,.24f,1f);

    private ShopWorldHUD hud;
    private ShopProgress progress;
    private ShopPreview preview;
    private TackleLureShopExtension legacyLureExtension;
    private Font font;
    private GameObject section;
    private GameObject fixedTabs;
    private TackleTab selected=TackleTab.Rods;
    private bool wasTackle;
    private bool rebuild;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach(ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(target!=null && target.GetComponent<TabbedTackleStoreExtension>()==null)
                target.gameObject.AddComponent<TabbedTackleStoreExtension>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        preview=GetComponent<ShopPreview>();
        if(preview==null)preview=gameObject.AddComponent<ShopPreview>();
        legacyLureExtension=GetComponent<TackleLureShopExtension>();
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void LateUpdate()
    {
        if(hud==null || progress==null)return;
        bool tackle=ShopWorldHUD.MenuOpen && HasHeading("TACKLE STORE");

        if(!tackle)
        {
            if(wasTackle)LeaveTackleStore();
            wasTackle=false;
            return;
        }

        if(!wasTackle)
        {
            selected=TackleTab.Rods;
            rebuild=true;
        }
        wasTackle=true;

        if(legacyLureExtension==null)legacyLureExtension=GetComponent<TackleLureShopExtension>();
        if(legacyLureExtension!=null && legacyLureExtension.enabled)legacyLureExtension.enabled=false;

        RectTransform rows=FindRows();
        if(rows==null)return;

        // ShopWorldHUD rebuilds Rows after purchases/equips. Unity-null semantics make
        // the destroyed section compare as null, so this recreates our selected tab
        // automatically without losing the user's category selection.
        if(section==null || section.transform.parent!=rows || fixedTabs==null)rebuild=true;
        if(rebuild)BuildStore(rows);
        HideCoreRows(rows);
        FitThreeItems(rows);
    }

    private void FitThreeItems(RectTransform rows)
    {
        if(section==null)return;
        RectTransform viewport=rows.parent as RectTransform;
        if(viewport==null || viewport.rect.height<=0f)return;
        // Three complete cards, two 12-unit gaps; the viewport itself is inset
        // 20 units from both the top and bottom of the large item surface.
        float height=Mathf.Max(1f,(viewport.rect.height-24f)/3f);
        int count=0;
        bool changed=false;
        for(int i=0;i<section.transform.childCount;i++)
        {
            Transform row=section.transform.GetChild(i);
            LayoutElement layout=row.GetComponent<LayoutElement>();
            if(!row.gameObject.activeSelf || layout==null || layout.ignoreLayout)continue;
            count++;
            if(Mathf.Abs(layout.preferredHeight-height)<.1f)continue;
            layout.preferredHeight=height;
            Transform frame=row.Find("ItemImageFrame");
            if(frame!=null)
            {
                float side=Mathf.Min(164f,Mathf.Max(1f,height-36f));
                ((RectTransform)frame).sizeDelta=new Vector2(side,side);
            }
            changed=true;
        }
        if(!changed)return;
        section.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(0f,count*(height+12f)-12f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
    }

    private void LeaveTackleStore()
    {
        RectTransform rows=FindRows();
        if(fixedTabs!=null){fixedTabs.SetActive(false);Destroy(fixedTabs);fixedTabs=null;}
        if(section!=null)
        {
            section.SetActive(false);
            Destroy(section);
            section=null;
        }
        if(rows!=null)
            for(int i=0;i<rows.childCount;i++)
                if(rows.GetChild(i)!=null)rows.GetChild(i).gameObject.SetActive(true);
        if(legacyLureExtension!=null)legacyLureExtension.enabled=true;
        rebuild=false;
    }

    private void OnDisable()
    {
        if(fixedTabs!=null){fixedTabs.SetActive(false);Destroy(fixedTabs);fixedTabs=null;}
        if(legacyLureExtension!=null)legacyLureExtension.enabled=true;
    }

    private bool HasHeading(string caption)
    {
        Text[] labels=hud.GetComponentsInChildren<Text>(true);
        for(int i=0;i<labels.Length;i++)
            if(labels[i]!=null && string.Equals(labels[i].text,caption,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private RectTransform FindRows()
    {
        RectTransform[] transforms=hud.GetComponentsInChildren<RectTransform>(true);
        for(int i=0;i<transforms.Length;i++)
            if(transforms[i]!=null && transforms[i].name=="Rows")return transforms[i];
        return null;
    }

    private void HideCoreRows(RectTransform rows)
    {
        for(int i=0;i<rows.childCount;i++)
        {
            Transform child=rows.GetChild(i);
            if(child==null)continue;
            bool ours=section!=null && child==section.transform;
            child.gameObject.SetActive(ours);
        }
    }

    private void BuildStore(RectTransform rows)
    {
        rebuild=false;
        if(section!=null)
        {
            section.SetActive(false);
            Destroy(section);
        }

        HideAllChildren(rows);
        section=new GameObject("TabbedTackleStore",typeof(RectTransform),typeof(LayoutElement),typeof(VerticalLayoutGroup));
        section.transform.SetParent(rows,false);
        VerticalLayoutGroup layout=section.GetComponent<VerticalLayoutGroup>();
        layout.spacing=12f;
        layout.childControlWidth=true;
        layout.childForceExpandWidth=true;
        layout.childControlHeight=true;
        layout.childForceExpandHeight=false;

        if(fixedTabs!=null){fixedTabs.SetActive(false);Destroy(fixedTabs);}
        // Tabs stay above the scroll viewport; only the item rows scroll.
        CreateTabs(rows.parent.parent);
        CreateCategoryHeading(section.transform);
        int itemCount=BuildSelectedRows(section.transform);
        section.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(0,itemCount*212f-12f);
        Canvas.ForceUpdateCanvases();
    }

    private static void HideAllChildren(RectTransform rows)
    {
        for(int i=0;i<rows.childCount;i++)
            if(rows.GetChild(i)!=null)rows.GetChild(i).gameObject.SetActive(false);
    }

    private void CreateTabs(Transform parent)
    {
        GameObject bar=new GameObject("TackleTabs",typeof(RectTransform));
        fixedTabs=bar;
        bar.transform.SetParent(parent,false);
        Anchor(bar.GetComponent<RectTransform>(),0,1,1,1,0,-240,0,-150);

        string[] captions={"RODS","REELS","LINES","LURES","BAIT"};
        for(int i=0;i<captions.Length;i++)
        {
            int index=i;
            TackleTab tab=(TackleTab)i;
            Button button=CreateButton(bar.transform,captions[i],()=>SelectTab((TackleTab)index));
            float x0=i/5f,x1=(i+1)/5f;
            Anchor(button.GetComponent<RectTransform>(),x0,0,x1,1,5,3,-5,-3);
            bool active=tab==selected;
            button.interactable=!active;
            FishingHudTheme.Panel(button.gameObject,active?1:0);
            Text label=button.GetComponentInChildren<Text>();
            if(label!=null)
            {
                label.color=active?new Color(.01f,.15f,.20f):Color.white;
                label.fontStyle=FontStyle.Bold;
                label.rectTransform.offsetMin=new Vector2(86,0);
                label.rectTransform.offsetMax=new Vector2(-10,0);
                label.alignment=TextAnchor.MiddleLeft;
            }
            string[] previewKeys={"Rod0","Reel0","Line",ShopCatalog.LurePreviewKey(0),"Bait0"};
            var icon=new GameObject("CategoryItemImage",typeof(RectTransform),typeof(RawImage));
            icon.transform.SetParent(button.transform,false);
            Rect(icon.GetComponent<RectTransform>(),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(18,0),new Vector2(58,58));
            var image=icon.GetComponent<RawImage>();image.raycastTarget=false;
            preview.Attach(image,0,1f,previewKeys[i]);
        }
    }

    private void CreateCategoryHeading(Transform parent)
    {
        GameObject heading=new GameObject("CategoryHeading",typeof(RectTransform),typeof(LayoutElement));
        heading.transform.SetParent(parent,false);
        // Retain this marker for the bait-restock companion, without a second heading.
        heading.GetComponent<LayoutElement>().ignoreLayout=true;
        heading.SetActive(false);
        Text text=CreateText(heading.transform,selected.ToString().ToUpperInvariant(),30,Gold,TextAnchor.MiddleLeft);
        Full(text.rectTransform);
        text.rectTransform.offsetMin=new Vector2(8,0);
        text.rectTransform.offsetMax=new Vector2(-8,0);
    }

    private int BuildSelectedRows(Transform parent)
    {
        switch(selected)
        {
            case TackleTab.Rods:return BuildGearRows(parent,GearKind.Rod,ShopCatalog.MaxRodTier);
            case TackleTab.Reels:return BuildGearRows(parent,GearKind.Reel,ShopCatalog.MaxReelTier);
            case TackleTab.Lines:return BuildGearRows(parent,GearKind.Line,ShopCatalog.LineNames.Length-1);
            case TackleTab.Lures:return BuildLureRows(parent);
            default:return BuildBaitRows(parent);
        }
    }

    private int BuildGearRows(Transform parent,GearKind kind,int maxTier)
    {
        for(int tier=0;tier<=maxTier;tier++)CreateGearRow(parent,kind,tier);
        return maxTier+1;
    }

    private void CreateGearRow(Transform parent,GearKind kind,int tier)
    {
        bool owned=progress.Data.OwnsGear(kind,tier);
        bool equipped=progress.Data.Equipped(kind)==tier;
        int price=ShopCatalog.GearPrice(kind,tier);
        string action=equipped?"EQUIPPED":owned?"EQUIP":"BUY "+price.ToString("N0");
        bool enabled=!progress.ReadOnly && !equipped && (owned || progress.Data.coins>=price);

        string detail;
        string previewKey;
        if(kind==GearKind.Rod)
        {
            detail=FishingBurstDamageRuntime.NormalMinimumForTier(tier)+"–"+FishingBurstDamageRuntime.NormalMaximumForTier(tier)+
                " damage per burst • "+(FishingBurstDamageRuntime.CriticalChanceForTier(tier)*100f).ToString("0")+"% critical chance • critical hits deal 2× damage";
            previewKey="Rod"+tier;
        }
        else if(kind==GearKind.Reel)
        {
            detail=((Level2FishingReelRuntime.ReelMultiplier(tier)-1f)*100f).ToString("0")+"% faster reeling";
            previewKey="Reel"+tier;
        }
        else
        {
            detail="40 m line • 30 m maximum cast • "+(tier*12)+"% more line tolerance";
            previewKey="Line";
        }

        GearKind selectedKind=kind;
        int selectedTier=tier;
        CreateItemRow(parent,ShopCatalog.GearName(kind,tier),detail,action,enabled,previewKey,()=>
        {
            bool result=owned
                ? (!progress.ReadOnly && progress.Commit(progress.Data.Equip(selectedKind,selectedTier)))
                : progress.BuyGear(selectedKind,selectedTier);
            if(result)rebuild=true;
        });
    }

    private int BuildLureRows(Transform parent)
    {
        for(int variant=0;variant<ShopCatalog.LureVariantCount;variant++)CreateLureRow(parent,variant);
        return ShopCatalog.LureVariantCount;
    }

    private void CreateLureRow(Transform parent,int variant)
    {
        bool owned=progress.Data.OwnsLure(variant);
        bool equipped=owned && progress.Data.baitEquipped==ShopCatalog.StarterLure && progress.Data.lureEquipped==variant;
        int price=ShopCatalog.LurePrices[variant];
        string action=equipped?"EQUIPPED":"EQUIP";
        bool enabled=!progress.ReadOnly && !equipped && owned;
        int selectedVariant=variant;
        CreateItemRow(parent,ShopCatalog.LureNames[variant],progress.Data.LureQuantity(variant)+" owned • Kept on catch; lost on escape.",action,enabled,ShopCatalog.LurePreviewKey(variant),()=>
        {
            if(progress.EquipLure(selectedVariant))rebuild=true;
        },()=>{if(progress.BuyLure(selectedVariant))rebuild=true;},price);
    }

    private int BuildBaitRows(Transform parent)
    {
        CreateBaitRow(parent,0);
        CreateBaitRow(parent,2);
        CreateBaitRow(parent,3);
        return 3;
    }

    private void CreateBaitRow(Transform parent,int id)
    {
        bool permanent=id==0;
        int amount=permanent?int.MaxValue:progress.Data.bait[id];
        bool owned=permanent || amount>0;
        bool equipped=progress.Data.baitEquipped==id;
        int price=ShopCatalog.BaitPrices[id];
        string action=equipped?"EQUIPPED":owned?"EQUIP":"BUY "+price.ToString("N0");
        bool enabled=!progress.ReadOnly && !equipped && (owned || progress.Data.coins>=price);

        string detail;
        if(id==0)detail="Unlimited • Infinite worms • Standard catch chances.";
        else if(id==2)detail=amount+" remaining • Pack of 10 • 20% shorter wait; favors snapper and goatfish.";
        else detail=amount+" remaining • Pack of 10 • Favors yellowtail and tuna; one squid per cast.";

        int selectedId=id;
        CreateItemRow(parent,ShopCatalog.BaitNames[id],detail,action,enabled,"Bait"+id,()=>
        {
            bool result;
            if(owned)
            {
                if(progress.ReadOnly)result=false;
                else
                {
                    progress.Data.baitEquipped=selectedId;
                    result=progress.Commit(true);
                }
            }
            else result=progress.BuyBait(selectedId);
            if(result)rebuild=true;
        });
    }

    private void CreateItemRow(Transform parent,string title,string detail,string action,bool enabled,string previewKey,Action callback,Action buyMore=null,int price=0)
    {
        GameObject row=Panel(title,parent,Color.white);
        FishingHudTheme.Panel(row);
        row.AddComponent<LayoutElement>().preferredHeight=200f;

        GameObject frame=Panel("ItemImageFrame",row.transform,Color.white);
        FishingHudTheme.Panel(frame);
        frame.GetComponent<Image>().raycastTarget=false;
        Rect(frame.GetComponent<RectTransform>(),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(24,0),new Vector2(164,164));
        GameObject picture=new GameObject("3D item preview",typeof(RectTransform),typeof(RawImage));
        picture.transform.SetParent(frame.transform,false);
        Anchor(picture.GetComponent<RectTransform>(),0,0,1,1,10,10,-10,-10);
        RawImage raw=picture.GetComponent<RawImage>();raw.raycastTarget=false;
        preview.Attach(raw,0,1f,previewKey);

        Text name=CreateText(row.transform,title,34,Color.white,TextAnchor.MiddleLeft);
        name.fontStyle=FontStyle.Bold;
        Anchor(name.rectTransform,0,.56f,1,1,216,0,-286,-18);
        Text description=CreateText(row.transform,detail,25,new Color(.70f,.90f,.96f),TextAnchor.UpperLeft);
        Anchor(description.rectTransform,0,0,1,.53f,216,20,-286,0);

        Button button=CreateButton(row.transform,action,callback);
        Rect(button.GetComponent<RectTransform>(),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,0),new Vector2(238,90));
        if(buyMore!=null)
        {
            Rect(button.GetComponent<RectTransform>(),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,45),new Vector2(238,70));
            Button buy=CreateButton(row.transform,"BUY "+price.ToString("N0"),buyMore);
            Rect(buy.GetComponent<RectTransform>(),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,-45),new Vector2(238,70));
            buy.interactable=!progress.ReadOnly && progress.Data.coins>=price;
            FishingHudTheme.Panel(buy.gameObject,buy.interactable?1:0);
        }
        button.interactable=enabled;
        FishingHudTheme.Panel(button.gameObject,enabled?1:0);
        button.GetComponent<Image>().color=enabled?Color.white:new Color(.60f,.77f,.82f,1f);
    }

    private void SelectTab(TackleTab tab)
    {
        if(selected==tab)return;
        selected=tab;
        rebuild=true;
    }

    private Text CreateText(Transform parent,string value,int size,Color color,TextAnchor alignment)
    {
        GameObject go=new GameObject("Label",typeof(RectTransform),typeof(Text));
        go.transform.SetParent(parent,false);
        Text text=go.GetComponent<Text>();
        text.font=font;
        text.text=value;
        text.fontSize=size;
        text.color=color;
        text.alignment=alignment;
        text.raycastTarget=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;
        text.verticalOverflow=VerticalWrapMode.Truncate;
        text.resizeTextForBestFit=true;
        text.resizeTextMinSize=20;
        text.resizeTextMaxSize=size;
        return text;
    }

    private Button CreateButton(Transform parent,string caption,Action action)
    {
        GameObject go=Panel(caption,parent,Teal);
        Button button=go.AddComponent<Button>();
        button.targetGraphic=go.GetComponent<Image>();
        button.transition=Selectable.Transition.None;
        button.onClick.AddListener(()=>action());
        Text label=CreateText(go.transform,caption,27,Color.white,TextAnchor.MiddleCenter);
        Full(label.rectTransform);
        return button;
    }

    private static GameObject Panel(string name,Transform parent,Color color)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);
        go.GetComponent<Image>().color=color;
        return go;
    }

    private static void Full(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;
        rect.anchorMax=Vector2.one;
        rect.offsetMin=Vector2.zero;
        rect.offsetMax=Vector2.zero;
    }

    private static void Rect(RectTransform rect,Vector2 min,Vector2 max,Vector2 pivot,Vector2 position,Vector2 size)
    {
        rect.anchorMin=min;
        rect.anchorMax=max;
        rect.pivot=pivot;
        rect.sizeDelta=size;
        rect.anchoredPosition=position;
    }

    private static void Anchor(RectTransform rect,float x0,float y0,float x1,float y1,float left,float bottom,float right,float top)
    {
        rect.anchorMin=new Vector2(x0,y0);
        rect.anchorMax=new Vector2(x1,y1);
        rect.offsetMin=new Vector2(left,bottom);
        rect.offsetMax=new Vector2(right,top);
    }
}
