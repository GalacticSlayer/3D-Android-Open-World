using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Final presentation/interaction pass for the Island / Biome Index.
/// This runs after the passive fishing-menu safety layer, because that layer opens
/// the index without setting ShopWorldHUD.MenuOpen. The older travel runtime only
/// watched MenuOpen, so its separate TELEPORT/FISH INDEX buttons never appeared.
/// </summary>
[DefaultExecutionOrder(6500)]
public sealed class IslandBiomeIndexPatchRuntime : MonoBehaviour
{
    private const string MarkerName = "__IslandBiomeIndexPatchRuntime_v1";

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo PageField = typeof(ShopWorldHUD).GetField("page", PrivateInstance);
    private static readonly FieldInfo ListField = typeof(ShopWorldHUD).GetField("list", PrivateInstance);
    private static readonly FieldInfo ModalField = typeof(ShopWorldHUD).GetField("modal", PrivateInstance);
    private static readonly MethodInfo BiomeThumbnailMethod = typeof(ShopWorldHUD).GetMethod("BiomeThumbnail", PrivateInstance);
    private static readonly FieldInfo FishingStateField = typeof(FishingSystem).GetField("state", PrivateInstance);
    private static readonly MethodInfo SafeOpenIndexMethod = typeof(FishingMenuSafetyRuntime).GetMethod("SafeOpenIndex", PrivateInstance);
    private static readonly MethodInfo SafeCloseIndexMethod = typeof(FishingMenuSafetyRuntime).GetMethod("SafeCloseIndex", PrivateInstance);
    private static readonly FieldInfo BoatInteractField = typeof(BoatSystem).GetField("interact", PrivateInstance);

    private ShopWorldHUD hud;
    private ShopDimensionManager travel;
    private FishingSystem fishing;
    private FirstPersonController player;
    private FishingMenuSafetyRuntime menuSafety;
    private BoatSystem boats;
    private Font font;

    private readonly Color teal = new Color(.04f, .36f, .39f, 1f);
    private readonly Color gold = new Color(.89f, .72f, .40f, 1f);
    private readonly Color muted = new Color(.23f, .23f, .23f, 1f);
    private readonly Color mutedText = new Color(.60f, .60f, .60f, 1f);

    private void Awake()
    {
        hud = GetComponent<ShopWorldHUD>();
        fishing = FindFirstObjectByType<FishingSystem>();
        player = fishing != null ? fishing.GetComponent<FirstPersonController>() : FindFirstObjectByType<FirstPersonController>();
        menuSafety = GetComponent<FishingMenuSafetyRuntime>();
        if (menuSafety == null) menuSafety = FindFirstObjectByType<FishingMenuSafetyRuntime>();
        boats = fishing != null ? fishing.GetComponent<BoatSystem>() : FindFirstObjectByType<BoatSystem>();
        travel = ShopDimensionManager.Instance;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (hud == null || PageField == null || ListField == null || ModalField == null)
            enabled = false;
    }

    private void LateUpdate()
    {
        if (!enabled) return;
        if (travel == null) travel = ShopDimensionManager.Instance;
        if (menuSafety == null) menuSafety = GetComponent<FishingMenuSafetyRuntime>();
        if (boats == null && fishing != null) boats = fishing.GetComponent<BoatSystem>();

        string page = PageField.GetValue(hud) as string;
        GameObject modal = ModalField.GetValue(hud) as GameObject;
        RectTransform list = ListField.GetValue(hud) as RectTransform;
        bool modalVisible = modal != null && modal.activeInHierarchy;

        if (!modalVisible || list == null) return;

        bool indexPage = page == "islands" || page == "reef-fish";
        if (indexPage) HideBoatInteraction();

        if (page == "travel")
        {
            // The user does not need an explanatory relocation banner. Just show
            // the actual travel controls.
            Transform hint = list.Find("IslandTravelHint");
            if (hint != null) hint.gameObject.SetActive(false);
            return;
        }

        if (page != "islands") return;
        if (list.Find(MarkerName) != null) return;

        BuildIslandIndex(list);
    }

    private void HideBoatInteraction()
    {
        if (boats == null || BoatInteractField == null) return;
        Button interact = BoatInteractField.GetValue(boats) as Button;
        if (interact != null && interact.gameObject.activeSelf)
            interact.gameObject.SetActive(false);
    }

    private void BuildIslandIndex(RectTransform list)
    {
        ClearList(list);
        int currentBiome = player != null ? IslandExpansionWorld.FishingBiome(player.transform.position) : 0;

        foreach (int biome in ReefCatalog.IslandIndexOrder)
        {
            if (biome == OffshoreOilRigZone.BiomeId && !ReefCatalog.OffshoreOilRigDiscovered) continue;
            CreateIslandCard(list, biome, currentBiome);
        }

        GameObject marker = new GameObject(MarkerName, typeof(RectTransform), typeof(LayoutElement));
        marker.transform.SetParent(list, false);
        LayoutElement markerLayout = marker.GetComponent<LayoutElement>();
        markerLayout.ignoreLayout = true;
        marker.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
    }

    private void CreateIslandCard(RectTransform list, int biome, int currentBiome)
    {
        ReefCatalog.Zone entry = ReefCatalog.Zones[biome];
        bool unlocked = entry.Unlocked;
        bool island = biome == 0 || biome == 1 || biome == 3 || biome == 4 || biome == OffshoreOilRigZone.BiomeId;
        bool here = travel != null && travel.Destination == 0 && currentBiome == biome;
        bool worldReady = biome == OffshoreOilRigZone.BiomeId ? OffshoreOilRigZone.TryGetArrival(out _) :
            biome == 4 ? SnapperIslandRuntime.Ready : biome == 0 || biome == 2 || (IslandExpansionWorld.Active != null && IslandExpansionWorld.Active.Ready);
        bool fishingBusy = IsFishingBusy();

        Color cardColor = biome == 0
            ? new Color(.045f, .24f, .30f, 1f)
            : biome == 1
                ? new Color(.20f, .23f, .25f, 1f)
                : new Color(.025f, .12f, .20f, 1f);
        if (!unlocked) cardColor = muted;

        GameObject card = Panel("BiomeCard_" + biome, list, cardColor);
        card.AddComponent<LayoutElement>().preferredHeight = 250f;

        RawImage picture = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        picture.transform.SetParent(card.transform, false);
        Anchor(picture.rectTransform, 0f, .06f, .25f, .94f, 16, 0, -8, 0);
        picture.raycastTarget = false;
        if (BiomeThumbnailMethod != null)
            picture.texture = BiomeThumbnailMethod.Invoke(hud, new object[] { biome }) as Texture;
        picture.color = unlocked ? Color.white : new Color(.38f, .38f, .38f, 1f);
        IslandIndexArtwork.Round(picture);

        Text title = Label(card.transform,
            entry.name.ToUpperInvariant() + "   •   " + (unlocked ? "UNLOCKED" : "LOCKED"),
            28, unlocked ? gold : mutedText, TextAnchor.UpperLeft);
        Anchor(title.rectTransform, .27f, .68f, .70f, .96f, 10, 0, -10, 0);
        title.horizontalOverflow = HorizontalWrapMode.Wrap;
        title.verticalOverflow = VerticalWrapMode.Truncate;

        Text description = Label(card.transform, entry.description, 22,
            unlocked ? Color.white : mutedText, TextAnchor.UpperLeft);
        // The description owns only the middle column. It cannot flow under the
        // right-side controls, even with long localization/wrapped text.
        Anchor(description.rectTransform, .27f, .08f, .70f, .69f, 10, 0, -12, 0);
        description.horizontalOverflow = HorizontalWrapMode.Wrap;
        description.verticalOverflow = VerticalWrapMode.Truncate;
        description.resizeTextForBestFit = true;
        description.resizeTextMinSize = 16;
        description.resizeTextMaxSize = 22;

        if (island)
        {
            bool allowed = travel == null || travel.TournamentAllowsIsland(biome);
            string teleportLabel = !allowed ? "TOURNAMENT LOCK" : here ? "YOU ARE HERE" : fishingBusy ? "FISHING…" : "TELEPORT";
            Button teleport = ButtonAt(card.transform, teleportLabel, () => TeleportToIsland(biome));
            Anchor(teleport.GetComponent<RectTransform>(), .735f, .55f, .975f, .88f, 0, 0, 0, 0);
            SetAvailable(teleport,
                allowed && unlocked && worldReady && !here && !fishingBusy && travel != null && !travel.Traveling);
        }

        Button fishIndex = ButtonAt(card.transform, "FISH INDEX", () => OpenFishIndex(biome));
        if (island)
            Anchor(fishIndex.GetComponent<RectTransform>(), .735f, .13f, .975f, .46f, 0, 0, 0, 0);
        else
            Anchor(fishIndex.GetComponent<RectTransform>(), .735f, .31f, .975f, .69f, 0, 0, 0, 0);
        SetAvailable(fishIndex, unlocked);
    }

    private void OpenFishIndex(int biome)
    {
        if (biome < 0 || biome >= ReefCatalog.Zones.Length || !ReefCatalog.Zones[biome].Unlocked) return;
        if (!hud.SelectIndexBiome(biome)) return;

        if (menuSafety != null && SafeOpenIndexMethod != null)
        {
            SafeOpenIndexMethod.Invoke(menuSafety, new object[] { "reef-fish" });
            return;
        }

        hud.Open("reef-fish");
    }

    private void TeleportToIsland(int biome)
    {
        if (IsFishingBusy() || travel == null || travel.Traveling || !travel.TournamentAllowsIsland(biome)) return;
        if (biome < 0 || biome >= ReefCatalog.Zones.Length || !ReefCatalog.Zones[biome].Unlocked) return;
        if (biome != 0 && biome != 1 && biome != 3 && biome != 4 && biome != OffshoreOilRigZone.BiomeId) return; // Deep Ocean has no teleport.
        if ((biome == 1 || biome == 3) && (IslandExpansionWorld.Active == null || !IslandExpansionWorld.Active.Ready)) return;

        if (menuSafety != null && SafeCloseIndexMethod != null)
            SafeCloseIndexMethod.Invoke(menuSafety, null);
        else
            hud.Close();

        travel.TravelIsland(biome);
    }

    private bool IsFishingBusy()
    {
        if (fishing == null || FishingStateField == null) return false;
        object value = FishingStateField.GetValue(fishing);
        return value != null && !string.Equals(value.ToString(), "Idle", StringComparison.Ordinal);
    }

    private static void ClearList(RectTransform list)
    {
        for (int i = list.childCount - 1; i >= 0; i--)
        {
            GameObject child = list.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private Button ButtonAt(Transform parent, string value, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = teal;
        Button button = go.GetComponent<Button>();
        if (action != null) button.onClick.AddListener(action);

        Text text = Label(go.transform, value, 25, Color.white, TextAnchor.MiddleCenter);
        Full(text.rectTransform, 10, 7, -10, -7);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 17;
        text.resizeTextMaxSize = 25;
        return button;
    }

    private void SetAvailable(Button button, bool available)
    {
        if (button == null) return;
        button.interactable = available;
        if (available) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = muted;
        foreach (Text text in button.GetComponentsInChildren<Text>(true)) text.color = mutedText;
    }

    private static GameObject Panel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private Text Label(Transform parent, string value, int size, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Full(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY,
        float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
}


