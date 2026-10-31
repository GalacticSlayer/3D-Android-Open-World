using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Runtime contract for the human-editable fishing balance CSVs under
/// Assets/Resources/FishingTuning. Invalid data is rejected instead of silently
/// normalized; callers then fall back to the embedded legacy rules.
/// </summary>
public static class FishingTuning
{
    public sealed class SpeciesStats
    {
        public int SpeciesId;
        public string SpeciesName;
        public float MinWeightKg;
        public float MaxWeightKg;
        public int MinHealth;
        public int MaxHealth;
        public int MinCostCoins;
        public int MaxCostCoins;
        public float YellowSpeedMps;
    }

    public sealed class BiomeSpeciesStats
    {
        public int MinHealth, MaxHealth, MinCoins, MaxCoins;
    }

    public sealed class WeightDistribution
    {
        public string BiomeId;
        public int SpeciesId;
        public float MinKg;
        public float MaxKg;
        public bool Unavailable => MinKg==0f && MaxKg==0f;
    }

    private const string StatsResource="FishingTuning/FishStats";
    private const string BiomeStatsResource="FishingTuning/BiomeFishStats";
    private const string WeightResource="FishingTuning/BiomeFishWeights";
    private const string ChanceResource="FishingTuning/BiomeBaitSpeciesChance";

    // Human tuning needs only min/max. Internally P01 sits 1% of the range above
    // min and P99 sits 1% below max; the normal curve is truncated/renormalized at
    // the hard endpoints so there is no probability pile-up at min/max.
    private const float NormalP01Z=2.326347874f;
    private const float BoundedNormalLowerCdf=0.008802461f;
    private const float BoundedNormalUpperCdf=0.991197539f;

    // Use the catalog's stable active IDs directly so adding a species in one place
    // cannot silently leave the editable tuning system one fish behind.
    private static readonly int[] ActiveSpecies=FishCatalog.ActiveIds;
    private static readonly string[] Biomes={"suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island"};
    private static readonly string[] RequiredBaits={
        "worms","worms-legacy","shrimp","squid",
        "lure:0","lure:1","lure:2","lure:3","lure:4"
    };

    private static readonly Dictionary<int,SpeciesStats> Stats=new Dictionary<int,SpeciesStats>();
    private static readonly Dictionary<string,BiomeSpeciesStats> BiomeStats=new Dictionary<string,BiomeSpeciesStats>();
    private static readonly Dictionary<string,WeightDistribution> WeightRows=new Dictionary<string,WeightDistribution>();
    private static readonly Dictionary<string,float[]> ChanceRows=new Dictionary<string,float[]>();

    private static bool loaded;
    private static bool valid;
    private static string validationError;

    public static int LastBiome { get; private set; }
    public static bool IsValid { get { EnsureLoaded(); return valid; } }
    public static string ValidationError { get { EnsureLoaded(); return validationError; } }

    public static void RememberBiome(int biome)
    {
        LastBiome=Mathf.Clamp(biome,0,Biomes.Length-1);
    }

    public static string BiomeId(int biome)
    {
        return Biomes[Mathf.Clamp(biome,0,Biomes.Length-1)];
    }

    public static string BaitKey(int bait)
    {
        if(bait==0)return "worms";
        if(bait==1)return "worms-legacy";
        if(bait==2)return "shrimp";
        if(bait==3)return "squid";
        if(bait==ShopCatalog.StarterLure)return "lure:"+Mathf.Clamp(ShopCatalog.ActiveLureVariant,0,4);
        return "worms";
    }

    public static void Reload()
    {
        loaded=false;
        Load();
    }

    public static bool TryGetSpeciesStats(int species,out SpeciesStats stats)
    {
        stats=null;
        EnsureLoaded();
        if(!valid)return false;
        return Stats.TryGetValue(Canonical(species),out stats);
    }

    public const float GreenToYellowSpeed=0.95f/1.35f;
    public const float RedToYellowSpeed=1.75f/1.35f;

    public static float YellowSpeed(int species,float legacyDifficulty)
    {
        SpeciesStats stats;
        return TryGetSpeciesStats(species,out stats)
            ? stats.YellowSpeedMps
            : Mathf.Lerp(1.85f,2.95f,legacyDifficulty)*1.35f;
    }

    public static bool TryGetWeightDistribution(int species,int biome,out WeightDistribution row)
    {
        row=null;
        EnsureLoaded();
        if(!valid)return false;
        return WeightRows.TryGetValue(WeightKey(BiomeId(biome),Canonical(species)),out row);
    }

    public static bool TryGetQuartiles(int species,int biome,out float p25,out float p50,out float p75)
    {
        WeightDistribution row;
        if(TryGetWeightDistribution(species,biome,out row) && !row.Unavailable)
        {
            p25=WeightAtPercentile(row,.25f);
            p50=WeightAtPercentile(row,.50f);
            p75=WeightAtPercentile(row,.75f);
            return true;
        }
        p25=p50=p75=0f;
        return false;
    }

    public static bool TryGetWeightRange(int species,int biome,out float minimum,out float maximum)
    {
        minimum=maximum=0f;
        WeightDistribution row;
        if(!TryGetWeightDistribution(species,biome,out row))return false;
        minimum=row.MinKg;
        maximum=row.MaxKg;
        return true;
    }

    public static bool TryRollWeight(int species,int biome,float random01,out float kg)
    {
        kg=0f;
        WeightDistribution row;
        if(!TryGetWeightDistribution(species,biome,out row) || row.Unavailable)return false;
        kg=WeightAtPercentile(row,Mathf.Clamp01(random01));
        return true;
    }

    private static float WeightAtPercentile(WeightDistribution row,float percentile)
    {
        return WeightAtPercentile(row.MinKg,row.MaxKg,percentile);
    }

    // Worldwide species bounds from FishStats (the species table), never the
    // current biome's narrower catch range. Reuse the actual bounded normal curve.
    public static string SpecimenClass(int speciesId,float weightKg)
    {
        var species=FishCatalog.Get(speciesId);
        float min=species.MinWeightKg,max=species.MaxWeightKg;
        if(max<=min || float.IsNaN(weightKg))return "Typical Specimen";
        if(weightKg<WeightAtPercentile(min,max,.20f))return "Small Specimen";
        if(weightKg<WeightAtPercentile(min,max,.70f))return "Typical Specimen";
        if(weightKg<WeightAtPercentile(min,max,.94f))return "Large Specimen";
        if(weightKg<WeightAtPercentile(min,max,.99f))return "Trophy Specimen";
        return "Record Class Specimen";
    }

    private static float WeightAtPercentile(float minimum,float maximum,float percentile)
    {
        float range=maximum-minimum;
        if(range<=0f)return minimum;

        float mean=(minimum+maximum)*.5f;
        float p01=minimum+range*.01f;
        float p99=maximum-range*.01f;
        float sigma=(p99-p01)/(2f*NormalP01Z);
        float boundedProbability=Mathf.Lerp(
            BoundedNormalLowerCdf,
            BoundedNormalUpperCdf,
            Mathf.Clamp01(percentile));
        float z=InverseNormalCdf(boundedProbability);
        return Mathf.Clamp(mean+sigma*z,minimum,maximum);
    }

    // Peter J. Acklam inverse-normal approximation.
    private static float InverseNormalCdf(float probability)
    {
        double p=Math.Max(1e-12,Math.Min(1.0-1e-12,probability));
        const double a1=-3.969683028665376e+01;
        const double a2= 2.209460984245205e+02;
        const double a3=-2.759285104469687e+02;
        const double a4= 1.383577518672690e+02;
        const double a5=-3.066479806614716e+01;
        const double a6= 2.506628277459239e+00;
        const double b1=-5.447609879822406e+01;
        const double b2= 1.615858368580409e+02;
        const double b3=-1.556989798598866e+02;
        const double b4= 6.680131188771972e+01;
        const double b5=-1.328068155288572e+01;
        const double c1=-7.784894002430293e-03;
        const double c2=-3.223964580411365e-01;
        const double c3=-2.400758277161838e+00;
        const double c4=-2.549732539343734e+00;
        const double c5= 4.374664141464968e+00;
        const double c6= 2.938163982698783e+00;
        const double d1= 7.784695709041462e-03;
        const double d2= 3.224671290700398e-01;
        const double d3= 2.445134137142996e+00;
        const double d4= 3.754408661907416e+00;
        const double low=.02425;
        const double high=1.0-low;

        double x;
        if(p<low)
        {
            double q=Math.Sqrt(-2.0*Math.Log(p));
            x=(((((c1*q+c2)*q+c3)*q+c4)*q+c5)*q+c6)/((((d1*q+d2)*q+d3)*q+d4)*q+1.0);
        }
        else if(p>high)
        {
            double q=Math.Sqrt(-2.0*Math.Log(1.0-p));
            x=-(((((c1*q+c2)*q+c3)*q+c4)*q+c5)*q+c6)/((((d1*q+d2)*q+d3)*q+d4)*q+1.0);
        }
        else
        {
            double q=p-.5;
            double r=q*q;
            x=((((((a1*r+a2)*r+a3)*r+a4)*r+a5)*r+a6)*q)/(((((b1*r+b2)*r+b3)*r+b4)*r+b5)*r+1.0);
        }
        return (float)x;
    }

    public static bool TryGetChance(int species,int bait,int biome,out float percent)
    {
        return TryGetChance(species,BaitKey(bait),biome,out percent);
    }

    // Read any lure's odds for the index without changing the equipped lure.
    public static bool TryGetChance(int species,string baitKey,int biome,out float percent)
    {
        percent=0f;
        EnsureLoaded();
        if(!valid)return false;
        float[] row;
        if(!ChanceRows.TryGetValue(ChanceKey(BiomeId(biome),baitKey),out row))return false;
        int index=ActiveIndex(Canonical(species));
        if(index<0)return false;
        percent=row[index];
        return true;
    }

    public static bool TryRollSpecies(float random01,int bait,int biome,out int species)
    {
        species=0;
        EnsureLoaded();
        if(!valid)return false;
        float[] row;
        if(!ChanceRows.TryGetValue(ChanceKey(BiomeId(biome),BaitKey(bait)),out row))return false;
        float target=Mathf.Clamp01(random01)*100f;
        float cumulative=0f;
        for(int i=0;i<ActiveSpecies.Length;i++)
        {
            if(row[i]<=0f)continue;
            species=ActiveSpecies[i];
            cumulative+=row[i];
            if(target<cumulative)return true;
        }
        return cumulative>0f;
    }

    public static bool TryGetHealth(int species,float weightKg,out int health)
    {
        health=0;
        SpeciesStats stats;
        if(!TryGetSpeciesStats(species,out stats))return false;
        health=Mathf.Max(1,Mathf.RoundToInt(QuadraticByWeight(
            weightKg,stats.MinWeightKg,stats.MaxWeightKg,stats.MinHealth,stats.MaxHealth)));
        return true;
    }

    public static bool TryGetCost(int species,float weightKg,out int coins)
    {
        coins=0;
        SpeciesStats stats;
        if(!TryGetSpeciesStats(species,out stats))return false;
        coins=species==FishCatalog.RoughtailStingrayId
            ? RoughtailCoins(weightKg)
            : Mathf.Max(1,Mathf.RoundToInt(QuadraticByWeight(
                weightKg,stats.MinWeightKg,stats.MaxWeightKg,stats.MinCostCoins,stats.MaxCostCoins)));
        return true;
    }

    // The Leopard and Roughtail values are identical until Leopard's max 85kg.
    // Only the additional Roughtail tail is quadratic, ending at its own max coins.
    private static int RoughtailCoins(float kg)
    {
        SpeciesStats leopard,roughtail;
        if(!Stats.TryGetValue(FishCatalog.LeopardStingrayId,out leopard) ||
           !Stats.TryGetValue(FishCatalog.RoughtailStingrayId,out roughtail))return 1;
        float value=kg<=leopard.MaxWeightKg
            ? QuadraticByWeight(kg,leopard.MinWeightKg,leopard.MaxWeightKg,
                leopard.MinCostCoins,leopard.MaxCostCoins)
            : QuadraticByWeight(kg,leopard.MaxWeightKg,roughtail.MaxWeightKg,
                leopard.MaxCostCoins,roughtail.MaxCostCoins);
        return Mathf.Max(1,Mathf.RoundToInt(value));
    }

    private static bool TryBiomeBalance(int species,string biomeId,
        out BiomeSpeciesStats stat,out float minimum,out float maximum)
    {
        stat=null;minimum=maximum=0f;
        EnsureLoaded();
        if(!valid || string.IsNullOrEmpty(biomeId))return false;
        int id=Canonical(species);
        if(!BiomeStats.TryGetValue(WeightKey(biomeId,id),out stat))return false;
        WeightDistribution distribution;
        int rangeId=id==FishCatalog.BattleScarredMakoSharkId
            ? FishCatalog.MakoSharkId:id;
        if(!WeightRows.TryGetValue(WeightKey(biomeId,rangeId),out distribution) ||
            distribution.Unavailable)return false;
        float factor=id==FishCatalog.BattleScarredMakoSharkId ? 1.5f:1f;
        minimum=distribution.MinKg*factor;
        maximum=distribution.MaxKg*factor;
        return maximum>minimum;
    }

    public static bool TryGetBiomeHealth(int species,float kg,int biome,out int hp)
    {
        hp=0;
        BiomeSpeciesStats stat;float min,max;
        if(!TryBiomeBalance(species,BiomeId(biome),out stat,out min,out max))
            return false;
        hp=Mathf.Max(1,Mathf.RoundToInt(QuadraticByWeight(
            kg,min,max,stat.MinHealth,stat.MaxHealth)));
        return true;
    }

    public static bool TryGetBiomeCost(int species,float kg,string biomeId,out int coins)
    {
        coins=0;
        BiomeSpeciesStats stat;float min,max;
        if(!TryBiomeBalance(species,biomeId,out stat,out min,out max))
            return false;
        coins=species==FishCatalog.RoughtailStingrayId
            ? RoughtailCoins(kg)
            : Mathf.Max(1,Mathf.RoundToInt(QuadraticByWeight(
                kg,min,max,stat.MinCoins,stat.MaxCoins)));
        return true;
    }

    public static float QuadraticByWeight(float weight,float minWeight,float maxWeight,float minValue,float maxValue)
    {
        if(maxWeight<=minWeight)return maxValue;
        float t=Mathf.Clamp01((weight-minWeight)/(maxWeight-minWeight));
        return minValue+(maxValue-minValue)*t*t;
    }

    private static void EnsureLoaded()
    {
        if(!loaded)Load();
    }

    private static void Load()
    {
        loaded=true;
        valid=false;
        validationError=null;
        Stats.Clear();
        BiomeStats.Clear();
        WeightRows.Clear();
        ChanceRows.Clear();

        try
        {
            TextAsset statsAsset=Resources.Load<TextAsset>(StatsResource);
            TextAsset biomeStatsAsset=Resources.Load<TextAsset>(BiomeStatsResource);
            TextAsset weightAsset=Resources.Load<TextAsset>(WeightResource);
            TextAsset chanceAsset=Resources.Load<TextAsset>(ChanceResource);
            if(statsAsset==null){Fail("Missing Resources/FishingTuning/FishStats.csv");return;}
            if(biomeStatsAsset==null){Fail("Missing Resources/FishingTuning/BiomeFishStats.csv");return;}
            if(weightAsset==null){Fail("Missing Resources/FishingTuning/BiomeFishWeights.csv");return;}
            if(chanceAsset==null){Fail("Missing Resources/FishingTuning/BiomeBaitSpeciesChance.csv");return;}

            if(!ParseStats(statsAsset.text))return;
            if(!ParseWeights(weightAsset.text))return;
            if(!ParseChances(chanceAsset.text))return;
            if(!ParseBiomeStats(biomeStatsAsset.text))return;
            if(!ValidateCompleteness())return;
            valid=true;
        }
        catch(Exception e)
        {
            Fail("Unexpected parser error: "+e.Message);
        }
    }

    private static bool ParseStats(string text)
    {
        string[] lines=Lines(text);
        for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
        {
            string line=lines[lineIndex].Trim();
            if(Ignore(line) || line.StartsWith("speciesId,"))continue;
            string[] c=line.Split(',');
            if(c.Length!=9)return Fail("FishStats.csv line "+(lineIndex+1)+" must have 9 columns (including yellowSpeedMps).");

            int id,minHealth,maxHealth,minCost,maxCost;
            float minWeight,maxWeight,yellowSpeed;
            if(!Int(c[0],out id) || !Float(c[2],out minWeight) || !Float(c[3],out maxWeight) ||
               !Int(c[4],out minHealth) || !Int(c[5],out maxHealth) ||
               !Int(c[6],out minCost) || !Int(c[7],out maxCost) || !Float(c[8],out yellowSpeed))
                return Fail("FishStats.csv line "+(lineIndex+1)+" contains an invalid number.");

            id=Canonical(id);
            if(ActiveIndex(id)<0)return Fail("FishStats.csv line "+(lineIndex+1)+" uses inactive/unknown species ID "+id+".");
            if(Stats.ContainsKey(id))return Fail("FishStats.csv has duplicate species ID "+id+".");
            if(yellowSpeed<=0f)return Fail("FishStats.csv yellowSpeedMps must be positive for species "+id+".");
            if(minWeight<=0f || maxWeight<=minWeight)return Fail("FishStats.csv species "+id+" requires 0 < minWeightKg < maxWeightKg.");
            if(minHealth<1 || maxHealth<minHealth)return Fail("FishStats.csv species "+id+" requires 1 <= minHealth <= maxHealth.");
            if(minCost<1 || maxCost<minCost)return Fail("FishStats.csv species "+id+" requires 1 <= minCost <= maxCost.");

            Stats[id]=new SpeciesStats{
                SpeciesId=id,SpeciesName=c[1].Trim(),MinWeightKg=minWeight,MaxWeightKg=maxWeight,
                MinHealth=minHealth,MaxHealth=maxHealth,MinCostCoins=minCost,MaxCostCoins=maxCost,
                YellowSpeedMps=yellowSpeed
            };
        }
        return true;
    }

    private static bool ParseWeights(string text)
    {
        string[] lines=Lines(text);
        for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
        {
            string line=lines[lineIndex].Trim();
            if(Ignore(line) || line.StartsWith("biomeId,"))continue;
            string[] c=line.Split(',');
            if(c.Length!=5)return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" must have 5 columns: biomeId,speciesId,speciesName,minKg,maxKg.");

            string biome=c[0].Trim();
            int id;
            float min,max;
            if(BiomeIndex(biome)<0)return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" has unknown biome '"+biome+"'.");
            if(!Int(c[1],out id) || !Float(c[3],out min) || !Float(c[4],out max))
                return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" contains an invalid number.");

            id=Canonical(id);
            SpeciesStats stats;
            if(!Stats.TryGetValue(id,out stats))return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" references species "+id+" before/without FishStats.");

            bool unavailable=min==0f && max==0f;
            if(!unavailable && (min<=0f || max<=min))
                return Fail("BiomeFishWeights.csv "+biome+" species "+id+" requires either minKg=0 and maxKg=0 (unavailable), or 0 < minKg < maxKg.");
            // Deep Ocean deliberately extends beyond the ordinary species size
            // limit. Every other biome must remain inside its global range.
            if(!unavailable && biome!="deep-ocean" &&
               (min<stats.MinWeightKg || max>stats.MaxWeightKg))
                return Fail("BiomeFishWeights.csv "+biome+" species "+id+" min/max must stay inside FishStats species min/max ("+
                    stats.MinWeightKg.ToString("0.###",CultureInfo.InvariantCulture)+"–"+
                    stats.MaxWeightKg.ToString("0.###",CultureInfo.InvariantCulture)+" kg).");

            string key=WeightKey(biome,id);
            if(WeightRows.ContainsKey(key))return Fail("BiomeFishWeights.csv has duplicate row for "+biome+" species "+id+".");
            WeightRows[key]=new WeightDistribution{BiomeId=biome,SpeciesId=id,MinKg=min,MaxKg=max};
        }
        return true;
    }

    private static bool ParseBiomeStats(string text)
    {
        string[] lines=Lines(text);
        for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
        {
            string line=lines[lineIndex].Trim();
            if(Ignore(line) || line.StartsWith("biomeId,"))continue;
            string[] c=line.Split(',');
            if(c.Length!=7)return Fail("BiomeFishStats.csv line "+(lineIndex+1)+" must have 7 columns.");
            string biome=c[0].Trim();int id,minHP,maxHP,minCoins,maxCoins;
            if(BiomeIndex(biome)<0 || !Int(c[1],out id) ||
               !Int(c[3],out minHP) || !Int(c[4],out maxHP) ||
               !Int(c[5],out minCoins) || !Int(c[6],out maxCoins))
                return Fail("BiomeFishStats.csv line "+(lineIndex+1)+" has an invalid value.");
            id=Canonical(id);
            SpeciesStats species;
            if(!Stats.TryGetValue(id,out species) || species.SpeciesName!=c[2].Trim())
                return Fail("BiomeFishStats.csv species name/id mismatch at line "+(lineIndex+1)+".");
            if(minHP<1 || maxHP<minHP || minCoins<1 || maxCoins<minCoins)
                return Fail("BiomeFishStats.csv invalid HP/coin range for "+biome+" species "+id+".");
            string key=WeightKey(biome,id);
            if(BiomeStats.ContainsKey(key))
                return Fail("BiomeFishStats.csv duplicate "+key+".");
            WeightDistribution weights;
            int weightId=id==FishCatalog.BattleScarredMakoSharkId
                ?FishCatalog.MakoSharkId:id;
            if(!WeightRows.TryGetValue(WeightKey(biome,weightId),out weights) || weights.Unavailable)
                return Fail("BiomeFishStats.csv "+key+" is unavailable in the weight table.");
            BiomeStats[key]=new BiomeSpeciesStats{
                MinHealth=minHP,MaxHealth=maxHP,MinCoins=minCoins,MaxCoins=maxCoins};
        }
        return true;
    }

    private static bool ParseChances(string text)
    {
        string[] lines=Lines(text);
        for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
        {
            string line=lines[lineIndex].Trim();
            if(Ignore(line) || line.StartsWith("biomeId,"))continue;
            string[] c=line.Split(',');
            if(c.Length!=2+ActiveSpecies.Length)
                return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" must have "+(2+ActiveSpecies.Length)+" columns.");

            string biome=c[0].Trim();
            string bait=c[1].Trim();
            if(BiomeIndex(biome)<0)return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" has unknown biome '"+biome+"'.");
            if(!RequiredBait(bait))return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" has unknown baitKey '"+bait+"'.");

            float[] chances=new float[ActiveSpecies.Length];
            int total=0;
            for(int i=0;i<chances.Length;i++)
            {
                int wholeChance;
                if(!Int(c[i+2],out wholeChance) || wholeChance<0)
                    return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" requires whole-number, non-negative percentages in species column "+ActiveSpecies[i]+".");

                WeightDistribution weights;
                if(wholeChance>0 && WeightRows.TryGetValue(WeightKey(biome,ActiveSpecies[i]),out weights) && weights.Unavailable)
                    return Fail("BiomeBaitSpeciesChance.csv "+biome+" + "+bait+" gives species "+ActiveSpecies[i]+" ("+
                        Stats[ActiveSpecies[i]].SpeciesName+") "+wholeChance+"%, but BiomeFishWeights.csv marks it unavailable with minKg=0 and maxKg=0. Set its chance to 0% for EVERY bait/lure in this biome, or restore a positive weight range. Keep each chance row totaling 100%.");

                chances[i]=wholeChance;
                total+=wholeChance;
            }

            if(total!=100)
                return Fail("BiomeBaitSpeciesChance.csv "+biome+" + "+bait+" totals "+total+"%, not 100%. Fix the row; probabilities are NOT auto-normalized.");

            string key=ChanceKey(biome,bait);
            if(ChanceRows.ContainsKey(key))return Fail("BiomeBaitSpeciesChance.csv has duplicate row for "+biome+" + "+bait+".");
            ChanceRows[key]=chances;
        }
        return true;
    }

    private static bool ValidateCompleteness()
    {
        for(int i=0;i<ActiveSpecies.Length;i++)
            if(!Stats.ContainsKey(ActiveSpecies[i]))
                return Fail("FishStats.csv is missing active species ID "+ActiveSpecies[i]+".");

        for(int b=0;b<Biomes.Length;b++)
        {
            for(int i=0;i<ActiveSpecies.Length;i++)
            {
                string key=WeightKey(Biomes[b],ActiveSpecies[i]);
                WeightDistribution available;
                if(!WeightRows.TryGetValue(key,out available))
                    return Fail("BiomeFishWeights.csv is missing "+Biomes[b]+" species "+ActiveSpecies[i]+".");
                if(!available.Unavailable && !BiomeStats.ContainsKey(key))
                    return Fail("BiomeFishStats.csv missing "+Biomes[b]+" species "+ActiveSpecies[i]+".");
            }

            for(int k=0;k<RequiredBaits.Length;k++)
                if(!ChanceRows.ContainsKey(ChanceKey(Biomes[b],RequiredBaits[k])))
                    return Fail("BiomeBaitSpeciesChance.csv is missing "+Biomes[b]+" + "+RequiredBaits[k]+".");
        }
        return true;
    }

    private static bool Fail(string message)
    {
        valid=false;
        validationError=message;
        Debug.LogError("[FISH TUNING] "+message);
        return false;
    }

    private static string[] Lines(string text)=>text.Replace("\r",string.Empty).Split('\n');
    private static bool Ignore(string line)=>string.IsNullOrWhiteSpace(line) || line.StartsWith("#");
    private static bool Int(string s,out int value)=>int.TryParse(s.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out value);
    private static bool Float(string s,out float value)=>float.TryParse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    private static int Canonical(int id)=>id==4?5:id;
    private static string WeightKey(string biome,int species)=>biome+"|"+species;
    private static string ChanceKey(string biome,string bait)=>biome+"|"+bait;

    private static int ActiveIndex(int species)
    {
        for(int i=0;i<ActiveSpecies.Length;i++)if(ActiveSpecies[i]==species)return i;
        return -1;
    }

    private static int BiomeIndex(string biome)
    {
        for(int i=0;i<Biomes.Length;i++)
            if(string.Equals(Biomes[i],biome,StringComparison.OrdinalIgnoreCase))return i;
        return -1;
    }

    private static bool RequiredBait(string bait)
    {
        for(int i=0;i<RequiredBaits.Length;i++)
            if(string.Equals(RequiredBaits[i],bait,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
}
