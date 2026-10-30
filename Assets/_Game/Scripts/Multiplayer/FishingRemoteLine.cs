using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Observer-only geometry. No fishing simulation, collisions, or network requests.
public sealed class FishingRemoteLine : MonoBehaviour
{
    private LineRenderer line;
    private Material lineMaterial;
    private GameObject tackle;
    private Transform tie;
    private Animator animator;
    private Quaternion retrieveFrame=Quaternion.identity;
    private Vector3 target,position;
    private int kind,variant=-1,sequence;
    private float receivedAt;
    private bool reeling,positionReady,fishHooked;
    private bool lightningInitialized;
    private int lastLightningSerial;
    private readonly Queue<int> pendingZaps = new Queue<int>();
    private readonly List<FishingLightningStrikeAnimation> activeZaps = new List<FishingLightningStrikeAnimation>();
    private readonly List<GameObject> activeRipples = new List<GameObject>();
    private OceanWater ocean;
    private Canvas distanceCanvas;
    private GameObject distanceRoot;
    private RectTransform distanceRect;
    private Text distanceLabel;
    private static Sprite greenDistanceSprite;
    private bool castImpactInitialized;
    private int lastCastImpactSequence;

    public void Receive(FishingMultiplayer.Pose pose)
    {
        int next=pose.rodEquipped && !pose.swimming && pose.world==0?pose.lineKind:0;
        receivedAt=Time.realtimeSinceStartup;
        reeling=pose.reeling;
        fishHooked=pose.hasFish && next!=0;
        ReceiveCastImpact(pose);
        ReceiveLightning(pose,fishHooked);
        Vector3 end=new Vector3(pose.lineX,pose.lineY,pose.lineZ);
        if(next!=kind || pose.castSequence!=sequence || !positionReady || (end-position).sqrMagnitude>400f)
        {position=end;positionReady=true;}
        if(next!=kind || (next==1 && variant!=pose.lureVariant))ClearTackle();
        kind=next;variant=pose.lureVariant;sequence=pose.castSequence;target=end;
        if(kind==0)Hide();
    }

    private void ReceiveCastImpact(FishingMultiplayer.Pose pose)
    {
        // A cast's successful landing may outlive the "Casting" network state,
        // so consume a separate monotonic landing ID with the exact water point.
        // Late joiners may see an impact only while it is still fresh.
        bool changed=!castImpactInitialized || pose.castImpactSequence!=lastCastImpactSequence;
        castImpactInitialized=true;
        lastCastImpactSequence=pose.castImpactSequence;
        if(!changed || pose.castImpactSequence<=0 || pose.world!=0 ||
            pose.castImpactAge<0f || pose.castImpactAge>1.4f)
            return;
        if(ocean==null)ocean=FindFirstObjectByType<OceanWater>();
        if(ocean==null)return;
        Vector3 point=new Vector3(pose.castImpactX,pose.castImpactY,pose.castImpactZ);
        Camera camera=FindFirstObjectByType<FishingSystem>()?.PresentationCamera;
        if(camera==null)camera=Camera.main;
        // Match the casting angler's authored 18-frame splash and 1m ripple.
        FishingCastSplashAnimation.Spawn(point,ocean,camera);
        FishingCastRippleEffect.Spawn(point,ocean);
    }

    private void ReceiveLightning(FishingMultiplayer.Pose pose, bool validFight)
    {
        // Baseline across all fishing states, not just fights. That lets the
        // very first zap of a new fight be seen, without replaying historic hits
        // when the spectator joins midway through an existing fight.
        if (!lightningInitialized || pose.lightningSerial < lastLightningSerial)
        {
            lightningInitialized=true;
            lastLightningSerial=pose.lightningSerial;
            pendingZaps.Clear();
            return;
        }
        int delta=pose.lightningSerial-lastLightningSerial;
        lastLightningSerial=pose.lightningSerial;
        if (!validFight || ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen)
        {
            pendingZaps.Clear();
            return;
        }
        if(delta<=0)return;
        // A delayed poll can include multiple hits. Reconstruct indices from
        // the newest one; bound catch-up so network stalls cannot flood FX.
        int count=delta>4?1:delta;
        for(int i=count-1;i>=0;i--)
            pendingZaps.Enqueue((pose.lightningZap-i+8)%8);
    }

    // Called after the avatar pose and rod flex, so the line starts at the bent tip.
    public void Present(Transform tip)
    {
        // The remote fish marker is independent of the other player's rod prefab
        // and line shader. It must still work if their rod tip cannot be resolved.
        if(kind==0 || Time.realtimeSinceStartup-receivedAt>1f)
        {
            Hide();
            PresentLightning();
            return;
        }
        position=Vector3.Lerp(position,target,1f-Mathf.Exp(-18f*Time.unscaledDeltaTime));
        PresentDistance(tip);
        if(tip==null)
        {
            if(line!=null)line.enabled=false;
            if(tackle!=null)tackle.SetActive(false);
            PresentLightning();
            return;
        }
        if(line==null)
        {
            var shader=Resources.Load<Shader>("Fishing/FishingUnlit");
            if(shader==null)return;
            lineMaterial=new Material(shader);
            SetColor(lineMaterial,new Color(.88f,.94f,.96f,.92f));
            var go=new GameObject("Observer Fishing Line");go.transform.SetParent(transform,false);
            line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;
            line.useWorldSpace=true;line.positionCount=2;line.startWidth=.008f;line.endWidth=.004f;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        }
        if(tackle==null && kind==1)CreateTackle();
        if(tackle!=null)
        {
            tackle.SetActive(true);
            if(kind==1)
            {
                Vector3 direction=tip.position-position;direction.y=0f;
                if(direction.sqrMagnitude>.0001f)
                    tackle.transform.rotation=Quaternion.LookRotation(direction,Vector3.up)*
                        Quaternion.AngleAxis(ShopCatalog.LurePresentationRoll(variant),Vector3.forward)*Quaternion.Inverse(retrieveFrame);
                if(animator!=null)animator.speed=reeling?2f:0f;
            }
            tackle.transform.position=position;
            if(tie!=null)tackle.transform.position+=position-tie.position;
        }
        line.enabled=true;line.SetPosition(0,tip.position);line.SetPosition(1,position);
        PresentLightning();
    }

    private void PresentLightning()
    {
        if (ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen)
        {
            foreach(var zap in activeZaps)if(zap!=null)zap.SetPaused(true);
            return;
        }
        if(ocean==null)ocean=FindFirstObjectByType<OceanWater>();
        // Use the observing angler's real gameplay camera for the hearing
        // boundary, including when Camera.main points at a UI/preview camera.
        var localFishing=FindFirstObjectByType<FishingSystem>();
        Camera camera=localFishing!=null?localFishing.PresentationCamera:null;
        if(camera==null)camera=Camera.main;
        while(pendingZaps.Count>0)
        {
            int zapIndex=pendingZaps.Dequeue();
            if(ocean==null)continue;
            var zap=FishingLightningStrikeAnimation.Create(transform);
            if(zap==null)continue;
            zap.transform.position=position;
            zap.Restart(zapIndex, true); // Remote-only 60 m hearing limit; visuals remain unchanged.
            activeZaps.Add(zap);
        }
        for(int i=activeZaps.Count-1;i>=0;i--)
        {
            var zap=activeZaps[i];
            if(zap==null){activeZaps.RemoveAt(i);continue;}
            zap.SetPaused(false);
            zap.Tick(position,ocean,camera);
            if(zap.TakeRippleCue())
            {
                // Exactly one electric ripple on frame two of this zap.
                var ripple=FishingElectricRippleEffect.Spawn(position,ocean);
                if(ripple!=null)activeRipples.Add(ripple);
            }
            if(zap.Finished){Destroy(zap.gameObject);activeZaps.RemoveAt(i);}
        }
        for(int i=activeRipples.Count-1;i>=0;i--)
            if(activeRipples[i]==null)activeRipples.RemoveAt(i);
    }

    private void PresentDistance(Transform tip)
    {
        if(!fishHooked || ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen)
        {
            HideDistance();
            return;
        }
        if(distanceRoot==null)BuildDistance();
        if(distanceRoot==null || distanceCanvas==null){return;}
        // Camera.main may refer to a menu/preview camera (or be untagged).
        // Use the same gameplay camera as the local fishing distance indicator.
        var localFishing=FindFirstObjectByType<FishingSystem>();
        Camera camera=localFishing!=null?localFishing.PresentationCamera:null;
        if(camera==null)camera=Camera.main;
        if(camera==null){HideDistance();return;}
        // Anchor directly above the OTHER player's line endpoint at water level.
        Vector3 screen=FishingThirdPersonView.WorldToScreenPoint(camera,position+Vector3.up*.10f);
        // The observing angler's distance to the fish controls visibility and
        // uniformly shrinks both the backdrop and its numbers as they move away.
        float viewerDistance=Vector3.Distance(camera.transform.position,position);
        if(viewerDistance>=80f)
        {
            HideDistance();
            return;
        }
        Rect viewPixels=camera.pixelRect;
        if(screen.z<=0f || !viewPixels.Contains(new Vector2(screen.x,screen.y)))
        {
            HideDistance();
            return;
        }
        RectTransform canvasRect=distanceCanvas.transform as RectTransform;
        Camera uiCamera=distanceCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:distanceCanvas.worldCamera;
        if(canvasRect==null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,screen,uiCamera,out Vector2 localPoint)){HideDistance();return;}
        // Use the real rod tip when present, otherwise the remote player's
        // world position. This is still their own line distance, not ours.
        Vector3 origin=tip!=null?tip.position:transform.position+Vector3.up*1.3f;
        float metres=Vector3.Distance(origin,position);
        distanceLabel.text=metres.ToString("0.0")+" m";
        float scale=Mathf.Lerp(1f,.42f,Mathf.Clamp01(viewerDistance/80f));
        distanceRect.localScale=Vector3.one*scale;
        // Centre the green box directly above the point of contact with water.
        float height=distanceRect.rect.height*scale;
        Vector2 location=localPoint+new Vector2(0f,height*.5f+7f);
        Rect bounds=canvasRect.rect;
        float halfWidth=distanceRect.rect.width*scale*.5f;
        location.x=Mathf.Clamp(location.x,bounds.xMin+halfWidth,bounds.xMax-halfWidth);
        location.y=Mathf.Clamp(location.y,bounds.yMin+height*.5f,bounds.yMax-height*.5f);
        distanceRect.anchoredPosition=location;
        distanceRoot.SetActive(true);
    }

    private void BuildDistance()
    {
        // Fish HUD is created at runtime on the gameplay canvas. Prefer the
        // canvas containing that HUD instead of an arbitrary scene canvas.
        var hud=FindFirstObjectByType<FishingHUD>();
        Canvas canvas=hud!=null?hud.GetComponentInParent<Canvas>():null;
        if(canvas==null)return;
        distanceCanvas=canvas;
        distanceRoot=new GameObject("Remote Fish Distance - Green",typeof(RectTransform),typeof(Image));
        distanceRoot.transform.SetParent(canvas.transform,false);
        distanceRect=distanceRoot.GetComponent<RectTransform>();
        distanceRect.anchorMin=distanceRect.anchorMax=new Vector2(.5f,.5f);
        distanceRect.pivot=new Vector2(.5f,.5f);
        distanceRect.sizeDelta=new Vector2(150f,54f);
        Image glass=distanceRoot.GetComponent<Image>();
        glass.sprite=GreenDistanceGlass();
        glass.type=Image.Type.Sliced;
        glass.color=Color.white;
        glass.raycastTarget=false;
        var label=new GameObject("Distance",typeof(RectTransform),typeof(Text),typeof(Outline));
        label.transform.SetParent(distanceRoot.transform,false);
        distanceLabel=label.GetComponent<Text>();
        distanceLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        distanceLabel.fontSize=28;
        distanceLabel.fontStyle=FontStyle.Bold;
        distanceLabel.alignment=TextAnchor.MiddleCenter;
        distanceLabel.color=new Color(.85f,1f,.87f);
        distanceLabel.raycastTarget=false;
        RectTransform rect=distanceLabel.rectTransform;
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(7f,2f);rect.offsetMax=new Vector2(-7f,-2f);
        Outline outline=label.GetComponent<Outline>();
        outline.effectColor=new Color(0f,.08f,.02f,.95f);
        outline.effectDistance=new Vector2(2f,-2f);
        distanceRoot.SetActive(false);
    }

    // Cached rounded nine-slice green glass; uses the fishing HUD's border shape.
    private static Sprite GreenDistanceGlass()
    {
        if(greenDistanceSprite!=null)return greenDistanceSprite;
        const int size=96;
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)
        for(int x=0;x<size;x++)
        {
            float qx=Mathf.Abs(x-47.5f)-28f, qy=Mathf.Abs(y-47.5f)-28f;
            float d=new Vector2(Mathf.Max(qx,0f),Mathf.Max(qy,0f)).magnitude+
                Mathf.Min(Mathf.Max(qx,qy),0f)-17f;
            float fill=Mathf.Clamp01(.5f-d);
            float rim=Mathf.Clamp01(1f-Mathf.Abs(d+1.8f)/1.4f);
            float glow=Mathf.Exp(-Mathf.Abs(d+2f)*.35f)*.25f;
            Color baseColor=Color.Lerp(new Color(.012f,.10f,.045f,.83f),
                new Color(.02f,.24f,.09f,.84f),y/95f);
            Color c=Color.Lerp(baseColor,new Color(.26f,1f,.42f,.98f),
                Mathf.Max(rim,glow));
            c.a*=fill;
            pixels[y*size+x]=c;
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.name="RemoteFishDistanceGreenGlass";
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.filterMode=FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(false,true);
        greenDistanceSprite=Sprite.Create(texture,new Rect(0f,0f,size,size),
            new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect,new Vector4(24,24,24,24));
        return greenDistanceSprite;
    }

    private void HideDistance(){if(distanceRoot!=null)distanceRoot.SetActive(false);}

    private void ClearLightning()
    {
        pendingZaps.Clear();
        foreach(var zap in activeZaps)if(zap!=null)Destroy(zap.gameObject);
        activeZaps.Clear();
        foreach(var ripple in activeRipples)if(ripple!=null)Destroy(ripple);
        activeRipples.Clear();
    }

    private void CreateTackle()
    {
        var prefab=Resources.Load<GameObject>(ShopCatalog.LurePrefabResource(variant));
        if(prefab==null)return;
        tackle=Instantiate(prefab,transform,false);tackle.name="Observer Retrieval Lure";
        foreach(var c in tackle.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var b in tackle.GetComponentsInChildren<Rigidbody>(true)){b.isKinematic=true;b.detectCollisions=false;}
        foreach(var script in tackle.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
        tie=Find(tackle.transform,"LineAttach");
        animator=tackle.GetComponentInChildren<Animator>(true);
        if(animator!=null){animator.applyRootMotion=false;animator.speed=0f;animator.Update(0f);}
        Transform frame=Find(tackle.transform,"RetrieveFrame");
        if(frame!=null)
            retrieveFrame=Quaternion.LookRotation(tackle.transform.InverseTransformDirection(frame.forward),tackle.transform.InverseTransformDirection(frame.up));
        else if(tie!=null && tie.parent!=tackle.transform)
            retrieveFrame=Quaternion.LookRotation(tackle.transform.InverseTransformDirection(tie.parent.TransformVector(Vector3.down)),
                tackle.transform.InverseTransformDirection(tie.parent.TransformVector(Vector3.forward)));
        else retrieveFrame=Quaternion.identity;
    }
    private static Transform Find(Transform root,string name)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
        return null;
    }
    private static void SetColor(Material material,Color color)
    {
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Color"))material.SetColor("_Color",color);
    }
    private void Hide(){if(line!=null)line.enabled=false;if(tackle!=null)tackle.SetActive(false);HideDistance();}
    private void ClearTackle()
    {
        if(tackle!=null){tackle.SetActive(false);Destroy(tackle);}
        tackle=null;tie=null;animator=null;
    }
    private void OnDisable(){Hide();positionReady=false;lightningInitialized=false;ClearLightning();}
    private void OnDestroy(){ClearLightning();ClearTackle();if(distanceRoot!=null)Destroy(distanceRoot);if(lineMaterial!=null)Destroy(lineMaterial);}
}

