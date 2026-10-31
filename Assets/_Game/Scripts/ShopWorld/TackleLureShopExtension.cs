using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Adds stocked lure variants to the existing Tackle Store without replacing
// ShopWorldHUD. The lure rows intentionally mirror ShopWorldHUD.Row so bought
// and owned tackle look like part of the same menu rather than a second UI.
public sealed class TackleLureShopExtension : MonoBehaviour
{
    private sealed class LureRowView
    {
        public int variant;
        public Button button;
        public Text buttonLabel;
        public Text nameLabel;
        public Button buyButton;
        public Image buttonImage;
    }

    private ShopWorldHUD hud;
    private ShopProgress progress;
    private ShopPreview preview;
    private GameObject section;
    private Font font;
    private bool sectionIsShop;
    private readonly List<LureRowView> rowViews = new List<LureRowView>();

    private static readonly Color Ink = new Color(0.07f,0.12f,0.14f,1f);
    private static readonly Color Teal = new Color(0.04f,0.36f,0.39f,1f);
    private static readonly Color Gold = new Color(0.89f,0.72f,0.40f,1f);
    private static readonly Color Detail = new Color(0.65f,0.79f,0.8f,1f);
    private static readonly Color Disabled = new Color(0.17f,0.23f,0.24f,1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        ShopWorldHUD target=FindFirstObjectByType<ShopWorldHUD>();
        if(target!=null && target.GetComponent<TackleLureShopExtension>()==null)
            target.gameObject.AddComponent<TackleLureShopExtension>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        preview=GetComponent<ShopPreview>();
        if(preview==null)preview=gameObject.AddComponent<ShopPreview>();
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void LateUpdate()
    {
        if(hud==null || progress==null)return;
        ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);
        SyncShortcutName();

        bool tackle=ShopWorldHUD.MenuOpen && HasHeading("TACKLE STORE");
        bool baitPage=ShopWorldHUD.MenuOpen && HasHeading("BAIT / LURES");
        if(!tackle && !baitPage)
        {
            section=null;
            rowViews.Clear();
            return;
        }

        RectTransform rows=FindRows();
        if(rows==null)return;
        if(section==null || section.transform.parent!=rows || !section.activeSelf || sectionIsShop!=tackle)
            BuildSection(rows,tackle);
        else
            RefreshRows();
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

    private void BuildSection(RectTransform rows,bool shop)
    {
        Transform existing=rows.Find("LureShopSection");
        if(existing!=null)Destroy(existing.gameObject);

        rowViews.Clear();
        sectionIsShop=shop;
        section=new GameObject("LureShopSection",typeof(RectTransform),typeof(LayoutElement),typeof(VerticalLayoutGroup));
        section.transform.SetParent(rows,false);
        VerticalLayoutGroup layout=section.GetComponent<VerticalLayoutGroup>();
        layout.spacing=10f;
        layout.childControlWidth=true;
        layout.childForceExpandWidth=true;
        layout.childControlHeight=true;
        layout.childForceExpandHeight=false;

        int visibleRows=0;
        for(int variant=0;variant<ShopCatalog.LureVariantCount;variant++)
            if(shop || progress.Data.OwnsLure(variant))visibleRows++;

        LayoutElement sectionSize=section.GetComponent<LayoutElement>();
        sectionSize.preferredHeight=58f+visibleRows*210f;

        GameObject titleRow=new GameObject(shop?"LuresHeading":"OwnedLuresHeading",typeof(RectTransform),typeof(LayoutElement));
        titleRow.transform.SetParent(section.transform,false);
        titleRow.GetComponent<LayoutElement>().preferredHeight=48f;
        Text title=CreateText(titleRow.transform,shop?"LURES • KEPT ON CATCH, LOST ON ESCAPE":"OWNED LURES",30,Gold,TextAnchor.MiddleLeft);
        Full(title.rectTransform);
        title.rectTransform.offsetMin=new Vector2(8f,0f);
        title.rectTransform.offsetMax=new Vector2(-8f,0f);

        for(int variant=0;variant<ShopCatalog.LureVariantCount;variant++)
        {
            bool owned=progress.Data.OwnsLure(variant);
            if(!shop && !owned)continue;
            CreateLureRow(section.transform,variant);
        }

        RefreshRows();
        Canvas.ForceUpdateCanvases();
    }

    private void CreateLureRow(Transform parent,int variant)
    {
        GameObject row=Panel("Lure_"+variant,parent,Ink);
        row.AddComponent<LayoutElement>().preferredHeight=200f;
        FishingHudTheme.Panel(row);

        GameObject accent=Panel("Accent",row.transform,Gold);
        Anchor(accent.GetComponent<RectTransform>(),0,0,0,1,0,0,4,0);
        accent.GetComponent<Image>().raycastTarget=false;
        accent.SetActive(false);

        GameObject picture=new GameObject("Lure item preview",typeof(RectTransform),typeof(RawImage));
        picture.transform.SetParent(row.transform,false);
        Rect(picture.GetComponent<RectTransform>(),new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(14,0),new Vector2(140,140));
        RawImage raw=picture.GetComponent<RawImage>();
        raw.raycastTarget=false;
        GameObject frame=Panel("ItemImageFrame",row.transform,Color.white);
        FishingHudTheme.Panel(frame);frame.GetComponent<Image>().raycastTarget=false;
        Rect(frame.GetComponent<RectTransform>(),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(24,0),new Vector2(164,164));
        picture.transform.SetParent(frame.transform,false);
        Anchor(picture.GetComponent<RectTransform>(),0,0,1,1,10,10,-10,-10);
        preview.Attach(raw,0,1f,ShopCatalog.LurePreviewKey(variant));

        Text name=CreateText(row.transform,ShopCatalog.LureNames[variant],34,Color.white,TextAnchor.MiddleLeft);
        name.fontStyle=FontStyle.Bold;
        Anchor(name.rectTransform,0,.56f,1,1,216,0,-286,-18);

        Text description=CreateText(row.transform,"Kept on catch; lost if a hooked fish escapes.",25,Detail,TextAnchor.MiddleLeft);
        description.color=new Color(.70f,.90f,.96f);
        Anchor(description.rectTransform,0,0,1,.53f,216,20,-286,0);

        int selected=variant;
        Button button=CreateButton(row.transform,"EQUIP",()=>SelectLure(selected));
        Rect(button.GetComponent<RectTransform>(),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,0),new Vector2(238,90));

        LureRowView view=new LureRowView();
        view.variant=variant;
        view.nameLabel=name;
        if(sectionIsShop)
        {
            Rect(button.GetComponent<RectTransform>(),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,45),new Vector2(238,70));
            view.buyButton=CreateButton(row.transform,"BUY 1 / "+ShopCatalog.LurePrices[variant],()=>{progress.BuyLure(selected);RefreshRows();});
            Rect(view.buyButton.GetComponent<RectTransform>(),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,-45),new Vector2(238,70));
        }
        view.button=button;
        view.buttonImage=button.GetComponent<Image>();
        view.buttonLabel=button.GetComponentInChildren<Text>();
        rowViews.Add(view);
    }

    private void RefreshRows()
    {
        for(int i=0;i<rowViews.Count;i++)
        {
            LureRowView view=rowViews[i];
            if(view==null || view.button==null)continue;

            int variant=view.variant;
            view.nameLabel.text=ShopCatalog.LureNames[variant]+" ×"+progress.Data.LureQuantity(variant);
            if(view.buyButton!=null)view.buyButton.interactable=!progress.ReadOnly && progress.Data.coins>=ShopCatalog.LurePrices[variant];
            bool owned=progress.Data.OwnsLure(variant);
            bool equipped=owned && progress.Data.baitEquipped==ShopCatalog.StarterLure && progress.Data.lureEquipped==variant;
            string action;
            bool interactable;

            if(equipped)
            {
                action="EQUIPPED";
                interactable=false;
            }
            else if(owned)
            {
                action="EQUIP";
                interactable=!progress.ReadOnly;
            }
            else
            {
                action="EQUIP";
                interactable=false;
            }

            if(view.buttonLabel!=null)view.buttonLabel.text=action;
            view.button.gameObject.name=action;
            view.button.interactable=interactable;
            if(view.buttonImage!=null)view.buttonImage.color=interactable?Teal:Disabled;
        }
    }

    private void SelectLure(int variant)
    {
        bool changed=progress.EquipLure(variant);
        if(!changed)
        {
            RefreshRows();
            return;
        }

        ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);
        SyncShortcutName();
        // Do not destroy/recreate this section here. Keeping the same row tree
        // avoids the old scroll-to-top behavior while the save/UI refresh runs.
        RefreshRows();
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
        text.resizeTextMinSize=22;
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
        ColorBlock colors=button.colors;
        colors.disabledColor=new Color(.4f,.4f,.4f,.45f);
        button.colors=colors;
        Text label=CreateText(go.transform,caption,28,Color.white,TextAnchor.MiddleCenter);
        Full(label.rectTransform);
        return button;
    }

    private GameObject Panel(string name,Transform parent,Color color)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);
        go.GetComponent<Image>().color=color;
        return go;
    }

    private void SyncShortcutName()
    {
        if(progress.Data.baitEquipped!=ShopCatalog.StarterLure)return;
        RawImage[] raws=hud.GetComponentsInChildren<RawImage>(true);
        Transform shortcut=null;
        for(int i=0;i<raws.Length;i++)
            if(raws[i]!=null && raws[i].name=="EquippedBaitPicture")
            {
                Button owner=raws[i].GetComponentInParent<Button>();
                shortcut=owner!=null?owner.transform:null;
                break;
            }
        if(shortcut==null)return;
        Text[] labels=shortcut.GetComponentsInChildren<Text>(true);
        for(int i=0;i<labels.Length;i++)
        {
            Text label=labels[i];
            if(label==null || string.IsNullOrWhiteSpace(label.text))continue;
            for(int lure=0;lure<ShopCatalog.LureNames.Length;lure++)
            {
                if(string.Equals(label.text,ShopCatalog.LureNames[lure],StringComparison.OrdinalIgnoreCase) || string.Equals(label.text,ShopCatalog.LureNames[lure].ToUpperInvariant(),StringComparison.OrdinalIgnoreCase))
                {
                    label.text=ShopCatalog.LureNames[progress.Data.lureEquipped].ToUpperInvariant();
                    return;
                }
            }
        }
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
