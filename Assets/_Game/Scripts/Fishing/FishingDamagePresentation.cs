using UnityEngine;
using UnityEngine.UI;

/// <summary>Styles each damage popup at spawn, including when its label is reused.</summary>
public static class FishingDamagePresentation
{
    private static Font electricFont;
    private static Font standardFont;

    public static void ConfigureDamageLabel(Text label, bool lightning, bool hardened, bool critical, bool skill)
    {
        if (standardFont == null) standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (lightning && electricFont == null) electricFont = Resources.Load<Font>("Fishing/Bungee-Regular");
        label.font = lightning && electricFont != null ? electricFont : standardFont;
        label.fontStyle = lightning ? FontStyle.Italic : FontStyle.Normal;
        label.resizeTextForBestFit = false;

        int fontSize;
        Vector2 dimensions;
        if (hardened)
        {
            fontSize = 32;
            dimensions = new Vector2(180f, 64f);
            label.color = new Color(.62f, .62f, .62f, 1f);
        }
        else if (lightning)
        {
            fontSize = 34;
            dimensions = new Vector2(220f, 76f);
            label.color = new Color(1f, .95f, .02f, 1f);
        }
        else if (critical || skill)
        {
            fontSize = 38;
            dimensions = new Vector2(205f, 72f);
            label.color = new Color(1f, .62f, .24f, 1f);
        }
        else
        {
            fontSize = 32;
            dimensions = new Vector2(180f, 64f);
            label.color = new Color(1f, .85f, .35f, 1f);
        }
        int scale = skill ? 2 : 1;
        label.fontSize = fontSize * scale;
        label.rectTransform.sizeDelta = dimensions * scale;

        Outline outline = label.GetComponent<Outline>();
        if (outline == null) outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .94f);
        outline.effectDistance = lightning ? new Vector2(2.2f, -2.2f) : new Vector2(1.6f, -1.6f);
        outline.useGraphicAlpha = true;
    }
}
