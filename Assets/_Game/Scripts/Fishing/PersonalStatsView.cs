using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Personal stats use the shared shop frame and original supplied artwork.</summary>
public static class PersonalStatsView
{
    private static readonly Dictionary<string, Sprite> artwork = new Dictionary<string, Sprite>();
    private static readonly Color Cyan = new Color(.1f,.95f,1f);
    private static readonly Color Detail = new Color(.67f,.89f,.96f);
    public static Sprite Art(string name, bool locked = false, bool trimTransparent = false)
    {
        string key = name + (locked ? "_Locked" : "") + (trimTransparent ? "_Trimmed" : "");
        if(artwork.TryGetValue(key,out Sprite sprite))return sprite;
        var source=Resources.Load<TextAsset>("Progression/"+name+".png");
        if(source==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        // Ability background PNGs have different amounts of transparent padding.
        // Keep the original textures for other UI, but crop the ability cards to
        // their visible artwork so all four drawn borders share one card rectangle.
        bool readable = locked || trimTransparent;
        if(!texture.LoadImage(source.bytes,!readable)){Object.Destroy(texture);return null;}
        Color32[] pixels=readable?texture.GetPixels32():null;
        if(locked)
        {
            // Cache a genuinely desaturated version once; preserve the supplied alpha.
            // Unlocked cards and combat buttons continue to use the original artwork.
            for(int i=0;i<pixels.Length;i++)
            {
                Color32 c=pixels[i];
                byte grey=(byte)Mathf.RoundToInt((c.r*.2126f+c.g*.7152f+c.b*.0722f)*.65f);
                pixels[i]=new Color32(grey,grey,grey,c.a);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
        }
        texture.wrapMode=TextureWrapMode.Clamp;
        // Keep original pixels; the card artwork includes transparent outer padding.
        Rect spriteRect=new Rect(0,0,texture.width,texture.height);
        if(trimTransparent && pixels!=null)
        {
            int minX=texture.width, minY=texture.height, maxX=-1, maxY=-1;
            for(int y=0;y<texture.height;y++)
            for(int x=0;x<texture.width;x++)
            {
                if(pixels[y*texture.width+x].a<=12)continue;
                minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);
                minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            }
            if(maxX>=minX && maxY>=minY)
            {
                int pad=Mathf.Max(1,Mathf.RoundToInt(Mathf.Min(texture.width,texture.height)*.004f));
                minX=Mathf.Max(0,minX-pad);minY=Mathf.Max(0,minY-pad);
                maxX=Mathf.Min(texture.width-1,maxX+pad);maxY=Mathf.Min(texture.height-1,maxY+pad);
                spriteRect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
            }
            // The sprite can still render normally after the source texture becomes non-readable.
            if(!locked)texture.Apply(false,true);
        }
        sprite=Sprite.Create(texture,spriteRect,new Vector2(.5f,.5f),100);
        artwork[key]=sprite;
        Resources.UnloadAsset(source);
        return sprite;
    }
    public static void Place(RectTransform rect,float x0,float y0,float x1,float y1,float inset=0)
    {
        rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);
        rect.offsetMin=new Vector2(inset,inset);rect.offsetMax=new Vector2(-inset,-inset);
    }
    private static GameObject Panel(Transform parent,string name,float x0,float y0,float x1,float y1,bool glass=true)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        Place(go.GetComponent<RectTransform>(),x0,y0,x1,y1,4);
        go.GetComponent<Image>().raycastTarget=false;
        if(glass)FishingHudTheme.Panel(go);else go.GetComponent<Image>().color=Color.clear;
        return go;
    }
    public static Text Label(Transform parent,string value,int size,float x0,float y0,float x1,float y1,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var go=new GameObject("Caption",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=12;t.resizeTextMaxSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;
        Place(t.rectTransform,x0,y0,x1,y1,3);return t;
    }
    private static void Meter(Transform parent,float value,float x0,float y0,float x1,float y1)
    {
        var track=Panel(parent,"Meter",x0,y0,x1,y1);FishingHudTheme.Panel(track,3);
        var fill=Panel(track.transform,"Fill",0,0,Mathf.Clamp01(value),1,false);
        fill.GetComponent<Image>().color=Cyan;FishingHudTheme.Fill(fill.GetComponent<Image>());
        Place(fill.GetComponent<RectTransform>(),0,0,Mathf.Clamp01(value),1,0);
    }
    public static void AddMenuIcon(Button button)
    {
        var icon=Panel(button.transform,"PersonalStatsShield",.02f,.12f,.14f,.88f,false).GetComponent<Image>();
        icon.sprite=Art("LevelShield");icon.color=Color.white;icon.preserveAspect=true;
    }
    public static GameObject Build(Transform parent,AnglerProgression data,ShopProgress progress=null)
    {
        if(data==null)data=new AnglerProgression();
        var root=Panel(parent,"PersonalStatsContent",.012f,.025f,.988f,1f,false);
        // Tuck the entire three-panel layout beneath the CLOSE button.
        root.GetComponent<RectTransform>().offsetMax=new Vector2(-4f,-94f);
        var band=Panel(root.transform,"LevelAndExperience",0,.815f,1,1);
        var shield=Panel(band.transform,"LevelShield",.012f,.04f,.14f,.96f,false).GetComponent<Image>();
        shield.sprite=Art("LevelShield");shield.color=Color.white;shield.preserveAspect=true;
        Label(shield.transform,"LV\n"+data.level,30,.19f,.18f,.81f,.82f,Color.white,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;
        Label(band.transform,"LEVEL "+data.level,32,.15f,.55f,.61f,.94f,Color.white).fontStyle=FontStyle.Bold;
        bool max=data.level>=AnglerProgression.MaxLevel;
        Label(band.transform,max?"MAX LEVEL":$"{data.experience:N0} / {AnglerProgression.RequiredXp(data.level):N0} XP",22,.15f,.30f,.61f,.59f,Cyan);
        Meter(band.transform,max?1f:(float)(data.experience/AnglerProgression.RequiredXp(data.level)),.15f,.12f,.61f,.29f);
        int upgradePoints=data.AvailableAbilityPoints;
        string next="Next special ability upgrade:\n"+
            (upgradePoints>0 ? upgradePoints+" UPGRADE"+(upgradePoints==1?"":"S")+" AVAILABLE"
            :data.level>=AnglerProgression.MaxLevel ? "MAX LEVEL"
            :"LEVEL "+((data.level/20+1)*20));
        Label(band.transform,next,26,.66f,.18f,.97f,.85f,Detail);
        var left=Panel(root.transform,"FishingAttributes",0,0,.505f,.805f);
        Label(left.transform,"FISHING ATTRIBUTES",29,.03f,.89f,.97f,.99f,Color.white).fontStyle=FontStyle.Bold;
        string[] descriptions={"Extra damage per reel hit","Boosts damage after the raw bonus","Retrieve line faster","Reduces tension gain","Bonus damage for your charged yank","Permanent larger-fish weight bonus"};
        // Even gaps and identical row heights throughout the enlarged panel.
        const float statTop=.88f, statBottom=.028f, statGap=.012f;
        float statHeight=(statTop-statBottom-5f*statGap)/6f;
        for(int i=0;i<6;i++)
        {
            float top=statTop-i*(statHeight+statGap);
            var row=Panel(left.transform,"Stat_"+i,.02f,top-statHeight,.98f,top);
            Label(row.transform,AnglerProgression.Names[i],25,.03f,.49f,.78f,.97f,Color.white).fontStyle=FontStyle.Bold;
            Label(row.transform,descriptions[i],17,.03f,.22f,.80f,.53f,Detail);
            Label(row.transform,"+"+data.Value(i)+(i==0?"":"%"),26,.80f,.25f,.98f,.91f,Cyan,TextAnchor.MiddleRight);
            Meter(row.transform,data.Value(i)/100f,.03f,.07f,.78f,.21f);
        }
        var right=Panel(root.transform,"SpecialAbilities",.51f,0,1,.805f);
        Label(right.transform,"SPECIAL ABILITIES",29,.03f,.90f,.97f,.99f,Color.white).fontStyle=FontStyle.Bold;
        Label(right.transform,"ONE ABILITY UPGRADE POINT EVERY 20 LEVELS",16,.03f,.85f,.97f,.91f,Detail);
        string[] descriptionsAbility={
            "After every skill use, zaps the fish 8 times over 4 seconds, dealing "+(data.BurningTotalFraction*100f).ToString("0.#")+"% of its maximum health as damage, up to 2,000 damage. The first zap deals half; the remaining seven share the rest.",
            "Automatically deals "+(data.HooksetDamageFraction*100f).ToString("0.#")+"% of max HP when hooking, up to 4,000 damage.",
            "Automatically triggers upon line reaching the maximum length. Exceed the limit and reel without tension gain or line breaks for "+data.ImmortalityDuration.ToString("0.#")+"s. Once per fight.",
            "The fish swims straight toward you at 2 m/s for "+data.SirenDuration.ToString("0.#")+"s after activation. Once per fight."
        };
        // A full-page touch target closes the larger full-color preview.
        // It belongs to the stats content so closing/rebuilding this page
        // automatically removes the overlay. Its bounds reach the CLOSE header.
        var preview=new GameObject("SpecialAbilityDetailPreview",
            typeof(RectTransform),typeof(Image),typeof(Button));
        preview.transform.SetParent(root.transform,false);
        var previewRect=preview.GetComponent<RectTransform>();
        Place(previewRect,0f,0f,1f,1f,0f);
        previewRect.offsetMin=new Vector2(-30f,-30f);
        previewRect.offsetMax=new Vector2(30f,138f);
        var previewShade=preview.GetComponent<Image>();
        previewShade.color=new Color(.005f,.02f,.055f,.78f);
        previewShade.raycastTarget=true;
        var dismiss=preview.GetComponent<Button>();
        dismiss.targetGraphic=previewShade;
        dismiss.transition=Selectable.Transition.None;
        dismiss.onClick.AddListener(()=>preview.SetActive(false));

        // Show a scaled COPY of the actual small card. This preserves its exact
        // text anchors, text, card proportions, and source artwork, instead of
        // rebuilding a second layout that can diverge from the small cards.
        // Only the card art/tints are restored to full color; no white pulse.
        GameObject previewCard=null;

        // An independent button consumes taps only when the player chooses to
        // spend an earned point. Every other tap still closes the enlarged card.
        var upgradeControl=Panel(preview.transform,"ApplySpecialAbilityUpgrade",
            .32f,.135f,.68f,.23f,false);
        FishingHudTheme.Panel(upgradeControl,1);
        var upgradeImage=upgradeControl.GetComponent<Image>();
        upgradeImage.raycastTarget=true;
        var upgradeButton=upgradeControl.AddComponent<Button>();
        upgradeButton.targetGraphic=upgradeImage;
        var upgradeLabel=Label(upgradeControl.transform,"UPGRADE ABILITY",24,
            .02f,.07f,.98f,.93f,Color.white,TextAnchor.MiddleCenter);
        upgradeLabel.fontStyle=FontStyle.Bold;
        int viewingAbility=-1;
        upgradeButton.onClick.AddListener(()=>
        {
            if(progress==null || viewingAbility<0 || !progress.UpgradeFishingAbility(viewingAbility))return;
            preview.SetActive(false);
        });
        var dismissHint=Label(preview.transform,"TAP ANYWHERE ELSE TO CLOSE",19,
            .15f,.065f,.85f,.135f,Color.white,TextAnchor.MiddleCenter);
        dismissHint.fontStyle=FontStyle.Bold;
        preview.SetActive(false);

        // Uniform visible card sizes (including the PNG border), inset equally
        // from both sides, with tighter horizontal and vertical card spacing.
        // The lower third remains blank for future special abilities.
        const float sidePad=.025f, cardGapX=.022f, cardGapY=.029f;
        // Increase height only; retain the previous card widths and side gaps.
        const float cardsTop=.81f, cardsBottom=.17f;
        float cardWidth=(1f-sidePad*2f-cardGapX)/2f;
        float cardHeight=(cardsTop-cardsBottom-cardGapY)/2f;
        for(int i=0;i<4;i++)
        {
            int column=i%2, row=i/2;
            float x=sidePad+column*(cardWidth+cardGapX);
            float top=cardsTop-row*(cardHeight+cardGapY);
            var card=Panel(right.transform,"Ability_"+i,x,top-cardHeight,x+cardWidth,top,false);
            bool unlocked=data.Unlocked(i);
            Color titleColor=unlocked?Color.white:new Color(.72f,.72f,.72f);
            Color detailColor=unlocked?Detail:new Color(.58f,.58f,.58f);
            Color accentColor=unlocked?Cyan:new Color(.66f,.66f,.66f);
            var art=card.GetComponent<Image>();
            art.sprite=Art(AnglerProgression.AbilityAssets[i],!unlocked,true);
            art.color=Color.white;art.preserveAspect=false;
            art.raycastTarget=true;
            var cardButton=card.AddComponent<Button>();
            cardButton.targetGraphic=art;
            cardButton.transition=Selectable.Transition.None;

            // One rounded, semi-transparent navy-glass textbox behind all four
            // labels. Use the same nine-slice surface as Fishing Attributes so
            // the ability art remains visible while the text stays readable.
            // The enlarged preview clones this entire small card, preserving
            // the exact textbox proportions and position at every scale.
            var textBackdrop=Panel(card.transform,"AbilityTextBackdrop",
                .035f,.035f,.965f,.965f,false);
            FishingHudTheme.Panel(textBackdrop);
            var textGlass=textBackdrop.GetComponent<Image>();
            textGlass.color=new Color(.65f,.80f,1f,.62f);
            textGlass.raycastTarget=false;

            int ability=i; // Capture the intended card, even after the loop.
            cardButton.onClick.AddListener(()=>
            {
                viewingAbility=ability;
                int rank=data.AbilityRank(ability);
                bool active=rank>0;
                bool canUpgrade=data.AvailableAbilityPoints>0 && progress!=null && !progress.ReadOnly;

                if(previewCard!=null)
                {
                    previewCard.SetActive(false);
                    Object.Destroy(previewCard);
                }
                // Clone this exact card, including all four existing text labels.
                // A uniform local scale makes text and background grow together.
                previewCard=Object.Instantiate(card,preview.transform,false);
                previewCard.name="FullColorEnlargedAbility_"+ability;
                var pulseCopy=previewCard.GetComponent<SpecialAbilityUpgradePulse>();
                if(pulseCopy!=null)pulseCopy.enabled=false;
                // The pulse's translucent overlay can be copied at any point in
                // its animation; hide both cloned overlays unconditionally.
                Transform flashBorder=previewCard.transform.Find("UpgradeFlashingWhiteBorder");
                if(flashBorder!=null)flashBorder.gameObject.SetActive(false);
                Transform whiteTone=previewCard.transform.Find("UpgradeWhiteTone");
                if(whiteTone!=null)whiteTone.gameObject.SetActive(false);
                var enlargedImage=previewCard.GetComponent<Image>();
                enlargedImage.sprite=Art(AnglerProgression.AbilityAssets[ability],false,true);
                enlargedImage.color=Color.white;
                enlargedImage.preserveAspect=false; // Same image scaling as the small card.
                foreach(Graphic g in previewCard.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget=false;
                var copyButton=previewCard.GetComponent<Button>();
                if(copyButton!=null)copyButton.enabled=false;
                Text[] copyLabels=previewCard.GetComponentsInChildren<Text>(true);
                if(copyLabels.Length>=4)
                {
                    copyLabels[0].color=Color.white;
                    copyLabels[1].color=Cyan;
                    copyLabels[2].color=Detail;
                    copyLabels[3].color=Cyan;
                }
                var originalRect=card.GetComponent<RectTransform>();
                var enlargedRect=previewCard.GetComponent<RectTransform>();
                enlargedRect.anchorMin=enlargedRect.anchorMax=new Vector2(.5f,.54f);
                enlargedRect.pivot=new Vector2(.5f,.5f);
                enlargedRect.sizeDelta=originalRect.rect.size;
                enlargedRect.anchoredPosition=Vector2.zero;
                var overlayRect=preview.GetComponent<RectTransform>();
                float scale=Mathf.Min(2.35f,
                    Mathf.Min(overlayRect.rect.width*.75f/Mathf.Max(1f,originalRect.rect.width),
                    overlayRect.rect.height*.48f/Mathf.Max(1f,originalRect.rect.height)));
                enlargedRect.localScale=Vector3.one*Mathf.Max(1f,scale);
                previewCard.transform.SetSiblingIndex(0);
                upgradeControl.SetActive(canUpgrade);
                upgradeLabel.text=active?"UPGRADE TO RANK "+(rank+1):"UNLOCK ABILITY";
                preview.transform.SetAsLastSibling();
                preview.SetActive(true);
            });
            var cardTitle=Label(card.transform,AnglerProgression.AbilityNames[i],22,.08f,.72f,.92f,.92f,titleColor);
            cardTitle.fontStyle=FontStyle.Bold;
            var cardRank=Label(card.transform,unlocked?"RANK "+data.AbilityRank(i):"NOT UNLOCKED",
                16,.08f,.59f,.92f,.72f,accentColor);
            var cardDescription=Label(card.transform,descriptionsAbility[i],18,.08f,.23f,.92f,.59f,detailColor);
            cardDescription.resizeTextMinSize=8;
            var cardStatus=Label(card.transform,upgradePoints>0?"UPGRADE AVAILABLE":unlocked?"UNLOCKED":"LOCKED",
                16,.08f,.08f,.92f,.23f,accentColor);
            if(upgradePoints>0 && progress!=null && !progress.ReadOnly)
            {
                var pulse=card.AddComponent<SpecialAbilityUpgradePulse>();
                pulse.Initialize(art,cardTitle,cardRank,cardDescription,cardStatus);
            }
        }
        return root;
    }
}
