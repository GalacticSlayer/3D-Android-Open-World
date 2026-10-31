using System.Collections.Generic;

// Index-only grouping. Catch IDs and saved records remain stable when variants
// are grouped into a single species card. Add future variant families here.
public static class FishIndexCatalog
{
    // Display order only: Suncrest, Brinebreak, Deep Ocean, Bluewater, Snapper.
    // Saved species IDs, spawn odds and variant grouping are independent of this.
    private static readonly int[][] RegionOrder = {
        new[] { 6,7,41,33,31,32,42,35,37,36,39 },
        new[] { 27,10,12,2,11,14,40,29,28 },
        new[] { 0,9,10,3,13,8,5,15,19 },
        new[] { 0,9,3,13,25,24,8,5,26,15,19 },
        new[] { 16,30,1,17,21,38,22,23,18 }
    };

    private static readonly int[][] VariantFamilies = {
        new[] { FishCatalog.MakoSharkId, FishCatalog.BattleScarredMakoSharkId },
        new[] { FishCatalog.EmperorAngelfishId, FishCatalog.JuvenileEmperorAngelfishId }
    };

    public sealed class BaitOption
    {
        public readonly string Key, Name, PreviewKey;
        public BaitOption(string key,string name,string previewKey)
        { Key=key; Name=name; PreviewKey=previewKey; }
    }

    public static List<BaitOption> Baits()
    {
        var result=new List<BaitOption>();
        foreach(int id in new[] { 0, 2, 3 })
            result.Add(new BaitOption(FishingTuning.BaitKey(id),ShopCatalog.BaitNames[id],"Bait"+id));
        for(int i=0;i<ShopCatalog.LureVariantCount;i++)
            result.Add(new BaitOption("lure:"+i,ShopCatalog.LureNames[i],ShopCatalog.LurePreviewKey(i)));
        return result;
    }

    public static int Family(int species)
    {
        species=FishCatalog.CanonicalId(species);
        foreach(var family in VariantFamilies)
            foreach(int variant in family) if(variant==species)return family[0];
        return species;
    }

    public static int[] Variants(int species)
    {
        int id=Family(species);
        foreach(var family in VariantFamilies)if(family[0]==id)return family;
        return new[] { id };
    }

    public static bool InRegion(int species,int biome)
    {
        if(FishingTuning.IsValid)
        {
            foreach(var bait in Baits())
                if(FishingTuning.TryGetChance(species,bait.Key,biome,out float odds) && odds>0f)return true;
            return false;
        }
        // Retain a usable index if editable tuning is invalid. Detail odds are
        // shown as unavailable instead of silently inventing percentages.
        foreach(int bait in new[] { 0, 2, 3, ShopCatalog.StarterLure })
            if(ReefCatalog.EquippedChance(species,bait,biome)>0f)return true;
        return false;
    }

    public static List<int> RegionSpecies(int biome)
    {
        var result=new List<int>();
        foreach(int id in FishCatalog.ActiveIds)
        {
            int family=Family(id);
            if(InRegion(id,biome) && !result.Contains(family))result.Add(family);
        }
        result.Sort((a,b)=>DisplayRank(a,biome).CompareTo(DisplayRank(b,biome)));
        return result;
    }

    private static int DisplayRank(int family,int biome)
    {
        if(biome<0 || biome>=RegionOrder.Length)return family;
        int[] order=RegionOrder[biome];
        for(int i=0;i<order.Length;i++)if(order[i]==family)return i;
        // Keep future eligible species visible after the explicitly ordered list.
        return order.Length+family;
    }

    public static float CatchChance(int species,string key,int biome)
    {
        if(species==FishCatalog.JuvenileEmperorAngelfishId)species=FishCatalog.EmperorAngelfishId;
        if(species==FishCatalog.BattleScarredMakoSharkId)
            return FishingTuning.TryGetChance(FishCatalog.MakoSharkId,key,biome,out float combinedOdds)
                ?combinedOdds*FishCatalog.BattleScarredMakoVariantChance:-1f;
        if(!FishingTuning.TryGetChance(species,key,biome,out float odds))return -1f;
        return species==FishCatalog.MakoSharkId
            ?odds*(1f-FishCatalog.BattleScarredMakoVariantChance):odds;
    }

    public static List<BaitOption> PreferredOptions(int species,int biome,bool discovered)
    {
        var options=Baits();
        if(!discovered)
            options.RemoveAll(b=>!b.Key.StartsWith("lure:") || CatchChance(species,b.Key,biome)<0f);
        options.Sort((a,b)=> {
            int compared=CatchChance(species,b.Key,biome).CompareTo(CatchChance(species,a.Key,biome));
            return compared!=0?compared:string.CompareOrdinal(a.Key,b.Key);
        });
        if(!discovered && options.Count>3)options.RemoveRange(3,options.Count-3);
        return options;
    }

    public static string ChanceLabel(float percent,bool discovered)
    {
        if(!discovered)return "???";
        return percent>=0f?percent.ToString("0.##")+"%":"—";
    }

    public static bool Discovered(ShopLedger data,int species)
    {
        return data!=null && data.totalCaught!=null && species>=0 &&
            species<data.totalCaught.Length && data.totalCaught[species]>0;
    }

    public static float Best(ShopLedger data,int species)
    {
        return Discovered(data,species) && data.personalBestKg!=null && species<data.personalBestKg.Length
            ? data.personalBestKg[species] : 0f;
    }

    public static int FoundVariants(ShopLedger data,int family)
    {
        int found=0;
        foreach(int id in Variants(family))if(Discovered(data,id))found++;
        return found;
    }

    public static int BestVariant(ShopLedger data,int family)
    {
        int best=family;
        foreach(int id in Variants(family))
            if(Best(data,id)>Best(data,best))best=id;
        return best;
    }
}
