using UnityEngine;

// Stable zone/species IDs are save-safe; add future zones here without renumbering.
public static class ReefCatalog
{
    // Menu order is independent of the stable biome IDs used by saves and fishing.
    public static readonly int[] IslandIndexOrder = { 0, 4, 1, 3, 2 };
    public static bool SnapperDiscovered {get;set;}
    public static bool BluewaterDiscovered {get;set;}
    public const string RuggedId="brinebreak-isle";
    public static bool BrinebreakDiscovered {get;set;}
    public const string StarterId="suncrest-reef";
    public const string StarterName="Suncrest Reef";
    public const int SnapperBiomeId=4;

    public sealed class Zone
    {
        public readonly string id,name,description;
        public readonly bool unlockedByDefault;
        public readonly float[] weights;
        public Zone(string id,string name,string description,bool unlocked,float[] weights)
        {this.id=id;this.name=name;this.description=description;unlockedByDefault=unlocked;this.weights=weights;}
        public bool Unlocked=>unlockedByDefault||(id==RuggedId&&BrinebreakDiscovered)||(id==PelagicIslandGeometry.Id&&BluewaterDiscovered)||(id==SnapperIslandGeometry.Id&&SnapperDiscovered);
    }

    // Embedded odds remain only as a fallback if an editable tuning CSV is invalid.
    public static readonly Zone[] Zones={
        new Zone(StarterId,StarterName,"A palm-lined beginner island, sandy coves and a broad shallow reef.",true,
            new float[]{20,11,13,6,0,2,22,22,4,6,6,13,6,4,6,0,13,4,0,0,0,3,0,0}),
        new Zone(RuggedId,"Brinebreak Isle","Rocky low hills, steep sandy shores and restless water. Land here to unlock fast travel.",false,
            new float[]{8,13,12,18,0,10,5,5,20,12,12,10,12,4,6}),
        new Zone("deep-ocean","Deep Ocean","Beyond the shared outer shelf. Pelagic fish, giant catches and demanding fights.",true,
            new float[]{5,5,5,20,0,16,2,2,32,20,8,14,6,4,6}),
        new Zone(PelagicIslandGeometry.Id,PelagicIslandGeometry.Name,"A southern cay with pelagic fishing waters extending 75 m beyond the shore. Land here to unlock travel.",false,
            new float[]{35,0,0,0,0,20,0,0,15,15,0,0,0,15,0}),
        new Zone(SnapperIslandGeometry.Id,SnapperIslandGeometry.Name,"A dedicated snapper island directly south of Suncrest. Snappers, blackfin and great barracuda, and Blacktip Reef Sharks bite within 100 m of its three cave rocks. Land here to unlock travel.",false,
            new float[]{0,38,0,0,0,0,0,0,0,0,0,0,0,0,0,0,37,13,2,0,0,10,4,2})
    };

    public static Zone Starter=>Zones[0];
    public static float Weight(int species)
    {
        if(species==4)return 0f;
        float configured;
        if(FishingTuning.TryGetChance(species,0,0,out configured))return configured;
        if(species==FishCatalog.PantherGrouperId || species==FishCatalog.CoralGrouperId)return 30f;
        if(FishCatalog.IsFlatfish(species))return Starter.weights[FishCatalog.RedSnapperId];
        if(species==FishCatalog.PicassoTriggerfishId || species==FishCatalog.TitanTriggerfishId)return 3f;
        return species>=0&&species<Starter.weights.Length?Starter.weights[species]:0f;
    }
    public static string Rarity(int id)=>Weight(id)>=18?"Common":Weight(id)>=10?"Uncommon":Weight(id)>=4?"Rare":"Very rare";

    private static readonly float[] ShrimpOdds={11,15,8,3,0,1,30,30,2,4,8,6,12,2,4,0,15,4,0,0,0,3};
    private static readonly float[] SquidOdds={14,8,10,20,0,6,15,15,12,8,4,8,10,4,6,0,8,4,0,0,0,3};
    private static readonly float[][] LureOdds=
    {
        new float[]{4,21,36,19,0,6,4,4,6,6,7,8,9,4,6,0,2,4,0,0,0,3},
        new float[]{4,20,42,16,0,6,3,3,6,4,10,10,12,4,6,0,2,4,0,0,0,3},
        new float[]{4,45,22,9,0,6,4,4,6,5,5,7,10,4,6,0,2,4,0,0,0,3},
        new float[]{3,24,34,16,0,9,3,3,8,10,4,12,8,4,6,0,2,4,0,0,0,3},
        new float[]{4,21,36,19,0,6,4,4,6,6,7,8,10,4,6,0,2,4,0,0,0,3}
    };

    private static float[] BaseEquippedWeights(int bait)
    {
        if(bait==ShopCatalog.StarterLure)
        {int lure=Mathf.Clamp(ShopCatalog.ActiveLureVariant,0,LureOdds.Length-1);return LureOdds[lure];}
        return bait==2?ShrimpOdds:bait==3?SquidOdds:Starter.weights;
    }

    private static float[] EquippedWeights(int bait,int biome)
    {
        if(biome==SnapperBiomeId)
        {
            var snapper=new float[FishCatalog.Count];
            var snapperBaseline=Zones[SnapperBiomeId].weights;
            System.Array.Copy(snapperBaseline,snapper,System.Math.Min(snapperBaseline.Length,snapper.Length));
            snapper[FishCatalog.MahoganySnapperId]=snapper[FishCatalog.YellowtailSnapperId];
            return snapper;
        }
        // Legacy fallback only: keep the trevally species exclusive to Bluewater Cay,
        // even when the editable fishing tables are temporarily invalid.
        if(biome==PelagicIslandGeometry.BiomeId)
        {
            var pelagic=new float[FishCatalog.Count];
            var baseline=Zones[PelagicIslandGeometry.BiomeId].weights;
            for(int i=0;i<baseline.Length && i<pelagic.Length;i++)pelagic[i]=baseline[i];
            pelagic[FishCatalog.BluefinTrevallyId]=5f;
            pelagic[FishCatalog.GoldenTrevallyId]=5f;
            pelagic[FishCatalog.GiantTrevallyId]=3f;
            return pelagic;
        }
        var original=BaseEquippedWeights(bait);
        if(biome<=0||biome>=Zones.Length)
        {
            var starter=new float[FishCatalog.Count];
            System.Array.Copy(original,starter,original.Length);
            starter[FishCatalog.WinterFlounderId]=starter[FishCatalog.RedSnapperId];
            starter[FishCatalog.RoughtailStingrayId]=starter[FishCatalog.RedSnapperId];
            starter[FishCatalog.LeopardStingrayId]=starter[FishCatalog.RedSnapperId];
            // When editable tables are invalid, retain the requested triggerfish
            // probabilities instead of diluting 20% shrimp by merely appending
            // weights to the old starter total. All other fish stay proportional.
            float triggerChance=bait==2?20f:3f;
            float otherTotal=0f;
            for(int i=0;i<starter.Length;i++)
                if(i!=FishCatalog.PicassoTriggerfishId && i!=FishCatalog.TitanTriggerfishId)
                    otherTotal+=Mathf.Max(0f,starter[i]);
            float otherShare=100f-2f*triggerChance;
            if(otherTotal>0f)
                for(int i=0;i<starter.Length;i++)
                    if(i!=FishCatalog.PicassoTriggerfishId && i!=FishCatalog.TitanTriggerfishId)
                        starter[i]*=otherShare/otherTotal;
            starter[FishCatalog.PicassoTriggerfishId]=triggerChance;
            starter[FishCatalog.TitanTriggerfishId]=triggerChance;
            if(bait!=2 && bait!=3 && bait!=ShopCatalog.StarterLure)
            {
                for(int i=0;i<starter.Length;i++)
                    if(i!=FishCatalog.PicassoTriggerfishId && i!=FishCatalog.TitanTriggerfishId)
                        starter[i]*=8f/94f;
                starter[FishCatalog.EmperorAngelfishId]=30f;
                starter[FishCatalog.PantherGrouperId]=30f;
                starter[FishCatalog.CoralGrouperId]=30f;
                starter[FishCatalog.PicassoTriggerfishId]=1f;
                starter[FishCatalog.TitanTriggerfishId]=1f;
            }
            return starter;
        }
        var weights=new float[original.Length];
        for(int i=0;i<weights.Length;i++)weights[i]=i<Zones[biome].weights.Length && i<Starter.weights.Length && Starter.weights[i]>0?original[i]*Zones[biome].weights[i]/Starter.weights[i]:0;
        // Invalid tuning must not reintroduce snappers into Deep Ocean catches.
        if(biome==2)
        {
            weights[FishCatalog.RedSnapperId]=0f;
            if(FishCatalog.MangroveSnapperId<weights.Length)weights[FishCatalog.MangroveSnapperId]=0f;
        }
        return weights;
    }

    public static float MinimumWeight(int species,int biome)
    {
        float minimum,maximum;
        if(FishingTuning.TryGetWeightRange(species,biome,out minimum,out maximum))return minimum;
        var fish=FishCatalog.Get(species);return biome==1?Mathf.Max(fish.MinWeightKg,fish.MaxWeightKg*.18f):fish.MinWeightKg;
    }

    public static float MaximumWeight(int species,int biome)
    {
        float minimum,maximum;
        if(FishingTuning.TryGetWeightRange(species,biome,out minimum,out maximum))return maximum;
        return FishCatalog.Get(species).MaxWeightKg*(biome==1?1.6f:biome==2?2.25f:1f);
    }

    // Health is now species + weight only. Kept for API compatibility with existing UI/code.
    public static float HealthMultiplier(int biome)=>1f;

    private static float Total(float[] weights)
    {float total=0f;if(weights!=null)for(int i=0;i<weights.Length;i++)total+=Mathf.Max(0f,weights[i]);return total;}

    public static float EquippedChance(int species,int bait,int biome=0)
    {
        if(species==4)return 0f;
        float configured;
        if(FishingTuning.TryGetChance(species,bait,biome,out configured))return configured;
        float[] weights=EquippedWeights(bait,biome);if(species<0||species>=weights.Length)return 0f;
        float total=Total(weights);return total>0f?Mathf.Max(0f,weights[species])/total*100f:0f;
    }

    public static int Roll(float random01,int bait=0,int biome=0)
    {
        FishingTuning.RememberBiome(biome);

        // Config rows are already validated to exactly 100%; roll those percentages
        // directly so a fish configured at 0% can never be selected, even at random=0.
        if(FishingTuning.IsValid)
        {
            float pick=Mathf.Clamp01(random01)*100f;
            int fallback=FishCatalog.ActiveIds[0];bool found=false;
            foreach(int id in FishCatalog.ActiveIds)
            {
                float chance;
                if(!FishingTuning.TryGetChance(id,bait,biome,out chance)){found=false;break;}
                if(chance<=0f)continue;
                found=true;fallback=id;
                if(pick<chance)return id;
                pick-=chance;
            }
            if(found)return fallback;
        }

        float[] weights=EquippedWeights(bait,biome);float total=Total(weights);if(total<=0f)return FishCatalog.ActiveIds[0];
        float legacyPick=Mathf.Clamp01(random01)*total;int legacyFallback=FishCatalog.ActiveIds[0];
        for(int i=0;i<weights.Length;i++)
        {float weight=Mathf.Max(0f,weights[i]);if(weight<=0f)continue;legacyFallback=i;legacyPick-=weight;if(legacyPick<0f)return i;}
        return legacyFallback;
    }
}



