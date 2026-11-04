using UnityEngine;
using UnityEngine.UI;

// Visual only: automatically swaps the hotbar rod artwork when the equipped
// rod tier changes. Does not alter game equipment, input, or progression.
public sealed class FishingHotbarRodArtwork : MonoBehaviour
{
    private RawImage image;
    private ShopProgress progress;
    private ShopPreview fallback;
    private int displayedTier = -1;

    public void Initialize(RawImage target)
    {
        image = target;
        Refresh();
    }

    private void Update()
    {
        if (image == null) return;
        if (progress == null) progress = FindObjectOfType<ShopProgress>();
        if (progress == null || progress.Data == null) return;
        int tier = Mathf.Clamp(progress.Data.Equipped(GearKind.Rod), 0, ShopCatalog.MaxRodTier);
        if (tier != displayedTier) Refresh();
    }

    private void Refresh()
    {
        if (image == null) image = GetComponent<RawImage>();
        if (progress == null) progress = FindObjectOfType<ShopProgress>();
        int tier = progress != null && progress.Data != null
            ? Mathf.Clamp(progress.Data.Equipped(GearKind.Rod), 0, ShopCatalog.MaxRodTier) : 0;
        displayedTier = tier;
        Texture2D texture = Resources.Load<Texture2D>("Fishing/ItemIcons/Rod" + tier);
        if (texture != null)
        {
            image.texture = texture;
            image.uvRect = new Rect(0, 0, 1, 1);
            image.color = Color.white;
            image.enabled = true;
            if (fallback != null) { Destroy(fallback); fallback = null; }
        }
        else if (fallback == null)
        {
            // Keep the existing hotbar representation if artwork is unavailable.
            fallback = gameObject.AddComponent<ShopPreview>();
            // Render the actual Level 7 rod if no hand-painted icon exists.
            // Earlier tiers retain their existing transparent artwork.
            fallback.Attach(image, 0, 0, tier >= 6 ? "Rod" + tier : "RodAssembly");
        }
    }
}
