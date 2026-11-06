using UnityEngine;

// Scene-authored procedural expansion: generates once before gameplay Start methods.
// Keeps the SAME Terrain component, so existing fishing and boat references stay valid.
[DefaultExecutionOrder(-600)]
public sealed class IslandExpansionWorld : MonoBehaviour
{
    public IslandExpansionConfig Config;
    [SerializeField, HideInInspector] private bool savedLayout;
    public bool HasSavedLayout => savedLayout;
    private void Awake() { if(savedLayout) UseSavedLayout(); }

    private void UseSavedLayout()
    {
        Active=this;
        reef=GetComponentInParent<ReefZone>();
        if(reef==null)reef=FindFirstObjectByType<ReefZone>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);
        Water=FindFirstObjectByType<OceanWater>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);
        if(Config==null)Config=Resources.Load<IslandExpansionConfig>("Islands/BrinebreakExpansion");
        Ready=Terrain!=null && Terrain.terrainData!=null && reef!=null && Water!=null && Config!=null;
        if(Ready)sea=Water.BaseWaterLevel;
        else Debug.LogError("Saved fishing map is missing its terrain, reef, water or configuration. Restore the missing reference; the map will not be regenerated over your edits.",this);
    }

#if UNITY_EDITOR
    public void SaveLayoutForEditing()
    {
        if(!Ready)throw new System.InvalidOperationException("Generate the expansion before saving it.");
        savedLayout=true;
        // Saved assets and authored transforms are now owned by the scene.
        originalData=null;generatedData=null;sceneryPositions.Clear();
        rockTexture=null;seabedTexture=null;seabedNormal=null;
        rockLayer=null;seabedLayer=null;rock=null;wood=null;grass=null;
    }
#endif
    public static IslandExpansionWorld Active {get;private set;}
    [field: SerializeField] public Terrain Terrain {get;private set;}
    public OceanWater Water {get;private set;}
    [field: SerializeField] public Vector3 NewCenter {get;private set;}
    [field: SerializeField] public Vector3 PelagicCenter {get;private set;}
    [field: SerializeField] public Transform PelagicArrival {get;private set;}
    [field: SerializeField] public Vector3 ShelfCenter {get;private set;}
    [field: SerializeField] public Vector2 ShelfRadii {get;private set;}
    [field: SerializeField] public Transform Arrival {get;private set;}
    public bool Ready {get;private set;}
    private TerrainData originalData,generatedData;
    private readonly System.Collections.Generic.Dictionary<Transform,Vector3> sceneryPositions = new System.Collections.Generic.Dictionary<Transform,Vector3>();
    private Vector3 originalPosition;
    private ReefZone reef;
    private float sea;
    private Texture2D rockTexture,seabedTexture,seabedNormal;
    private TerrainLayer rockLayer,seabedLayer;
    private Material rock,wood,grass;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForExistingScenes()
    {
        if(FindFirstObjectByType<IslandExpansionWorld>()!=null || ReefZone.Active==null)return;
        var go=new GameObject("Island Expansion");go.AddComponent<IslandExpansionWorld>();
    }
    private void Start(){Build();}
    public void Build()
    {
        if(savedLayout){UseSavedLayout();return;}
        if(Ready)return;
        reef=FindFirstObjectByType<ReefZone>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);Water=FindFirstObjectByType<OceanWater>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);
        if(reef==null || Water==null){Debug.LogWarning("Island expansion needs the installed Suncrest Reef scene. Run Tools/Setup Island Expansion after installing Suncrest Reef.");return;}
        Terrain=reef.GetComponentInChildren<Terrain>(!Application.isPlaying);if(Terrain==null)Terrain=UnityEngine.Terrain.activeTerrain;
        if(Terrain==null)return;
        if(Config==null)Config=Resources.Load<IslandExpansionConfig>("Islands/BrinebreakExpansion");
        if(Config==null){Debug.LogError("Brinebreak expansion configuration missing.");return;}
        Active=this;sea=Water.BaseWaterLevel;originalData=Terrain.terrainData;originalPosition=Terrain.transform.position;
        NewCenter=reef.center+new Vector3(reef.islandRadiusX+Config.OffsetBeyondSuncrest,0,35);
        // Bluewater Cay must NOT move just because Brinebreak's coastline shrank.
        // Preserve its original position derived from the historical 95x65 radii.
        PelagicCenter=PelagicIslandGeometry.Center(NewCenter,new Vector2(95f,65f));
        float left=reef.center.x-reef.islandRadiusX-reef.reefWidth-60;
        float right=NewCenter.x+Config.IslandRadii.x+150;
        ShelfCenter=new Vector3((left+right)*.5f,sea,reef.center.z+17.5f);
        // Oversize the outer ellipse so both complete coastal shelves fit inside.
        ShelfRadii=new Vector2((right-left)*.5f,Mathf.Max(reef.islandRadiusZ+reef.reefWidth+95,Config.IslandRadii.y+190));
        BuildTerrain();GroundExistingScenery();BuildScenery();BuildPelagicScenery();ResizeWaterQuery();Ready=true;
    }
    private float SourceHeight(Vector3 p)
    {
        Vector3 q=p-originalPosition;var size=originalData.size;
        if(q.x<0 || q.z<0 || q.x>size.x || q.z>size.z)return sea-Config.OceanDepth;
        return originalPosition.y+originalData.GetInterpolatedHeight(q.x/size.x,q.z/size.z);
    }
    public float Height(Vector3 p)
    {
        if (savedLayout && Terrain != null)
        {
            if (WorldTerrain.TryGetHeight(p, out float ground)) return ground;
            return sea - Config.OceanDepth;
        }
        return SeabedRelief.Height(p.x,p.z,PelagicIslandGeometry.Height(p,PelagicCenter,sea,BaseHeight(p)),sea);
    }
    private float BaseHeight(Vector3 p)
    {
        float beyond=IslandGeometry.Beyond(p,ShelfCenter,ShelfRadii);
        float shelf=IslandGeometry.ShelfFloor(sea,Config.ShelfDepth,Config.OceanDepth,beyond,Config.DropoffWidth);
        float sunDistance=IslandGeometry.Beyond(p,reef.center,new Vector2(reef.islandRadiusX,reef.islandRadiusZ));
        float retain=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(reef.reefWidth-40,reef.reefWidth+50,sunDistance));
        shelf=Mathf.Max(shelf,Mathf.Lerp(shelf,SourceHeight(p),retain));
        float q=IslandGeometry.Ellipse(p,NewCenter,Config.IslandRadii);
        if(q<1)
        {
            float inland=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,.63f,q));
            float hills=4+7*Mathf.PerlinNoise(p.x*.022f+13,p.z*.025f+71)+3*Mathf.PerlinNoise(p.x*.07f,p.z*.07f);
            float h=sea+.06f+inland*hills;
            return Mathf.Max(shelf,h);
        }
        float coast=(q-1)*Mathf.Min(Config.IslandRadii.x,Config.IslandRadii.y);
        return IslandGeometry.CoastalFloor(shelf,sea,coast,Config.ShelfDepth);
    }
    private void BuildTerrain()
    {
        float extent=Mathf.Max(ShelfRadii.x,ShelfRadii.y)+Config.DropoffWidth+150;
        float size=extent*2;
        Vector3 origin=new Vector3(ShelfCenter.x-extent,sea-Config.OceanDepth-10-SeabedRelief.ExtraDepth,ShelfCenter.z-extent);
        int n=Mathf.ClosestPowerOfTwo(Mathf.Clamp(Config.HeightResolution-1,256,2048))+1;
        generatedData=new TerrainData{name="Suncrest + Brinebreak Shared Shelf",heightmapResolution=n,alphamapResolution=512,baseMapResolution=1024};
        generatedData.size=new Vector3(size,Config.OceanDepth+40+SeabedRelief.ExtraDepth,size);
        var heights=new float[n,n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {var p=origin+new Vector3(x/(float)(n-1)*size,0,z/(float)(n-1)*size);heights[z,x]=Mathf.Clamp01((Height(p)-origin.y)/generatedData.size.y);}
        generatedData.SetHeights(0,0,heights);
        var sourceLayers=originalData.terrainLayers;
        rockTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);rockTexture.wrapMode=TextureWrapMode.Repeat;
        var pixels=new Color[1024];for(int i=0;i<pixels.Length;i++){float noise=Mathf.PerlinNoise(i%32*.3f,i/32*.3f);pixels[i]=Color.Lerp(new Color(.23f,.25f,.26f),new Color(.49f,.47f,.41f),noise);}rockTexture.SetPixels(pixels);rockTexture.Apply();
        rockLayer=new TerrainLayer{name="Brinebreak island stone",diffuseTexture=rockTexture,tileSize=new Vector2(7,7)};
        BuildSeabedTexture();
        int islandRock=sourceLayers.Length,underwaterRock=sourceLayers.Length+1;
        var layers=new TerrainLayer[sourceLayers.Length+2];sourceLayers.CopyTo(layers,0);
        layers[islandRock]=rockLayer;layers[underwaterRock]=seabedLayer;generatedData.terrainLayers=layers;
        var oldAlpha=originalData.GetAlphamaps(0,0,originalData.alphamapWidth,originalData.alphamapHeight);
        var alpha=new float[512,512,layers.Length];
        for(int z=0;z<512;z++)for(int x=0;x<512;x++)
        {
            Vector3 p=origin+new Vector3(x/511f*size,0,z/511f*size);
            float q=IslandGeometry.Ellipse(p,NewCenter,Config.IslandRadii);
            float pelagicDistance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(PelagicCenter.x,PelagicCenter.z));
            if(pelagicDistance<=PelagicIslandGeometry.Radius)
            {
                float inland=Mathf.SmoothStep(0,1,Mathf.InverseLerp(PelagicIslandGeometry.Radius*.88f,PelagicIslandGeometry.Radius*.45f,pelagicDistance));
                alpha[z,x,0]=1-inland;
                alpha[z,x,sourceLayers.Length>1?1:0]+=inland;
            }
            else if(q<1.2f)
            {
                float rocky=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.96f,.75f,q));
                alpha[z,x,0]=1-rocky;alpha[z,x,islandRock]=rocky;
            }
            else
            {
                Vector3 local=p-originalPosition;int ox=Mathf.Clamp(Mathf.RoundToInt(local.x/originalData.size.x*(originalData.alphamapWidth-1)),0,originalData.alphamapWidth-1);int oz=Mathf.Clamp(Mathf.RoundToInt(local.z/originalData.size.z*(originalData.alphamapHeight-1)),0,originalData.alphamapHeight-1);
                for(int l=0;l<sourceLayers.Length;l++)alpha[z,x,l]=oldAlpha[oz,ox,l];
                if(sourceLayers.Length==0)alpha[z,x,0]=1;
            }
            // Start from the original island paint, then add stone only below the shore buffer.
            float depth=sea-(origin.y+generatedData.GetInterpolatedHeight(x/511f,z/511f));
            float cliff=SeabedRelief.RockWeight(depth,generatedData.GetSteepness(x/511f,z/511f));
            float submerged=SeabedRelief.RockWeight(depth,90f);
            for(int l=0;l<underwaterRock;l++)alpha[z,x,l]*=1-submerged;
            alpha[z,x,0]+=submerged-cliff;
            alpha[z,x,underwaterRock]=cliff;
        }
        generatedData.SetAlphamaps(0,0,alpha);
        Terrain.transform.position=origin;Terrain.terrainData=generatedData;
        var collider=Terrain.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=generatedData;
        Terrain.heightmapPixelError=6;Terrain.drawInstanced=true;Terrain.basemapDistance=350;
        Physics.SyncTransforms();
    }
    // Suncrest props were placed on the saved heightmap. The runtime seabed can
    // lower that floor by many metres, so move each complete prop by the floor
    // difference while preserving its authored pivot/burial offset.
    private void GroundExistingScenery()
    {
        foreach(Transform prop in reef.transform)
        {
            if(!prop.gameObject.activeSelf || prop.GetComponent<Terrain>()!=null ||
                prop.GetComponent<PondWater>()!=null || UserPlacedScenery.Contains(prop) ||
                prop.GetComponentInChildren<UserPlacedScenery>(true)!=null)continue;
            if(prop.name=="Shell bank")
            {
                foreach(Transform shell in prop)GroundExistingProp(shell);
            }
            else if(prop.GetComponentInChildren<Renderer>()!=null || prop.name=="SuncrestArrival")
                GroundExistingProp(prop);
        }
        Physics.SyncTransforms();
    }
    private void GroundExistingProp(Transform prop)
    {
        Vector3 position=prop.position;
        float shift=Terrain.SampleHeight(position)+Terrain.transform.position.y-SourceHeight(position);
        // Sample the base footprint too: wide reef props should sit into a
        // sloping floor instead of balancing above its lower side.
        string name=prop.name;
        if(name.Contains("limestone") || name.Contains("coral") || name.Contains("shrub") ||
            name=="Limestone" || name=="Coral" || name=="SeaGrape")
        {
            float radius=.65f*Mathf.Max(Mathf.Abs(prop.lossyScale.x),Mathf.Abs(prop.lossyScale.z));
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;
                Vector3 sample=position+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                shift=Mathf.Min(shift,Terrain.SampleHeight(sample)+Terrain.transform.position.y-SourceHeight(sample));
            }
        }
        sceneryPositions[prop]=position;
        prop.position=position+Vector3.up*shift;
    }
    private void BuildSeabedTexture()
    {
        const int n=256;
        seabedTexture=new Texture2D(n,n,TextureFormat.RGBA32,true);
        seabedTexture.name="Submerged fractured stone";seabedTexture.wrapMode=TextureWrapMode.Repeat;
        seabedTexture.filterMode=FilterMode.Trilinear;seabedTexture.anisoLevel=4;
        seabedNormal=new Texture2D(n,n,TextureFormat.RGBA32,true,true);
        seabedNormal.name="Submerged stone normal";seabedNormal.wrapMode=TextureWrapMode.Repeat;
        var colors=new Color[n*n];var normals=new Color[n*n];var relief=new float[n*n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float u=x/(float)n,v=z/(float)n;
            // Periodic fields keep every edge of the repeating texture seamless.
            float strata=Mathf.Sin(v*Mathf.PI*12f+.6f*Mathf.Sin(u*Mathf.PI*4f));
            float cross=Mathf.Sin(u*Mathf.PI*10f+.9f*Mathf.Sin(v*Mathf.PI*6f));
            float grain=Mathf.Sin(u*Mathf.PI*94f+Mathf.Sin(v*Mathf.PI*66f))*Mathf.Sin(v*Mathf.PI*82f);
            float crack=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.12f,Mathf.Min(Mathf.Abs(strata),Mathf.Abs(cross))));
            float tone=Mathf.Clamp01(.48f+.14f*strata+.065f*grain-.33f*crack);
            colors[z*n+x]=Color.Lerp(new Color(.18f,.22f,.23f),new Color(.50f,.51f,.46f),tone);
            relief[z*n+x]=.55f+.12f*strata+.035f*grain-.2f*crack;
        }
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float dx=relief[z*n+(x+1)%n]-relief[z*n+(x+n-1)%n];
            float dz=relief[((z+1)%n)*n+x]-relief[((z+n-1)%n)*n+x];
            Vector3 normal=new Vector3(-dx*2f,-dz*2f,1).normalized;
            // RGBA packing works with Unity's RGB and RG/AG terrain normal paths.
            float nx=normal.x*.5f+.5f,ny=normal.y*.5f+.5f;
            normals[z*n+x]=new Color(nx,ny,normal.z*.5f+.5f,1);
        }
        seabedTexture.SetPixels(colors);seabedTexture.Apply(true,false);
        seabedNormal.SetPixels(normals);seabedNormal.Apply(true,false);
        seabedLayer=new TerrainLayer{name="Underwater cliff stone",diffuseTexture=seabedTexture,
            normalMapTexture=seabedNormal,tileSize=new Vector2(9,9),normalScale=.75f,smoothness=.2f};
    }
    private Material Surface(Color color)
    {
        var shader=Resources.Load<Shader>("Fishing/ShopSurface");var m=new Material(shader);m.color=color;return m;
    }
    private void BuildScenery()
    {
        rock=Surface(new Color(.32f,.34f,.35f));wood=Surface(new Color(.32f,.20f,.10f));grass=Surface(new Color(.25f,.31f,.15f));
        Arrival=new GameObject("BrinebreakArrival").transform;Arrival.SetParent(transform,false);
        Vector3 approach=NewCenter+new Vector3(-Config.IslandRadii.x*.82f,0,-8);
        approach.y=Terrain.SampleHeight(approach)+Terrain.transform.position.y+.12f;
        Arrival.SetPositionAndRotation(approach,Quaternion.Euler(0,90,0));
        // Legacy Brinebreak decorations were intentionally retired after the
        // island moved to two artist-placed southern rock meshes. Never spawn
        // the old Weathered rock/Wind scrub props or a Brinebreak welcome sign.
        // The new saved scene owns its authored rocks and terrain independently.
        var trigger=new GameObject("Brinebreak Land Discovery");trigger.transform.SetParent(transform,false);trigger.transform.position=NewCenter+Vector3.up*(sea+12-NewCenter.y);
        var box=trigger.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(Config.IslandRadii.x*2,30,Config.IslandRadii.y*2);
        var rb=trigger.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;trigger.AddComponent<IslandDiscovery>();
    }
    private void BuildPelagicScenery()
    {
        PelagicArrival=new GameObject("BluewaterArrival").transform;PelagicArrival.SetParent(transform,false);
        Vector3 approach=PelagicCenter+new Vector3(0,0,PelagicIslandGeometry.Radius*.70f);
        approach.y=Terrain.SampleHeight(approach)+Terrain.transform.position.y+.15f;
        PelagicArrival.SetPositionAndRotation(approach,Quaternion.Euler(0,180,0));
        var random=new System.Random(Config.ScenerySeed+73);
        for(int i=0;i<55;i++)
        {
            float angle=(float)random.NextDouble()*Mathf.PI*2;
            float radius=Mathf.Sqrt((float)random.NextDouble())*PelagicIslandGeometry.Radius*.82f;
            Vector3 p=PelagicCenter+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(Vector3.ProjectOnPlane(p-approach,Vector3.up).magnitude<10)continue;
            bool scrub=i%3==0;float size=1.2f+(float)random.NextDouble()*2.2f;
            var prop=GameObject.CreatePrimitive(scrub?PrimitiveType.Sphere:PrimitiveType.Cube);
            prop.name=scrub?"Bluewater coastal scrub":"Bluewater weathered stone";
            prop.transform.SetParent(transform,false);prop.transform.localScale=new Vector3(size,scrub?.8f:size*.65f,size*.8f);
            prop.transform.rotation=Quaternion.Euler(scrub?0:random.Next(-15,16),random.Next(360),scrub?0:random.Next(-15,16));
            p.y=Terrain.SampleHeight(p)+Terrain.transform.position.y;prop.transform.position=p;
            var renderer=prop.GetComponent<Renderer>();renderer.sharedMaterial=scrub?grass:rock;
            prop.transform.position+=Vector3.up*renderer.bounds.extents.y*.4f;
            if(scrub)RemoveGeneratedObject(prop.GetComponent<Collider>());prop.isStatic=true;
        }
        Vector3 signPoint=approach+new Vector3(4,0,-3);signPoint.y=Terrain.SampleHeight(signPoint)+Terrain.transform.position.y;
        IslandWelcomeSign.Create(transform,"BLUEWATER CAY",signPoint,Quaternion.identity,wood);
        var trigger=new GameObject("Bluewater Land Discovery");trigger.transform.SetParent(transform,false);
        trigger.transform.position=new Vector3(PelagicCenter.x,sea+12,PelagicCenter.z);
        var box=trigger.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(PelagicIslandGeometry.Radius*2,30,PelagicIslandGeometry.Radius*2);
        var body=trigger.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
        trigger.AddComponent<IslandDiscovery>().Biome=PelagicIslandGeometry.BiomeId;
    }
    private void ResizeWaterQuery()
    {
        var query=GameObject.Find("BoatPlacementWater");if(query==null)return;
        var box=query.GetComponent<BoxCollider>();if(box==null)return;
        query.transform.position=new Vector3(Terrain.transform.position.x+Terrain.terrainData.size.x*.5f,sea-.025f,Terrain.transform.position.z+Terrain.terrainData.size.z*.5f);
        box.size=new Vector3(Terrain.terrainData.size.x,.05f,Terrain.terrainData.size.z);
    }
    public int BiomeAt(Vector3 p)
    {
        // Offshore Oil Rig overrides the default deep-ocean biome only inside
        // its own 350 m circle. Existing island classifications stay identical.
        if (OffshoreOilRigZone.Contains(p)) return OffshoreOilRigZone.BiomeId;
        if(SnapperIslandRuntime.Ready && SnapperCavesRockRegion.ContainsBiome(p))
            return ReefCatalog.SnapperBiomeId;
        // The relocated Brinebreak is ONLY the union of 200m circles around its stalagmite meshes.
        bool newBrinebreak=BrinebreakRockRegion.HasRocks;
        if(newBrinebreak && BrinebreakRockRegion.ContainsBiome(p))return 1;
        if(PelagicIslandGeometry.Contains(p,PelagicCenter) || BluewaterCayRockRegion.ContainsBiome(p))
            return PelagicIslandGeometry.BiomeId;
        // Never fall back to the retired Brinebreak ellipse when its old rocks are deleted.
        // Preserve the original Suncrest boundary; moving Brinebreak must not
        // inflate Suncrest's fishing waters or leave the old Brinebreak biome.
        Vector3 oldBrinebreak=reef.center+
            new Vector3(reef.islandRadiusX+Config.OffsetBeyondSuncrest,0f,35f);
        float suncrestRadius=IslandGeometry.ZoneRadius(reef.center,oldBrinebreak);
        float dx=p.x-reef.center.x,dz=p.z-reef.center.z;
        return dx*dx+dz*dz<=suncrestRadius*suncrestRadius?0:2;
    }
    public float OffshoreAt(Vector3 p)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,180,IslandGeometry.Beyond(p,ShelfCenter,ShelfRadii)));
    public static int FishingBiome(Vector3 p)=>Active!=null && Active.Ready?Active.BiomeAt(p):0;
    private static void RemoveGeneratedObject(Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
    private void OnDestroy()
    {
        if(Active==this)Active=null;
        if(savedLayout)return;
        foreach(var entry in sceneryPositions)if(entry.Key!=null)entry.Key.position=entry.Value;
        sceneryPositions.Clear();
        if(Terrain!=null && originalData!=null){Terrain.terrainData=originalData;Terrain.transform.position=originalPosition;var c=Terrain.GetComponent<TerrainCollider>();if(c!=null)c.terrainData=originalData;}
        if(generatedData!=null)Destroy(generatedData);if(rockLayer!=null)Destroy(rockLayer);if(rockTexture!=null)Destroy(rockTexture);
        if(seabedLayer!=null)Destroy(seabedLayer);if(seabedTexture!=null)Destroy(seabedTexture);if(seabedNormal!=null)Destroy(seabedNormal);
        if(rock!=null)Destroy(rock);if(wood!=null)Destroy(wood);if(grass!=null)Destroy(grass);
    }
}




