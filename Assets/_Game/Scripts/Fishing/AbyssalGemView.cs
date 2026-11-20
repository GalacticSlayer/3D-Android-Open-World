using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class AbyssalGemView
{
    private static readonly Dictionary<string,Sprite> artwork=new Dictionary<string,Sprite>();
    public static Sprite Art(string name)
    {
        if(artwork.TryGetValue(name,out var sprite))return sprite;
        var bytes=Resources.Load<TextAsset>("Fishing/Gems/"+name+".png");
        if(bytes==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!texture.LoadImage(bytes.bytes,true)){UnityEngine.Object.Destroy(texture);return null;}
        texture.wrapMode=TextureWrapMode.Clamp;
        sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
        artwork[name]=sprite;Resources.UnloadAsset(bytes);return sprite;
    }
    private static void Place(RectTransform r,float x0,float y0,float x1,float y1,float inset=4)
        =>PersonalStatsView.Place(r,x0,y0,x1,y1,inset);
    private static GameObject Panel(Transform parent,string name,float x0,float y0,float x1,float y1,int style=0)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        Place(go.GetComponent<RectTransform>(),x0,y0,x1,y1);
        FishingHudTheme.Panel(go,style);return go;
    }
    private static Text Label(Transform parent,string text,int size,float x0,float y0,float x1,float y1,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        =>PersonalStatsView.Label(parent,text,size,x0,y0,x1,y1,color,alignment);
    private static Button Button(Transform parent,string title,float x0,float y0,float x1,float y1,Action action,int style=0,bool interactable=true)
    {
        var go=Panel(parent,title,x0,y0,x1,y1,style);
        // Apply availability before OnEnable paints the first frame. A rebuilt
        // unaffordable upgrade must not fade from the default enabled tint.
        go.SetActive(false);
        var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
        button.interactable=interactable;
        if(!interactable)
        {
            var colors=button.colors;
            colors.disabledColor=new Color(.45f,.45f,.45f,.8f);
            button.colors=colors;
        }
        button.onClick.AddListener(()=>action());
        Label(go.transform,title,25,.04f,.06f,.96f,.94f,Color.white,TextAnchor.MiddleCenter);
        go.SetActive(true);
        return button;
    }
    private static Image Picture(Transform parent,string name,float x0,float y0,float x1,float y1)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.sprite=Art(name);image.preserveAspect=true;image.raycastTarget=false;
        Place(image.rectTransform,x0,y0,x1,y1);
        return image;
    }
    public static string Value(AbyssalGem gem,int slot)=>"+"+gem.values[slot].ToString("0.#")+(gem.attributes[slot]==6?" m":"%");
    public static string Summary(AbyssalGem gem)
    {
        var parts=new List<string>();
        for(int i=0;i<4;i++)parts.Add(AbyssalGemCollection.Names[gem.attributes[i]]+" "+Value(gem,i));
        return string.Join("  /  ",parts);
    }
    public static void Inventory(Transform parent,Transform modal,ShopProgress progress,Action<string> inspect,Action refresh,Action beginReset,bool resetting=false)
    {
        foreach(var gem in progress.Data.gems.SortedItems)
        {
            var row=Panel(parent,"Abyssal Gem inventory item",0,0,1,1);
            row.AddComponent<LayoutElement>().preferredHeight=230;
            Picture(row.transform,"AbyssalGem",.01f,.08f,.17f,.92f);
            Label(row.transform,"ABYSSAL GEM  •  LEVEL "+gem.level,30,.18f,.68f,.74f,.96f,Color.white);
            Label(row.transform,Summary(gem),22,.18f,.25f,.74f,.68f,new Color(.7f,.9f,1f));
            if(resetting)
            {
                Button(row.transform,"RESET GEM",.76f,.31f,.99f,.69f,()=>ConfirmReset(modal,progress,gem.id,refresh),8,progress.CanManageGems);
                continue;
            }
            Button(row.transform,"VIEW ATTRIBUTES",.18f,.03f,.5f,.26f,()=>inspect(gem.id));
            var equip=Button(row.transform,progress.Data.gems.equippedId==gem.id?"UNEQUIP":"EQUIP",.76f,.53f,.99f,.91f,()=>{progress.EquipGem(gem.id);refresh();},1);
            equip.interactable=progress.CanManageGems;
            var dismantle=Button(row.transform,"BREAK DOWN",.76f,.09f,.99f,.47f,()=>Confirm(modal,progress,gem.id,refresh),8);
            dismantle.interactable=progress.CanManageGems;
        }
        if(!resetting && progress.Data.gems.HasResetDial)
        {
            var row=Panel(parent,"Gem Reset Dial inventory item",0,0,1,1);
            row.AddComponent<LayoutElement>().preferredHeight=230;
            Picture(row.transform,"GemResetDial",.01f,.08f,.17f,.92f);
            Label(row.transform,"GEM RESET DIAL",30,.18f,.68f,.74f,.96f,Color.white);
            Button(row.transform,"RESET GEMS",.76f,.31f,.99f,.69f,beginReset,11,progress.CanManageGems);
        }
    }
    private static void ConfirmReset(Transform parent,ShopProgress progress,string id,Action refresh)
    {
        var gem=progress.Data.gems.Find(id);if(gem==null)return;
        var shade=new GameObject("Permanent gem reset confirmation",typeof(RectTransform),typeof(Image));
        shade.AddComponent<AbyssalGemConfirmation>();
        shade.transform.SetParent(parent,false);Place(shade.GetComponent<RectTransform>(),0,0,1,1,0);
        shade.GetComponent<Image>().color=new Color(0,0,0,.8f);
        var box=Panel(shade.transform,"Confirm reset",.2f,.22f,.8f,.78f);
        Label(box.transform,"RESET ABYSSAL GEM?",34,.06f,.72f,.94f,.94f,Color.white);
        Label(box.transform,"Reset this Level "+gem.level+" gem to Level 1 and Scale Piercer to 1.50×. Reroll all four substat values at their starting ranges; attribute types stay the same.\n\nAll upgrades are lost. No Crystal Dust is refunded. This cannot be undone.",26,.06f,.28f,.94f,.72f,Color.white);
        Button(box.transform,"CANCEL",.05f,.05f,.47f,.24f,()=>UnityEngine.Object.Destroy(shade));
        Button(box.transform,"RESET GEM",.5f,.05f,.95f,.24f,()=>{
            progress.ResetGem(id);UnityEngine.Object.Destroy(shade);refresh();
        },8,progress.CanManageGems);
    }
    private static void Confirm(Transform parent,ShopProgress progress,string id,Action refresh)
    {
        var shade=new GameObject("Permanent gem breakdown confirmation",typeof(RectTransform),typeof(Image));
        shade.AddComponent<AbyssalGemConfirmation>();
        shade.transform.SetParent(parent,false);Place(shade.GetComponent<RectTransform>(),0,0,1,1,0);
        shade.GetComponent<Image>().color=new Color(0,0,0,.8f);
        var box=Panel(shade.transform,"Confirm breakdown",.2f,.22f,.8f,.78f);
        Label(box.transform,"BREAK DOWN ABYSSAL GEM?",34,.06f,.72f,.94f,.94f,Color.white);
        var gem=progress.Data.gems.Find(id);
        int refund=AbyssalGemCollection.BreakdownDust(gem?.level??1);
        Label(box.transform,"Permanently destroy this Level "+(gem?.level??1)+" gem for "+refund+" Crystal Dust (10 base + 20% of upgrade dust).\n\nThis cannot be undone. "+(progress.Data.gems.equippedId==id?"Your equipped gem will be removed.":""),26,.06f,.28f,.94f,.72f,Color.white);
        Button(box.transform,"CANCEL",.05f,.05f,.47f,.24f,()=>UnityEngine.Object.Destroy(shade));
        Button(box.transform,"BREAK DOWN • +"+refund+" DUST",.5f,.05f,.95f,.24f,()=>{
            progress.BreakDownGem(id);UnityEngine.Object.Destroy(shade);refresh();
        },8);
    }
    public static GameObject Build(Transform parent,ShopProgress progress,string id,int selected,Action<int> select,Action upgrade)
    {
        var root=new GameObject("Abyssal Gem Attributes",typeof(RectTransform));root.transform.SetParent(parent,false);
        Place(root.GetComponent<RectTransform>(),.015f,.02f,.985f,.86f,0);
        var gem=progress.Data.gems.Find(id);
        if(gem==null){Label(root.transform,"Equip an Abyssal Gem from your inventory.",32,0,0,1,1,Color.white);return root;}
        var left=Panel(root.transform,"Pentagon",0,.17f,.61f,1);
        var right=Panel(root.transform,"Attribute details",.615f,.17f,1,1);
        LargeShopBackdropTheme.Apply(left.GetComponent<Image>());
        LargeShopBackdropTheme.Apply(right.GetComponent<Image>());
        // Five vertex positions in local panel coordinates, with Scale Piercer at the apex.
        Vector2[] points={new Vector2(.5f,.84f),new Vector2(.22f,.57f),new Vector2(.32f,.16f),new Vector2(.68f,.16f),new Vector2(.78f,.57f)};
        var lines=new GameObject("Pentagon connectors",typeof(RectTransform),typeof(AbyssalGemConnections));
        lines.transform.SetParent(left.transform,false);Place(lines.GetComponent<RectTransform>(),0,0,1,1,0);
        lines.GetComponent<AbyssalGemConnections>().raycastTarget=false;
        Picture(left.transform,"AbyssalGem",.34f,.26f,.66f,.73f);
        for(int i=0;i<5;i++)
        {
            int choice=i;var pos=points[i];
            string title=i==0?"<color=#FFEA00>PRIMARY</color>\nSCALE PIERCER":AbyssalGemCollection.Names[gem.attributes[i-1]].ToUpperInvariant();
            var statButton=Button(left.transform,title,pos.x-.17f,pos.y-.115f,pos.x+.17f,pos.y+.115f,()=>select(choice),i==0?(selected==0?10:6):selected==i?1:0);
            Place(statButton.GetComponentInChildren<Text>().rectTransform,.04f,.35f,.96f,.96f);
            string statValue=i==0?gem.ScalePiercer.ToString("0.00")+"×":Value(gem,i-1);
            var valueLabel=Label(statButton.transform,statValue,29,.04f,.08f,.96f,.47f,new Color(1f,.86f,.28f),TextAnchor.MiddleCenter);
            var outline=valueLabel.gameObject.AddComponent<Outline>();
            outline.effectColor=Color.black;
            outline.effectDistance=new Vector2(1f,-1f);
        }
        selected=Mathf.Clamp(selected,0,4);
        Label(right.transform,"ATTRIBUTE DETAILS",31,.06f,.86f,.94f,.99f,new Color(1f,.86f,.28f));
        string name=selected==0?"SCALE PIERCER":AbyssalGemCollection.Names[gem.attributes[selected-1]].ToUpperInvariant();
        Label(right.transform,name,34,.06f,.66f,.94f,.86f,Color.white);
        Label(right.transform,Description(gem,selected),26,.06f,.13f,.94f,.66f,Color.white,TextAnchor.UpperLeft);

        var footer=Panel(root.transform,"Gem progression",0,0,1,.16f);
        Label(footer.transform,"LEVEL "+gem.level+" / 15\n"+(gem.level==15?"MAX LEVEL":"Scale Piercer +0.05× and one random attribute"),24,.02f,.05f,.45f,.95f,Color.white);
        bool canUpgrade=progress.CanManageGems && gem.level<15 && progress.Data.gems.dust>=AbyssalGemCollection.UpgradeCost(gem.level);
        var upgradeButton=Button(footer.transform,gem.level==15?"MAX LEVEL":"LEVEL UP GEM\n"+AbyssalGemCollection.UpgradeCost(gem.level)+" Crystal Dust",.70f,.06f,.99f,.94f,upgrade,1,canUpgrade);
        var upgradeLabel=upgradeButton.GetComponentInChildren<Text>();
        Place(upgradeLabel.rectTransform,.04f,.06f,.96f,.94f);
        // Keep the label centered on the entire button and enlarge the icon in place.
        var dustIcon=Picture(upgradeButton.transform,"CrystalDust",.04f,.18f,.24f,.82f);
        dustIcon.rectTransform.localScale=Vector3.one*2f;
        dustIcon.color=canUpgrade?Color.white:new Color(.45f,.45f,.45f,.8f);
        return root;
    }
    public static GameObject BuildDustWallet(Transform parent,RectTransform close,int dust)
    {
        var box=Panel(parent,"Crystal dust wallet",0,0,1,1);
        var rect=box.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one;
        rect.sizeDelta=new Vector2(0,close.sizeDelta.y);
        rect.anchoredPosition=close.anchoredPosition-new Vector2(close.sizeDelta.x+18f,0);
        box.GetComponent<Image>().raycastTarget=false;
        var layout=box.AddComponent<HorizontalLayoutGroup>();
        layout.padding=new RectOffset(22,26,0,0);layout.spacing=12;
        layout.childAlignment=TextAnchor.MiddleCenter;
        layout.childControlWidth=true;layout.childControlHeight=true;
        layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        var icon=Picture(box.transform,"CrystalDust",0,0,1,1);
        var iconSize=icon.gameObject.AddComponent<LayoutElement>();
        iconSize.minWidth=iconSize.preferredWidth=54;iconSize.preferredHeight=48;
        WalletLabel(box.transform,"CRYSTAL DUST",25,Color.white);
        WalletLabel(box.transform,"|",27,FishingHudTheme.Cyan);
        WalletLabel(box.transform,dust.ToString("N0"),30,FishingHudTheme.Cyan);
        // The right pivot keeps the gap to Close fixed. More digits increase
        // preferred width, so only the left edge moves when the balance grows.
        var fit=box.AddComponent<ContentSizeFitter>();
        fit.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
        return box;
    }
    private static void WalletLabel(Transform parent,string value,int size,Color color)
    {
        var label=Label(parent,value,size,0,0,1,1,color,TextAnchor.MiddleCenter);
        label.resizeTextForBestFit=false;label.fontStyle=FontStyle.Bold;
        label.horizontalOverflow=HorizontalWrapMode.Overflow;
        var layout=label.gameObject.AddComponent<LayoutElement>();
        layout.minWidth=layout.preferredWidth=Mathf.Ceil(label.preferredWidth);
        layout.preferredHeight=42;
    }
    // Built outside the detail view so a save-driven refresh cannot remove the result.
    // Values are captured at the successful upgrade, not re-read from the live gem.
    public static GameObject ShowUpgradeSuccess(Transform parent,int oldLevel,int newLevel,
        float oldPrimary,float newPrimary,int attribute,float oldValue,float newValue,Action dismiss)
    {
        var shade=new GameObject("Gem upgrade success overlay",typeof(RectTransform),typeof(Image));
        shade.transform.SetParent(parent,false);
        Place(shade.GetComponent<RectTransform>(),0,0,1,1,0);
        var dimmer=shade.GetComponent<Image>();
        dimmer.color=new Color(.12f,.14f,.16f,.76f);
        dimmer.raycastTarget=true; // Block further upgrades and all covered controls.
        var banner=Panel(shade.transform,"Upgrade success banner",.12f,.23f,.88f,.83f);
        Label(banner.transform,"ENHANCEMENT SUCCESS",36,.06f,.86f,.94f,.98f,Color.white,TextAnchor.MiddleCenter);
        var band=Panel(banner.transform,"Ocean blue level-up band",.012f,.37f,.988f,.85f,1);
        var bandImage=band.GetComponent<Image>();
        // Clip the supplied artwork to the theme's rounded nine-slice contour.
        FishingHudTheme.Fill(bandImage);
        bandImage.raycastTarget=false;
        band.AddComponent<Mask>().showMaskGraphic=false;
        var background=Picture(band.transform,"UpgradeSuccessBackground",0,0,1,1);
        Place(background.rectTransform,0,0,1,1,0);
        background.preserveAspect=false;
        var border=new GameObject("Cyan rounded border",typeof(RectTransform),typeof(Image));
        border.transform.SetParent(band.transform,false);
        var borderImage=border.GetComponent<Image>();
        Place(borderImage.rectTransform,0,0,1,1,0);
        FishingHudTheme.RoundedWhiteBorder(borderImage);
        borderImage.color=FishingHudTheme.Cyan;
        Picture(banner.transform,"AbyssalGem",.40f,.54f,.60f,.84f);
        Label(banner.transform,"LEVEL "+oldLevel+"  →  <color=#FFDB47>LEVEL "+newLevel+"</color>",36,.20f,.385f,.80f,.53f,
            Color.white,TextAnchor.MiddleCenter);
        UpgradeRow(banner.transform,"SCALE PIERCER",oldPrimary.ToString("0.00")+"×",
            newPrimary.ToString("0.00")+"×",.20f,.34f);
        string unit=attribute==6?" m":"%";
        UpgradeRow(banner.transform,AbyssalGemCollection.Names[attribute].ToUpperInvariant(),
            "+"+oldValue.ToString("0.#")+unit,"+"+newValue.ToString("0.#")+unit,.045f,.185f);
        Button(shade.transform,"CONTINUE",.39f,.12f,.61f,.21f,dismiss,1);
        shade.transform.SetAsLastSibling();
        return shade;
    }
    private static void UpgradeRow(Transform parent,string name,string before,string after,float bottom,float top)
    {
        var row=Panel(parent,name+" upgrade",.035f,bottom,.965f,top,3);
        row.GetComponent<Image>().raycastTarget=false;
        Label(row.transform,name,27,.025f,.08f,.46f,.92f,Color.white);
        Label(row.transform,before,28,.46f,.08f,.64f,.92f,new Color(.72f,.83f,.87f),TextAnchor.MiddleCenter);
        Label(row.transform,"→",32,.64f,.08f,.72f,.92f,FishingHudTheme.Cyan,TextAnchor.MiddleCenter);
        Label(row.transform,after,30,.72f,.08f,.91f,.92f,new Color(1f,.86f,.28f),TextAnchor.MiddleCenter);
        Label(row.transform,"↑",30,.91f,.08f,.98f,.92f,FishingHudTheme.Cyan,TextAnchor.MiddleCenter);
    }
    private static string Description(AbyssalGem gem,int selected)
    {
        if(selected==0)return "When you use a skill while the fish is Angry, it deals "+gem.ScalePiercer.ToString("0.00")+"× damage.\n\nIncreases by 0.05× every gem level, reaching 2.20× at Level 15. Lightning uses the state at skill activation.";
        int slot=selected-1,kind=gem.attributes[slot];
        string[] descriptions={"Reduces tension gained while fighting. Stacks with your rod and angler tension control.","Increases the speed at which tension recovers.","Increases reel attacks, high-drag damage and the charged skill's base attack.","Increases charged skill and Lightning Strike damage.","Adds to your critical damage multiplier (for example, +4% turns 2.00× into 2.04×).","Adds percentage points to your rod's critical hit chance.","Extends your maximum line length. Casting distance stays the same.","Chance to keep your lure when the line snaps. Chance is capped at 100%. Does not protect bait or a fight abandoned by quitting."};
        string unit=kind==6?" m":"%";
        return "CURRENT BONUS: "+Value(gem,slot)+"\n\n"+descriptions[kind]+"\n\nWhen this attribute is rolled on upgrade: +"+AbyssalGemCollection.Minimum[kind]+"–"+AbyssalGemCollection.Maximum[kind]+unit+".";
    }
}
