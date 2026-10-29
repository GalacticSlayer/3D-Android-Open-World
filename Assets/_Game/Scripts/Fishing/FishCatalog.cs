using UnityEngine;

public struct FishSpeciesDefinition
{
    public string Name;
    public Color BodyColor;
    public Color AccentColor;
    public float MinWeightKg;
    public float MaxWeightKg;
    public float Difficulty;
    public float RelativeChance;
    public int BaseSellCoins;

    public FishSpeciesDefinition(string name,Color bodyColor,Color accentColor,float minWeightKg,float maxWeightKg,float difficulty,float relativeChance,int baseSellCoins)
    {
        Name=name;BodyColor=bodyColor;AccentColor=accentColor;MinWeightKg=minWeightKg;MaxWeightKg=maxWeightKg;
        Difficulty=difficulty;RelativeChance=relativeChance;BaseSellCoins=baseSellCoins;
    }
}

public static class FishCatalog
{
    public const int RedSnapperId=1;
    public const int YellowfinTunaId=5;
    public const int YellowGoatfishId=6;
    public const int BlackSpotGoatfishId=7;
    public const int BigeyeTunaId=8;
    public const int BonitoId=9;
    public const int BlackSeaBassId=10;
    public const int StripedBassId=11;
    public const int SpottedSandBassId=12;
    public const int AlbacoreId=13;
    public const int GreaterAmberjackId=14;
    public const int BlacktipSharkId=15;
    public const int YellowtailSnapperId=16;
    public const int MuttonSnapperId=17;
    public const int BlacktipReefSharkId=18;
    public const int MakoSharkId=19;
    public const int BattleScarredMakoSharkId=20;
    public const int MangroveSnapperId=21;
    public const int BlackfinBarracudaId=22;
    public const int GreatBarracudaId=23;
    public const int BluefinTrevallyId=24;
    public const int GoldenTrevallyId=25;
    public const int GiantTrevallyId=26;
    public const int WinterFlounderId=27;
    public const int RoughtailStingrayId=28;
    public const int LeopardStingrayId=29;
    public const int MahoganySnapperId=30;
    public const int PicassoTriggerfishId=31;
    public const int TitanTriggerfishId=32;
    public const int EmperorAngelfishId=33;
    public const int JuvenileEmperorAngelfishId=34;
    public const int PantherGrouperId=35;
    public const int CoralGrouperId=36;
    // Positive Unity yaw is clockwise viewed from above. Keep pitch/roll and mouth pinning unchanged.
    public static float HeldYaw(int id)=>id==RoughtailStingrayId || id==LeopardStingrayId ? 16f : IsFlatfish(id) ? -4f : 86f;
    public static bool IsFlatfish(int id)=>id==WinterFlounderId || id==RoughtailStingrayId || id==LeopardStingrayId;

    // These embedded definitions are deliberately retained as a safe legacy fallback.
    // Editable min/max weight, health, value and catch odds live in Resources/FishingTuning.
    private static readonly FishSpeciesDefinition[] Species=
    {
        new FishSpeciesDefinition("Blue Mackerel",new Color(.20f,.42f,.58f),new Color(.72f,.86f,.90f),.25f,1.10f,.25f,42f,16),
        new FishSpeciesDefinition("Red Snapper",new Color(.72f,.20f,.16f),new Color(.96f,.54f,.34f),.70f,3.20f,.42f,26f,34),
        new FishSpeciesDefinition("Sea Bass",new Color(.25f,.38f,.31f),new Color(.65f,.72f,.58f),.90f,4.80f,.52f,18f,52),
        new FishSpeciesDefinition("Yellowtail",new Color(.18f,.38f,.56f),new Color(.94f,.76f,.18f),1.40f,6.50f,.68f,10f,92),
        default, // Retired species ID 4; never reuse saved IDs.
        new FishSpeciesDefinition("Yellowfin Tuna",new Color(.07f,.18f,.30f),new Color(.96f,.72f,.08f),4f,28f,.90f,2f,280),
        new FishSpeciesDefinition("Yellow Goatfish",new Color(.78f,.76f,.62f),new Color(.96f,.78f,.08f),.35f,2.20f,.46f,13f,48),
        new FishSpeciesDefinition("Black Spot Goatfish",new Color(.72f,.73f,.70f),new Color(.22f,.18f,.15f),.30f,1.80f,.43f,14f,44),
        new FishSpeciesDefinition("Bigeye Tuna",new Color(.10f,.22f,.32f),new Color(.75f,.80f,.80f),3f,40f,.86f,4f,220),
        new FishSpeciesDefinition("Bonito",new Color(.08f,.23f,.38f),new Color(.72f,.82f,.86f),.50f,5f,.58f,6f,78),
        new FishSpeciesDefinition("Black Sea Bass",new Color(.10f,.14f,.18f),new Color(.50f,.57f,.62f),.35f,2f,.68f,6f,74),
        new FishSpeciesDefinition("Striped Bass",new Color(.34f,.40f,.39f),new Color(.76f,.78f,.69f),.90f,25f,.52f,13f,128),
        new FishSpeciesDefinition("Spotted Sand Bass",new Color(.42f,.37f,.28f),new Color(.76f,.66f,.43f),.25f,2f,.68f,6f,72),
        new FishSpeciesDefinition("Albacore",new Color(.12f,.24f,.36f),new Color(.8f,.85f,.9f),2f,45f,.84f,4f,185),
        new FishSpeciesDefinition("Greater Amberjack",new Color(.3f,.4f,.42f),new Color(.84f,.72f,.3f),1.5f,70f,.88f,4f,195),
        new FishSpeciesDefinition("Blacktip Shark",new Color(.38f,.42f,.43f),new Color(.12f,.14f,.15f),5f,30f,1f,0f,1200),
        new FishSpeciesDefinition("Yellowtail Snapper",new Color(.64f,.70f,.72f),new Color(.98f,.82f,.16f),.25f,4.1f,.46f,13f,48),
        new FishSpeciesDefinition("Mutton Snapper",new Color(.58f,.32f,.24f),new Color(.88f,.62f,.38f),.5f,13f,.55f,5f,90),
        new FishSpeciesDefinition("Blacktip Reef Shark",new Color(.4f,.45f,.46f),new Color(.1f,.12f,.13f),3f,18f,.95f,0f,900),
        new FishSpeciesDefinition("Mako Shark",new Color(.12f,.27f,.36f),new Color(.7f,.75f,.78f),20f,150f,1f,0f,1800),
        new FishSpeciesDefinition("Battle Scarred Mako Shark",new Color(.12f,.27f,.36f),new Color(.6f,.65f,.68f),30f,225f,1f,0f,2700),
        new FishSpeciesDefinition("Mangrove Snapper",new Color(.42f,.34f,.25f),new Color(.88f,.58f,.24f),.5f,10.5f,.60f,3f,95),
        new FishSpeciesDefinition("Blackfin Barracuda",new Color(.25f,.35f,.39f),new Color(.66f,.70f,.68f),2f,30f,.83f,0f,450),
        new FishSpeciesDefinition("Great Barracuda",new Color(.64f,.70f,.72f),new Color(.20f,.26f,.28f),3.2f,48f,.83f,0f,720),
        new FishSpeciesDefinition("Bluefin Trevally",new Color(.18f,.43f,.64f),new Color(.33f,.75f,.80f),1f,70f,.93f,0f,286),
        new FishSpeciesDefinition("Golden Trevally",new Color(.91f,.68f,.22f),new Color(.97f,.86f,.42f),1f,70f,.90f,0f,260),
        new FishSpeciesDefinition("Giant Trevally",new Color(.27f,.32f,.36f),new Color(.67f,.73f,.76f),1.8f,126f,1f,0f,572),
        new FishSpeciesDefinition("Winter Flounder",new Color(.35f,.27f,.16f),new Color(.85f,.83f,.75f),.25f,3.6f,.68f,26f,150),
        new FishSpeciesDefinition("Roughtail Stingray",new Color(.16f,.15f,.14f),new Color(.83f,.81f,.76f),.25f,3.6f,.68f,26f,150),
        new FishSpeciesDefinition("Leopard Stingray",new Color(.16f,.15f,.14f),new Color(.83f,.81f,.76f),.25f,3.6f,.68f,26f,150),
        new FishSpeciesDefinition("Mahogany Snapper",new Color(.66f,.34f,.35f),new Color(.52f,.81f,.86f),.25f,4.1f,.48f,13f,48),
        new FishSpeciesDefinition("Picasso Triggerfish",new Color(.76f,.67f,.45f),new Color(.13f,.32f,.58f),.06f,.56f,.42f,3f,40),
        new FishSpeciesDefinition("Titan Triggerfish",new Color(.55f,.45f,.24f),new Color(.85f,.70f,.35f),.10f,11.32f,.84f,3f,80),
        new FishSpeciesDefinition("Emperor Angelfish",new Color(.1f,.3f,.7f),new Color(1f,.8f,.1f),0.013184078f,1.358574268f,.42f,30f,40),
        new FishSpeciesDefinition("Juvenile Emperor Angelfish",new Color(.05f,.08f,.15f),new Color(.2f,.6f,1f),0.013184078f,1.358574268f,.42f,0f,40),
        new FishSpeciesDefinition("Panther Grouper",new Color(.78f,.74f,.63f),new Color(.16f,.10f,.08f),0.017781491f,7.126399046f,1.05f,30f,100),
        new FishSpeciesDefinition("Coral Grouper",new Color(.9f,.2f,.08f),new Color(.1f,.6f,.9f),0.012587570f,1.678072426f,1.26f,30f,120)
    };

    public static int Count=>Species.Length;
    public static readonly int[] ActiveIds={0,1,2,3,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36};
    public static int CanonicalId(int id)=>id==4?YellowfinTunaId:id;

    // Tournament identity is deliberately separate from save/render identity.
    public static int TournamentSpeciesId(int id)
    {
        switch(CanonicalId(id))
        {
            case BattleScarredMakoSharkId:return MakoSharkId;
            case GreatBarracudaId:return BlackfinBarracudaId;
            case GiantTrevallyId:return BluefinTrevallyId;
            case LeopardStingrayId:return RoughtailStingrayId;
            case JuvenileEmperorAngelfishId:return EmperorAngelfishId;
            default:return CanonicalId(id);
        }
    }

    public static int ResolveSizeVariant(int id,float weightKg)
    {
        if(id!=EmperorAngelfishId && id!=JuvenileEmperorAngelfishId)return id;
        // Bottom 30% of the 10–50 cm length range, evaluated AFTER size bonuses.
        return weightKg<=FishSizeTable.WeightForLength(EmperorAngelfishId,.22f)
            ?JuvenileEmperorAngelfishId:EmperorAngelfishId;
    }

    public static FishSpeciesDefinition Get(int id)
    {
        int index=Mathf.Clamp(CanonicalId(id),0,Species.Length-1);
        var definition=Species[index];

        FishingTuning.SpeciesStats stats;
        if(FishingTuning.TryGetSpeciesStats(index,out stats))
        {
            definition.MinWeightKg=stats.MinWeightKg;
            definition.MaxWeightKg=stats.MaxWeightKg;
            definition.BaseSellCoins=stats.MinCostCoins;
        }

        float configuredChance;
        definition.RelativeChance=FishingTuning.TryGetChance(index,0,0,out configuredChance)
            ? configuredChance
            : ReefCatalog.Weight(index);
        return definition;
    }

    public static string FormatWeight(float kg)=>kg<.1f?(kg*1000f).ToString("0.0")+" g":kg.ToString("0.00")+" kg";
    public static int RollSpecies()=>ReefCatalog.Roll(Random.value);

    public static float RollWeight(int speciesId)
    {
        float configured;
        if(FishingTuning.TryRollWeight(speciesId,FishingTuning.LastBiome,Random.value,out configured))return configured;
        FishSpeciesDefinition definition=Get(speciesId);float t=Random.value;t*=t;
        return Mathf.Lerp(definition.MinWeightKg,definition.MaxWeightKg,t);
    }

    public static int GetSellValue(int speciesId,float weightKg)
    {
        int configured;
        if(FishingTuning.TryGetCost(speciesId,weightKg,out configured))return configured;

        FishSpeciesDefinition species=Get(speciesId);
        float length=FishSizeTable.LengthMetres(speciesId,weightKg);
        if(length<=.15f)
        {
            float tiny=Mathf.InverseLerp(.05f,.15f,length);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f,12f,tiny)),1,12);
        }
        float weightPercent=Mathf.InverseLerp(species.MinWeightKg,species.MaxWeightKg,weightKg);
        float weightMultiplier=Mathf.Lerp(.70f,1.90f,weightPercent);
        return Mathf.Max(1,Mathf.RoundToInt(species.BaseSellCoins*weightMultiplier));
    }

    public static float GetVisualScale(int speciesId,float weightKg)=>FishSizeTable.LengthMetres(speciesId,weightKg);
}


