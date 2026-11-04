using UnityEngine;
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
