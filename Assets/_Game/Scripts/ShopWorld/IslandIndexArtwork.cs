using System;
using UnityEngine;
using UnityEngine.UI;

// Shared by the island cards and fish-index banner. Decode each supplied image once.
public static class IslandIndexArtwork
{
    private static readonly Texture2D[] Cache = new Texture2D[6];
    private static readonly bool[] Attempted = new bool[6];

    public static Texture2D Load(int biome)
    {
        if (biome < 0 || biome >= Cache.Length || biome == 2 || biome == OffshoreOilRigZone.BiomeId) return null;
        if (Attempted[biome]) return Cache[biome];
        Attempted[biome] = true;
        string name = biome == 0 ? "SuncrestBeach" : biome == 1 ? "BrinebreakIsle" :
            biome == 3 ? "BluewaterCay" : "SnapperIsland";
        var source = Resources.Load<TextAsset>("IslandIndexArtwork/" + name);
        if (source == null) return null;
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, Convert.FromBase64String(source.text), true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.name = name + " Index Artwork";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Cache[biome] = texture;
            return texture;
        }
        catch (FormatException)
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogError("Invalid island index artwork: " + name);
            return null;
        }
        finally { Resources.UnloadAsset(source); }
    }

    public static void Round(RawImage image)
    {
        if (image == null || image.texture == null) return;
        var rect = image.rectTransform;
        // Keep the original layout slot; fit the entire landscape inside it without distortion.
        var slot = new GameObject(image.name + "Slot", typeof(RectTransform)).GetComponent<RectTransform>();
        slot.SetParent(rect.parent, false);
        slot.SetSiblingIndex(rect.GetSiblingIndex());
        slot.anchorMin = rect.anchorMin; slot.anchorMax = rect.anchorMax;
        slot.pivot = rect.pivot; slot.sizeDelta = rect.sizeDelta;
        slot.anchoredPosition = rect.anchoredPosition;
        var frame = new GameObject("RoundedArtwork", typeof(RectTransform), typeof(Image),
            typeof(Mask), typeof(AspectRatioFitter));
        frame.transform.SetParent(slot, false);
        var background = frame.GetComponent<Image>();
        FishingHudTheme.Fill(background);
        background.color = Color.white;
        background.raycastTarget = false;
        frame.GetComponent<Mask>().showMaskGraphic = false;
        var fit = frame.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = (float)image.texture.width / image.texture.height;
        rect.SetParent(frame.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.maskable = true;
    }
}
