using UnityEngine;
using UnityEngine.UI;

/// <summary>Uses the supplied artwork unchanged, excluding its transparent margins via UVs.</summary>
public static class HardenedScalesArtwork
{
    private static Texture2D circle, bar, shield;

    private static Rect Pixels(float x, float y, float width, float height, float textureWidth, float textureHeight)
    {
        // Source pixel bounds use a top-left origin; Unity UVs start at bottom-left.
        return new Rect(x / textureWidth, 1f - (y + height) / textureHeight,
            width / textureWidth, height / textureHeight);
    }

    private static RawImage Create(Transform parent, string name, Texture2D texture, Rect uv)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.uvRect = uv;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.SetActive(false);
        return image;
    }

    public static RawImage Circle(RawImage fishIcon)
    {
        if (circle == null) circle = Resources.Load<Texture2D>("Fishing/HardenedScales/Overlay");
        var image = Create(fishIcon.transform, "Hardened Scales", circle,
            Pixels(303, 62, 925, 919, 1536, 1024));
        // Child anchors track the icon's centre and size, including canvas scaling.
        image.rectTransform.anchorMin = new Vector2(-.1f, -.1f);
        image.rectTransform.anchorMax = new Vector2(1.1f, 1.1f);
        return image;
    }

    public static RawImage HealthBar(Image healthFill)
    {
        if (bar == null) bar = Resources.Load<Texture2D>("Fishing/HardenedScales/HealthBar");
        // Match the entire HP track, not the shrinking fill. A rounded stencil
        // clips the artwork precisely to the existing meter silhouette.
        var go = new GameObject("Hardened Scales Health Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
        go.transform.SetParent(healthFill.transform.parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var mask = go.GetComponent<Image>();
        mask.sprite = healthFill.sprite;
        mask.type = Image.Type.Sliced;
        mask.raycastTarget = false;
        go.GetComponent<Mask>().showMaskGraphic = false;
        return Create(go.transform, "Hardened Scales Health", bar,
            Pixels(44, 256, 1960, 171, 2048, 682));
    }

    public static RawImage Shield(Image healthFill)
    {
        if (shield == null) shield = Resources.Load<Texture2D>("Fishing/HardenedScales/Icon");
        var image = Create(healthFill.transform.parent, "Hardened Scales Icon", shield,
            Pixels(273, 201, 708, 820, 1254, 1254));
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
        rect.pivot = new Vector2(1f, .5f);
        rect.sizeDelta = new Vector2(26f, 30f);
        rect.anchoredPosition = new Vector2(-5f, 0f);
        return image;
    }

    public static void Show(RawImage image, bool visible, float opacity)
    {
        if (image == null) return;
        image.gameObject.SetActive(visible && image.texture != null);
        image.color = new Color(1f, 1f, 1f, opacity);
    }
}
