using UnityEngine;
using UnityEngine.UI;

public sealed class AbyssalGemHotbar : MonoBehaviour
{
    private ShopProgress progress;
    private Image picture;
    private Outline glow;
    private Text caption;
    private Button button;
    private float pulse;
    public void Initialize(ShopProgress source)
    {
        progress=source;
        var icon=new GameObject("Equipped Abyssal Gem",typeof(RectTransform),typeof(Image));icon.transform.SetParent(transform,false);
        picture=icon.GetComponent<Image>();picture.sprite=AbyssalGemView.Art("AbyssalGem");picture.preserveAspect=true;picture.raycastTarget=false;
        PersonalStatsView.Place(picture.rectTransform,.12f,.12f,.88f,.92f,0);
        caption=PersonalStatsView.Label(transform,"",18,0,0,1,.26f,FishingHudTheme.Cyan,TextAnchor.MiddleCenter);
        glow=GetComponent<Outline>();
        button=gameObject.AddComponent<Button>();button.targetGraphic=GetComponent<Image>();
        button.onClick.AddListener(()=>{
            if(progress.Data.gems.Equipped!=null)FindFirstObjectByType<ShopWorldHUD>()?.OpenGem(progress.Data.gems.equippedId);
        });
        if(progress!=null){progress.Changed+=Refresh;progress.CrystalDustAwarded+=OnDust;}
        Refresh();
    }
    private void Refresh()
    {
        bool equipped=progress!=null && progress.Data.gems.Equipped!=null;
        picture.enabled=equipped;button.interactable=equipped;
        if(pulse<=0)caption.text=equipped?"Lv. "+progress.Data.gems.Equipped.level:"";
    }
    private void OnDust(int amount){pulse=2.5f;caption.text="+"+amount+" DUST";}
    private void Update()
    {
        if(button!=null)button.interactable=progress!=null && progress.Data.gems.Equipped!=null && progress.CanManageGems;
        if(pulse<=0)return;
        pulse=Mathf.Max(0,pulse-Time.unscaledDeltaTime);
        if(glow!=null){glow.effectColor=Color.Lerp(new Color(.22f,.28f,.31f,.7f),FishingHudTheme.Cyan,.5f+.5f*Mathf.Sin(pulse*12));glow.effectDistance=new Vector2(5,-5);}
        if(pulse<=0){if(glow!=null){glow.effectColor=new Color(.22f,.28f,.31f,.7f);glow.effectDistance=new Vector2(2,-2);}Refresh();}
    }
    private void OnDestroy(){if(progress!=null){progress.Changed-=Refresh;progress.CrystalDustAwarded-=OnDust;}}
}
