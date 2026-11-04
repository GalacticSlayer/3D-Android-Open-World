using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Home-only placement, deliberately parallel to the boat's translucent preview.
// The ledger is never mutated on Begin or Cancel: residents and the old position
// are therefore safe even if the player leaves Home halfway through moving.
[DisallowMultipleComponent]
public sealed class HomeAquariumPlacement : MonoBehaviour
{
    public static bool IsPlacing { get; private set; }
    public static bool StorageOpen { get; private set; }
    public static string PreviewKey { get; private set; }

    private const float InteractionRange=5.5f;
    private const float PreviewDistance=20f;
    private const float StoragePlacementExclusionRadius=2.5f;
    private const float StorageSignVisibilityRange=25f;
    private ShopDimensionManager travel;
    private ShopProgress progress;
    private FirstPersonController controller;
    private FishingMenuSafetyRuntime menuSafety;
    private ShopWorldEnvironment world;
    private GameObject storageBox,ghost,canvasRoot,storagePanel;
    private MeshRenderer storageSignRenderer;
    private ShopHabitat hiddenSource,nearest;
    private Material previewMaterial,boxMaterial,storageSignMaterial;
    private Button contextButton,moveToStorageButton,placeButton,cancelButton;
    private Text contextText,placementHint;
    private RectTransform storageRows;
    private Font font;
    private readonly List<Renderer> decorativeObstacles=new List<Renderer>();
    private string pendingKey;
    private bool valid;
    private Vector3 localTarget;
    private float localYaw;

    private void Start()
    {
        travel=GetComponent<ShopDimensionManager>();
        progress=GetComponent<ShopProgress>();
        controller=GetComponent<FirstPersonController>();
        menuSafety=FindFirstObjectByType<FishingMenuSafetyRuntime>();
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }
    private void OnDisable(){CancelPlacement();CloseStorage();}
    private void OnDestroy()
    {
        CancelPlacement();CloseStorage();
        if(canvasRoot!=null)Destroy(canvasRoot);
        if(previewMaterial!=null)Destroy(previewMaterial);
        if(boxMaterial!=null)Destroy(boxMaterial);
        if(storageSignMaterial!=null)Destroy(storageSignMaterial);
    }
    private bool AtHome => travel!=null && travel.InHome && !travel.Traveling &&
        travel.World!=null && progress!=null && !progress.ReadOnly &&
        (HomeDirectoryClient.Instance==null || !HomeDirectoryClient.Instance.IsVisiting);

    private void Update()
    {
        if(canvasRoot==null)return;
        if(!AtHome)
        {
            CancelPlacement();CloseStorage();
            if(storageBox!=null)Destroy(storageBox);
            storageBox=null;storageSignRenderer=null;world=null;
            contextButton.gameObject.SetActive(false);
            moveToStorageButton.gameObject.SetActive(false);
            return;
        }
        if(world!=travel.World)
        {
            CancelPlacement();CloseStorage();
            world=travel.World;
            decorativeObstacles.Clear();
            foreach(var renderer in world.GetComponentsInChildren<Renderer>(true))
                if(renderer.name=="TreeCrown" || renderer.name=="Lantern")
                    decorativeObstacles.Add(renderer);
            CreateStorageBox();
        }
        if(!string.IsNullOrEmpty(pendingKey))
        {
            contextButton.gameObject.SetActive(false);
            moveToStorageButton.gameObject.SetActive(false);
            if(ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen)
            {CancelPlacement();return;}
            UpdatePreview();
            return;
        }
        placeButton.gameObject.SetActive(false);
        cancelButton.gameObject.SetActive(false);
        placementHint.gameObject.SetActive(false);

        float boxDistance=storageBox!=null?Vector3.Distance(transform.position,storageBox.transform.position):float.MaxValue;
        // Match other Home exhibit signs: do not draw label text across the courtyard.
        if(storageSignRenderer!=null)
        {
            bool showSign=boxDistance<=StorageSignVisibilityRange;
            if(storageSignRenderer.enabled!=showSign)storageSignRenderer.enabled=showSign;
        }
        if(StorageOpen)
        {
            contextButton.gameObject.SetActive(false);
            moveToStorageButton.gameObject.SetActive(false);
            if(boxDistance>InteractionRange || ShopWorldHUD.MenuOpen)CloseStorage();
            return;
        }
        nearest=NearestAquarium(out float aquariumDistance);
        // The passive Fish Index intentionally leaves ShopWorldHUD.MenuOpen false.
        // Hide BOTH aquarium buttons while that overlay is displayed.
        if(menuSafety==null)menuSafety=FindFirstObjectByType<FishingMenuSafetyRuntime>();
        bool canInteract=!ShopWorldHUD.MenuOpen && !FishingTournament.ModalOpen &&
            (menuSafety==null || !menuSafety.IsIndexOpen);
        bool boxNearest=boxDistance<=InteractionRange && boxDistance<=aquariumDistance;
        bool show=canInteract && (boxNearest || nearest!=null);
        contextButton.gameObject.SetActive(show);
        var selected=!boxNearest && nearest!=null
            ?progress.Data.Habitat(nearest.instanceKey):null;
        bool canStore=show && selected!=null && selected.placed &&
            selected.fish!=null && selected.fish.Count==0;
        moveToStorageButton.gameObject.SetActive(canStore);
        // Only reposition the existing button while its optional companion is visible.
        contextButton.GetComponent<RectTransform>().anchoredPosition=
            new Vector2(canStore?-225f:0f,230f);
        if(!show)return;
        contextText.text=boxNearest?"AQUARIUM STORAGE":"MOVE AQUARIUM";
        // E remains assigned to the existing fish management interaction.
        currentContextIsBox=boxNearest;
    }

    private bool currentContextIsBox;
    private ShopHabitat NearestAquarium(out float bestDistance)
    {
        bestDistance=float.MaxValue;
        ShopHabitat result=null;
        if(world==null)return null;
        foreach(var h in world.ActiveHabitats())
        {
            if(h==null || string.IsNullOrEmpty(h.instanceKey))continue;
            var d=ShopCatalog.Habitat(h.id);
            if(d==null || d.pond)continue;
            Vector3 local=h.transform.InverseTransformPoint(transform.position);
            float x=Mathf.Max(0,Mathf.Abs(local.x)-d.width*0.5f);
            float z=Mathf.Max(0,Mathf.Abs(local.z)-d.depth*0.5f);
            float distance=Mathf.Sqrt(x*x+z*z);
            if(distance<bestDistance && distance<=InteractionRange)
            {result=h;bestDistance=distance;}
        }
        return result;
    }
    private void ContextAction()
    {
        if(currentContextIsBox)OpenStorage();
        else if(nearest!=null)Begin(nearest.instanceKey);
    }

    private void MoveToStorage()
    {
        if(!AtHome || StorageOpen || IsPlacing || ShopWorldHUD.MenuOpen ||
            FishingTournament.ModalOpen)return;
        // Recheck proximity and ownership at the moment of the click. The ledger
        // independently refuses storage if any fish has been added in the meantime.
        var aquarium=NearestAquarium(out float distance);
        if(aquarium==null || aquarium!=nearest || distance>InteractionRange)return;
        var owned=progress.Data.Habitat(aquarium.instanceKey);
        if(owned==null || !owned.placed || owned.fish==null || owned.fish.Count!=0)return;
        var definition=ShopCatalog.Habitat(owned.id);
        if(definition==null || definition.pond)return;

        // Do not remove a swimmable aquarium while the player is inside it.
        Vector3 local=aquarium.transform.InverseTransformPoint(transform.position);
        if(Mathf.Abs(local.x)<definition.width*.5f &&
           Mathf.Abs(local.z)<definition.depth*.5f &&
           transform.position.y>aquarium.transform.position.y+.4f)return;

        if(!progress.StoreAquarium(aquarium.instanceKey))return;
        nearest=null;
        moveToStorageButton.gameObject.SetActive(false);
        contextButton.gameObject.SetActive(false);
    }

    private void CreateStorageBox()
    {
        if(world==null)return;
        storageBox=new GameObject("Aquarium Storage Box");
        storageBox.transform.SetParent(world.transform,false);
        // The Home arrival is positioned one metre in front of this crate's
        // front edge, facing the label. No extra travel destination is needed.
        storageBox.transform.localPosition=new Vector3(0,0,-9f);
        if(boxMaterial==null)
            boxMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
        if(boxMaterial.HasProperty("_BaseColor"))boxMaterial.SetColor("_BaseColor",new Color(.12f,.42f,.48f));
        if(boxMaterial.HasProperty("_Color"))boxMaterial.color=new Color(.12f,.42f,.48f);
        BoxPart("Storage Crate",new Vector3(0,.55f,0),new Vector3(2.6f,1.1f,1.5f));
        BoxPart("Storage Lid",new Vector3(0,1.16f,0),new Vector3(2.85f,.16f,1.72f));

        // A wooden brown plaque on the front of the crate, not floating text.
        // Keep the original label, orientation, and crate interaction unchanged.
        if(storageSignMaterial==null)
            storageSignMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
        Color brown=new Color(.34f,.18f,.09f,1f);
        if(storageSignMaterial.HasProperty("_BaseColor"))
            storageSignMaterial.SetColor("_BaseColor",brown);
        if(storageSignMaterial.HasProperty("_Color"))
            storageSignMaterial.color=brown;
        var board=GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name="Brown Aquarium Storage Sign";
        board.transform.SetParent(storageBox.transform,false);
        board.transform.localPosition=new Vector3(0,.68f,-.80f);
        board.transform.localScale=new Vector3(2.42f,.78f,.12f);
        board.GetComponent<Renderer>().sharedMaterial=storageSignMaterial;
        Destroy(board.GetComponent<Collider>());

        var sign=new GameObject("Aquarium Storage Label",typeof(TextMesh));
        sign.transform.SetParent(storageBox.transform,false);
        sign.transform.localPosition=new Vector3(0,.68f,-.90f);
        // ShopWorldSetup.Sign uses yaw 0 for front-facing aquarium labels.
        // Rotating 180 degrees mirrors the text when viewed from the Home arrival.
        sign.transform.localRotation=Quaternion.identity;
        var text=sign.GetComponent<TextMesh>();
        text.text="AQUARIUM\nSTORAGE";text.font=font;text.fontSize=68;text.characterSize=.015f;
        text.color=Color.white;
        text.alignment=TextAlignment.Center;text.anchor=TextAnchor.MiddleCenter;

        // Use the actual aquarium sign shader instead of the font's default
        // overlay material: front-face culling + depth testing prevent the text
        // showing through the board or appearing backwards from behind it.
        var example=world.Template("nano");
        Material aquariumSign=example!=null && example.label!=null
            ?example.label.GetComponent<MeshRenderer>().sharedMaterial:null;
        storageSignRenderer=sign.GetComponent<MeshRenderer>();
        storageSignRenderer.sharedMaterial=aquariumSign!=null?aquariumSign:font.material;
        var fit=sign.AddComponent<ShopSign>();
        fit.area=new Vector2(2.18f,.64f);
        fit.Fit();
    }
    private void BoxPart(string name,Vector3 position,Vector3 size)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name=name;part.transform.SetParent(storageBox.transform,false);
        part.transform.localPosition=position;part.transform.localScale=size;
        part.GetComponent<Renderer>().sharedMaterial=boxMaterial;
    }

    private void Begin(string key)
    {
        if(!AtHome || StorageOpen || !string.IsNullOrEmpty(pendingKey) ||
           ShopWorldHUD.MenuOpen)return;
        var ownership=progress.Data.Habitat(key);
        if(ownership==null || ownership.instanceKey!=key)return;
        var d=ShopCatalog.Habitat(ownership.id);
        if(d==null || d.pond)return;
        hiddenSource=world.Habitat(key);
        if(hiddenSource!=null)
        {
            Vector3 pos=hiddenSource.transform.InverseTransformPoint(transform.position);
            if(Mathf.Abs(pos.x)<d.width*.5f && Mathf.Abs(pos.z)<d.depth*.5f &&
               transform.position.y>hiddenSource.transform.position.y+.4f)
            {
                // Never remove the water while a player is swimming inside.
                hiddenSource=null;return;
            }
            hiddenSource.gameObject.SetActive(false);
        }
        var template=world.Template(ownership.id);
        if(template==null){RestoreSource();return;}
        ghost=Instantiate(template.gameObject,world.transform,false);
        foreach(var child in ghost.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.isStatic=false;
            // The Home template is inactive, so AquariumStepGeometryFix does
            // not process it on scene load. Match the actual thin treads here.
            if(child.name=="AccessStep" && child.localScale.y>.09f)
            {
                const float thickness=.09f;
                float top=child.localPosition.y+child.localScale.y*.5f;
                Vector3 scale=child.localScale,position=child.localPosition;
                scale.y=thickness;position.y=top-thickness*.5f;
                child.localScale=scale;child.localPosition=position;
            }
        }
        ghost.name="Aquarium Placement Preview";
        ghost.SetActive(false);
        var habitat=ghost.GetComponent<ShopHabitat>();
        if(habitat!=null)
        {
            habitat.enabled=false;
            if(habitat.fishRoot!=null)habitat.fishRoot.gameObject.SetActive(false);
        }
        foreach(var c in ghost.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var w in ghost.GetComponentsInChildren<ShopWaterVolume>(true))w.enabled=false;
        foreach(var light in ghost.GetComponentsInChildren<Light>(true))light.enabled=false;
        if(previewMaterial==null)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            previewMaterial=new Material(shader);
            if(previewMaterial.HasProperty("_Surface"))previewMaterial.SetFloat("_Surface",1);
            if(previewMaterial.HasProperty("_Blend"))previewMaterial.SetFloat("_Blend",0);
            if(previewMaterial.HasProperty("_SrcBlend"))previewMaterial.SetFloat("_SrcBlend",5);
            if(previewMaterial.HasProperty("_DstBlend"))previewMaterial.SetFloat("_DstBlend",10);
            if(previewMaterial.HasProperty("_ZWrite"))previewMaterial.SetFloat("_ZWrite",0);
            if(previewMaterial.HasProperty("_Cull"))previewMaterial.SetFloat("_Cull",0);
            previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            previewMaterial.renderQueue=3000;
        }
        foreach(var r in ghost.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial=previewMaterial;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows=false;
        }
        ghost.SetActive(true);
        pendingKey=key;PreviewKey=key;IsPlacing=true;valid=false;
        placeButton.gameObject.SetActive(true);
        cancelButton.gameObject.SetActive(true);
        placementHint.gameObject.SetActive(true);
    }

    private void UpdatePreview()
    {
        if(ghost==null || world==null){CancelPlacement();return;}
        Camera view=Camera.main;
        if(view==null)view=GetComponentInChildren<Camera>();
        Vector3 forward=view!=null?Vector3.ProjectOnPlane(view.transform.forward,Vector3.up):transform.forward;
        if(forward.sqrMagnitude<.001f)forward=transform.forward;
        forward.Normalize();
        Vector3 target=transform.position+forward*6f;
        if(view!=null)
        {
            Ray aim=view.ViewportPointToRay(new Vector3(.5f,.5f));
            var plane=new Plane(Vector3.up,world.transform.position);
            if(plane.Raycast(aim,out float distance) && distance>0 && distance<=PreviewDistance &&
               Vector3.Distance(transform.position,aim.GetPoint(distance))<=PreviewDistance)
                target=aim.GetPoint(distance);
        }
        localTarget=world.transform.InverseTransformPoint(target);
        localTarget.y=0;
        localYaw=view!=null?view.transform.eulerAngles.y-world.transform.eulerAngles.y:
            transform.eulerAngles.y-world.transform.eulerAngles.y;
        ghost.transform.localPosition=localTarget;
        ghost.transform.localRotation=Quaternion.Euler(0,localYaw,0);
        valid=CanPlace();
        Color color=valid?new Color(.10f,.92f,.31f,.46f):new Color(.97f,.14f,.15f,.46f);
        if(previewMaterial!=null)
        {
            if(previewMaterial.HasProperty("_BaseColor"))previewMaterial.SetColor("_BaseColor",color);
            if(previewMaterial.HasProperty("_Color"))previewMaterial.color=color;
        }
        placeButton.interactable=valid;
        placementHint.text=valid?"GREEN PREVIEW • PLACE AQUARIUM":
            "RED PREVIEW • Find flat, clear ground inside Home";
    }

    private bool CanPlace()
    {
        if(!AtHome || ghost==null || string.IsNullOrEmpty(pendingKey))return false;
        var owned=progress.Data.Habitat(pendingKey);
        var d=owned!=null?ShopCatalog.Habitat(owned.id):null;
        if(d==null || d.pond)return false;
        if(Vector3.Distance(transform.position,ghost.transform.position)>PreviewDistance)return false;

        // Include the access staircase and sign, not just the glass walls.
        float stair=d.swimmable?Mathf.CeilToInt((.45f+d.height+.3f)/.18f)*.32f:0;
        float left=-d.width*.5f-.55f;
        float right=d.width*.5f+(d.swimmable?2.15f:.55f);
        float front=-d.depth*.5f-Mathf.Max(3.8f,stair+.55f);
        float back=d.depth*.5f+.55f;
        Vector3 center=new Vector3((left+right)*.5f,0,(front+back)*.5f);
        Vector3 half=new Vector3((right-left)*.5f,0,(back-front)*.5f);

        // Block the FULL aquarium footprint (including steps/sign) from the
        // 2.5 m area around the storage crate, not just the tank's pivot.
        if(storageBox!=null)
        {
            Vector3 storageInPreview=ghost.transform.InverseTransformPoint(storageBox.transform.position);
            float closestX=Mathf.Clamp(storageInPreview.x,left,right);
            float closestZ=Mathf.Clamp(storageInPreview.z,front,back);
            float dx=storageInPreview.x-closestX;
            float dz=storageInPreview.z-closestZ;
            if(dx*dx+dz*dz<StoragePlacementExclusionRadius*StoragePlacementExclusionRadius)
                return false;
        }

        // All corners, edges and the center need a real horizontal Home floor.
        // This excludes holes, slopes, objects and partly-off-plot placements.
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
        {
            Vector3 point=ghost.transform.TransformPoint(center+
                new Vector3(x*half.x,0,z*half.z));
            if(!OnFlatHomeFloor(point))return false;
        }

        var boxCenter=ghost.transform.TransformPoint(center+Vector3.up*(d.height+1f)*.5f);
        var halfBox=new Vector3(half.x,(d.height+1f)*.5f,half.z);
        foreach(var hit in Physics.OverlapBox(boxCenter,halfBox,ghost.transform.rotation,
            ~0,QueryTriggerInteraction.Ignore))
        {
            if(hit==null || !hit.enabled || IsHomeFloor(hit) ||
               hit.transform.IsChildOf(ghost.transform) ||
               hit.transform.IsChildOf(transform))continue;
            return false;
        }
        // Tree crowns and lanterns are decorative renderers without colliders
        // in the authored Home. Their geometry must also obstruct placement.
        Vector3 xAxis=ghost.transform.right*half.x;
        Vector3 zAxis=ghost.transform.forward*half.z;
        var visualExtents=new Vector3(Mathf.Abs(xAxis.x)+Mathf.Abs(zAxis.x),
            halfBox.y,Mathf.Abs(xAxis.z)+Mathf.Abs(zAxis.z));
        var area=new Bounds(boxCenter,visualExtents*2);
        foreach(var renderer in decorativeObstacles)
            if(renderer!=null && renderer.gameObject.activeInHierarchy &&
               renderer.bounds.Intersects(area))return false;
        return true;
    }
    private bool OnFlatHomeFloor(Vector3 point)
    {
        foreach(var hit in Physics.RaycastAll(point+Vector3.up*4,Vector3.down,8f,
            ~0,QueryTriggerInteraction.Ignore))
        {
            if(IsHomeFloor(hit.collider) && Vector3.Dot(hit.normal,Vector3.up)>.985f &&
               Mathf.Abs(hit.point.y-world.transform.position.y)<.13f)return true;
        }
        return false;
    }
    private bool IsHomeFloor(Collider c)
    {
        if(c==null || world==null || c.transform.parent!=world.transform)return false;
        switch(c.name)
        {
            case "TiledCourtyard":case "GardenFront":case "GardenRear":
            case "GardenLeft":case "GardenRight":case "GardenWalk":
            case "ReservedPondPlot":return true;
            default:return false;
        }
    }
    private void Place()
    {
        if(!valid || !CanPlace())return; // Always recheck at the moment of purchase.
        if(!progress.PlaceAquarium(pendingKey,localTarget.x,localTarget.y,localTarget.z,localYaw))
            return;
        CancelPlacement();
    }
    private void RestoreSource()
    {
        if(hiddenSource!=null)hiddenSource.gameObject.SetActive(true);
        hiddenSource=null;
    }
    public void CancelPlacement()
    {
        IsPlacing=false;valid=false;pendingKey=null;PreviewKey=null;
        if(ghost!=null)Destroy(ghost);
        ghost=null;
        RestoreSource();
        if(placeButton!=null)placeButton.gameObject.SetActive(false);
        if(cancelButton!=null)cancelButton.gameObject.SetActive(false);
        if(placementHint!=null)placementHint.gameObject.SetActive(false);
    }

    private void OpenStorage()
    {
        if(!AtHome || ShopWorldHUD.MenuOpen || storageBox==null ||
           Vector3.Distance(transform.position,storageBox.transform.position)>InteractionRange)return;
        StorageOpen=true;storagePanel.SetActive(true);
        controller.SetMenuOpen(true);
        RefreshStorageRows();
    }
    public void CloseStorage()
    {
        if(!StorageOpen)return;
        StorageOpen=false;
        if(storagePanel!=null)storagePanel.SetActive(false);
        if(controller!=null && !ShopWorldHUD.MenuOpen)controller.SetMenuOpen(false);
    }
    private void PlaceFromStorage(string key)
    {
        CloseStorage();
        Begin(key);
    }
    private void RefreshStorageRows()
    {
        if(storageRows==null)return;
        for(int i=storageRows.childCount-1;i>=0;i--)
        {
            storageRows.GetChild(i).gameObject.SetActive(false);
            Destroy(storageRows.GetChild(i).gameObject);
        }
        int count=0;
        foreach(var aquarium in progress.Data.habitats)
        {
            if(aquarium==null || aquarium.placed || string.IsNullOrEmpty(aquarium.instanceKey))continue;
            var d=ShopCatalog.Habitat(aquarium.id);
            if(d==null || d.pond)continue;
            count++;
            var row=Panel("Stored Aquarium",storageRows,new Color(.09f,.17f,.2f,.96f));
            row.AddComponent<LayoutElement>().preferredHeight=88;
            string key=aquarium.instanceKey;
            var name=TextAt(row.transform,d.name.ToUpperInvariant()+"  •  "+key,26);
            Stretch(name.rectTransform,new Vector2(0,0),new Vector2(.66f,1),new Vector2(18,0),new Vector2(-6,0));
            var button=ButtonAt(row.transform,"PLACE AQUARIUM",()=>PlaceFromStorage(key));
            Stretch(button.GetComponent<RectTransform>(),new Vector2(.68f,.08f),new Vector2(.99f,.92f),
                Vector2.zero,Vector2.zero);
        }
        if(count==0)
        {
            var text=TextAt(storageRows,"All purchased aquariums are placed. Buy another at Tideglass Quay.",24);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight=115;
        }
    }

    private void BuildUI()
    {
        canvasRoot=new GameObject("HomeAquariumControls",typeof(RectTransform),typeof(Canvas),
            typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=180;
        var scaler=canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);
        scaler.matchWidthOrHeight=.5f;
        contextButton=ButtonAt(canvasRoot.transform,"AQUARIUM STORAGE",ContextAction);
        AnchorBottom(contextButton.GetComponent<RectTransform>(),new Vector2(0,230),new Vector2(420,74));
        contextText=contextButton.GetComponentInChildren<Text>();
        moveToStorageButton=ButtonAt(canvasRoot.transform,"MOVE TO STORAGE",MoveToStorage);
        AnchorBottom(moveToStorageButton.GetComponent<RectTransform>(),new Vector2(225,230),new Vector2(420,74));
        placeButton=ButtonAt(canvasRoot.transform,"PLACE AQUARIUM",Place);
        AnchorBottom(placeButton.GetComponent<RectTransform>(),new Vector2(-235,220),new Vector2(420,80));
        cancelButton=ButtonAt(canvasRoot.transform,"CANCEL",CancelPlacement);
        AnchorBottom(cancelButton.GetComponent<RectTransform>(),new Vector2(235,220),new Vector2(260,80));
        placementHint=TextAt(canvasRoot.transform,"",28);
        placementHint.alignment=TextAnchor.MiddleCenter;
        AnchorBottom(placementHint.rectTransform,new Vector2(0,315),new Vector2(860,62));
        contextButton.gameObject.SetActive(false);
        moveToStorageButton.gameObject.SetActive(false);
        placeButton.gameObject.SetActive(false);
        cancelButton.gameObject.SetActive(false);
        placementHint.gameObject.SetActive(false);

        storagePanel=Panel("Aquarium Storage",canvasRoot.transform,new Color(.035f,.095f,.13f,.985f));
        var rect=storagePanel.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
        rect.sizeDelta=new Vector2(820,740);rect.anchoredPosition=Vector2.zero;
        var header=TextAt(storagePanel.transform,"AQUARIUM STORAGE",38);
        header.alignment=TextAnchor.MiddleCenter;
        Stretch(header.rectTransform,new Vector2(0,.87f),new Vector2(1,1),Vector2.zero,Vector2.zero);
        var close=ButtonAt(storagePanel.transform,"CLOSE",CloseStorage);
        Stretch(close.GetComponent<RectTransform>(),new Vector2(.72f,.02f),new Vector2(.97f,.12f),
            Vector2.zero,Vector2.zero);
        var viewport=Panel("Scroll Window",storagePanel.transform,new Color(.02f,.055f,.08f,1));
        Stretch(viewport.GetComponent<RectTransform>(),new Vector2(.04f,.16f),new Vector2(.96f,.85f),
            Vector2.zero,Vector2.zero);
        viewport.AddComponent<RectMask2D>();
        var scroll=viewport.AddComponent<ScrollRect>();scroll.horizontal=false;
        scroll.movementType=ScrollRect.MovementType.Clamped;
        var content=new GameObject("Stored Aquarium Entries",typeof(RectTransform),
            typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform,false);
        storageRows=content.GetComponent<RectTransform>();
        storageRows.anchorMin=new Vector2(0,1);storageRows.anchorMax=Vector2.one;
        storageRows.pivot=new Vector2(.5f,1);storageRows.anchoredPosition=Vector2.zero;
        storageRows.sizeDelta=Vector2.zero;
        var layout=content.GetComponent<VerticalLayoutGroup>();
        layout.padding=new RectOffset(12,12,12,12);layout.spacing=10;
        layout.childControlWidth=true;layout.childControlHeight=true;
        layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        var fitter=content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=storageRows;
        storagePanel.SetActive(false);
    }
    private static GameObject Panel(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);
        go.GetComponent<Image>().color=color;
        return go;
    }
    private Text TextAt(Transform parent,string content,int size)
    {
        var go=new GameObject("Text",typeof(RectTransform),typeof(Text));
        go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=content;
        t.color=Color.white;t.alignment=TextAnchor.MiddleLeft;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=16;t.resizeTextMaxSize=size;
        t.raycastTarget=false;return t;
    }
    private Button ButtonAt(Transform parent,string label,UnityEngine.Events.UnityAction action)
    {
        var go=Panel(label,parent,new Color(.03f,.34f,.38f,.98f));
        var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
        button.onClick.AddListener(action);
        var text=TextAt(go.transform,label,27);text.alignment=TextAnchor.MiddleCenter;
        Stretch(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,6),new Vector2(-8,-6));
        return button;
    }
    private static void AnchorBottom(RectTransform r,Vector2 pos,Vector2 size)
    {
        r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);
        r.anchoredPosition=pos;r.sizeDelta=size;
    }
    private static void Stretch(RectTransform r,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper)
    {
        r.anchorMin=min;r.anchorMax=max;r.offsetMin=lower;r.offsetMax=upper;
    }
}
