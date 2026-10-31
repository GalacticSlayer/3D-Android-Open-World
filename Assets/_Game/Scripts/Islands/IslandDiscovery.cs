using UnityEngine;
using UnityEngine.UI;

public sealed class IslandDiscovery : MonoBehaviour
{
    public int Biome=1;
    private FirstPersonController bluewaterPlayer;
    private float nextBluewaterCheck;

    // The original discovery trigger covers only the old island ellipse.
    // Test Bluewater's authored rock colliders independently, including rocks
    // outside that trigger. Throttle proximity checks on Android.
    private void Update()
    {
        if(Biome!=PelagicIslandGeometry.BiomeId ||
           ReefCatalog.BluewaterDiscovered ||
           Time.unscaledTime<nextBluewaterCheck)return;
        nextBluewaterCheck=Time.unscaledTime+.25f;
        if(bluewaterPlayer==null)
            bluewaterPlayer=FindFirstObjectByType<FirstPersonController>();
        TryDiscoverBluewater(bluewaterPlayer);
    }

    private void TryDiscoverBluewater(FirstPersonController player)
    {
        IslandExpansionWorld world=IslandExpansionWorld.Active;
        if(player==null || world==null || !world.Ready ||
           ReefCatalog.Zones[Biome].Unlocked)return;
        if(ShopDimensionManager.Instance!=null &&
           ShopDimensionManager.Instance.InDimension)return;
        if(!BluewaterCayRockRegion.WithinDistanceOfRock(player.transform.position,1f))
            return;
        CompleteDiscovery(player);
    }

    private void Start()
    {
        if (Biome != 1 || !BrinebreakRockRegion.TryGetWorldBounds(out Bounds rockBounds))
            return;
        // Fit the physical trigger to the entire new cluster plus the biome radius.
        // Discovery still requires dry footing inside the 200m rock region.
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        transform.position = rockBounds.center;
        box.center = Vector3.zero;
        float diameter = BrinebreakRockRegion.BiomeRadius * 2f + 4f;
        box.size = new Vector3(rockBounds.size.x + diameter,
            Mathf.Max(160f, rockBounds.size.y + 100f),
            rockBounds.size.z + diameter);
        Physics.SyncTransforms();
    }

    private void OnTriggerStay(Collider other)
    {
        var player=other.GetComponentInParent<FirstPersonController>();
        var world=IslandExpansionWorld.Active;
        if(player==null || world==null || !world.Ready || ReefCatalog.Zones[Biome].Unlocked)return;
        if(ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.InDimension)return;
        if(Biome==PelagicIslandGeometry.BiomeId)
        {
            bluewaterPlayer=player;
            TryDiscoverBluewater(player);
            return;
        }
        var capsule=player.GetComponent<CharacterController>();var passenger=player.GetComponent<BoatPassenger>();
        if(capsule==null || !capsule.isGrounded || player.IsSwimming || (passenger!=null && passenger.Boat!=null))return;
        Vector3 p=player.transform.position;
        if(Biome==ReefCatalog.SnapperBiomeId)
        {
            // Cave islands are imported rock meshes, not raised Unity Terrain.
            if(!SnapperCavesRockRegion.ContainsBiome(p))return;
        }
        else if(Biome==1)
        {
            // The retired Brinebreak ellipse can never unlock the new island.
            if(!BrinebreakRockRegion.HasRocks || !BrinebreakRockRegion.ContainsBiome(p))return;
            float floor=world.Terrain.SampleHeight(p)+world.Terrain.transform.position.y;
            if(!BrinebreakRockRegion.StandingOnRock(p) &&
               (floor<world.Water.BaseWaterLevel+.2f || Mathf.Abs(p.y-floor)>.6f))return;
        }
        else
        {
            Vector3 center=Biome==3?world.PelagicCenter:world.NewCenter;
            Vector2 radii=Biome==3?Vector2.one*PelagicIslandGeometry.Radius:world.Config.IslandRadii;
            if(IslandGeometry.Ellipse(p,center,radii)>=.99f)return;
            float floor=world.Terrain.SampleHeight(p)+world.Terrain.transform.position.y;
            if(floor<world.Water.BaseWaterLevel+.2f || Mathf.Abs(p.y-floor)>.6f)return;
        }
        CompleteDiscovery(player);
    }

    private void CompleteDiscovery(FirstPersonController player)
    {
        if(ReefCatalog.Zones[Biome].Unlocked)return;
        var progress=player.GetComponent<ShopProgress>();if(progress==null || progress.ReadOnly)return;
        if(Biome==4){progress.Data.snapperDiscovered=true;ReefCatalog.SnapperDiscovered=true;}
        else if(Biome==3){progress.Data.bluewaterDiscovered=true;ReefCatalog.BluewaterDiscovered=true;}
        else {progress.Data.brinebreakDiscovered=true;ReefCatalog.BrinebreakDiscovered=true;}
        progress.Save();
        var go=new GameObject("Island discovered",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var label=new GameObject("Discovery",typeof(RectTransform),typeof(Image));label.transform.SetParent(go.transform,false);label.GetComponent<Image>().color=new Color(.035f,.14f,.2f,.95f);label.GetComponent<Image>().raycastTarget=false;
        var rect=label.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.8f);rect.sizeDelta=new Vector2(800,130);
        var textObject=new GameObject("Text",typeof(RectTransform),typeof(Text));textObject.transform.SetParent(label.transform,false);
        var text=textObject.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=30;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(1,.89f,.59f);text.raycastTarget=false;text.text=ReefCatalog.Zones[Biome].name.ToUpperInvariant()+" DISCOVERED\nFast travel unlocked in ISLAND / BIOME INDEX";text.rectTransform.sizeDelta=rect.sizeDelta;
        Destroy(go,6f);
    }
}


