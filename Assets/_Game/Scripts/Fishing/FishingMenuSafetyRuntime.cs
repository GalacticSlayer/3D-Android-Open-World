using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Active fishing is a committed interaction. While a cast, lure retrieve, bite or
/// fight is in progress, gear/travel/boat-changing controls are visibly locked
/// instead of cancelling the fishing state. The Island / Fish Index is the one
/// exception: it opens as a passive overlay over the live game and never calls
/// PrepareForMenu, never pauses FishingSystem, and never resets held REEL input.
/// </summary>
[DefaultExecutionOrder(5000)]
public sealed class FishingMenuSafetyRuntime : MonoBehaviour
{
    private ShopWorldHUD shopHud;
    private FishingSystem fishing;
    private FishingHUD fishingHud;
    private FirstPersonController player;
    private BoatSystem boatSystem;

    private FieldInfo fishingStateField;
    private FieldInfo shopRootField;
    private FieldInfo shopModalField;
    private FieldInfo shopPageField;
    private FieldInfo shopMessageField;
    private FieldInfo shopSessionField;
    private FieldInfo shopBagPickerField;
    private FieldInfo shopHabitatPickerField;
    private FieldInfo menuShortcutField;
    private FieldInfo indexShortcutField;
    private FieldInfo baitShortcutField;
    private FieldInfo nearbyButtonField;
    private MethodInfo refreshMethod;

    private FieldInfo rodSlotImageField;
    private FieldInfo boatSlotImageField;
    private FieldInfo boatInteractField;

    private GameObject shopRoot;
    private GameObject modal;
    private GameObject menuShortcut;
    private GameObject indexShortcut;
    private GameObject baitShortcut;
    private GameObject nearbyButton;

    private Button menuButton;
    private Button indexButton;
    private Button baitButton;
    private Button nearbyUiButton;
    private Button rodSlotButton;
    private Button boatSlotButton;
    private Button boatInteractButton;

    private bool bound;
    private bool safeIndexOpen;
    private bool shopHudDisabledByUs;
    private bool boatSystemDisabledByUs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
        {
            if(hud!=null && hud.GetComponent<FishingMenuSafetyRuntime>()==null)
                hud.gameObject.AddComponent<FishingMenuSafetyRuntime>();
        }
    }

    private void Awake()
    {
        shopHud=GetComponent<ShopWorldHUD>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;

        fishingStateField=typeof(FishingSystem).GetField("state",flags);
        shopRootField=typeof(ShopWorldHUD).GetField("root",flags);
        shopModalField=typeof(ShopWorldHUD).GetField("modal",flags);
        shopPageField=typeof(ShopWorldHUD).GetField("page",flags);
        shopMessageField=typeof(ShopWorldHUD).GetField("message",flags);
        shopSessionField=typeof(ShopWorldHUD).GetField("shopSession",flags);
        shopBagPickerField=typeof(ShopWorldHUD).GetField("bagPicker",flags);
        shopHabitatPickerField=typeof(ShopWorldHUD).GetField("habitatPicker",flags);
        menuShortcutField=typeof(ShopWorldHUD).GetField("menuShortcut",flags);
        indexShortcutField=typeof(ShopWorldHUD).GetField("indexShortcut",flags);
        baitShortcutField=typeof(ShopWorldHUD).GetField("baitShortcut",flags);
        nearbyButtonField=typeof(ShopWorldHUD).GetField("nearbyButton",flags);
        refreshMethod=typeof(ShopWorldHUD).GetMethod("Refresh",flags);

        rodSlotImageField=typeof(FishingHUD).GetField("rodSlotImage",flags);
        boatSlotImageField=typeof(BoatSystem).GetField("slotImage",flags);
        boatInteractField=typeof(BoatSystem).GetField("interact",flags);
    }

    private void Update()
    {
        TryBind();
        if(!bound)return;

        if(safeIndexOpen && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SafeCloseIndex();
            return;
        }

        ApplyInteractionLocks();
    }

    private void LateUpdate()
    {
        if(!bound)return;
        ApplyInteractionLocks();
    }

    private void TryBind()
    {
        if(bound || shopHud==null)return;

        shopRoot=shopRootField?.GetValue(shopHud) as GameObject;
        modal=shopModalField?.GetValue(shopHud) as GameObject;
        menuShortcut=menuShortcutField?.GetValue(shopHud) as GameObject;
        indexShortcut=indexShortcutField?.GetValue(shopHud) as GameObject;
        baitShortcut=baitShortcutField?.GetValue(shopHud) as GameObject;
        nearbyButton=nearbyButtonField?.GetValue(shopHud) as GameObject;
        if(shopRoot==null || modal==null || menuShortcut==null || indexShortcut==null || baitShortcut==null)return;

        fishing=FindFirstObjectByType<FishingSystem>();
        if(fishing==null)return;
        fishingHud=GetComponent<FishingHUD>();
        if(fishingHud==null)fishingHud=FindFirstObjectByType<FishingHUD>();
        player=fishing.GetComponent<FirstPersonController>();
        boatSystem=fishing.GetComponent<BoatSystem>();

        menuButton=menuShortcut.GetComponent<Button>();
        indexButton=indexShortcut.GetComponent<Button>();
        baitButton=baitShortcut.GetComponent<Button>();
        nearbyUiButton=nearbyButton!=null?nearbyButton.GetComponent<Button>():null;
        RefreshDynamicButtons();

        // The stock index button routes through ShopWorldHUD.Open(), which calls
        // FishingSystem.PrepareForMenu() and cancels the cast/fight. Replace only
        // this informational shortcut with the passive live-game version.
        if(indexButton!=null)
        {
            indexButton.onClick.RemoveAllListeners();
            indexButton.onClick.AddListener(()=>SafeOpenIndex("islands"));
        }

        bound=true;
    }

    private void RefreshDynamicButtons()
    {
        if(rodSlotButton==null && fishingHud!=null && rodSlotImageField!=null)
        {
            Image image=rodSlotImageField.GetValue(fishingHud) as Image;
            if(image!=null)rodSlotButton=image.GetComponent<Button>();
        }
        if(boatSystem!=null)
        {
            if(boatSlotButton==null)
            {
                Image image=boatSlotImageField?.GetValue(boatSystem) as Image;
                if(image!=null)boatSlotButton=image.GetComponent<Button>();
            }
            if(boatInteractButton==null)
                boatInteractButton=boatInteractField?.GetValue(boatSystem) as Button;
        }
    }

    private bool FishingBusy
    {
        get
        {
            if(fishing==null || fishingStateField==null)return false;
            object value=fishingStateField.GetValue(fishing);
            return value!=null && !string.Equals(value.ToString(),"Idle",StringComparison.Ordinal);
        }
    }

    private void ApplyInteractionLocks()
    {
        RefreshDynamicButtons();
        bool busy=FishingBusy;

        // If a normal menu is open, it must always keep its own update loop and
        // CLOSE button. This also repairs the case where the persistent CLOSE button
        // had previously been temporarily rewired for the passive fish index.
        if(!safeIndexOpen && ShopWorldHUD.MenuOpen)
        {
            RestoreNormalCloseButton();
            RestoreSystemsIfPossible(true);
            return;
        }

        if(safeIndexOpen)
        {
            DisableShopHudUpdate();
            SetShortcutVisibility(false);
            if(nearbyButton!=null)nearbyButton.SetActive(false);
        }
        else
        {
            SetShortcutVisibility(true);
            SetButton(menuButton,!busy);
            SetButton(indexButton,true);
            SetButton(baitButton,!busy);
            SetButton(nearbyUiButton,!busy);

            if(busy)DisableShopHudUpdate();
            else
            {
                RestoreSystemsIfPossible(true);
                RestoreNormalCloseButton();
            }
        }

        // Do not let a hotbar switch silently stow the rod during a cast/fight.
        SetButton(rodSlotButton,!busy);
        SetButton(boatSlotButton,!busy);

        // Hide the entire shared boat prompt during a fight, including its
        // background and label. BoatSystem resumes its proximity/menu visibility
        // checks when fishing ends, restoring the prompt only where appropriate.
        SetButton(boatInteractButton,!busy && !safeIndexOpen);
        if(boatInteractButton!=null && fishing!=null &&
            string.Equals(fishingStateField?.GetValue(fishing)?.ToString(),"Fighting",StringComparison.Ordinal))
            boatInteractButton.gameObject.SetActive(false);

        // BoatSystem also accepts keyboard shortcuts (2/E). Disable only its UI /
        // interaction driver while fishing so those shortcuts cannot cancel a fight;
        // the actual BoatController physics keeps running independently.
        if(boatSystem!=null)
        {
            if(busy && boatSystem.enabled)
            {
                boatSystem.enabled=false;
                boatSystemDisabledByUs=true;
            }
            else if(!busy && boatSystemDisabledByUs)
            {
                boatSystem.enabled=true;
                boatSystemDisabledByUs=false;
            }
        }
    }

    private void DisableShopHudUpdate()
    {
        if(shopHud!=null && shopHud.enabled)
        {
            shopHud.enabled=false;
            shopHudDisabledByUs=true;
        }
    }

    private void RestoreSystemsIfPossible(bool restoreHud)
    {
        if(restoreHud && shopHud!=null && shopHudDisabledByUs && !safeIndexOpen)
        {
            shopHud.enabled=true;
            shopHudDisabledByUs=false;
        }
        if(!FishingBusy && boatSystem!=null && boatSystemDisabledByUs)
        {
            boatSystem.enabled=true;
            boatSystemDisabledByUs=false;
        }
    }

    // Typed entry point for index views that rebuild after a catch/save event.
    public void OpenIndexPage(string destination){SafeOpenIndex(destination);}

    private void SafeOpenIndex(string destination)
    {
        if(destination!="islands" && destination!="reef-fish")return;
        if(ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.Traveling)return;
        TryBind();
        if(!bound || refreshMethod==null)return;

        safeIndexOpen=true;
        DisableShopHudUpdate();

        shopSessionField?.SetValue(shopHud,null);
        shopBagPickerField?.SetValue(shopHud,null);
        shopHabitatPickerField?.SetValue(shopHud,null);
        shopMessageField?.SetValue(shopHud,string.Empty);
        shopPageField?.SetValue(shopHud,destination);

        modal.SetActive(true);
        shopRoot.transform.SetAsLastSibling();
        if(player!=null)player.SetMenuOpen(true);

        // Deliberately DO NOT call FishingSystem.PrepareForMenu() and DO NOT call
        // FishingHUD.SetMenuCovered(). Casts, bites, fish AI, tension/HP and held
        // REEL input continue in real time behind the informational index.
        refreshMethod.Invoke(shopHud,new object[]{true});
        RewireIndexButtons();
        ApplyInteractionLocks();
    }

    private void SafeCloseIndex()
    {
        if(!safeIndexOpen)return;
        safeIndexOpen=false;
        if(modal!=null)modal.SetActive(false);
        if(player!=null)player.SetMenuOpen(false);

        // Rebuild the persistent CLOSE button's normal listener immediately. The
        // modal object is reused for BAIT/LURES, TRAVEL, equipment, etc.; leaving
        // SafeCloseIndex attached here made CLOSE do nothing on a later normal menu.
        RestoreNormalCloseButton();

        // Do not call FishingHUD.SetMenuCovered(false): that method resets the
        // FishingActionButton and would release an in-progress REEL hold.
        if(!FishingBusy)RestoreSystemsIfPossible(true);
        ApplyInteractionLocks();
    }

    private void RewireIndexButtons()
    {
        if(modal==null)return;
        foreach(Button button in modal.GetComponentsInChildren<Button>(true))
        {
            if(button==null)continue;
            string text=CombinedText(button.gameObject);
            if(string.Equals(text.Trim(),"CLOSE",StringComparison.OrdinalIgnoreCase))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(SafeCloseIndex);
            }
            else if(text.IndexOf("OPEN FISH INDEX",StringComparison.OrdinalIgnoreCase)>=0)
            {
                button.onClick.RemoveAllListeners();
                var link=button.GetComponent<BiomeIndexLink>();
                if(link==null)continue;
                int biome=link.Biome;
                button.onClick.AddListener(()=>
                {
                    if(shopHud.SelectIndexBiome(biome))SafeOpenIndex("reef-fish");
                });
            }
            else if(text.IndexOf("ISLAND INDEX",StringComparison.OrdinalIgnoreCase)>=0)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(()=>SafeOpenIndex("islands"));
            }
        }
    }

    private void RestoreNormalCloseButton()
    {
        if(modal==null || shopHud==null || safeIndexOpen)return;
        foreach(Button button in modal.GetComponentsInChildren<Button>(true))
        {
            if(button==null)continue;
            string text=CombinedText(button.gameObject);
            if(!string.Equals(text.Trim(),"CLOSE",StringComparison.OrdinalIgnoreCase))continue;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(shopHud.CloseMenuPage);
            SetButton(button,true);
            break;
        }
    }

    private static string CombinedText(GameObject root)
    {
        Text[] labels=root.GetComponentsInChildren<Text>(true);
        if(labels==null || labels.Length==0)return string.Empty;
        string value=string.Empty;
        for(int i=0;i<labels.Length;i++)
        {
            if(labels[i]==null || string.IsNullOrEmpty(labels[i].text))continue;
            if(value.Length>0)value+="\n";
            value+=labels[i].text;
        }
        return value;
    }

    private void SetShortcutVisibility(bool visible)
    {
        if(menuShortcut!=null)menuShortcut.SetActive(visible);
        if(indexShortcut!=null)indexShortcut.SetActive(visible);
        if(baitShortcut!=null)baitShortcut.SetActive(visible);
    }

    private static void SetButton(Button button,bool enabled)
    {
        if(button==null)return;

        // Unity's generated buttons previously used nearly identical normal and
        // disabled colors, so interactable=false was functionally correct but did
        // not LOOK disabled. Force an obvious neutral disabled tint and dim the
        // whole button (background + icon + text) while preserving normal colors.
        ColorBlock colors=button.colors;
        colors.disabledColor=new Color(.34f,.34f,.34f,.82f);
        colors.fadeDuration=.025f;
        button.colors=colors;
        button.interactable=enabled;

        CanvasGroup group=button.GetComponent<CanvasGroup>();
        if(group==null)group=button.gameObject.AddComponent<CanvasGroup>();
        group.alpha=enabled?1f:.46f;
        group.interactable=enabled;
        group.blocksRaycasts=enabled;
    }

    private void OnDestroy()
    {
        if(safeIndexOpen && player!=null)player.SetMenuOpen(false);
        safeIndexOpen=false;
        RestoreNormalCloseButton();
        if(shopHud!=null && shopHudDisabledByUs)shopHud.enabled=true;
        if(boatSystem!=null && boatSystemDisabledByUs)boatSystem.enabled=true;
    }
}
