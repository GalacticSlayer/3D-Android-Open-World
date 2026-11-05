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
    private ShopHabitat visitorPond;

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
            else
            {
                // Home aquariums have no sign or pedestal. The shop displays and
                // the separate storage-box sign are unaffected.
                HideHomeAquariumSign(h);
                h.gameObject.SetActive(false);
            }
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
                    HideHomeAquariumSign(instance);
                    aquariumInstances[ownership.instanceKey]=instance;
                    clone.transform.localPosition=new Vector3(ownership.localX,ownership.localY,ownership.localZ);
                    clone.transform.localRotation=Quaternion.Euler(0,ownership.yaw,0);
                    clone.SetActive(true);
                }
                else
                {
                    HideHomeAquariumSign(instance);
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
        // A Home with no pond must have walkable garden ground, including when
        // visiting a player who never bought one. Previously this was always
        // disabled for visitors, leaving the foundation hole exposed.
        bool hasPond=visiting
            ? directory.VisitingHome!=null && directory.VisitingHome.pond!=null
            : progress.Data.CurrentHabitat(true)!=null;
        if(emptyPondCover!=null)emptyPondCover.SetActive(!hasPond);
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
        RenderVisitorPond(snapshot.pond);
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
            HideHomeAquariumSign(habitat);
            if(d.swimmable && clone.GetComponent<AquariumAccessStyle>()==null)
                clone.AddComponent<AquariumAccessStyle>();
            if(habitat.water!=null)habitat.water.available=d.swimmable;
            // Nano / Reef are NOT swimmable. Restore the invisible solid top,
            // exactly as on the owner's purchased aquarium.
            if(habitat.purchaseBarrier!=null)
                habitat.purchaseBarrier.enabled=!d.swimmable;
            if(habitat.fishRoot!=null)habitat.fishRoot.gameObject.SetActive(false);
            var fish=clone.AddComponent<HomeVisitorTank>();
            fish.Configure(habitat,tank,progress.transform);
            visitorAquariums[tank.key]=habitat;
            clone.SetActive(true);
        }
    }
    private void RenderVisitorPond(HomeDirectoryClient.TankInfo pond)
    {
        if(pond==null)return; // Missing in older snapshots means no pond.
        var definition=ShopCatalog.Habitat(pond.id);
        var template=Template(pond.id);
        if(definition==null || !definition.pond || template==null)return;
        var clone=Instantiate(template.gameObject,transform,false);
        foreach(var part in clone.GetComponentsInChildren<Transform>(true))
            part.gameObject.isStatic=false;
        clone.name="Visited Home Pond - "+definition.name;
        var habitat=clone.GetComponent<ShopHabitat>();
        habitat.enabled=false; // Prevent binding to the visitor's own save.
        if(habitat.water!=null)habitat.water.available=true;
        if(habitat.purchaseBarrier!=null)habitat.purchaseBarrier.enabled=false;
        if(habitat.fishRoot!=null)habitat.fishRoot.gameObject.SetActive(false);
        var fish=clone.AddComponent<HomeVisitorTank>();
        fish.Configure(habitat,pond,progress.transform);
        visitorPond=habitat;
        clone.SetActive(true);
    }

    private static void HideHomeAquariumSign(ShopHabitat habitat)
    {
        if(habitat==null)return;
        var definition=ShopCatalog.Habitat(habitat.id);
        if(definition==null || definition.pond)return;
        if(habitat.label!=null)habitat.label.gameObject.SetActive(false);
        var pedestal=habitat.transform.Find("SignPedestal");
        if(pedestal!=null)pedestal.gameObject.SetActive(false);
    }

    private void ClearVisitorHome()
    {
        visitingSnapshot=null;
        if(visitorPond!=null)Destroy(visitorPond.gameObject);
        visitorPond=null;
        foreach(var h in visitorAquariums.Values)
            if(h!=null)Destroy(h.gameObject);
        visitorAquariums.Clear();
    }
    public ShopHabitat VisitorTank(string key)
    {
        if(visitorPond!=null && visitorPond.id==key)return visitorPond;
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
        if(visitorPond!=null && visitorPond.gameObject.activeInHierarchy)
            yield return visitorPond;
    }
    public ShopHabitat Habitat(string id)
    {
        if(visitorPond!=null && visitorPond.id==id &&
           visitorPond.gameObject.activeInHierarchy)return visitorPond;
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
