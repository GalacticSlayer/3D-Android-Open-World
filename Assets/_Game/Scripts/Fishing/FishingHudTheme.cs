using UnityEngine;
using UnityEngine.UI;

// Presentation only: shared, cached nine-slice artwork. No gameplay state or input.
public static class FishingHudTheme
{
    public static readonly Color Cyan = new Color(.05f, .95f, 1f, 1f);
    private static readonly Sprite[] sprites = new Sprite[7];

    public static void Panel(GameObject target, int style = 0)
    {
        var image = target.GetComponent<Image>();
        if (image == null) return;
        image.sprite = Surface(style);
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    // 0: navy glass; 1: teal action; 2: translucent bait; 3: meter track; 5: cancel.
    private static Sprite Surface(int style)
    {
        if (sprites[style] != null) return sprites[style];
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "FishingHudGlass" + style;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float qx = Mathf.Abs(x - 47.5f) - 28f;
            float qy = Mathf.Abs(y - 47.5f) - 28f;
            float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude
                + Mathf.Min(Mathf.Max(qx, qy), 0) - 17f;
            float vertical = y / 95f;
            Color c = style == 1
                ? Color.Lerp(new Color(.005f,.29f,.46f,.98f), new Color(0,.83f,.83f,.98f), vertical)
                : Color.Lerp(new Color(.008f,.06f,.09f,.92f), new Color(.015f,.19f,.28f,.90f), vertical);
            if (style == 2) c = new Color(.12f,.38f,.48f,.35f);
            if (style == 3) c = new Color(.025f,.16f,.22f,.92f);
            float rim = Mathf.Clamp01(1f - Mathf.Abs(d + 1.7f) / 1.3f);
            float glow = Mathf.Exp(-Mathf.Abs(d + 2f) * .35f) * .32f;
            Color edge = Color.Lerp(new Color(0,.78f,.87f,1), new Color(.68f,.96f,1,1), vertical);
            c = Color.Lerp(c, edge, Mathf.Max(rim, glow) * (style == 3 ? .35f : style == 2 ? .6f : 1f));
            if (style == 4) c = Color.white;
            if (style == 5)
                c = Color.Lerp(new Color(.72f,.09f,.07f,.96f),Color.black,
                    Mathf.Clamp01(d+4f));
            if (style == 6)
            {
                Color selectedEdge = new Color(1f,.86f,.04f,1f);
                c = Color.Lerp(new Color(.025f,.12f,.14f,.98f), selectedEdge, Mathf.Max(rim,glow*1.8f));
            }
            c.a *= Mathf.Clamp01(.5f - d);
            pixels[y * size + x] = c;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sprites[style] = Sprite.Create(texture, new Rect(0,0,size,size), new Vector2(.5f,.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(24,24,24,24));
        return sprites[style];
    }

    public static void Fill(Image image)
    {
        image.sprite = Surface(4); image.type = Image.Type.Sliced;
    }

    public static void Divider(Transform parent, float fromTop)
    {
        var go = new GameObject("GlassDivider",typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.color=new Color(.2f,.72f,.85f,.28f);image.raycastTarget=false;
        var r=image.rectTransform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;
        r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(-20,1);
        r.anchoredPosition=new Vector2(0,-fromTop);
    }

    public static void Distance(Text text)
    {
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.rectTransform.sizeDelta = new Vector2(132f, 52f);
        var box = new GameObject("DistanceGlass", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(text.transform, false);
        box.transform.SetAsFirstSibling();
        var r = box.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(-8,-2); r.offsetMax = new Vector2(8,2);
        Panel(box);
        box.GetComponent<Image>().raycastTarget = false;
        // Text's own canvas graphic renders before its children; keep glass behind it.
        box.transform.SetParent(text.transform.parent, true);
        box.transform.SetAsFirstSibling();
        var follow = box.AddComponent<FishingHudDistanceBackdrop>();
        follow.label = text.rectTransform;
    }
}
