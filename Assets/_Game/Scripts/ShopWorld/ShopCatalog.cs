using System;

public enum GearKind { Rod, Reel, Line }

[Serializable]
public sealed class HabitatDefinition
{
    public string id, name;
    public int price, fishLimit;
    public float maxFishKg, totalKg, width, depth, height;
    public bool pond, swimmable;
    public HabitatDefinition(string id, string name, int price, int count, float maxKg, float totalKg,
        float width, float height, float depth, bool pond = false, bool swim = false)
    {
        this.id=id; this.name=name; this.price=price; fishLimit=count; maxFishKg=maxKg; this.totalKg=totalKg;
        this.width=width; this.height=height; this.depth=depth; this.pond=pond; swimmable=swim;
    }
}

public static class ShopCatalog
{
    public const int StarterLure=4;
    public const int LureVariantCount=5;
    // Stable numeric IDs preserve saved purchases, equipment and tuning tables.
    public const int HeavyJigLureVariant=0;
    public const int LightJigLureVariant=1;
    public const int PopperLureVariant=4;
    public const int NeonBreachLureVariant=HeavyJigLureVariant; // Legacy tuning alias.
    public const int FireShadLureVariant=2;
    public const int BloodyBaitLureVariant=PopperLureVariant; // Legacy tuning alias.
    public const int MetalSpoonLureVariant=3; // Retains Deep Flash's saved ownership and tuning slot.
    public const int MaxRodTier=4;
    public const int MaxReelTier=3;

    public static readonly string[] LureNames =
    {
        "Heavy Jig",
        "Light Jig",
        "Fire Shad Crankbait",
        "Metal Spoon",
        "Popper"
    };

    public static readonly int[] LurePrices =
    {
        500,
        500,
        500,
        500,
        500
    };

    public static readonly string[] LureDescriptions =
    {
        "Balanced permanent heavy jig. Strong Sea Bass and Red Snapper focus.",
        "Quick-strike light jig. Highest Sea Bass odds and 15% more strike chance.",
        "Red Snapper specialist crankbait. Strongly favors Red Snapper while keeping the same tuna odds.",
        "Metal spoon. Slower strikes, larger fish, and the strongest tuna secondary odds.",
        "Permanent popper. Uses the same balanced fishing behavior as the Heavy Jig."
    };

    public static int ActiveLureVariant { get; private set; }

    public static bool PermanentBait(int id)=>id==0 || id==StarterLure;
    public static bool IsLure(int id)=>id==StarterLure;

    public static void SetActiveLureVariant(int variant)
    {
        ActiveLureVariant=Math.Max(0,Math.Min(LureVariantCount-1,variant));
        BaitNames[StarterLure]=LureNames[ActiveLureVariant];
    }

    public static string LurePrefabResource(int variant)
    {
        switch(variant)
        {
            case 1:return "Fishing/LightJig";
            case 2:return "Fishing/FireShadCrankbait";
            case MetalSpoonLureVariant:return "Fishing/MetalSpoon";
            case 4:return "Fishing/Popper";
            default:return "Fishing/HeavyJig";
        }
    }

    public static float LurePresentationRoll(int variant)
    {
        return variant == FireShadLureVariant ? 10f : 0f;
    }

    public static string LurePreviewKey(int variant)
    {
        return "Lure"+Math.Max(0,Math.Min(LureVariantCount-1,variant));
    }

    public static string RodPrefabResource(int tier)
    {
        switch(tier)
        {
            case 4:return "Fishing/FishingRodReelLevel5Rod";
            case 3:return "Fishing/FishingRodReelLevel4Rod";
            case 2:return "Fishing/FishingRodReelLevel3Rod";
            case 1:return "Fishing/FishingRodReelLevel2Rod";
            default:return "Fishing/FishingRodReel";
        }
    }

    public static string ReelPrefabResource(int tier)
    {
        switch(tier)
        {
            case 3:return "Fishing/FishingRodReelLevel4";
            case 2:return "Fishing/FishingRodReelLevel3";
            case 1:return "Fishing/FishingRodReelLevel2";
            default:return "Fishing/FishingRodReel";
        }
    }

    // Individual-fish and total-weight capacities are intentionally 3x the
    // original values. Fish-count limits and the physical habitat dimensions stay
    // unchanged, so this expands what can be housed without inflating collection
    // slot counts or visually resizing the tanks/ponds.
    public static readonly HabitatDefinition[] Habitats = {
        new HabitatDefinition("nano", "Tidepool Cabinet", 250, 3, 2.4f, 5.4f, 2.2f, 1.2f, 1.4f),
        new HabitatDefinition("reef", "Reef Gallery", 700, 6, 12f, 42f, 4.8f, 2.2f, 2.8f),
        new HabitatDefinition("lagoon", "Lagoon Suite", 2000, 10, 36f, 165f, 8f, 3.6f, 5f, false, true),
        new HabitatDefinition("grand", "Grand Ocean Gallery", 6500, 20, 105f, 660f, 14f, 5f, 9f, false, true),
        new HabitatDefinition("ocean", "Oceanarium", 18000, 40, 360f, 3000f, 26f, 7f, 16f, false, true),
        new HabitatDefinition("pond", "Courtyard Pond", 1400, 12, 24f, 135f, 10f, 2.8f, 8f, true, true),
        new HabitatDefinition("garden", "Garden Lagoon", 5000, 25, 105f, 720f, 18f, 4f, 14f, true, true),
        new HabitatDefinition("lake", "Sanctuary Lake", 15000, 50, 360f, 3600f, 32f, 6f, 24f, true, true)
    };
    public static readonly string[] RodNames = { "Woodland Rod", "Level 2 Fishing Rod", "Level 3 Fishing Rod", "Level 4 Fishing Rod", "Level 5 Fishing Rod" };
    public static readonly string[] ReelNames = { "Starter Reel", "Level 2 Fishing Reel", "Level 3 Fishing Reel", "Level 4 Fishing Reel" };
    public static readonly string[] LineNames = { "Standard Line", "Reinforced Line", "Braided Line", "Elite Braid" };
    public static readonly string[] BaitNames = { "Worms", "Worms (legacy)", "Shrimp", "Squid", "Heavy Jig" };
    public static readonly int[] BaitPrices = { 0, 35, 90, 180, 0 };
    public static readonly int[] RodPrices = { 0, 2000, 6000, 10000, 25000 };
    public static readonly int[] ReelPrices = { 0, 1500, 4500, 6500 };
    public static readonly int[] LinePrices = { 0, 100, 400, 1100 };
    public static readonly float[] LineBonus = { 0, 0, 0, 0 };
    public static HabitatDefinition Habitat(string id) => Array.Find(Habitats, h => h.id == id);
    public static string GearName(GearKind kind, int tier) => (kind == GearKind.Rod ? RodNames : kind == GearKind.Reel ? ReelNames : LineNames)[tier];
    public static int GearPrice(GearKind kind, int tier) => (kind == GearKind.Rod ? RodPrices : kind == GearKind.Reel ? ReelPrices : LinePrices)[tier];
    public static float FishLength(int species,float kg) => FishSizeTable.LengthMetres(species,kg);
}
