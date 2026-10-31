using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single presentation owner for the Tackle Store and Island / Fish Index.
/// Uses the supplied wave artwork with fixed rounded corners and themes only the
/// visual layer; no gameplay, travel, purchase or fishing logic is changed.
/// </summary>
[DefaultExecutionOrder(6900)]
public sealed class ShopAndIndexBackdropRepairRuntime : MonoBehaviour
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo PageField = typeof(ShopWorldHUD).GetField("page", PrivateInstance);
    private static readonly FieldInfo ModalField = typeof(ShopWorldHUD).GetField("modal", PrivateInstance);
    private static readonly FieldInfo HeadingField = typeof(ShopWorldHUD).GetField("heading", PrivateInstance);

    private static readonly Color DefaultMenuColor = new Color(.025f, .055f, .07f, .98f);
    private static readonly Color BrightGold = new Color(1f, .86f, .28f, 1f);
    private static readonly Color DetailCyan = new Color(.70f, .90f, .96f, 1f);
    private static readonly Color DisabledTint = new Color(.52f, .64f, .68f, .72f);
    private const float IslandTextDrop = 12f;

    private ShopWorldHUD hud;
    private Font shopFont;
    private bool wasThemed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach (ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if (target != null && target.GetComponent<ShopAndIndexBackdropRepairRuntime>() == null)
                target.gameObject.AddComponent<ShopAndIndexBackdropRepairRuntime>();
    }

    private void Awake()
    {
        hud = GetComponent<ShopWorldHUD>();
        shopFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void LateUpdate()
    {
        if (hud == null || PageField == null || ModalField == null) return;

        // These older presentation companions are superseded by this component.
        TackleStoreBackdropRuntime storePass = GetComponent<TackleStoreBackdropRuntime>();
        if (storePass != null && storePass.enabled) storePass.enabled = false;
        IslandFishIndexThemeRuntime indexPass = GetComponent<IslandFishIndexThemeRuntime>();
        if (indexPass != null && indexPass.enabled) indexPass.enabled = false;
        LargeShopBackdropSimpleImageFixRuntime oldImagePass = GetComponent<LargeShopBackdropSimpleImageFixRuntime>();
        if (oldImagePass != null && oldImagePass.enabled) oldImagePass.enabled = false;

        string page = PageField.GetValue(hud) as string;
        GameObject modalObject = ModalField.GetValue(hud) as GameObject;
        bool visible = modalObject != null && modalObject.activeInHierarchy;
        bool tackle = visible && page == "gear";
        bool index = visible && (page == "islands" || page == "reef-fish" || page == "stats");
        bool market = visible && (page == "market" || page == "sell-confirm" || page == "bait" || page == "bag" || page == "equipment" || page == "multiplayer" || page == "travel");

        if (!tackle && !index && !market)
        {
            if (wasThemed && modalObject != null)
                LargeShopBackdropTheme.RestoreDefault(modalObject.GetComponent<Image>(), DefaultMenuColor);
            wasThemed = false;
            return;
        }

        Image modal = modalObject.GetComponent<Image>();
        if (modal == null) return;
        LargeShopBackdropTheme.Apply(modal);

        if (index || market)
        {
            Text heading = HeadingField != null ? HeadingField.GetValue(hud) as Text : FindIndexHeading();
            if (heading != null)
            {
                if (page == "islands") heading.text = "ISLAND / FISH INDEX";
                heading.font = shopFont;
                heading.fontStyle = FontStyle.Bold;
                heading.color = market ? Color.white : BrightGold;
            }

            StyleIndexChrome(modalObject.transform);
            // The new fish view owns its card selection, silhouettes, and text.
            // Keep the supplied backdrop/CLOSE styling without restyling its tree.
            if(page=="reef-fish" || page=="stats"){wasThemed=true;return;}
            if(page=="travel"){wasThemed=true;return;}
            StyleIndexRows();
            StyleIndexText(modalObject.transform, heading);
            if (market && heading != null) heading.color = Color.white;
        }

        wasThemed = true;
    }

    private void StyleIndexChrome(Transform modal)
    {
        Image viewport = FindImage("ScrollViewport");
        if (viewport != null) viewport.color = new Color(0f, 0f, 0f, .04f);

        Image scrollbar = FindImage("Scrollbar");
        if (scrollbar != null)
        {
            FishingHudTheme.Panel(scrollbar.gameObject, 3);
            scrollbar.color = Color.white;
        }

        Image handle = FindImage("Handle");
        if (handle != null)
        {
            FishingHudTheme.Panel(handle.gameObject, 1);
            handle.color = Color.white;
        }

        Button[] buttons = modal.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy) continue;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null || !string.Equals(label.text, "CLOSE", StringComparison.OrdinalIgnoreCase)) continue;
            FishingHudTheme.Panel(button.gameObject);
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = Color.white;
            break;
        }
    }

    private void StyleIndexRows()
    {
        RectTransform rows = FindRect("Rows");
        if (rows == null) return;

        for (int i = 0; i < rows.childCount; i++)
        {
            Transform child = rows.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy) continue;

            if (child.name.StartsWith("BiomeCard_", StringComparison.Ordinal))
                DropIslandCardText(child);

            Button cardButton = child.GetComponent<Button>();
            Image panel = child.GetComponent<Image>();
            if (panel != null)
            {
                FishingHudTheme.Panel(child.gameObject);
                panel.color = cardButton != null && !cardButton.interactable ? DisabledTint : Color.white;
            }

            Button[] actions = child.GetComponentsInChildren<Button>(true);
            for (int b = 0; b < actions.Length; b++)
            {
                Button action = actions[b];
                if (action == null || !action.gameObject.activeInHierarchy || action.transform == child) continue;
                FishingHudTheme.Panel(action.gameObject, 1);
                Image image = action.GetComponent<Image>();
                if (image != null) image.color = action.interactable ? Color.white : DisabledTint;
            }
        }
    }

    private void StyleIndexText(Transform modal, Text pageHeading)
    {
        Text[] labels = modal.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            label.font = shopFont;

            Color original = label.color;
            if (label == pageHeading || IsGold(original))
            {
                label.color = BrightGold;
                label.fontStyle = FontStyle.Bold;
                continue;
            }

            string value = label.text ?? string.Empty;
            if ((value.IndexOf("COINS", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 value.IndexOf("FISH IN BAG", StringComparison.OrdinalIgnoreCase) >= 0) || IsDetail(original))
            {
                label.color = DetailCyan;
                label.fontStyle = FontStyle.Normal;
                continue;
            }

            Button ownerButton = label.GetComponentInParent<Button>();
            if (ownerButton != null)
            {
                label.fontStyle = FontStyle.Bold;
                if (ownerButton.interactable && IsNearWhite(label.color)) label.color = Color.white;
                continue;
            }

            Transform card = FindAncestorWithPrefix(label.transform, "BiomeCard_");
            if (card != null && IsNearWhite(label.color))
            {
                label.color = DetailCyan;
                label.fontStyle = FontStyle.Normal;
                continue;
            }

            if (IsNearWhite(label.color)) label.fontStyle = FontStyle.Bold;
        }
    }

    private static void DropIslandCardText(Transform card)
    {
        const string markerName = "__IslandIndexTextSpacing_v3";
        if (card == null || card.Find(markerName) != null) return;

        for (int i = 0; i < card.childCount; i++)
        {
            Transform child = card.GetChild(i);
            if (child == null || child.GetComponent<Button>() != null) continue;
            Text text = child.GetComponent<Text>();
            if (text == null) continue;
            RectTransform rect = text.rectTransform;
            rect.offsetMin += new Vector2(0f, -IslandTextDrop);
            rect.offsetMax += new Vector2(0f, -IslandTextDrop);
        }

        GameObject marker = new GameObject(markerName, typeof(RectTransform));
        marker.transform.SetParent(card, false);
        marker.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
    }

    private Text FindIndexHeading()
    {
        Text[] labels = hud.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            string value = label.text ?? string.Empty;
            if (string.Equals(value, "ISLAND / BIOME INDEX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ISLAND / FISH INDEX", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(" • FISH INDEX", StringComparison.OrdinalIgnoreCase))
                return label;
        }
        return null;
    }

    private Image FindImage(string name)
    {
        RectTransform rect = FindRect(name);
        return rect != null ? rect.GetComponent<Image>() : null;
    }

    private RectTransform FindRect(string name)
    {
        RectTransform[] transforms = hud.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < transforms.Length; i++)
            if (transforms[i] != null && transforms[i].name == name) return transforms[i];
        return null;
    }

    private static Transform FindAncestorWithPrefix(Transform start, string prefix)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.name.StartsWith(prefix, StringComparison.Ordinal)) return current;
            current = current.parent;
        }
        return null;
    }

    private static bool IsGold(Color color)
    {
        return color.r > .72f && color.g > .52f && color.g < .94f && color.b < .62f;
    }

    private static bool IsDetail(Color color)
    {
        return color.r > .48f && color.r < .82f && color.g > .68f && color.b > .70f;
    }

    private static bool IsNearWhite(Color color)
    {
        return color.r > .82f && color.g > .82f && color.b > .82f;
    }
}
