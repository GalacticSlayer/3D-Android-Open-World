using UnityEngine;

public sealed class ShopHabitat : MonoBehaviour
{
    public string id;
    public string instanceKey; // Stable Home instance; empty on shop displays and templates.
    public Transform interaction, entrance, exit, fishRoot;
    public ShopWaterVolume water;
    public BoxCollider purchaseBarrier;
    public TextMesh label;
    private ShopProgress progress;
    private int fishSignature=int.MinValue;
    private float visibilityTimer;
    private void Update()
    {
        if(progress==null || fishRoot==null) return;
        visibilityTimer-=Time.deltaTime; if(visibilityTimer>0) return; visibilityTimer=0.5f;
        var d=ShopCatalog.Habitat(id);
        float range=55f+Mathf.Max(d.width,d.depth)*0.6f;
        bool visible=(progress.transform.position-transform.position).sqrMagnitude<range*range;
        if(fishRoot.gameObject.activeSelf!=visible) fishRoot.gameObject.SetActive(visible);
    }
    private void Start()
    {
        var definition=ShopCatalog.Habitat(id);
        if(definition!=null && !definition.pond && definition.swimmable && GetComponent<AquariumAccessStyle>()==null)
            gameObject.AddComponent<AquariumAccessStyle>();
        progress=FindFirstObjectByType<ShopProgress>();
        if(progress!=null) { progress.Changed+=Refresh; Refresh(); }
    }
    private void OnDestroy() { if(progress!=null) progress.Changed-=Refresh; }
    private void Refresh()
    {
        var d=ShopCatalog.Habitat(id);
        bool home=GetComponentInParent<ShopWorldEnvironment>().isHome;
        var owned=progress.Data.Habitat(!string.IsNullOrEmpty(instanceKey)?instanceKey:id);
        bool purchased=home && owned!=null &&
            (d.pond || !string.IsNullOrEmpty(instanceKey) && owned.placed);
        water.available=purchased && d.swimmable;
        if(purchaseBarrier!=null) purchaseBarrier.enabled=!purchased || !d.swimmable;
        float kg=0; int signature=purchased?17:0;
        if(purchased) foreach(var f in owned.fish) { kg+=f.weightKg; unchecked { signature=signature*31+f.GetHashCode(); } }
        if(label!=null)
        {
            int next=d.pond?progress.Data.UpgradeCost(id):progress.Data.NextAquariumPrice(id);
            string offer=d.pond?$"{d.price:N0} TOTAL VALUE  |  UPGRADE FOR HOME":
                (next<0?"MAXIMUM PURCHASE PRICE REACHED":$"{next:N0} COINS  |  BUY FOR HOME");
            label.text=d.name.ToUpperInvariant()+
                (purchased && !d.pond?"  ["+instanceKey.ToUpperInvariant()+"]":"")+"\n"+
                (purchased?$"OWNED  |  {owned.fish.Count}/{d.fishLimit} FISH  |  {kg:0.0}/{d.totalKg:0} KG":offer)+
                $"\nMAX {d.maxFishKg:0.0} KG EACH";
        }
        var sign=label!=null?label.GetComponent<ShopSign>():null; if(sign!=null)sign.Fit();
        if(signature==fishSignature) return;
        fishSignature=signature;
        fishRoot.gameObject.SetActive(true);
        for(int i=fishRoot.childCount-1;i>=0;i--) { fishRoot.GetChild(i).gameObject.SetActive(false); Destroy(fishRoot.GetChild(i).gameObject); }
        if(!purchased) return;
        for(int i=0;i<owned.fish.Count;i++)
        {
            var record=owned.fish[i];
            var fish=FishWorldSize.Create("Resident_"+i,fishRoot,record.speciesId,record.weightKg);
            float length=ShopCatalog.FishLength(record.speciesId,record.weightKg);
            var agent=fish.AddComponent<TankFishAgent>();
            agent.Configure(i*1.73f,record.speciesId);
            agent.ConfigureHabitat(water.size,length);
        }
    }
}
