using UnityEngine;
using UnityEngine.UI;

// One clear exit from a read-only, offline Home visit. This is hidden by menus
// (including the passive fish index) and when the player returns to their Home.
public sealed class HomeDirectoryVisitorUI : MonoBehaviour
{
    private GameObject root;
    private Button returnButton;
    private HomeDirectoryClient directory;
    private ShopDimensionManager travel;
    private FishingMenuSafetyRuntime menuSafety;
    private void Start()
    {
        directory=HomeDirectoryClient.Instance;
        travel=GetComponent<ShopDimensionManager>();
        var go=new GameObject("Return From Visiting Home",typeof(RectTransform),
            typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root=go;
        var canvas=go.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=175;
        var scaler=go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);
        scaler.matchWidthOrHeight=.5f;
        var button=new GameObject("Return Home",typeof(RectTransform),
            typeof(Image),typeof(Button));
        button.transform.SetParent(go.transform,false);
        var rect=button.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);
        rect.pivot=new Vector2(.5f,0);
        rect.anchoredPosition=new Vector2(0,190);
        rect.sizeDelta=new Vector2(410,70);
        FishingHudTheme.Panel(button,1);
        returnButton=button.GetComponent<Button>();
        returnButton.targetGraphic=button.GetComponent<Image>();
        returnButton.onClick.AddListener(()=>
        {
            if(directory!=null)directory.ReturnToMyHome();
        });
        var label=new GameObject("Label",typeof(RectTransform),typeof(Text));
        label.transform.SetParent(button.transform,false);
        var text=label.GetComponent<Text>();
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text="RETURN HOME";text.fontSize=29;
        text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;
        text.raycastTarget=false;
        var t=label.GetComponent<RectTransform>();
        t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;
        root.SetActive(false);
    }
    private void Update()
    {
        if(root==null)return;
        if(menuSafety==null)menuSafety=FindFirstObjectByType<FishingMenuSafetyRuntime>();
        bool show=directory!=null && directory.IsVisiting && travel!=null &&
            travel.InHome && !travel.Traveling &&
            !ShopWorldHUD.MenuOpen &&
            (menuSafety==null || !menuSafety.IsIndexOpen);
        if(root.activeSelf!=show)root.SetActive(show);
    }
    private void OnDestroy(){if(root!=null)Destroy(root);}
}
