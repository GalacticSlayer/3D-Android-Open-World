using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// One reusable off-screen studio. Static snapshots avoid one camera per inventory row.
public sealed class ShopPreview : MonoBehaviour
{
    private readonly Dictionary<string,RenderTexture> cache=new Dictionary<string,RenderTexture>();
    // Static reel thumbnails are prepared once per tier and reused across UI cards.
    private readonly Dictionary<string,Texture2D> reelIcons=new Dictionary<string,Texture2D>();
    private readonly Queue<System.Action> requests=new Queue<System.Action>();
    private static int studioCount;
    private Camera studio;
    private GameObject stage, model;
    private bool busy;
    private Material itemMaterial;
    private RenderTexture rendering;
    public void Attach(RawImage target,int species,float kg,string gear=null,bool fitWholeFish=false,
        bool transparentBackground=false,bool faceRight=false)
    {
        target.enabled=false;
        string resolvedGear=ResolveGearKey(target,gear);
        // The three bait illustrations are kept in a single transparent atlas:
        // worm, shrimp, squid (left to right). All preview consumers share this
        // path, including the shortcut, bait list, tackle store, and fish index.
        int baitCell=resolvedGear=="Bait0" || resolvedGear=="Bait1"?0:
                     resolvedGear=="Bait2"?1:resolvedGear=="Bait3"?2:-1;
        // Leave each bait's surrounding icon frame unchanged. Scale only the
        // artwork inside it by 15%, producing an even transparent margin.
        // Restore the equipped shortcut when switching back to a lure.
        if(baitCell>=0)
            target.rectTransform.localScale=new Vector3(.85f,.85f,1f);
        else if(target.name=="EquippedBaitPicture")
            target.rectTransform.localScale=Vector3.one;
        if(baitCell>=0)
        {
            Texture2D baitIcons=Resources.Load<Texture2D>("Fishing/BaitIconsAtlas");
            if(baitIcons!=null)
            {
                target.texture=baitIcons;
                target.uvRect=new Rect(baitCell/3f,0f,1f/3f,1f);
                target.enabled=true;
                return;
            }
        }
        // These changes affect UI imagery only; rod/reel/line mechanics stay unchanged.
        // Line artwork is displayed separately from the blue background so only
        // the transparent spool can be made 15% smaller.
        if(resolvedGear!=null)
        {
            bool rod=resolvedGear.StartsWith("Rod",System.StringComparison.Ordinal) && resolvedGear!="RodAssembly";
            bool line=resolvedGear=="Line" || resolvedGear.StartsWith("Line",System.StringComparison.Ordinal);
            bool reel=resolvedGear.StartsWith("Reel",System.StringComparison.Ordinal);
            if(line)
            {
                Texture2D transparentLine=Resources.Load<Texture2D>("Fishing/ItemIcons/Line");
                if(transparentLine!=null && SetWaterBackground(target))
                {
                    target.rectTransform.localScale=new Vector3(.85f,.85f,1f);
                    target.rectTransform.localRotation=Quaternion.identity;
                    // Align the spool relative to the fixed, unshifted blue card.
                    RectTransform lineBackground=target.transform.parent.Find("FishingItemWaterBackground") as RectTransform;
                    if(lineBackground!=null)target.rectTransform.anchoredPosition=lineBackground.anchoredPosition+Vector2.right*9f;
                    target.texture=transparentLine;
                    target.uvRect=new Rect(0f,0f,1f,1f);
                    target.color=Color.white;
                    target.enabled=true;
                    return;
                }
            }
            if(reel && SetWaterBackground(target))
            {
                // No large or rotated RectTransform: that was moving the reel
                // outside the icon mask. Instead the rendered reel is rotated,
                // centred and enlarged WITHIN a square texture below.
                target.rectTransform.localRotation=Quaternion.identity;
                target.rectTransform.localScale=Vector3.one;
                RectTransform reelBackground=target.transform.parent.Find("FishingItemWaterBackground") as RectTransform;
                if(reelBackground!=null)target.rectTransform.anchoredPosition=reelBackground.anchoredPosition;
                target.color=Color.white;
            }
            if(rod || line)
            {
                // Existing pre-composited artwork is retained as a fallback.
                string asset=rod?resolvedGear:"Line";
                Texture2D artwork=Resources.Load<Texture2D>("Fishing/ItemIcons/"+asset+"Card");
                if(artwork!=null)
                {
                    target.texture=artwork;
                    target.uvRect=new Rect(0,0,1,1);
                    target.color=Color.white;
                    target.enabled=true;
                    return;
                }
            }
        }
        // Cached render textures and lure/gear previews use their full UVs.
        target.uvRect=new Rect(0f,0f,1f,1f);
        string key=(transparentBackground?"clear:":"")+(faceRight?"right:":"")+(fitWholeFish?"index:":"")+(resolvedGear??(species+":"+kg.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        // A reel's original 3D preview is captured once, then cropped,
        // rotated and packed into this cache so each card stays centred.
        if(resolvedGear!=null && resolvedGear.StartsWith("Reel",System.StringComparison.Ordinal)
            && reelIcons.TryGetValue(key,out Texture2D reelIcon) && reelIcon!=null)
        {
            target.texture=reelIcon;target.enabled=true;return;
        }
        if(cache.TryGetValue(key,out var ready))
        {
            if(ready!=null && ready.IsCreated())
            {
                target.texture=ready;target.enabled=true;return;
            }
            cache.Remove(key);
            if(ready!=null)Destroy(ready);
        }
        requests.Enqueue(()=> {if(target!=null)StartCoroutine(Render(target,key,species,kg,resolvedGear,fitWholeFish,transparentBackground,faceRight));else busy=false;});
    }
    private void Update() { if(!busy && requests.Count>0) {busy=true;requests.Dequeue()();} }
    private IEnumerator Render(RawImage target,string key,int species,float kg,string gear,bool fitWholeFish,
        bool transparentBackground,bool faceRight)
    {
        if(cache.TryGetValue(key,out var existing) && existing!=null && existing.IsCreated())
        {target.texture=existing;target.enabled=true;busy=false;yield break;}
        if(stage==null)
        {
            stage=new GameObject("InventoryPreviewStudio");stage.transform.position=new Vector3(10000+100*studioCount++,10000,10000);
            studio=new GameObject("PreviewCamera").AddComponent<Camera>();studio.transform.SetParent(stage.transform,false);
            studio.clearFlags=CameraClearFlags.SolidColor;studio.backgroundColor=new Color(0.08f,0.18f,0.20f);
            studio.orthographic=true;studio.nearClipPlane=0.01f;studio.farClipPlane=15;studio.cullingMask=1<<30;studio.enabled=false;
        }
        // Reels render with alpha so the new blue water remains behind them.
        bool reelWithBackdrop=gear!=null && gear.StartsWith("Reel",System.StringComparison.Ordinal)
            && target!=null && target.transform.parent!=null
            && target.transform.parent.Find("FishingItemWaterBackground")!=null;
        studio.backgroundColor=transparentBackground || gear=="RodAssembly" || reelWithBackdrop
            ?Color.clear:new Color(0.08f,0.18f,0.20f);
        float aspect=transparentBackground?2f:1f;
        studio.aspect=aspect;
        bool lurePreview=gear!=null && gear.StartsWith("Lure",System.StringComparison.Ordinal);
        bool reelPreview=gear!=null && gear.StartsWith("Reel",System.StringComparison.Ordinal);
        bool rodPreview=gear!=null && gear.StartsWith("Rod",System.StringComparison.Ordinal) && gear!="RodAssembly";
        if(gear==null)model=FishWorldSize.Create("FishPreview",stage.transform,species,kg);
        else
        {
            GameObject prefab=null;
            if(gear=="RodAssembly")
                prefab=Resources.Load<GameObject>("Fishing/FishingRodReel");
            else if(rodPreview)
            {
                int tier=0;
                if(gear.Length>3)int.TryParse(gear.Substring(3),out tier);
                prefab=Resources.Load<GameObject>(ShopCatalog.RodPrefabResource(tier));
                if(prefab==null && tier>0)prefab=Resources.Load<GameObject>(ShopCatalog.RodPrefabResource(0));
            }
            else if(reelPreview)
            {
                int tier=0;
                if(gear.Length>4)int.TryParse(gear.Substring(4),out tier);
                prefab=Resources.Load<GameObject>(ShopCatalog.ReelPrefabResource(tier));
                if(prefab==null && tier>0)prefab=Resources.Load<GameObject>(ShopCatalog.ReelPrefabResource(0));
            }
            else if(lurePreview)
            {
                int variant=0;
                if(gear.Length>4)int.TryParse(gear.Substring(4),out variant);
                prefab=Resources.Load<GameObject>(ShopCatalog.LurePrefabResource(variant));
            }
            if(prefab!=null)model=Instantiate(prefab,stage.transform);
            else model=BuildItem(gear);
            model.transform.SetParent(stage.transform,false);
        }
        foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
        if(transparentBackground)
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))
            {animator.applyRootMotion=false;animator.Update(0f);animator.enabled=false;}
        foreach(var collider in model.GetComponentsInChildren<Collider>())collider.enabled=false;
        foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
        if(rodPreview || reelPreview)
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                bool reel=false;for(var t=renderer.transform;t!=null && t!=model.transform;t=t.parent)if(t.name=="ReelMount")reel=true;
                renderer.enabled=reelPreview?reel:!reel;
            }
        }

        if(lurePreview)
        {
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion=false;animator.speed=0f;animator.Update(0f);animator.enabled=false;
            }
            OrientLureSideProfile(model);
        }
        else if(reelPreview)
        {
            OrientReelLikeReference(model);
            // Show the opposite face by yawing the physical reel 180 degrees.
            model.transform.rotation=Quaternion.AngleAxis(180f,Vector3.up)*model.transform.rotation;
        }
        else
        {
            model.transform.localRotation=rodPreview || gear=="RodAssembly"?Quaternion.Euler(0,-90,40):Quaternion.Euler(0,faceRight?90:-90,0);
            // All fish UI previews show the flatfish's brown upper side, like the fish index.
            // The default bag/transfer thumbnails face left; index cards face right.
            // Reverse the roll for left-facing previews so both show the same brown side.
            if(gear==null && FishCatalog.IsFlatfish(species))
                model.transform.localRotation*=Quaternion.AngleAxis(faceRight?-90f:90f,Vector3.forward);
        }

        var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        if(renderers.Length==0){Destroy(model);busy=false;yield break;}
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(gear==null && !fitWholeFish)
        {
            studio.orthographic=true;
            studio.orthographicSize=0.26f;
            Vector3 head=new Vector3(bounds.min.x+0.18f,bounds.center.y,bounds.center.z);
            studio.transform.position=head+Vector3.back*5;
            studio.transform.rotation=Quaternion.identity;
        }
        else if(reelPreview)
        {
            studio.orthographic=false;
            studio.fieldOfView=30f;
            float halfFov=studio.fieldOfView*Mathf.Deg2Rad*0.5f;
            float required=Mathf.Max(bounds.extents.y/Mathf.Tan(halfFov),bounds.extents.x/Mathf.Tan(halfFov));
            float distance=(required+bounds.extents.z)*1.38f;
            Vector3 previewTarget=bounds.center;
            studio.transform.position=previewTarget+Vector3.back*distance;
            studio.transform.LookAt(previewTarget,Vector3.up);
        }
        else
        {
            studio.orthographic=true;
            float margin=lurePreview?0.66f:rodPreview?0.62f:0.58f;
            studio.orthographicSize=Mathf.Max(bounds.size.x/aspect,bounds.size.y)*margin;
            studio.transform.position=bounds.center+Vector3.back*(bounds.size.z+5);
            studio.transform.rotation=Quaternion.identity;
        }
        var texture=new RenderTexture(transparentBackground?384:192,192,16,RenderTextureFormat.ARGB32){name="Inventory "+key,wrapMode=TextureWrapMode.Clamp};texture.Create();rendering=texture;
        studio.targetTexture=texture;studio.enabled=true;
        yield return new WaitForEndOfFrame();
        // Read back the actual visible silhouette rather than its mesh pivot.
        // This corrects the bottom-heavy offset shown in the Equipment menu.
        Texture2D centredReel=reelPreview && reelWithBackdrop ? MakeCentredReelIcon(texture) : null;
        studio.enabled=false;studio.targetTexture=null;Destroy(model);model=null;
        if(centredReel!=null)
        {
            reelIcons[key]=centredReel;
            texture.Release();
            Destroy(texture);
        }
        else cache[key]=texture;
        rendering=null;
        if(target!=null){target.texture=centredReel!=null?(Texture)centredReel:texture;target.enabled=true;}
        busy=false;
    }

    // Composites the already-rendered 3D reel into a fixed 256px transparent
    // square. The final orientation is 90 degrees CCW from the prior -30
    // degree UI presentation, i.e. +60 degrees. Reels remain 1.69x the
    // original icon scale unless that would cause them to leave the card.
    private static Texture2D MakeCentredReelIcon(RenderTexture source)
    {
        if(source==null)return null;
        RenderTexture previous=RenderTexture.active;
        Texture2D snapshot=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
        Color32[] pixels;
        try
        {
            RenderTexture.active=source;
            snapshot.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);
            snapshot.Apply(false,false);
            pixels=snapshot.GetPixels32();
        }
        finally
        {
            RenderTexture.active=previous;
            Destroy(snapshot);
        }

        int width=source.width,height=source.height;
        int minX=width,minY=height,maxX=-1,maxY=-1;
        // Some graphics pipelines give an opaque black alpha channel to a
        // clear camera background; account for that when finding the reel.
        Color32 corner=pixels[0];
        bool opaqueBlackBackground=corner.a>240 &&
            corner.r<12 && corner.g<12 && corner.b<12;
        for(int y=0;y<height;y++)
        for(int x=0;x<width;x++)
        {
            Color32 p=pixels[y*width+x];
            bool visible=opaqueBlackBackground
                ? (p.r>18 || p.g>18 || p.b>18)
                : p.a>28;
            if(!visible)continue;
            minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);
            minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
        }
        if(maxX<minX || maxY<minY)return null;

        float cx=(minX+maxX)*.5f,cy=(minY+maxY)*.5f;
        const float rotationDegrees=60f; // -30 degrees + 90 degrees CCW
        float radians=rotationDegrees*Mathf.Deg2Rad;
        float cos=Mathf.Cos(radians),sin=Mathf.Sin(radians);
        float spanX=Mathf.Abs(cos)*(maxX-minX+1)+Mathf.Abs(sin)*(maxY-minY+1);
        float spanY=Mathf.Abs(sin)*(maxX-minX+1)+Mathf.Abs(cos)*(maxY-minY+1);
        // At most 169% of the original size; fit safely within 90% of the
        // blue square even for unusual reel models and different tiers.
        float scale=Mathf.Min(1.69f,
            .90f*Mathf.Min(width/Mathf.Max(spanX,1f),height/Mathf.Max(spanY,1f)));

        const int outputSize=256;
        Color32[] centred=new Color32[outputSize*outputSize];
        for(int y=0;y<outputSize;y++)
        for(int x=0;x<outputSize;x++)
        {
            // Inverse mapping: every output pixel asks where it originated.
            float dx=((x+.5f)/outputSize-.5f)*width/scale;
            float dy=((y+.5f)/outputSize-.5f)*height/scale;
            int sx=Mathf.RoundToInt(cx+dx*cos+dy*sin);
            int sy=Mathf.RoundToInt(cy-dx*sin+dy*cos);
            if(sx>=0 && sx<width && sy>=0 && sy<height)
            {
                Color32 p=pixels[sy*width+sx];
                // Avoid exposing an opaque black camera clear colour.
                if(opaqueBlackBackground && p.r<=18 && p.g<=18 && p.b<=18)
                    p.a=0;
                centred[y*outputSize+x]=p;
            }
        }
        Texture2D result=new Texture2D(outputSize,outputSize,TextureFormat.RGBA32,false);
        result.name="CenteredReelPreview";
        result.wrapMode=TextureWrapMode.Clamp;
        result.filterMode=FilterMode.Bilinear;
        result.SetPixels32(centred);
        result.Apply(false,true);
        return result;
    }

    // Put the SAME water texture used by the authored rod/line cards behind a
    // transparent item image. The sibling background is never rotated/scaled.
    private static bool SetWaterBackground(RawImage target)
    {
        if(target==null || target.transform.parent==null)return false;
        Texture2D water=Resources.Load<Texture2D>("Fishing/ItemIcons/WaterBackground");
        if(water==null)return false;

        Transform parent=target.transform.parent;
        Transform existing=parent.Find("FishingItemWaterBackground");
        GameObject background=existing!=null?existing.gameObject:
            new GameObject("FishingItemWaterBackground",typeof(RectTransform),typeof(RawImage));
        if(existing==null)background.transform.SetParent(parent,false);

        RectTransform source=target.rectTransform;
        RectTransform rect=background.GetComponent<RectTransform>();
        if(existing==null)
        {
            rect.anchorMin=source.anchorMin;
            rect.anchorMax=source.anchorMax;
            rect.pivot=source.pivot;
            rect.anchoredPosition=source.anchoredPosition;
            rect.sizeDelta=source.sizeDelta;
            rect.localScale=Vector3.one;
            rect.localRotation=Quaternion.identity;
        }

        RawImage image=background.GetComponent<RawImage>();
        image.texture=water;
        image.color=Color.white;
        image.uvRect=new Rect(0f,0f,1f,1f);
        image.raycastTarget=false;
        background.transform.SetSiblingIndex(target.transform.GetSiblingIndex());

        // Existing shop/equipment cards already have a rounded Image frame.
        // Use its exact alpha silhouette to clip both the water and the item.
        if(parent.name=="ItemImageFrame" && parent.GetComponent<Image>()!=null)
        {
            Mask mask=parent.GetComponent<Mask>();
            if(mask==null)mask=parent.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic=true;
        }
        return true;
    }

    private static string ResolveGearKey(RawImage target,string gear)
    {
        // ShopWorldHUD historically asks for Bait4 before it knows which permanent
        // lure variant is equipped. Resolve that request here, before any cache key
        // or placeholder render is created. This removes both the old bait-symbol
        // flash and the black preview that could appear after closing/reopening.
        if(gear=="Bait4")return ShopCatalog.LurePreviewKey(ShopCatalog.ActiveLureVariant);
        if((gear!="Reel" && gear!="Rod") || target==null)return gear;
        bool reel=gear=="Reel";
        Transform row=target.transform;
        for(int i=0;i<8 && row!=null;i++,row=row.parent)
        {
            Text[] labels=row.GetComponentsInChildren<Text>(true);
            string[] names=reel?ShopCatalog.ReelNames:ShopCatalog.RodNames;
            for(int tier=names.Length-1;tier>=0;tier--)
                if(labels.Any(t=>t!=null && string.Equals(t.text,names[tier],System.StringComparison.OrdinalIgnoreCase)))
                    return (reel?"Reel":"Rod")+tier;
            if(row.GetComponent<LayoutElement>()!=null)break;
        }
        return reel?"Reel0":"Rod0";
    }

    private static void OrientReelLikeReference(GameObject reel)
    {
        Transform mount=FindDeepChild(reel.transform,"ReelMount");
        Transform spool=FindDeepChild(reel.transform,"Spool");
        Transform handle=FindDeepChild(reel.transform,"Handle");
        Renderer spoolRenderer=spool!=null?spool.GetComponent<Renderer>():null;
        Renderer handleRenderer=handle!=null?handle.GetComponent<Renderer>():null;
        if(mount==null || spoolRenderer==null || handleRenderer==null)
        {reel.transform.localRotation=Quaternion.Euler(0,-22,0);return;}

        Vector3 centre=spoolRenderer.bounds.center;
        Vector3 sourceUp=mount.position-centre;
        if(sourceUp.sqrMagnitude<0.000001f){reel.transform.localRotation=Quaternion.Euler(0,-22,0);return;}
        sourceUp.Normalize();
        Vector3 sourceRight=Vector3.ProjectOnPlane(handleRenderer.bounds.center-centre,sourceUp);
        if(sourceRight.sqrMagnitude<0.000001f){reel.transform.localRotation=Quaternion.Euler(0,-22,0);return;}
        sourceRight.Normalize();
        Vector3 sourceForward=Vector3.Cross(sourceRight,sourceUp).normalized;
        Quaternion sourceBasis=Quaternion.LookRotation(sourceForward,sourceUp);
        Quaternion targetBasis=Quaternion.LookRotation(Vector3.forward,Vector3.up);
        Quaternion aligned=(targetBasis*Quaternion.Inverse(sourceBasis))*reel.transform.rotation;
        reel.transform.rotation=Quaternion.AngleAxis(-22f,Vector3.up)*aligned;
    }

    private static void OrientLureSideProfile(GameObject lure)
    {
        if(lure==null)return;
        Transform previewFrame=FindDeepChild(lure.transform,"PreviewFrame");
        if(previewFrame!=null)
        {
            Quaternion source=Quaternion.LookRotation(previewFrame.forward,previewFrame.up);
            lure.transform.rotation=Quaternion.LookRotation(Vector3.left,Vector3.up)*
                Quaternion.Inverse(source)*lure.transform.rotation;
            return;
        }
        Transform lineAttach=FindDeepChild(lure.transform,"LineAttach");
        Renderer[] renderers=lure.GetComponentsInChildren<Renderer>(true)
            .Where(r=>r!=null && r.enabled && !IsUnderLightingRig(r.transform,lure.transform)).ToArray();
        if(lineAttach==null || renderers.Length<2)return;

        Renderer body=renderers[0];float bodyScore=-1f;
        for(int i=0;i<renderers.Length;i++)
        {
            Vector3 size=renderers[i].bounds.size;
            float score=Mathf.Max(size.x,Mathf.Max(size.y,size.z))*Mathf.Max(0.000001f,size.x*size.y*size.z);
            if(score>bodyScore){bodyScore=score;body=renderers[i];}
        }

        Vector3 centre=body.bounds.center;Vector3 head=lineAttach.position-centre;
        if(head.sqrMagnitude<0.000001f)return;head.Normalize();
        Renderer bellyHook=null;Vector3 belly=Vector3.zero;float bestPerpendicular=-1f;
        for(int i=0;i<renderers.Length;i++)
        {
            Renderer candidate=renderers[i];if(candidate==body)continue;
            Vector3 fromBody=candidate.bounds.center-centre;
            Vector3 perpendicular=fromBody-Vector3.Dot(fromBody,head)*head;
            float score=perpendicular.sqrMagnitude;
            if(score>bestPerpendicular){bestPerpendicular=score;bellyHook=candidate;belly=perpendicular;}
        }
        if(bellyHook==null || belly.sqrMagnitude<0.000001f)return;belly.Normalize();
        Quaternion sourceBasis=Quaternion.LookRotation(head,-belly);
        Quaternion targetBasis=Quaternion.LookRotation(Vector3.left,Vector3.up);
        lure.transform.rotation=(targetBasis*Quaternion.Inverse(sourceBasis))*lure.transform.rotation;
    }

    private static bool IsUnderLightingRig(Transform transform,Transform root)
    {
        for(Transform current=transform;current!=null && current!=root;current=current.parent)
            if(current.name=="AuthoredLightingRig")return true;
        return false;
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        Transform[] all=root.GetComponentsInChildren<Transform>(true);
        for(int i=0;i<all.Length;i++)if(all[i]!=null && all[i].name==name)return all[i];
        return null;
    }

    private GameObject BuildItem(string kind)
    {
        var root=new GameObject(kind);root.transform.SetParent(stage.transform,false);
        if(itemMaterial==null)itemMaterial=new Material(Resources.Load<Shader>("Fishing/ShopSurface"));
        System.Action<PrimitiveType,Vector3,Vector3,Color> part=(type,pos,size,color)=>
        {
            var go=GameObject.CreatePrimitive(type);go.transform.SetParent(root.transform,false);go.transform.localPosition=pos;go.transform.localScale=size;
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=itemMaterial;
            var props=new MaterialPropertyBlock();props.SetColor("_BaseColor",color);renderer.SetPropertyBlock(props);
        };
        if(kind=="Bait4")
        {
            // Kept only as a defensive fallback for old save/UI code. ResolveGearKey
            // converts current lure requests to Lure# before reaching this branch.
            part(PrimitiveType.Capsule,Vector3.zero,new Vector3(.2f,.42f,.16f),new Color(.82f,.89f,.94f));
            part(PrimitiveType.Sphere,new Vector3(0,.28f,-.04f),new Vector3(.19f,.2f,.17f),new Color(.85f,.15f,.08f));
            part(PrimitiveType.Cylinder,new Vector3(0,-.5f,0),new Vector3(.025f,.12f,.025f),Color.gray);
            for(int i=-1;i<=1;i++)part(PrimitiveType.Sphere,new Vector3(i*.065f,-.59f,0),Vector3.one*.07f,Color.gray);
        }
        else if(kind=="Line")
        {
            part(PrimitiveType.Cylinder,Vector3.zero,new Vector3(0.65f,0.2f,0.65f),new Color(0.65f,0.83f,0.86f));
            foreach(int side in new[]{-1,1})part(PrimitiveType.Cylinder,Vector3.up*side*0.21f,new Vector3(0.85f,0.025f,0.85f),Color.gray);
        }
        else if(kind=="Bait3")
        {
            part(PrimitiveType.Capsule,Vector3.up*0.2f,new Vector3(0.24f,0.25f,0.24f),new Color(0.8f,0.6f,0.65f));
            for(int i=0;i<6;i++)part(PrimitiveType.Capsule,new Vector3((i-2.5f)*0.055f,-0.22f,0),new Vector3(0.035f,0.18f,0.035f),new Color(0.9f,0.7f,0.75f));
        }
        else if(kind=="Bait0" || kind=="Bait1" || kind=="Bait2")
        {
            for(int i=0;i<8;i++)part(PrimitiveType.Sphere,new Vector3(Mathf.Sin(i*0.4f)*0.23f,(i-4)*0.075f,0),Vector3.one*(kind!="Bait2"?0.10f:0.14f),kind!="Bait2"?new Color(0.5f,0.21f,0.16f):new Color(1,0.55f,0.4f));
        }
        else part(PrimitiveType.Capsule,Vector3.zero,new Vector3(0.2f,0.35f,0.2f),new Color(0.9f,0.65f,0.15f));
        root.transform.localRotation=Quaternion.Euler(20,0,20);return root;
    }
    public void Suspend()
    {
        StopAllCoroutines(); requests.Clear();busy=false;
        if(rendering!=null){rendering.Release();Destroy(rendering);rendering=null;}
        if(studio!=null){studio.enabled=false;studio.targetTexture=null;}
        if(model!=null)Destroy(model);
        if(cache.Count>96){foreach(var t in cache.Values){if(t!=null){t.Release();Destroy(t);}}cache.Clear();}
    }
    public void Clear()
    {
        Suspend();foreach(var t in cache.Values){if(t!=null){t.Release();Destroy(t);}}cache.Clear();
        foreach(var icon in reelIcons.Values)if(icon!=null)Destroy(icon);
        reelIcons.Clear();
    }
    private void OnDestroy(){Clear();if(stage!=null)Destroy(stage);if(itemMaterial!=null)Destroy(itemMaterial);}
}

