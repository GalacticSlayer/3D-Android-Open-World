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
    private string movingVisitorTank;
    private struct CommittedPlacement
    {
        public Vector3 position;
        public float yaw;
    }
    // A successful placement is a pending visual override until the latest
    // disk-backed Home snapshot contains that exact transform. This prevents
    // an old snapshot from teleporting a visitor's aquarium backwards.
    private readonly Dictionary<string,CommittedPlacement> pendingPlacements=
        new Dictionary<string,CommittedPlacement>();
    private readonly List<Collider> movingTankColliders=new List<Collider>();
    private readonly List<bool> movingColliderStates=new List<bool>();

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
        EnsurePondGround();
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
        // Cover the plot unless a real pond was successfully instantiated.
        // A published pond record alone is not proof that its model loaded.
        bool hasPond=visiting
            ? visitorPond!=null && visitorPond.gameObject.activeInHierarchy
            : progress.Data.CurrentHabitat(true)!=null;
        if(emptyPondCover!=null)emptyPondCover.SetActive(!hasPond);
    }
    private void EnsurePondGround()
    {
        if(emptyPondCover==null)
        {
            var found=transform.Find("ReservedPondPlot");
            if(found!=null)emptyPondCover=found.gameObject;
        }
        if(emptyPondCover==null)
        {
            // Legacy installed Home scenes may lack the serialized cover
            // reference. Recreate a solid, visible walkable garden plot
            // rather than leaving an eight-metre drop to the foundation.
            emptyPondCover=GameObject.CreatePrimitive(PrimitiveType.Cube);
            emptyPondCover.name="ReservedPondPlot";
            emptyPondCover.transform.SetParent(transform,false);
            emptyPondCover.transform.localPosition=new Vector3(24f,-.15f,30f);
            emptyPondCover.transform.localScale=new Vector3(32f,.3f,24f);
            var garden=transform.Find("GardenFront");
            var source=garden!=null?garden.GetComponent<Renderer>():null;
            var target=emptyPondCover.GetComponent<Renderer>();
            if(source!=null && target!=null)
                target.sharedMaterial=source.sharedMaterial;
        }
        if(emptyPondCover.GetComponent<Collider>()==null)
            emptyPondCover.AddComponent<BoxCollider>();
    }

    private string visitingSnapshot;
    private void RenderVisitorHome(HomeDirectoryClient.Reply snapshot)
    {
        if(snapshot==null){ClearVisitorHome();return;}
        // Unity may deserialize an empty tank list as null. The pond still
        // needs rendering, even if its owner has not placed any aquariums.
        // Rebuild only when the owner changes their published snapshot.
        string key=snapshot.code+":"+snapshot.version;
        if(visitingSnapshot==key)return;
        // Preserve the current preview across unrelated fish/pond revisions.
        string previewKey=movingVisitorTank;
        Vector3 previewPosition=Vector3.zero;
        float previewYaw=0f;
        if(!string.IsNullOrEmpty(previewKey) &&
           visitorAquariums.TryGetValue(previewKey,out var preview) && preview!=null)
        {
            previewPosition=preview.transform.localPosition;
            previewYaw=preview.transform.localEulerAngles.y;
        }
        else previewKey=null;
        ClearVisitorHome(true);
        visitingSnapshot=key;
        RenderVisitorPond(snapshot.pond);
        if(snapshot.tanks==null)
        {
            pendingPlacements.Clear();
            return;
        }
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
        ReconcileCommittedPlacements(snapshot);
        if(!string.IsNullOrEmpty(previewKey))
            SetVisitorLiveAquariumMove(previewKey,previewPosition,previewYaw);
    }

    private static bool MatchesCommittedPosition(HomeDirectoryClient.TankInfo published,
        CommittedPlacement commit)
    {
        return (new Vector3(published.x,published.y,published.z)-commit.position)
            .sqrMagnitude<=.0025f &&
            Mathf.Abs(Mathf.DeltaAngle(published.yaw,commit.yaw))<=.5f;
    }

    private void ReconcileCommittedPlacements(HomeDirectoryClient.Reply snapshot)
    {
        foreach(var key in new List<string>(pendingPlacements.Keys))
        {
            if(!visitorAquariums.TryGetValue(key,out var habitat) || habitat==null)
            {
                // A removed aquarium must disappear, not remain pinned.
                pendingPlacements.Remove(key);
                continue;
            }
            var pending=pendingPlacements[key];
            bool published=false;
            foreach(var tank in snapshot.tanks)
            {
                if(tank==null || tank.key!=key)continue;
                published=MatchesCommittedPosition(tank,pending);
                break;
            }
            if(published)
            {
                // Now the server's durable layout agrees. All subsequent
                // visits can use it without any transient override.
                pendingPlacements.Remove(key);
            }
            else
            {
                habitat.transform.localPosition=pending.position;
                habitat.transform.localRotation=Quaternion.Euler(0,pending.yaw,0);
            }
        }
    }

    public void CommitVisitorLiveAquariumPlacement(string key,
        Vector3 localPosition,float yaw)
    {
        if(directory==null || !directory.IsVisiting ||
           string.IsNullOrEmpty(key))return;
        if(!visitorAquariums.TryGetValue(key,out var habitat) || habitat==null)
            return;
        var committed=new CommittedPlacement{position=localPosition,yaw=yaw};
        bool alreadyPublished=false;
        var snapshot=directory.VisitingHome;
        if(snapshot!=null && snapshot.tanks!=null)
            foreach(var tank in snapshot.tanks)
                if(tank!=null && tank.key==key &&
                   MatchesCommittedPosition(tank,committed))
                {
                    alreadyPublished=true;
                    break;
                }
        if(alreadyPublished)pendingPlacements.Remove(key);
        else pendingPlacements[key]=committed;
        habitat.transform.localPosition=localPosition;
        habitat.transform.localRotation=Quaternion.Euler(0,yaw,0);
        // Restore collision and local fish simulation immediately when the
        // owner's drag ends, but do not move the aquarium back to old data.
        if(movingVisitorTank==key)RestoreVisitorMove();
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

    // Preview positions are transient and never modify another player's
    // fish, local ShopProgress ledger, or the published Home snapshot.
    public void SetVisitorLiveAquariumMove(string key,Vector3 localPosition,float yaw)
    {
        if(directory==null || !directory.IsVisiting)return;
        // A new drag supersedes an earlier, not-yet-acknowledged placement
        // of the same aquarium.
        if(!string.IsNullOrEmpty(key) && movingVisitorTank!=key)
            pendingPlacements.Remove(key);
        if(movingVisitorTank!=key)RestoreVisitorMove();
        if(string.IsNullOrEmpty(key))return;
        if(!visitorAquariums.TryGetValue(key,out var habitat) || habitat==null)return;
        if(movingVisitorTank!=key)
        {
            movingVisitorTank=key;
            movingTankColliders.Clear();movingColliderStates.Clear();
            foreach(var c in habitat.GetComponentsInChildren<Collider>(true))
            {
                movingTankColliders.Add(c);
                movingColliderStates.Add(c.enabled);
                c.enabled=false;
            }
            var agent=habitat.GetComponent<HomeVisitorTank>();
            if(agent!=null)agent.enabled=false;
            if(habitat.fishRoot!=null)habitat.fishRoot.gameObject.SetActive(false);
            if(habitat.water!=null)habitat.water.available=false;
        }
        habitat.transform.localPosition=localPosition;
        habitat.transform.localRotation=Quaternion.Euler(0,yaw,0);
    }

    private void RestoreVisitorMove()
    {
        if(string.IsNullOrEmpty(movingVisitorTank))return;
        if(visitorAquariums.TryGetValue(movingVisitorTank,out var habitat) && habitat!=null)
        {
            if(pendingPlacements.TryGetValue(movingVisitorTank,out var committed))
            {
                habitat.transform.localPosition=committed.position;
                habitat.transform.localRotation=Quaternion.Euler(0,committed.yaw,0);
            }
            else if(directory!=null && directory.VisitingHome!=null &&
                    directory.VisitingHome.tanks!=null)
            foreach(var tank in directory.VisitingHome.tanks)
                if(tank!=null && tank.key==movingVisitorTank)
                {
                    habitat.transform.localPosition=new Vector3(tank.x,tank.y,tank.z);
                    habitat.transform.localRotation=Quaternion.Euler(0,tank.yaw,0);
                    break;
                }
            for(int i=0;i<movingTankColliders.Count;i++)
                if(movingTankColliders[i]!=null)
                    movingTankColliders[i].enabled=movingColliderStates[i];
            if(habitat.water!=null)
            {
                var d=ShopCatalog.Habitat(habitat.id);
                habitat.water.available=d!=null && d.swimmable;
            }
            var agent=habitat.GetComponent<HomeVisitorTank>();
            if(agent!=null)agent.enabled=true;
        }
        movingVisitorTank=null;
        movingTankColliders.Clear();
        movingColliderStates.Clear();
    }

    private void ClearVisitorHome(bool preservePlacements=false)
    {
        RestoreVisitorMove();
        if(!preservePlacements)pendingPlacements.Clear();
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
