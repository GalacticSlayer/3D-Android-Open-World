using UnityEngine;
using UnityEngine.UI;

/// <summary>Persists Oceanix discovery on approach, including swimming and boats.</summary>
[DisallowMultipleComponent]
public sealed class OceanixDatacenterDiscovery : MonoBehaviour
{
    private ShopProgress progress;
    private float nextCheck;

    private void Awake() { progress = GetComponent<ShopProgress>(); }

    private void Update()
    {
        if (ReefCatalog.OceanixDiscovered || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.35f;
        if (progress == null || progress.ReadOnly || progress.Data == null) return;
        ShopDimensionManager travel = ShopDimensionManager.Instance;
        if (travel != null && (travel.InDimension || travel.Traveling)) return;
        if (!OceanixDatacenterZone.Contains(transform.position)) return;

        progress.Data.oceanixDiscovered = true;
        ReefCatalog.OceanixDiscovered = true;
        progress.Save();
        ShowDiscovery();
    }

    private static void ShowDiscovery()
    {
        var go = new GameObject("Oceanix Datacenter Discovery",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = new GameObject("Discovery Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(go.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .8f);
        rect.sizeDelta = new Vector2(850, 135);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(.035f, .14f, .2f, .95f);
        panelImage.raycastTarget = false;

        var label = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(panel.transform, false);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 30;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, .89f, .59f);
        label.raycastTarget = false;
        label.text = "OCEANIX DATACENTER DISCOVERED\nTeleport and fish index unlocked";
        label.rectTransform.sizeDelta = rect.sizeDelta;
        UnityEngine.Object.Destroy(go, 6f);
    }
}
