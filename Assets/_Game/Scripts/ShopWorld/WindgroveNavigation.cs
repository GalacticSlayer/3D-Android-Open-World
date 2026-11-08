using UnityEngine;
using UnityEngine.UI;

/// <summary>Local guidance only; discovery and teleport eligibility retain their existing rules.</summary>
[DefaultExecutionOrder(8000)]
public sealed class WindgroveNavigation : MonoBehaviour
{
    private ShopProgress progress;
    private FirstPersonController player;
    private FishingSystem fishing;
    private GameObject badge, surface, announcement, menu;
    private RectTransform strip, marker;
    private Text distance, destinationTitle;
    private GameObject shortcutBorder;
    private AbyssalLeviathanEncounter boss;
    private FishingHUD fishingHud;
    public static bool CompassVisible => active != null && active.surface != null && active.surface.activeInHierarchy;
    private readonly RectTransform[] ticks = new RectTransform[32];
    private readonly Text[] headings = new Text[8];
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    private bool navigating;
    private float announcementUntil;
    private static WindgroveNavigation active;

    public static bool Pending
    {
        get
        {
            var data = active != null && active.progress != null && !active.progress.ReadOnly ? active.progress.Data : null;
            return data != null && !data.windgroveDiscovered && !ReefCatalog.WindgroveDiscovered &&
                data.totalCaught != null && data.totalCaught.Length > FishCatalog.AbyssalLeviathanId &&
                data.totalCaught[FishCatalog.AbyssalLeviathanId] > 0;
        }
    }

    public void Initialize(ShopProgress owner, FirstPersonController controller, FishingSystem system,
        GameObject shortcut, Transform hudRoot, GameObject modal)
    {
        menu = modal;
        progress = owner;
        player = controller;
        fishing = system;
        active = this;
        boss = owner.GetComponent<AbyssalLeviathanEncounter>();
        fishingHud = GetComponent<FishingHUD>();
        shortcutBorder = AddFlashingBorder(shortcut.transform);
        shortcutBorder.SetActive(false);
        var icon = new GameObject("Windgrove Alert", typeof(RectTransform), typeof(RawImage));
        icon.transform.SetParent(shortcut.transform, false);
        var raw = icon.GetComponent<RawImage>();
        raw.texture = Resources.Load<Texture2D>("WindgroveNotification");
        // Use the supplied transparent image as-is; its artwork occupies the central square.
        raw.uvRect = new Rect(.25f, .25f, .5f, .55f);
        raw.raycastTarget = false;
        Place(raw.rectTransform, new Vector2(1, 1), new Vector2(-5, -4), new Vector2(64, 64));
        badge = icon;
        badge.SetActive(false);
        BuildCompass(hudRoot);
    }

    public void BeginNavigation()
    {
        if (!Pending) return;
        if (!navigating) announcementUntil = Time.unscaledTime + 10f;
        navigating = true;
    }

    private void LateUpdate()
    {
        if (badge == null || surface == null) return;
        bool pending = Pending;
        badge.SetActive(pending);
        shortcutBorder.SetActive(pending);
        if (!pending) navigating = false;
        var travel = ShopDimensionManager.Instance;
        if (boss == null && progress != null) boss = progress.GetComponent<AbyssalLeviathanEncounter>();
        bool bossActive = boss != null && boss.Active;
        bool guideBoss = bossActive && !boss.NavigationArrived;
        bool show = (bossActive ? guideBoss : pending && navigating) && player != null && player.ViewCamera != null &&
            (travel == null || (!travel.InDimension && !travel.Traveling)) &&
            !ShopWorldHUD.MenuOpen && (menu == null || !menu.activeInHierarchy) && (fishing == null || !fishing.IsFishPosing);
        Vector3 targetPosition = Vector3.zero;
        if (show)
        {
            if (guideBoss) targetPosition = boss.RegionCenter;
            else if (WindgroveShoalZone.TryGetNorthmost(out Transform target)) targetPosition = target.position;
            else show = false;
        }
        surface.SetActive(show);
        if (fishingHud != null) fishingHud.SetNavigationCovered(show);
        if (!show) return;
        destinationTitle.text = guideBoss ? "ABYSSAL LEVIATHAN" : "WINDGROVE SHOAL";
        destinationTitle.color = guideBoss ? new Color(1f,.16f,.20f) : FishingHudTheme.Cyan;
        announcement.SetActive(!guideBoss && Time.unscaledTime < announcementUntil);
        Vector3 forward = FishingThirdPersonView.ViewingRotation(player.ViewCamera) * Vector3.forward;
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float width = strip.rect.width;
        // A 180 degree window, wrapping at north, with a fixed center heading indicator.
        for (int i = 0; i < ticks.Length; i++)
        {
            float delta = Mathf.DeltaAngle(yaw, i * 11.25f);
            ticks[i].gameObject.SetActive(Mathf.Abs(delta) < 89f);
            ticks[i].anchoredPosition = new Vector2(delta / 180f * width, -24f);
        }
        for (int i = 0; i < headings.Length; i++)
        {
            float delta = Mathf.DeltaAngle(yaw, i * 45f);
            headings[i].gameObject.SetActive(Mathf.Abs(delta) < 83f);
            headings[i].rectTransform.anchoredPosition = new Vector2(delta / 180f * width, 8f);
        }
        Vector3 offset = targetPosition - player.transform.position;
        offset.y = 0;
        float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        float relative = Mathf.DeltaAngle(yaw, bearing);
        marker.anchoredPosition = new Vector2(Mathf.Clamp(relative, -78f, 78f) / 180f * width, 0);
        string arrow = relative < -78f ? "◀ " : relative > 78f ? "▶ " : "";
        float meters = guideBoss ? Mathf.Max(0, offset.magnitude - AbyssalLeviathanEncounter.CatchRegionRadius) : offset.magnitude;
        distance.text = arrow + Mathf.RoundToInt(meters) + "m";
    }

    private void BuildCompass(Transform parent)
    {
        surface = new GameObject("Windgrove Navigation", typeof(RectTransform));
        surface.transform.SetParent(parent, false);
        var layout = surface.GetComponent<RectTransform>();
        layout.anchorMin = new Vector2(.27f, 1f);
        layout.anchorMax = new Vector2(.73f, 1f);
        layout.pivot = new Vector2(.5f, 1f);
        layout.offsetMin = new Vector2(0, -275);
        layout.offsetMax = new Vector2(0, -26);
        var title = Panel(surface.transform, "Destination", new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(320, 56));
        destinationTitle = Label(title, "WINDGROVE SHOAL", 25, FishingHudTheme.Cyan, Vector2.zero, new Vector2(310, 50));
        Line(surface.transform, new Vector2(.15f, 1), new Vector2(0, -28), new Vector2(120, 2), FishingHudTheme.Cyan);
        Line(surface.transform, new Vector2(.85f, 1), new Vector2(0, -28), new Vector2(120, 2), FishingHudTheme.Cyan);
        strip = Panel(surface.transform, "Compass", new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(0, 74));
        strip.anchorMin = new Vector2(0, 1); strip.anchorMax = Vector2.one;
        strip.sizeDelta = new Vector2(0, 74);
        for (int i = 0; i < ticks.Length; i++)
            ticks[i] = Line(strip, new Vector2(.5f, .5f), Vector2.zero, new Vector2(2, i % 4 == 0 ? 15 : 10), new Color(.7f,.88f,.95f));
        for (int i = 0; i < headings.Length; i++)
            headings[i] = Label(strip, Directions[i], i % 2 == 0 ? 29 : 22, Color.white, Vector2.zero, new Vector2(60, 38));
        marker = Panel(strip, "Destination Bearing", new Vector2(.5f,.5f), Vector2.zero, new Vector2(48, 74));
        marker.GetComponent<Image>().color = new Color(0,.95f,1,.48f);
        // Keep the highlight beneath the compass text.
        marker.SetAsFirstSibling();
        Label(marker, "◇", 43, FishingHudTheme.Cyan, new Vector2(0,-50), new Vector2(58,50));
        var box = Panel(marker, "Distance", new Vector2(.5f,.5f), new Vector2(0,-88), new Vector2(130,40));
        distance = Label(box, "", 26, Color.white, Vector2.zero, new Vector2(128,38));
        Line(strip, new Vector2(.5f,.5f), new Vector2(0,-23), new Vector2(3,20), Color.white);
        Label(strip, "▼", 17, Color.white, new Vector2(0,42), new Vector2(22,22));
        announcement = Label(surface.transform, "New area detected to the South.", 24, Color.white,
            Vector2.zero, new Vector2(600,40)).gameObject;
        var text = announcement.GetComponent<Text>(); text.fontStyle = FontStyle.Italic;
        Place(text.rectTransform, new Vector2(.5f,1), new Vector2(0,-235), new Vector2(600,40));
        surface.SetActive(false);
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        FishingHudTheme.Panel(go);
        go.GetComponent<Image>().raycastTarget = false;
        var rect = go.GetComponent<RectTransform>(); Place(rect, anchor, position, size); return rect;
    }
    private static RectTransform Line(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        Place(image.rectTransform, anchor, position, size); return image.rectTransform;
    }
    private static Text Label(Transform parent, string value, int size, Color color, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = size; text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter; text.color = color; text.raycastTarget = false;
        Place(text.rectTransform, new Vector2(.5f,.5f), position, dimensions); return text;
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    public static GameObject AddFlashingBorder(Transform parent)
    {
        var go = new GameObject("White Discovery Border", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        FishingHudTheme.RoundedWhiteBorder(go.GetComponent<Image>());
        go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        go.AddComponent<WindgroveFlashingBorder>();
        return go;
    }
    private void OnDisable()
    {
        if (surface != null) surface.SetActive(false);
        if (fishingHud != null) fishingHud.SetNavigationCovered(false);
    }
    private void OnDestroy() { if (active == this) active = null; }
}
