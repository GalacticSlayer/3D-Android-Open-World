using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class AmbientFishManager : MonoBehaviour
{
    [SerializeField] private OceanWater oceanWater;
    [SerializeField] private Transform player;
    [SerializeField,Range(4,16)] private int fishCount=12;
    [SerializeField] private float minRadius=7f, maxRadius=32f;
    private Camera view;
    private int poolBiome,pendingBiome;
    private float biomeChangedAt;
    private bool replacing=true;
    private readonly List<AmbientFishAgent> fish=new List<AmbientFishAgent>();
    public bool InReef => ShopDimensionManager.Instance==null || !ShopDimensionManager.Instance.InDimension;
    private IEnumerator Start()
    {
        if(oceanWater==null)oceanWater=FindFirstObjectByType<OceanWater>();
        if(player==null){var p=FindFirstObjectByType<FirstPersonController>();if(p!=null)player=p.transform;}
        if(player!=null)view=player.GetComponentInChildren<Camera>();
        fishCount=Mathf.Clamp(fishCount,4,12); // Includes old scenes serialized with 18.
        poolBiome=SnapperIslandGeometry.ResolveBiome(player!=null?player.position:Vector3.zero);
        for(int i=0;i<fishCount;i++)
        {
            fish.Add(CreateFish(i,poolBiome));
            yield return null; // Spread skin/prefab setup across frames.
        }
        replacing=false;
    }
    // Windgrove ambience must represent local wildlife, not the worms bait table.
    // The seven species families are equally likely; blue Mahi Mahi is a 10%
    // visual variant of its family. Actual hooked catches still use bait odds.
    private static int RollAmbientSpecies(int biome)
    {
        if(biome!=WindgroveShoalZone.BiomeId)
            return ReefCatalog.Roll(Random.value,0,biome);
        FishingTuning.RememberBiome(biome);
        float sample=Random.value;
        if(sample<1f/7f)return FishCatalog.BlackMarlinId;
        if(sample<2f/7f)
            return FishCatalog.RollMahiMahiVariant(FishCatalog.MahiMahiId,Random.value);
        if(sample<3f/7f)return FishCatalog.RoosterfishId;
        if(sample<4f/7f)return FishCatalog.RainbowRunnerId;
        if(sample<5f/7f)return FishCatalog.QueenfishId;
        if(sample<6f/7f)return FishCatalog.SpanishMackerelId;
        return FishCatalog.GoldenPompanoId;
    }
    private AmbientFishAgent CreateFish(int i,int biome)
    {
            int id=RollAmbientSpecies(biome);
            var species=FishCatalog.Get(id);
            var visual=FishWorldSize.Create("Reef_"+species.Name,transform,id,
                Mathf.Lerp(species.MinWeightKg,species.MaxWeightKg,Random.Range(0.05f,0.28f)));
            // A fixed pool: fish are repositioned, not repeatedly instantiated.
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=false;
            }
            foreach(var animator in visual.GetComponentsInChildren<Animator>())animator.cullingMode=AnimatorCullingMode.CullCompletely;
            var agent=visual.AddComponent<AmbientFishAgent>();
            agent.Configure(this,Random.Range(0.45f,0.95f),i,biome);
            return agent;
    }
    private void Update()
    {
        if(replacing || player==null || !InReef)return;
        int biome=SnapperIslandGeometry.ResolveBiome(player.position);
        if(biome!=pendingBiome){pendingBiome=biome;biomeChangedAt=Time.time;}
        // Snapper Island is deliberately exclusive. Replace the old area's ambient
        // pool immediately on entry so a lingering visible mackerel/tuna cannot make
        // the island look like it contains non-snapper species.
        bool immediateSnapperEntry=biome==ReefCatalog.SnapperBiomeId && biome!=poolBiome;
        if(biome!=poolBiome && (immediateSnapperEntry || Time.time-biomeChangedAt>2f))StartCoroutine(ReplacePool(biome));
    }
    private IEnumerator ReplacePool(int biome)
    {
        replacing=true;
        bool strictSnapper=biome==ReefCatalog.SnapperBiomeId;
        for(int i=0;i<fish.Count;i++)
        {
            // Normal biome changes avoid replacing a visible fish in front of the
            // player. Snapper Island is stricter: old non-snapper visuals disappear
            // immediately because the 50 m ring must contain snappers only.
            if(!strictSnapper)
                while(fish[i]!=null && Visible(fish[i].transform.position))yield return null;
            if(fish[i]!=null){fish[i].gameObject.SetActive(false);Destroy(fish[i].gameObject);}
            fish[i]=CreateFish(i,biome);yield return null;
        }
        poolBiome=biome;replacing=false;
    }
    public void Configure(OceanWater water,Transform target){oceanWater=water;player=target;fishCount=12;}
    // Outside saved terrain tiles there is still real ocean. A virtual deep
    // floor permits ambient fish to spawn/swim there; authored islands and
    // mesh-rock clearance checks remain authoritative wherever present.
    private bool TryFloor(Vector3 point, out float floor)
    {
        if (WorldTerrain.TryGetHeight(point, out floor)) return true;
        if (oceanWater == null) return false;
        floor = oceanWater.BaseWaterLevel - 250f;
        return true;
    }

    public bool Visible(Vector3 position)
    {
        if(!InReef||view==null)return false;
        var p=view.WorldToViewportPoint(position);
        return p.z>0f && p.z<42f && p.x>-0.15f && p.x<1.15f && p.y>-0.15f && p.y<1.15f;
    }
    public bool IsTooFar(Vector3 position) => player==null || (position-player.position).sqrMagnitude>48f*48f;
    public bool Safe(Vector3 position,float clearance=0.6f,int biome=-1)
    {
        if(!InReef||oceanWater==null)return false;
        if(biome>=0 && SnapperIslandGeometry.ResolveBiome(position)!=biome)return false;
        var world=IslandExpansionWorld.Active;
        if(biome==3 && world!=null && PelagicIslandGeometry.DistanceFromShore(position,world.PelagicCenter)+clearance>PelagicIslandGeometry.FishingMargin && !BluewaterCayRockRegion.ContainsBiome(position,clearance))return false;
        if(biome==ReefCatalog.SnapperBiomeId && SnapperIslandRuntime.Ready &&
            !SnapperCavesRockRegion.ContainsBiome(position,clearance))return false;
        if(!MarinaDockExtensionRuntime.WaterClear(position,clearance))return false;
        // Imported Blender rocks have their own MeshColliders; Terrain.SampleHeight
        // alone cannot detect a submerged or overhanging rock.
        if(!SnapperRockFishObstacles.AmbientPointClear(position,clearance))return false;
        if(!BrinebreakRockRegion.AmbientPointClear(position,clearance))return false;
        if(!BluewaterCayRockCollision.AmbientPointClear(position,clearance))return false;
        if(!SuncrestMainRockCollision.AmbientPointClear(position,clearance))return false;
        // Ocean-sized ambient fish must not be recycled into the tiny-fish pond.
        if(PondWater.Active!=null && PondWater.Active.Contains(position))return false;
        if(ReefZone.Active!=null&&!ReefZone.Active.Contains(position))return false;
        // Check each footprint sample against its own tile, including seams.
        if(!TryFloor(position,out float floor))return false;
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI*.25f;
            Vector3 sample=position+new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*clearance;
            if(!TryFloor(sample,out float sampleFloor))return false;
            floor=Mathf.Max(floor,sampleFloor);
        }
        return position.y>floor+clearance && position.y<oceanWater.GetSurfaceHeight(position)-clearance;
    }
    public bool ClearRoute(Vector3 from,Vector3 to,float clearance,int biome)
    {
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.4f));
        for(int i=1;i<=steps;i++)
            if(!Safe(Vector3.Lerp(from,to,(float)i/steps),clearance,biome))return false;
        return true;
    }

    public bool TryGetSwimPoint(out Vector3 point) => TryGetSwimPoint(out point,0.6f);
    public bool TryGetSwimPoint(out Vector3 point,float clearance,int biome=-1)
    {
        point=Vector3.zero;if(player==null||oceanWater==null||!InReef)return false;
        for(int attempt=0;attempt<32;attempt++)
        {
            Vector2 d=Random.insideUnitCircle.normalized*Random.Range(minRadius,maxRadius);
            var p=player.position+new Vector3(d.x,0,d.y);
            if(!TryFloor(p,out float floor))continue;
            float surface=oceanWater.GetSurfaceHeight(p);
            if(surface-floor<clearance*2+0.25f)continue;
            p.y=Random.Range(Mathf.Max(floor+clearance+0.12f,surface-6f),surface-clearance-0.12f);
            if(!Safe(p,clearance,biome))continue;
            // Do not visibly pop a recycled fish into the camera view.
            if(Visible(p)&&Vector3.Distance(player.position,p)<18f)continue;
            point=p;return true;
        }
        return false;
    }
}

public class AmbientFishAgent : MonoBehaviour
{
    private AmbientFishManager manager;
    private Vector3 target;
    private bool hasTarget;
    private int spawnBiome;
    private float speed, retry, clearance, decisionTime;
    private bool shown=true;
    private bool avoiding;
    private float escapeRetry;
    private Vector3 motionSample, recoveryDirection;
    private float motionSampleTime, recoveryRemaining;
    private bool recovering, recoveryTurning;
    private Collider[] recoveryColliders;
    private bool[] colliderWasEnabled;
    private readonly List<Vector3> safeTrail=new List<Vector3>();
    private Renderer[] renderers;
    private Animator[] animators;
    private ReefFishTrail trail;
    public void Configure(AmbientFishManager owner,float swimSpeed,float offset,int biome)
    {
        manager=owner;speed=swimSpeed;spawnBiome=biome;
        renderers=GetComponentsInChildren<Renderer>();animators=GetComponentsInChildren<Animator>();
        trail=gameObject.AddComponent<ReefFishTrail>();
        clearance=Mathf.Clamp(trail.BodyLength*0.5f,0.5f,1.3f);
        retry=offset*0.03f;
        SetVisible(false);
    }
    private void SetVisible(bool visible)
    {
        if(shown==visible)return;
        shown=visible;
        foreach(var r in renderers)r.enabled=visible;
        foreach(var a in animators)a.enabled=visible;
        trail.SetActive(visible);
    }
    private void Update()
    {
        if(manager==null)return;
        if(!manager.InReef){EndRecovery();SetVisible(false);hasTarget=false;return;}
        if(!hasTarget||manager.IsTooFar(transform.position))
        {
            EndRecovery();
            SetVisible(false);retry-=Time.deltaTime;
            if(retry>0)return;retry=0.6f;
            if(!manager.TryGetSwimPoint(out var point,clearance,spawnBiome))return;
            transform.position=point;transform.rotation=Quaternion.Euler(0,Random.Range(0,360f),0);
            target=point;hasTarget=true;trail.ResetTrail();decisionTime=0;avoiding=false;escapeRetry=0;
            safeTrail.Clear();safeTrail.Add(point);
            motionSample=point;motionSampleTime=0f;
        }
        SetVisible(true);
        if(recovering)
        {
            UpdateRecovery();
            trail.SetActive(manager.Visible(transform.position));
            trail.Record();
            return;
        }
        decisionTime-=Time.deltaTime;
        escapeRetry-=Time.deltaTime;
        if(!avoiding && (decisionTime<=0 || (target-transform.position).sqrMagnitude<1f))
        {
            decisionTime=2f+Random.value*2f;
            // Short local waypoints, so fish never aim straight through an island.
            for(int i=0;i<12;i++)
            {
                var p=transform.position+Random.insideUnitSphere*5f;
                p.y=Mathf.Lerp(transform.position.y,p.y,0.15f);
                if(manager.ClearRoute(transform.position,p,clearance,spawnBiome)){target=p;break;}
            }
        }
        Vector3 direction=target-transform.position;
        if(direction.sqrMagnitude>0.01f)
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),55f*Time.deltaTime);
        var next=transform.position+transform.forward*speed*Time.deltaTime;
        var ahead=next+transform.forward*Mathf.Max(0.8f,clearance);
        if(manager.ClearRoute(transform.position,ahead,clearance,spawnBiome))
        {
            transform.position=next;
            if(manager.Safe(next,clearance,spawnBiome) &&
                (safeTrail.Count==0 || Vector3.Distance(safeTrail[safeTrail.Count-1],next)>.4f))
            {
                safeTrail.Add(next);
                if(safeTrail.Count>24)safeTrail.RemoveAt(0);
            }
            if(avoiding && (target-transform.position).sqrMagnitude<.5f)
            {avoiding=false;decisionTime=0f;}
        }
        else
        {
            // Stay visible and turn in place until the nose faces open water.
            // Give a fixed escape heading enough time for a complete U-turn.
            if(!avoiding || escapeRetry<=0f)
            {
                if(!TryEscape())
                {
                    Vector3 back=safeTrail.Count>0?safeTrail[0]:transform.position-transform.forward*2f;
                    if((back-transform.position).sqrMagnitude<.1f)back=transform.position-transform.forward*2f;
                    target=back;
                }
                avoiding=true;
                escapeRetry=4f;
                decisionTime=.5f;
            }
        }
        motionSampleTime+=Time.deltaTime;
        if(motionSampleTime>=2f)
        {
            if(Vector3.Distance(transform.position,motionSample)<.15f)BeginRecovery();
            motionSample=transform.position;motionSampleTime=0f;
        }
        // Rendering already culls naturally; avoid train/bone work offscreen.
        trail.SetActive(manager.Visible(transform.position));
        trail.Record();
    }
    private void BeginRecovery()
    {
        recovering=true;
        recoveryTurning=true;
        recoveryRemaining=2f;
        recoveryDirection=Vector3.ProjectOnPlane(-transform.forward,Vector3.up).normalized;
        if(recoveryDirection.sqrMagnitude<.01f)recoveryDirection=-Vector3.forward;
        recoveryColliders=GetComponentsInChildren<Collider>(true);
        colliderWasEnabled=new bool[recoveryColliders.Length];
        for(int i=0;i<recoveryColliders.Length;i++)
        {
            colliderWasEnabled[i]=recoveryColliders[i].enabled;
            recoveryColliders[i].enabled=false;
        }
    }

    private void UpdateRecovery()
    {
        Quaternion facing=Quaternion.LookRotation(recoveryDirection);
        if(recoveryTurning)
        {
            transform.rotation=Quaternion.RotateTowards(transform.rotation,facing,120f*Time.deltaTime);
            if(Quaternion.Angle(transform.rotation,facing)>.5f)return;
            transform.rotation=facing;
            recoveryTurning=false;
        }
        // Recovery is transform-driven too: validate the escape against terrain
        // so a stalled fish cannot tunnel through the new southern seabed.
        float step=Mathf.Min(recoveryRemaining,speed*Time.deltaTime);
        Vector3 recovered=transform.position+recoveryDirection*step;
        // Do not let the legacy stall-recovery mode tunnel through authored rocks.
        if(!manager.ClearRoute(transform.position,recovered,clearance,spawnBiome) ||
            !SnapperRockFishObstacles.AmbientSegmentClear(transform.position,recovered,clearance) ||
            !BrinebreakRockRegion.AmbientSegmentClear(transform.position,recovered,clearance) ||
            !BluewaterCayRockCollision.AmbientSegmentClear(transform.position,recovered,clearance) ||
            !SuncrestMainRockCollision.AmbientSegmentClear(transform.position,recovered,clearance))
        {
            EndRecovery();
            SetVisible(false);
            hasTarget=false;
            retry=0f;
            safeTrail.Clear();
            return;
        }
        transform.position=recovered;
        recoveryRemaining-=step;
        if(recoveryRemaining>0f)return;
        EndRecovery();
        avoiding=false;
        target=transform.position;
        decisionTime=0f;
        escapeRetry=0f;
        safeTrail.Clear();
        if(manager.Safe(transform.position,clearance,spawnBiome))safeTrail.Add(transform.position);
        motionSample=transform.position;
        motionSampleTime=0f;
    }

    private void EndRecovery()
    {
        if(recoveryColliders!=null)
            for(int i=0;i<recoveryColliders.Length;i++)
                if(recoveryColliders[i]!=null)recoveryColliders[i].enabled=colliderWasEnabled[i];
        recoveryColliders=null;
        colliderWasEnabled=null;
        recovering=false;
        recoveryTurning=false;
    }

    private void OnDisable() { EndRecovery(); }

    private bool TryEscape()
    {
        Vector3 origin=transform.position;
        float bestScore=float.NegativeInfinity;
        bool found=false;
        // Retrace water we actually swam through before choosing a new route.
        for(int i=safeTrail.Count-1;i>=0;i--)
        {
            Vector3 point=safeTrail[i];
            if(Vector3.Distance(origin,point)<.8f)continue;
            if(!manager.ClearRoute(origin,point,clearance,spawnBiome))continue;
            target=point;
            safeTrail.RemoveRange(i+1,safeTrail.Count-i-1);
            return true;
        }
        for(int range=0;range<3;range++)
        for(int i=0;i<16;i++)
        {
            Vector3 direction=Quaternion.Euler(0f,i*22.5f,0f)*Vector3.forward;
            Vector3 point=origin+direction*(range==0?Mathf.Max(2.5f,clearance*2f):range==1?1.5f:.8f);
            if(!manager.ClearRoute(origin,point,clearance,spawnBiome))continue;
            float score=Vector3.Dot(transform.forward,direction);
            if(score<=bestScore)continue;
            bestScore=score;target=point;found=true;
        }
        return found;
    }

}



