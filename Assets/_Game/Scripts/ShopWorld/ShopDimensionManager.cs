using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class ShopDimensionManager : MonoBehaviour
{
    public const string SceneName="TideglassShopWorld", HomeSceneName="FishingHomeWorld";
    public static ShopDimensionManager Instance { get; private set; }
    public Transform islandArrival;
    public int Destination { get; private set; }
    public bool InShop => Destination==1;
    public bool InHome => Destination==2;
    public bool InDimension => Destination!=0;
    public bool Traveling { get; private set; }
    public ShopWorldEnvironment World { get; private set; }
    public string TravelError { get; private set; }
    private FirstPersonController controller;
    private FishingSystem fishing;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private bool hasReturn;
    private float oldFogDensity;
    private bool oldFog;
    private Color oldFogColor;
    private void Awake()
    {
        Instance=this;
        controller=GetComponent<FirstPersonController>();
        fishing=GetComponent<FishingSystem>();
        // Runtime attachment works with existing installed scenes; no scene rebuild.
        if(GetComponent<HomeAquariumPlacement>()==null)
            gameObject.AddComponent<HomeAquariumPlacement>();
        if(GetComponent<HomeDirectoryClient>()==null)
            gameObject.AddComponent<HomeDirectoryClient>();
    }
    private void OnDestroy() { if(Instance==this) Instance=null; }
    public bool Near(string id)
    {
        if(!InDimension || World==null || Traveling) return false;
        Transform point=World.Point(id);
        return point!=null && Vector3.Distance(transform.position,point.position)<=5.5f;
    }
    public string Nearest()
    {
        if(!InDimension || World==null) return null;
        if(Near("gear")) return "gear"; if(Near("market")) return "market";
        foreach(var h in World.ActiveHabitats())
        {
            string key=string.IsNullOrEmpty(h.instanceKey)?h.id:h.instanceKey;
            if(Near(key))return key;
        }
        return null;
    }
    // The total-weight tournament restricts teleport destinations, not movement
    // or access to the Tackle/Market shop dimension. This is the authoritative
    // guard for BOTH the old travel cards and the newer island index.
    public bool TournamentTravelRestricted =>
        GetComponent<FishingTournament>()?.SuncrestTravelRestricted==true;
    public bool TournamentAllowsIsland(int biome) => !TournamentTravelRestricted || biome==0;
    public bool TournamentAllowsDimension(int destination) =>
        !TournamentTravelRestricted || destination==0 || destination==1;
    private int travelIslandBiome;
    public void TravelIsland(bool brinebreak)=>TravelIsland(brinebreak?1:0);
    public void TravelIsland(int biome)
    {
        if(Traveling || !TournamentAllowsIsland(biome) || (biome!=0 && biome!=1 && biome!=3 && biome!=4))return;
        if(!ReefCatalog.Zones[biome].Unlocked)return;
        var expansion=IslandExpansionWorld.Active;
        if(biome!=0 && (expansion==null || !expansion.Ready))return;
        if(biome==4 && !SnapperIslandRuntime.Ready)return;
        Transform point=biome==4?SnapperIslandRuntime.Arrival:biome==3?expansion.PelagicArrival:biome==1?expansion.Arrival:GetComponent<BoatSystem>()?.IslandDock;
        if(point==null && biome==0)point=islandArrival;
        if(point==null)return;
        travelIslandBiome=biome;
        if(Destination!=0){StartCoroutine(TravelRoutine(0));return;}
        fishing.PrepareForWorldTravel();Teleport(point.position,point.rotation,biome==4,biome==3,biome==1);
        var ui=FindFirstObjectByType<ShopWorldHUD>();if(ui!=null)ui.Close();
        travelIslandBiome=0;
    }
    public void Travel(bool shop) => Travel(shop?1:0);
    public void Travel(int destination)
    {
        if(Traveling || !TournamentAllowsDimension(destination) || destination==Destination || destination<0 || destination>2)return;
        travelIslandBiome=0;StartCoroutine(TravelRoutine(destination));
    }
    private IEnumerator TravelRoutine(int destination)
    {
        TravelError=null;
        string next=destination==1?SceneName:HomeSceneName;
        if(destination!=0 && !Application.CanStreamedLevelBeLoaded(next))
        { TravelError="Run Install Shop World (One Click) to create the updated worlds."; yield break; }
        Traveling=true; controller.SetUIBlocked(true); fishing.PrepareForWorldTravel();
        int previous=Destination;
        ShopWorldEnvironment target=null;
        if(destination!=0)
        {
            AsyncOperation operation=null;
            try { operation=SceneManager.LoadSceneAsync(next,LoadSceneMode.Additive); }
            catch(System.Exception ex) { TravelError=ex.Message; }
            if(operation==null) { Traveling=false; controller.SetUIBlocked(false); yield break; }
            yield return operation;
            var scene=SceneManager.GetSceneByName(next);
            foreach(var root in scene.GetRootGameObjects())
            { target=root.GetComponentInChildren<ShopWorldEnvironment>(); if(target!=null)break; }
            if(target==null || target.spawn==null)
            { TravelError="Arrival point missing. Reinstall Shop World."; yield return SceneManager.UnloadSceneAsync(next); Traveling=false; controller.SetUIBlocked(false); yield break; }
        }
        // An async Home load may have begun before the tournament countdown.
        // If the travel lock activated during loading, unload that destination
        // before moving the player, leaving them available for the forced
        // Suncrest arrival on the next multiplayer tournament update.
        if(destination!=0 && !TournamentAllowsDimension(destination))
        {
            yield return SceneManager.UnloadSceneAsync(next);
            Traveling=false;
            controller.SetUIBlocked(false);
            var interruptedMenu=FindFirstObjectByType<ShopWorldHUD>();
            if(interruptedMenu!=null && ShopWorldHUD.MenuOpen)interruptedMenu.Close();
            yield break;
        }
        if(previous==0)
        {
            returnPosition=transform.position; returnRotation=transform.rotation; hasReturn=true;
            oldFog=RenderSettings.fog; oldFogColor=RenderSettings.fogColor; oldFogDensity=RenderSettings.fogDensity;
        }
        Destination=destination; World=target;
        if(destination==2 && target!=null && target.spawn!=null)
        {
            // The storage crate is centred at Home-local Z=-9, with its front
            // lid edge at Z=-9.86. Spawn one metre in front of that edge so the
            // player starts behind the crate, facing its readable front sign.
            // Apply at runtime to existing installed Home scenes too.
            target.spawn.localPosition=new Vector3(0f,.25f,-10.86f);
        }
        if(destination==0)
        {
            var boats=GetComponent<BoatSystem>();
            Transform dock=boats!=null?boats.IslandDock:null;
            var expansion=IslandExpansionWorld.Active;
            if(expansion!=null && expansion.Ready)
            {
                if(travelIslandBiome==1 && ReefCatalog.BrinebreakDiscovered)dock=expansion.Arrival;
                if(travelIslandBiome==4 && ReefCatalog.SnapperDiscovered && SnapperIslandRuntime.Ready)dock=SnapperIslandRuntime.Arrival;
                if(travelIslandBiome==3 && ReefCatalog.BluewaterDiscovered)dock=expansion.PelagicArrival;
            }
            bool returningToSnapper=travelIslandBiome==4 && dock==SnapperIslandRuntime.Arrival &&
                SnapperIslandRuntime.Ready;
            bool returningToBluewater=travelIslandBiome==3 && expansion!=null &&
                dock==expansion.PelagicArrival;
            bool returningToBrinebreak=travelIslandBiome==1 && expansion!=null &&
                dock==expansion.Arrival;
            travelIslandBiome=0;
            Teleport(dock!=null?dock.position:hasReturn?returnPosition:islandArrival.position,
                dock!=null?dock.rotation:hasReturn?returnRotation:islandArrival.rotation,
                returningToSnapper,returningToBluewater,returningToBrinebreak);
            RenderSettings.fog=oldFog; RenderSettings.fogColor=oldFogColor; RenderSettings.fogDensity=oldFogDensity;
        }
        else Teleport(target.spawn.position,target.spawn.rotation);
        if(previous!=0)yield return SceneManager.UnloadSceneAsync(previous==1?SceneName:HomeSceneName);
        fishing.SetShopWorld(InDimension); Traveling=false; controller.SetUIBlocked(false);
        var ui=FindFirstObjectByType<ShopWorldHUD>();
        if(ui!=null)ui.Close(); else controller.SetUIBlocked(false);
    }
    public void Visit(string id)
    {
        if(World==null || Traveling) return;
        Transform p=World.Point(id); if(p==null) return;
        Teleport(p.position,p.rotation);
    }
    public void EnterHabitat(string id)
    {
        if(!InHome || !Near(id)) return;
        var h=World.Habitat(id); var progress=GetComponent<ShopProgress>();
        var directory=GetComponent<HomeDirectoryClient>();
        bool visitorTank=directory!=null && directory.IsVisiting && World.VisitorTank(id)!=null;
        if(h==null || !visitorTank && progress.Data.Habitat(id)==null ||
           !ShopCatalog.Habitat(h.id).swimmable)return;
        Teleport(h.entrance.position,h.entrance.rotation);
    }
    public void ReturnToHomeSpawn()
    {
        if(InHome && !Traveling && World!=null && World.spawn!=null)
            Teleport(World.spawn.position,World.spawn.rotation);
    }
    public void ExitWater()
    {
        if(!InDimension || World==null) return;
        ShopHabitat nearest=null; float distance=float.MaxValue;
        foreach(var h in World.ActiveHabitats())
        { float d=(h.transform.position-transform.position).sqrMagnitude; if(d<distance) { distance=d; nearest=h; } }
        if(nearest!=null) Teleport(nearest.exit.position,nearest.exit.rotation);
    }
    // Preserve the authored SnapperArrival X/Z and existing cave rocks, but
    // land ON the edited Unity terrain rather than atop an imported cave mesh.
    private bool TrySnapperArrival(Vector3 marker,CharacterController capsule,out Vector3 arrival)
    {
        arrival=marker;
        IslandExpansionWorld expansion=IslandExpansionWorld.Active;
        Terrain terrain=expansion!=null?expansion.Terrain:Terrain.activeTerrain;
        OceanWater ocean=expansion!=null?expansion.Water:FindFirstObjectByType<OceanWater>();
        if(terrain==null || terrain.terrainData==null || ocean==null)return false;
        float sea=ocean.BaseWaterLevel;
        Vector3 scale=transform.lossyScale;
        float radius=Mathf.Max(.15f,capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)));
        float halfHeight=Mathf.Max(radius,capsule.height*Mathf.Abs(scale.y)*.5f);
        float centerY=capsule.center.y*scale.y;
        float feetOffset=centerY-halfHeight;
        float cylinderHalf=Mathf.Max(0f,halfHeight-radius);
        float[] searchRadii={0f,1.5f,3f,5f,8f,12f,18f,26f,35f,46f,58f};
        Vector3 terrainOrigin=terrain.transform.position,terrainSize=terrain.terrainData.size;
        Physics.SyncTransforms();
        for(int ring=0;ring<searchRadii.Length;ring++)
        {
            int samples=ring==0?1:20;
            for(int i=0;i<samples;i++)
            {
                float angle=i*Mathf.PI*2f/samples;
                Vector3 p=marker+new Vector3(Mathf.Cos(angle)*searchRadii[ring],0f,
                    Mathf.Sin(angle)*searchRadii[ring]);
                float u=(p.x-terrainOrigin.x)/terrainSize.x;
                float v=(p.z-terrainOrigin.z)/terrainSize.z;
                if(u<0f||u>1f||v<0f||v>1f)continue;
                float ground=terrain.SampleHeight(p)+terrainOrigin.y;
                if(ground<sea+.35f || terrain.terrainData.GetSteepness(u,v)>38f)
                    continue;
                p.y=ground+.38f-feetOffset;
                Vector3 center=p+Vector3.up*centerY;
                if(Physics.CheckCapsule(center-Vector3.up*cylinderHalf,
                    center+Vector3.up*cylinderHalf,Mathf.Max(.05f,radius-.02f),
                    ~0,QueryTriggerInteraction.Ignore))continue;
                arrival=p;
                return true;
            }
        }
        Debug.LogError("[SNAPPER TRAVEL] No clear dry terrain surface near SnapperArrival; teleport cancelled.");
        return false;
    }

    private void Teleport(Vector3 position,Quaternion rotation,bool snapperArrival=false,bool bluewaterArrival=false,bool brinebreakArrival=false)
    {
        var capsule=GetComponent<CharacterController>();
        // Disable first so the overlap query cannot mistake our own collider
        // for a rock. Preserve the player's old location on an aborted travel.
        GetComponent<BoatSystem>()?.BeforeTeleport();
        bool wasEnabled=capsule!=null && capsule.enabled;
        if(capsule!=null)capsule.enabled=false;
        try
        {
            if(snapperArrival && capsule!=null &&
                !TrySnapperArrival(position,capsule,out position))
                return;
            // Brinebreak is relocated to the former SeaGrape_0350 marker.
            // Resolve nearby clear footing on the new rocks/terrain before travel.
            if(brinebreakArrival && (capsule==null ||
                !BrinebreakRockRegion.TryGetArrival(position,capsule,out position)))
                return;
            // Bluewater is now a single imported rock, not the old terrain island.
            // Resolve a safe near-water NW rock surface each loaded world session.
            if(bluewaterArrival && (capsule==null ||
                !BluewaterCayRockCollision.TryGetArrival(capsule,out position,out rotation)))
                return;
            // North on the fishing map is world +Z. Always face north after
            // travelling to Snapper, including when returning from the shop.
            if(snapperArrival)rotation=Quaternion.LookRotation(Vector3.forward,Vector3.up);
            transform.SetPositionAndRotation(position,rotation);
            controller.ResetMotion(); controller.ResetViewPitch();
            Physics.SyncTransforms();
        }
        finally
        {
            if(capsule!=null)capsule.enabled=wasEnabled;
        }
    }
    private void LateUpdate()
    {
        if(InDimension && !Traveling && World!=null && transform.position.y<World.transform.position.y-8f)
            Teleport(World.spawn.position,World.spawn.rotation);
    }
}




