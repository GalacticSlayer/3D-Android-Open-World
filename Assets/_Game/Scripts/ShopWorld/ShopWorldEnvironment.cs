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
    private readonly Dictionary<string,ShopHabitat> aquariumInstances=new Dictionary<string,ShopHabitat>();

    private void Start()
    {
        if(!isHome)return;
        progress=FindFirstObjectByType<ShopProgress>();
        if(progress!=null){progress.Changed+=RefreshHome;RefreshHome();}
    }
    private void OnDestroy(){if(progress!=null)progress.Changed-=RefreshHome;}

    private void RefreshHome()
    {
        if(progress==null || progress.Data==null)return;
        foreach(var h in habitats)
        {
            if(h==null)continue;
            var definition=ShopCatalog.Habitat(h.id);
            if(definition.pond)h.gameObject.SetActive(progress.Data.Habitat(h.id)!=null);
            else h.gameObject.SetActive(false);
        }

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
                // The authored showcase props are static-batched. Owned Home
                // instances must be movable, so clear the static flags.
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
                // Keep residents invisible during a move, even if XP/coins
                // trigger an unrelated save while the preview is active.
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
        if(emptyPondCover!=null)emptyPondCover.SetActive(progress.Data.CurrentHabitat(true)==null);
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
    }
    public ShopHabitat Habitat(string id)
    {
        if(!string.IsNullOrEmpty(id) && aquariumInstances.TryGetValue(id,out var exact) &&
           exact!=null && exact.gameObject.activeInHierarchy)return exact;
        foreach(var h in ActiveHabitats())
            if(h.id==id)return h;
        return null;
    }
    public Transform Point(string id) => id=="gear"?gearPoint:id=="market"?marketPoint:Habitat(id)?.interaction;
}
