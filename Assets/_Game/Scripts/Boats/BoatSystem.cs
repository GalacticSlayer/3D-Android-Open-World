using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class BoatSystem : MonoBehaviour
{
    public Transform MarinaCounter;
    public Transform IslandDock;
    public OceanWater Water;
    public BoatData[] Catalog;
    public BoatController ActiveBoat {get;private set;}
    public bool PlacementMode {get;private set;}

    private const float PlacementDistance=18f;
    private const float DefaultNoticeSeconds=4f;

    private ShopProgress progress;
    private FirstPersonController controls;
    private BoatPassenger passenger;
    private Camera view;
    private GameObject ghost,shop;
    private Material ghostMaterial;
    private Button place,interact;
    private Text status,slotLabel;
    private Image slotImage;
    private bool valid;
    private Vector3 target;
    private Quaternion heading;
    private float statusUntil;
    private bool placementStatus;

    private BoatData Selected => Catalog!=null?Array.Find(Catalog,b=>b!=null && b.ID==progress.Data.boatEquipped):null;
    private bool OnIsland => ShopDimensionManager.Instance==null || (!ShopDimensionManager.Instance.InDimension && !ShopDimensionManager.Instance.Traveling);
    private bool NearMarina => OnIsland && MarinaCounter!=null && Vector3.Distance(transform.position,MarinaCounter.position)<6;
    // ShopProgress can recognize any future purchase made through this UI,
    // without adding an audio callback to each catalog entry.
    public bool MarinaShopOpen => shop!=null && shop.activeInHierarchy && NearMarina;

    private void Awake()
    {
        progress=GetComponent<ShopProgress>();controls=GetComponent<FirstPersonController>();
        passenger=GetComponent<BoatPassenger>();view=GetComponentInChildren<Camera>();
    }

    private void Start(){MergeBoatCatalog();BuildUI();}

    private void MergeBoatCatalog()
    {
        var boats=new System.Collections.Generic.List<BoatData>();
        foreach(string id in new[]{"raft","rowboat","sailboat","yacht"})
        {
            var data=Catalog!=null?Array.Find(Catalog,b=>b!=null && b.ID==id):null;
            if(data==null)data=Resources.Load<BoatData>("Boats/"+(id=="rowboat"?"Rowboat":id=="raft"?"Raft":id=="sailboat"?"Sailboat":"Yacht"));
            if(data!=null && data.Prefab!=null)boats.Add(data);
        }
        if(Catalog!=null)foreach(var data in Catalog)
            if(data!=null && data.Prefab!=null && !boats.Exists(b=>b.ID==data.ID))boats.Add(data);
        Catalog=boats.ToArray();
    }

    public void BindSlot(GameObject slot)
    {
        slotImage=slot.GetComponent<Image>();
        var button=slot.GetComponent<Button>()??slot.AddComponent<Button>();button.onClick.AddListener(TogglePlacement);
        slotLabel=Label(slot.transform,"BOAT",20,Vector2.zero,Vector2.zero);
        var rect=slotLabel.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(5,4);rect.offsetMax=new Vector2(-5,-4);
        slotLabel.resizeTextForBestFit=true;slotLabel.resizeTextMinSize=9;slotLabel.resizeTextMaxSize=20;
        slotLabel.horizontalOverflow=HorizontalWrapMode.Wrap;
    }

    public void TogglePlacement()
    {
        if(PlacementMode){CancelPlacement();return;}
        if(!OnIsland || controls.BoatUIBlocked || passenger.Seated)return;
        if(Selected==null){Notice("Buy a permanent boat at Suncrest Marina.");return;}

        GetComponent<FishingSystem>()?.UnequipHands();
        PlacementMode=true;
        ghost=Instantiate(Selected.Prefab);ghost.name="Boat Placement Preview";
        foreach(var b in ghost.GetComponentsInChildren<BoatController>())b.enabled=false;
        foreach(var c in ghost.GetComponentsInChildren<Collider>())c.enabled=false;
        foreach(var rb in ghost.GetComponentsInChildren<Rigidbody>()){rb.isKinematic=true;rb.detectCollisions=false;}
        ghostMaterial=new Material(Selected.Prefab.GetComponentInChildren<Renderer>().sharedMaterial);
        ghostMaterial.SetFloat("_Surface",1);ghostMaterial.SetFloat("_ZWrite",0);
        ghostMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ghostMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");ghostMaterial.renderQueue=3000;
        foreach(var r in ghost.GetComponentsInChildren<Renderer>())
        {
            // A placement preview must neither punch a water hole nor display its
            // invisible interior volume as a solid green/red block.
            if(r.sharedMaterial!=null && r.sharedMaterial.shader!=null &&
               r.sharedMaterial.shader.name=="OpenWorld/BoatWaterMask")
            {
                r.enabled=false;
                continue;
            }
            r.sharedMaterial=ghostMaterial;
        }
        ghost.SetActive(false);
        Notice("Aim at open water. Green: place. Red: shore or obstruction. Slot 2 cancels.",0f,true);
    }

    public void CancelPlacement()
    {
        PlacementMode=false;valid=false;
        if(ghost!=null)Destroy(ghost);if(ghostMaterial!=null)Destroy(ghostMaterial);
        ghost=null;ghostMaterial=null;
        if(place!=null)place.gameObject.SetActive(false);
        if(placementStatus)ClearNotice();
    }

    private void Update()
    {
        if(place==null)return;

        if(!placementStatus && status!=null && statusUntil>0f && Time.unscaledTime>=statusUntil)
            ClearNotice();

        bool tournamentNearby=GetComponent<FishingTournament>()?.NearStand??false;
        bool otherMenu=ShopWorldHUD.MenuOpen || FishingTournament.ModalOpen || (controls.BoatUIBlocked && !shop.activeSelf);
        if(!OnIsland || otherMenu){CancelPlacement();if(shop.activeSelf)CloseShop();}
        if(shop.activeSelf && !NearMarina)CloseShop();
        if(slotLabel!=null){slotLabel.text=Selected!=null?Selected.name.ToUpperInvariant():"BOAT";slotImage.color=PlacementMode?new Color(.05f,.45f,.35f,.95f):new Color(.05f,.07f,.09f,.72f);}

        bool board=passenger.Boat==null && NearestBoat()!=null;
        bool helm=passenger.Boat!=null && passenger.Boat.CanUseSeat(passenger);
        interact.gameObject.SetActive(!tournamentNearby && !otherMenu && !shop.activeSelf && !PlacementMode && (NearMarina || helm || board));
        interact.GetComponentInChildren<Text>().text=passenger.Sitting?"STAND UP":
            passenger.Boat!=null && passenger.Boat.CanSit(passenger)?"SIT":helm?(passenger.Seated?(passenger.Boat.IsRowboat?"STOP ROWING":"LEAVE HELM"):passenger.Boat.IsRowboat?"ROW BOAT":"DRIVE BOAT"):board?"BOARD BOAT":"MARINA SHOP";

        if(Keyboard.current!=null && Keyboard.current.digit2Key.wasPressedThisFrame && !shop.activeSelf && !otherMenu)TogglePlacement();
        if(Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame && interact.gameObject.activeSelf)Interact();
        if(!PlacementMode)return;
        if(controls.BoatUIBlocked || passenger.Seated){CancelPlacement();return;}

        // Placement intentionally stays close to the player. This keeps the ghost
        // easy to judge and prevents dropping a boat a long distance offshore.
        Ray ray=view.ViewportPointToRay(new Vector3(.5f,.5f));
        // The old BoatPlacementWater collider ends at the original terrain edge.
        // Use the ocean datum for aiming; BoatClearance still validates the entire
        // hull against real terrain and props before allowing a spawn.
        bool hitWater = BoatClearance.TryGetWaterTarget(ray, Water, PlacementDistance, out float waterDistance);
        valid=false;
        if(ghost!=null)ghost.SetActive(hitWater);
        if(hitWater && ghost!=null)
        {
            target=ray.GetPoint(waterDistance);target.y=Water.GetSurfaceHeight(target);
            heading=Quaternion.Euler(0,view.transform.eulerAngles.y,0);
            target.y=BoatController.FloatingHeight(Selected,Water,target,heading);
            ghost.transform.SetPositionAndRotation(target,heading);
            valid=BoatClearance.Valid(Selected,target,heading,Water,ActiveBoat!=null?ActiveBoat.transform:null,transform);

            // Do not place through a rock/wall before the water hit.
            foreach(var obstruction in Physics.RaycastAll(ray,waterDistance,~(1<<LayerMask.NameToLayer("Water")),QueryTriggerInteraction.Ignore))
                if(!obstruction.transform.IsChildOf(transform) && !(ActiveBoat!=null && obstruction.transform.IsChildOf(ActiveBoat.transform)))valid=false;

            if(ghostMaterial!=null)ghostMaterial.SetColor("_BaseColor",valid?new Color(.1f,.9f,.35f,.6f):new Color(1,.12f,.12f,.6f));
        }
        place.gameObject.SetActive(true);place.interactable=valid;
        if(Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame && (EventSystem.current==null || !EventSystem.current.IsPointerOverGameObject()))Place();
    }

    private void Place()
    {
        if(!PlacementMode || !valid || Selected==null || !OnIsland)return;
        if(ActiveBoat!=null && ActiveBoat.PassengerCount>0){Notice("Everyone must step off before moving the boat.");return;}
        if(!BoatClearance.Valid(Selected,target,heading,Water,ActiveBoat!=null?ActiveBoat.transform:null,transform))return;
        if(ActiveBoat!=null && ActiveBoat.Data!=Selected){ActiveBoat.gameObject.SetActive(false);Destroy(ActiveBoat.gameObject);ActiveBoat=null;}
        if(ActiveBoat==null)ActiveBoat=Instantiate(Selected.Prefab,target,heading).GetComponent<BoatController>();
        else
        {
            var body=ActiveBoat.GetComponent<Rigidbody>();
            body.position=target;body.rotation=heading;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        }
        ActiveBoat.Initialize(Selected,Water);
        CancelPlacement();
        Notice("Boat ready. Step aboard; use DRIVE BOAT at the seat. Leave helm to walk and fish.",4.5f);
    }

    public void BeforeTeleport()
    {
        CancelPlacement();CloseShop();passenger.Detach();ClearNotice();
        if(ActiveBoat!=null){ActiveBoat.gameObject.SetActive(false);Destroy(ActiveBoat.gameObject);ActiveBoat=null;}
    }

    private BoatController NearestBoat()
    {
        BoatController own=ActiveBoat!=null && Vector3.Distance(transform.position,ActiveBoat.DeckExit.position)<4?ActiveBoat:null;
        var multiplayer=GetComponent<FishingBoatMultiplayer>();
        var other=multiplayer!=null?multiplayer.NearestBoardable():null;
        if(own==null)return other;
        return other!=null && Vector3.Distance(transform.position,other.DeckExit.position)<Vector3.Distance(transform.position,own.DeckExit.position)?other:own;
    }

    private void Interact()
    {
        if(passenger.Boat!=null){passenger.ToggleHelm();return;}
        BoatController nearby=NearestBoat();
        if(nearby!=null)
        {
            if(!passenger.BoardFromWater(nearby))Notice("Boat is full.");
            return;
        }
        if(!NearMarina)return;
        CancelPlacement();GetComponent<FishingSystem>()?.PrepareForMenu();shop.SetActive(true);controls.SetMenuOpen(true);RefreshShop();
    }

    private void CloseShop(){if(shop!=null && shop.activeSelf){shop.SetActive(false);controls.SetMenuOpen(ShopWorldHUD.MenuOpen);}}

    private void RefreshShop()
    {
        foreach(Transform child in shop.transform)Destroy(child.gameObject);
        Label(shop.transform,"SUNCREST MARINA   ·   "+progress.Data.coins+" coins",27,new Vector2(0,280),new Vector2(650,55));
        for(int i=0;i<Catalog.Length;i++)
        {
            var data=Catalog[i];bool owned=progress.Data.OwnsBoat(data.ID);
            string detail=data.name+"  ·  "+(data.ID=="rowboat"?data.Speed+" / "+(data.Speed*2)+" m/s":data.Speed+" m/s")+"  ·  "+data.MaxCapacity+" aboard\n"+(owned?(Selected==data?"EQUIPPED":"OWNED — EQUIP"):data.Cost+" coins — PERMANENT UNLOCK");
            var button=MakeButton(shop.transform,detail,new Vector2(0,175-i*110),new Vector2(620,96),()=>
            {
                if(!NearMarina || progress.ReadOnly)return;
                bool changed=owned?progress.Data.EquipBoat(data.ID):progress.Data.BuyBoat(data.ID,data.Cost);
                if(changed)progress.Save();else Notice("Not enough coins.");RefreshShop();
            });
            button.interactable=!progress.ReadOnly && (owned || progress.Data.coins>=data.Cost);
        }
        MakeButton(shop.transform,"CLOSE",new Vector2(0,-265),new Vector2(220,55),CloseShop);
    }

    private void Notice(string text,float seconds=DefaultNoticeSeconds,bool placementOnly=false)
    {
        if(status==null)return;
        status.text=text;
        placementStatus=placementOnly;
        statusUntil=placementOnly?0f:Time.unscaledTime+Mathf.Max(.25f,seconds);
    }

    private void ClearNotice()
    {
        if(status!=null)status.text=string.Empty;
        placementStatus=false;
        statusUntil=0f;
    }

    private void BuildUI()
    {
        var root=new GameObject("BoatHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        status=Label(root.transform,"",22,new Vector2(0,320),new Vector2(900,90));
        place=MakeButton(root.transform,"PLACE BOAT",new Vector2(650,-180),new Vector2(245,90),Place);place.gameObject.SetActive(false);
        interact=MakeButton(root.transform,"MARINA SHOP",new Vector2(0,-300),new Vector2(280,75),Interact);
        shop=new GameObject("Marina Inventory",typeof(RectTransform),typeof(Image));shop.transform.SetParent(root.transform,false);
        shop.GetComponent<RectTransform>().sizeDelta=new Vector2(700,660);shop.GetComponent<Image>().color=new Color(.035f,.1f,.13f,.98f);shop.SetActive(false);
    }

    private static Text Label(Transform parent,string text,int size,Vector2 position,Vector2 dimensions)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.text=text;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.95f,.95f,.86f);t.raycastTarget=false;
        t.rectTransform.sizeDelta=dimensions;t.rectTransform.anchoredPosition=position;return t;
    }

    private static Button MakeButton(Transform parent,string text,Vector2 position,Vector2 dimensions,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=dimensions;rect.anchoredPosition=position;
        go.GetComponent<Image>().color=new Color(.07f,.3f,.34f,.97f);
        Label(go.transform,text,24,Vector2.zero,dimensions-new Vector2(16,8));var button=go.GetComponent<Button>();button.onClick.AddListener(action);return button;
    }

    private void OnDestroy(){CancelPlacement();if(ActiveBoat!=null)Destroy(ActiveBoat.gameObject);}
}




