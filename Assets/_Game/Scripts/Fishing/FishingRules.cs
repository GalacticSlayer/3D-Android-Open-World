using UnityEngine;

// Shared fishing rules. Editable balance now comes from Resources/FishingTuning;
// the older depth/offshore formulas remain below strictly as a safe fallback.
public static class FishingRules
{
    public static bool LureBiteAllowed { get; set; } = true;
    public static float OffshoreFactor { get; set; }
    public static float CastForwardOffset { get; set; }
    public static float CastPerpendicularOffsetSqr { get; set; }
    public static float CastVerticalOffset { get; set; }

    public static float CastPower(float elapsed) => 1f-Mathf.Sqrt(1f-Mathf.PingPong(Mathf.Max(0,elapsed)/1.25f,1f));

    public static float RequestedCastDistance(float power,float maximum)
        => Mathf.Lerp(5f,maximum,Mathf.Clamp01(power));

    public static float CastDistance(float power,float maximum)
    {
        float requested=RequestedCastDistance(power,maximum);
        float side=Mathf.Max(0f,CastPerpendicularOffsetSqr);
        float vertical=CastVerticalOffset*CastVerticalOffset;
        float remaining=requested*requested-side-vertical;
        if(remaining<=0f)return requested;
        float target=CastForwardOffset+Mathf.Sqrt(remaining);
        return Mathf.Max(0.01f,target);
    }

    public static bool ContinuousCastRange(bool[] samples,out float minimumPower)
    {
        minimumPower=1f;
        if(samples==null || samples.Length<2)return false;
        for(int i=0;i<samples.Length-1;i++)if(samples[i] && samples[i+1])
        {minimumPower=i/(float)(samples.Length-1);return true;}
        return false;
    }

    public static bool IsCastPowerAvailable(bool[] samples,float power)
    {
        if(samples==null || samples.Length<2)return false;
        int i=Mathf.Clamp((int)(Mathf.Clamp01(power)*(samples.Length-1)),0,samples.Length-2);
        return samples[i] && samples[i+1];
    }

    public static float OffshoreMaximumWeight(int species)
    {
        FishingTuning.SpeciesStats stats;
        if(FishingTuning.TryGetSpeciesStats(species,out stats))return stats.MaxWeightKg;
        return FishCatalog.Get(species).MaxWeightKg*2.25f;
    }

    private static float BaseWeightAtDepth(int species,float depth,float random01)
    {
        var fish=FishCatalog.Get(species);
        float deep=Mathf.InverseLerp(.6f,6f,depth);
        float upper=Mathf.Lerp(.30f,1f,deep);
        float sample=Mathf.Pow(Mathf.Clamp01(random01),Mathf.Lerp(3.5f,.65f,deep));
        return Mathf.Lerp(fish.MinWeightKg,fish.MaxWeightKg,upper*sample);
    }

    private static float ApplyOffshoreSize(int species,float normalWeight,float random01,float factor)
    {
        float offshore=Mathf.Clamp01(factor);
        if(offshore<=0f)return normalWeight;
        var fish=FishCatalog.Get(species);
        float offshoreMaximum=OffshoreMaximumWeight(species);
        float giantRoll=Mathf.Pow(Mathf.Clamp01(random01),.55f);
        float giant=Mathf.Lerp(fish.MaxWeightKg*.92f,offshoreMaximum,giantRoll);
        float strength=Mathf.SmoothStep(0f,1f,offshore);
        return Mathf.Lerp(normalWeight,Mathf.Max(normalWeight,giant),strength);
    }

    private static bool TryConfiguredWeight(int species,float random01,int biome,out float weight)
    {
        weight=0f;
        // Catch callers pass their frozen biome; preserve legacy callers' default.
        if(biome<0)biome=FishingTuning.LastBiome;
        float desired;
        // The battle-scarred Mako shares the normal Mako bite and
        // biome weight distribution, then scales its caught weight by 1.5x.
        // This also preserves Deep Ocean's 30% extra size for both variants.
        int rollSpecies=species==FishCatalog.BattleScarredMakoSharkId
            ? FishCatalog.MakoSharkId:species;
        if(!FishingTuning.TryRollWeight(rollSpecies,biome,random01,out desired))return false;
        if(species==FishCatalog.BattleScarredMakoSharkId)desired*=1.5f;

        // FishingSystem still contains a historical Brinebreak-only remap after this
        // call. Feed that remap its inverse so the FINAL caught weight remains the
        // normal-distribution sample specified in BiomeFishWeights.csv. This keeps
        // the large FishingSystem state machine untouched and prevents double-tuning.
        if(biome==1)
        {
            float practicalMin,practicalMax;
            FishingTuning.SpeciesStats stats;
            if(FishingTuning.TryGetWeightRange(species,biome,out practicalMin,out practicalMax) &&
               FishingTuning.TryGetSpeciesStats(species,out stats) && practicalMax>practicalMin)
            {
                float t=Mathf.InverseLerp(practicalMin,practicalMax,desired);
                weight=Mathf.Lerp(stats.MinWeightKg,stats.MaxWeightKg,t);
                return true;
            }
        }

        weight=desired;
        return true;
    }

    public static float WeightAtDepth(int species,float depth,float random01,float offshore=-1,int biome=-1)
    {
        float configured;
        if(TryConfiguredWeight(species,random01,biome,out configured))return configured;
        return ApplyOffshoreSize(species,BaseWeightAtDepth(species,depth,random01),random01,offshore<0?OffshoreFactor:offshore);
    }

    public static float WeightAtCastDistance(int species,float distance,float random01,float offshore=-1,int biome=-1)
    {
        float configured;
        if(TryConfiguredWeight(species,random01,biome,out configured))return configured;

        // Legacy fallback: Metal Spoon biases size upward. In configured mode bait
        // chooses species only; biome + species owns the weight distribution exactly.
        float sizeBias=ShopCatalog.ActiveLureVariant==3 ? .18f : 0f;
        float sample=Mathf.Lerp(Mathf.Clamp01(random01),1f,sizeBias);
        float depth=Mathf.Lerp(.6f,6f,Mathf.InverseLerp(5f,30f,distance));
        return ApplyOffshoreSize(species,BaseWeightAtDepth(species,depth,sample),sample,offshore<0?OffshoreFactor:offshore);
    }

    public static float LureBiteChance(float castDistance,float retrievedFraction)
    {
        if(!LureBiteAllowed)return 0f;
        float biteMultiplier=ShopCatalog.ActiveLureVariant==1 ? 1.15f : ShopCatalog.ActiveLureVariant==3 ? .85f : 1f;
        // Base lure: 70% over a complete 30 m retrieve; retain lure-specific bonuses.
        float fullChance=Mathf.Clamp01(.7f*Mathf.Clamp01(castDistance/30f)*biteMultiplier);
        return 1f-Mathf.Pow(1f-fullChance,Mathf.Clamp01(retrievedFraction));
    }

    public static int MaxHealth(int species,float kg,int biome=-1)
    {
        int configured;
        if(biome>=0 && FishingTuning.TryGetBiomeHealth(species,kg,biome,out configured))
            return configured;
        if(FishingTuning.TryGetHealth(species,kg,out configured))return configured;

        var fish=FishCatalog.Get(species);
        float rarity=1f/Mathf.Sqrt(Mathf.Max(1,ReefCatalog.Weight(species)));
        return Mathf.Max(1,Mathf.RoundToInt(2.5f*(10+22*Mathf.Sqrt(Mathf.Max(0,kg))+3*kg)*(1+fish.Difficulty*.5f+rarity)));
    }
}
