using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The rig is a remote ocean fishing zone, so discovery happens on entry by
/// boat/swimming as well as by foot. Saves the one-time unlock in ShopLedger.
/// Doesn't create a new world, change the terrain or touch multiplayer state.
/// </summary>
[DisallowMultipleComponent]
public sealed class OffshoreOilRigDiscovery : MonoBehaviour
{
    private ShopProgress progress;
    private float nextCheck;

    private void Awake() { progress = GetComponent<ShopProgress>(); }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck || ReefCatalog.OffshoreOilRigDiscovered) return;
        nextCheck = Time.unscaledTime + 0.4f;
        if (progress == null || progress.ReadOnly || progress.Data == null) return;
        ShopDimensionManager travel = ShopDimensionManager.Instance;
        if (travel != null && (travel.InDimension || travel.Traveling)) return;
        if (!OffshoreOilRigZone.Contains(transform.position)) return;
        progress.Data.offshoreOilRigDiscovered = true;
        ReefCatalog.OffshoreOilRigDiscovered = true;
        progress.Save();
        ShowDiscovery();
    }

    private static void ShowDiscovery()
    {
        var go = new GameObject("The Windgrove Shoal Discovered",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var panel = new GameObject("Discovery Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(go.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .8f);
        rect.sizeDelta = new Vector2(850, 135);
        var image = panel.GetComponent<Image>();
        image.color = new Color(.035f, .14f, .2f, .95f);
        image.raycastTarget = false;
        var label = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(panel.transform, false);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 30;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, .89f, .59f);
        label.raycastTarget = false;
        label.text = "THE WINDGROVE SHOAL DISCOVERED\nNew fishing area unlocked in ISLAND / FISH INDEX";
        label.rectTransform.sizeDelta = rect.sizeDelta;
        Destroy(go, 6f);
    }
}
