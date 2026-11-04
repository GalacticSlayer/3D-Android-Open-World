using System;
using System.Collections.Generic;
using UnityEngine;

// The authored Home aquariums are templates. Owned aquariums are independent
// instances so they can be moved, stored, and filled without replacing each other.
public sealed class ShopWorldEnvironment : MonoBehaviour
{
    public bool isHome;
    public GameObject emptyPondCover;
    private ShopProgress progress;
    private HomeDirectoryClient directory;
    private readonly Dictionary<string,ShopHabitat> aquariumInstances=new Dictionary<string,ShopHabitat>();
    private readonly Dictionary<string,ShopHabitat> visitorAquariums=new Dictionary<string,ShopHabitat>();

    private void Start()
    {
        if(!isHome)return;
        progress=FindFirstObjectByType<ShopProgress>();
        directory=HomeDirectoryClient.Instance;
        if(progress!=null){progress.Changed+=RefreshHome;RefreshHome();}
    }
    private void OnDestroy(){if(progress!=null)progress.Changed-=RefreshHome;}

    private void RefreshHome() => RefreshHomeView();

    // A visitor's temporary aquarium instances never touch ShopProgress.Data.
    // Returning home simply removes these clones and restores local instances.
    public void RefreshHomeView()
    {
        if(progress==null || progress.Data==null)return;
        if(directory==null)directory=HomeDirectoryClient.Instance;
        bool visiting=directory!=null && directory.IsVisiting;
        foreach(var h in habitats)
        {
            if(h==null)continue;
            var definition=ShopCatalog.Habitat(h.id);
            if(definition==null)continue;
            if(definition.pond)h.gameObject.SetActive(!visiting && progress.Data.Habitat(h.id)!=null);
            else h.gameObject.SetActive(false);
        }
        if(visiting)
        {
            foreach(var instance in aquariumInstances.Values)
                if(instance!=null)instance.gameObject.SetActive(false);
            RenderVisitorHome(directory.VisitingHome);
        }
        else
        {
            ClearVisitorHome();
            var active=new HashSet<string>();
            foreach(var ownership in progress.Data.habitats)
            {
                if(ownership==null || !ownership.placed || string.IsNullOrEmpty(ownership.instanceKey))continue;
                var definition=ShopCatalog.Habitat(ownership.id);
                if(definition==null || definition.pond)continue;
                active.Add(ownership.instanceKey);
                if(!aquariumInstances.TryGetValue(ownership.instanceKey,out var instance) || instance==null)
                {
                    var template=Template(ownership.id);
                    if(template==null)continue;
                    var clone=Instantiate(template.gameObject,transform,false);
                    foreach(var part in clone.GetComponentsInChildren<Transform>(true))
                        part.gameObject.isStatic=false;
                    clone.name=definition.name+" ["+ownership.instanceKey+"]";
                    instance=clone.GetComponent<ShopHabitat>();
                    instance.instanceKey=ownership.instanceKey;
                    aquariumInstances[ownership.instanceKey]=instance;
                    clone.transform.localPosition=new Vector3(ownership.localX,ownership.localY,ownership.localZ);
                    clone.transform.localRotation=Quaternion.Euler(0,ownership.yaw,0);
                    clone.SetActive(true);
                }
                else
                {
                    instance.transform.localPosition=new Vector3(ownership.localX,ownership.localY,ownership.localZ);
                    instance.transform.localRotation=Quaternion.Euler(0,ownership.yaw,0);
                    if(!instance.gameObject.activeSelf &&
                       !(HomeAquariumPlacement.IsPlacing && HomeAquariumPlacement.PreviewKey==ownership.instanceKey))
                        instance.gameObject.SetActive(true);
                }
            }
            foreach(var key in new List<string>(aquariumInstances.Keys))
            {
                if(active.Contains(key))continue;
                if(aquariumInstances[key]!=null)Destroy(aquariumInstances[key].gameObject);
                aquariumInstances.Remove(key);
            }
        }
        if(emptyPondCover!=null)emptyPondCover.SetActive(!visiting && progress.Data.CurrentHabitat(true)==null);
    }
    private string visitingSnapshot;
    private void RenderVisitorHome(HomeDirectoryClient.Reply snapshot)
    {
        if(snapshot==null || snapshot.tanks==null){ClearVisitorHome();return;}
        // Rebuild only when the owner changes their published snapshot.
        string key=snapshot.code+":"+snapshot.version;
        if(visitingSnapshot==key)return;
        ClearVisitorHome();
        visitingSnapshot=key;
        foreach(var tank in snapshot.tanks)
        {
            if(tank==null || visitorAquariums.ContainsKey(tank.key))continue;
            var template=Template(tank.id);
            var d=ShopCatalog.Habitat(tank.id);
            if(template==null || d==null || d.pond)continue;
            var clone=Instantiate(template.gameObject,transform,false);
            foreach(var part in clone.GetComponentsInChildren<Transform>(true))
                part.gameObject.isStatic=false;
            clone.name="Visiting "+snapshot.name+" - "+d.name+" ["+tank.key+"]";
            clone.transform.localPosition=new Vector3(tank.x,tank.y,tank.z);
            clone.transform.localRotation=Quaternion.Euler(0,tank.yaw,0);
            var habitat=clone.GetComponent<ShopHabitat>();
            habitat.instanceKey=tank.key;
            habitat.enabled=false; // Never bind another player's fish to our save.
            if(d.swimmable && clone.GetComponent<AquariumAccessStyle>()==null)
                clone.AddComponent<AquariumAccessStyle>();
            if(habitat.water!=null)habitat.water.available=d.swimmable;
            if(habitat.purchaseBarrier!=null)habitat.purchaseBarrier.enabled=false;
            if(habitat.label!=null)
            {
                habitat.label.text=d.name.ToUpperInvariant()+"  ["+tank.key.ToUpperInvariant()+"]\n"+
                    snapshot.name.ToUpperInvariant()+"'S HOME  |  "+
                    (tank.fish!=null?tank.fish.Length:0)+" FISH";
                var sign=habitat.label.GetComponent<ShopSign>();
                if(sign!=null)sign.Fit();
            }
            if(habitat.fishRoot!=null)habitat.fishRoot.gameObject.SetActive(false);
            var fish=clone.AddComponent<HomeVisitorTank>();
            fish.Configure(habitat,tank,progress.transform);
            visitorAquariums[tank.key]=habitat;
            clone.SetActive(true);
        }
    }
    private void ClearVisitorHome()
    {
        visitingSnapshot=null;
        foreach(var h in visitorAquariums.Values)
            if(h!=null)Destroy(h.gameObject);
        visitorAquariums.Clear();
    }
    public ShopHabitat VisitorTank(string key)
    {
        return visitorAquariums.TryGetValue(key,out var h)?h:null;
    }

    public Transform spawn,gearPoint,marketPoint;
    public ShopHabitat[] habitats;
    public ShopHabitat Template(string id) => Array.Find(habitats,h=>h!=null && h.id==id);
    public IEnumerable<ShopHabitat> ActiveHabitats()
    {
        foreach(var h in habitats)
            if(h!=null && h.gameObject.activeInHierarchy)yield return h;
        foreach(var h in aquariumInstances.Values)
            if(h!=null && h.gameObject.activeInHierarchy)yield return h;
        foreach(var h in visitorAquariums.Values)
            if(h!=null && h.gameObject.activeInHierarchy)yield return h;
    }
    public ShopHabitat Habitat(string id)
    {
        if(!string.IsNullOrEmpty(id) && visitorAquariums.TryGetValue(id,out var guest) &&
           guest!=null && guest.gameObject.activeInHierarchy)return guest;
        if(!string.IsNullOrEmpty(id) && aquariumInstances.TryGetValue(id,out var exact) &&
           exact!=null && exact.gameObject.activeInHierarchy)return exact;
        foreach(var h in ActiveHabitats())
            if(h.id==id)return h;
        return null;
    }
    public Transform Point(string id) => id=="gear"?gearPoint:id=="market"?marketPoint:Habitat(id)?.interaction;
}


// Fish move on the visiting device. Instantiate only when the viewer approaches;
// remote Homes never transmit per-frame positions or animation updates.
public sealed class HomeVisitorTank : MonoBehaviour
{
    private ShopHabitat habitat;
    private HomeDirectoryClient.TankInfo tank;
    private Transform observer;
    private float nextCheck;
    private bool spawned;
    public void Configure(ShopHabitat h,HomeDirectoryClient.TankInfo info,Transform player)
    {habitat=h;tank=info;observer=player;}
    private void Update()
    {
        if(habitat==null || habitat.fishRoot==null || observer==null)return;
        if(Time.unscaledTime<nextCheck)return;
        nextCheck=Time.unscaledTime+.5f;
        var d=ShopCatalog.Habitat(tank.id);
        float range=55f+Mathf.Max(d.width,d.depth)*.6f;
        bool visible=(observer.position-transform.position).sqrMagnitude<range*range;
        if(visible&&!spawned)SpawnFish();
        if(habitat.fishRoot.gameObject.activeSelf!=visible)
            habitat.fishRoot.gameObject.SetActive(visible);
    }
    private void SpawnFish()
    {
        spawned=true;
        if(tank.fish==null)return;
        for(int i=0;i<tank.fish.Length;i++)
        {
            var record=tank.fish[i];
            if(record==null)continue;
            try
            {
                var fish=FishWorldSize.Create("VisitorResident_"+i,habitat.fishRoot,
                    record.speciesId,record.weightKg);
                float length=ShopCatalog.FishLength(record.speciesId,record.weightKg);
                var agent=fish.AddComponent<TankFishAgent>();
                agent.Configure(i*1.73f,record.speciesId);
                agent.ConfigureHabitat(habitat.water.size,length);
            }
            catch(System.Exception ex)
            {
                Debug.LogWarning("Skipped invalid remote aquarium fish: "+ex.Message);
            }
        }
    }
}
