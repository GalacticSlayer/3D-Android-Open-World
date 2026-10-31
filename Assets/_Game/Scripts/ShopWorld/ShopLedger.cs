using System;
using System.Collections.Generic;

[Serializable] public sealed class HabitatOwnership
{
    public string id;
    public List<CaughtFishRecord> fish = new List<CaughtFishRecord>();
}

[Serializable] public sealed class TournamentWager
{
    public string id,ticket;
    public int amount,balance;
}

[Serializable] public sealed class ShopLedger
{
    public int version = 1, coins;
    public string playerName;
    public AnglerProgression angler = new AnglerProgression();
    public List<TournamentWager> tournamentWagers = new List<TournamentWager>();
    public bool brinebreakDiscovered;
    public bool bluewaterDiscovered;
    public bool snapperDiscovered;
    public List<string> boats = new List<string>();
    public string boatEquipped;
    public bool OwnsBoat(string id) => !string.IsNullOrEmpty(id) && boats!=null && boats.Contains(id);
    public bool EquipBoat(string id) { if(!OwnsBoat(id))return false;boatEquipped=id;return true; }
    public bool BuyBoat(string id,int cost)
    {
        if(string.IsNullOrWhiteSpace(id) || cost<0 || OwnsBoat(id) || coins<cost)return false;
        if(boats==null)boats=new List<string>();
        coins-=cost;boats.Add(id);boatEquipped=id;return true;
    }

    // rodOwned/reelOwned/lineOwned remain as the highest owned tier for backwards
    // compatibility. Ownership masks are authoritative. Buying a higher tier no
    // longer requires the previous one; lower tiers are marked owned automatically
    // so the existing equipment UI remains consistent after a direct purchase.
    public int rodOwned, reelOwned, lineOwned, rodEquipped, reelEquipped, lineEquipped, baitEquipped;
    public int gearOwnershipVersion;
    public int rodOwnedMask = 1, reelOwnedMask = 1, lineOwnedMask = 1;
    public int[] bait = new int[4];
    public int lureOwnedMask = 1;
    public int lureEquipped;
    public int lureQuantityVersion;
    public int[] lureQuantities;
    public bool lureFightPending;
    public int fightLureVariant;
    public bool infiniteWormsMigrated;
    public int catchStatsVersion;
    public int[] totalCaught;
    public float[] personalBestKg;

    public bool EnsureGearOwnership()
    {
        bool changed=false;
        if(gearOwnershipVersion<1)
        {
            rodOwnedMask=LegacyGearMask(rodOwned);
            reelOwnedMask=LegacyGearMask(reelOwned);
            lineOwnedMask=LegacyGearMask(lineOwned);
            gearOwnershipVersion=1;
            changed=true;
        }

        if((rodOwnedMask&1)==0){rodOwnedMask|=1;changed=true;}
        if((reelOwnedMask&1)==0){reelOwnedMask|=1;changed=true;}
        if((lineOwnedMask&1)==0){lineOwnedMask|=1;changed=true;}

        int rodHighest=HighestOwnedTier(rodOwnedMask);
        int reelHighest=HighestOwnedTier(reelOwnedMask);
        int lineHighest=HighestOwnedTier(lineOwnedMask);
        if(rodOwned!=rodHighest){rodOwned=rodHighest;changed=true;}
        if(reelOwned!=reelHighest){reelOwned=reelHighest;changed=true;}
        if(lineOwned!=lineHighest){lineOwned=lineHighest;changed=true;}

        if(!OwnsGear(GearKind.Rod,rodEquipped)){rodEquipped=0;changed=true;}
        if(!OwnsGear(GearKind.Reel,reelEquipped)){reelEquipped=0;changed=true;}
        if(!OwnsGear(GearKind.Line,lineEquipped)){lineEquipped=0;changed=true;}
        return changed;
    }

    private static int LegacyGearMask(int highest)
    {
        highest=Math.Max(0,Math.Min(ShopCatalog.MaxRodTier,highest));
        return (1<<(highest+1))-1;
    }

    private static int HighestOwnedTier(int mask)
    {
        for(int tier=ShopCatalog.MaxRodTier;tier>=0;tier--)if((mask&(1<<tier))!=0)return tier;
        return 0;
    }

    private static int MaxTier(GearKind kind)=>kind==GearKind.Rod?ShopCatalog.MaxRodTier:kind==GearKind.Reel?ShopCatalog.MaxReelTier:ShopCatalog.LineNames.Length-1;
    private int GearMask(GearKind kind) => kind==GearKind.Rod?rodOwnedMask:kind==GearKind.Reel?reelOwnedMask:lineOwnedMask;
    private void SetGearMask(GearKind kind,int mask)
    {
        if(kind==GearKind.Rod)rodOwnedMask=mask;
        else if(kind==GearKind.Reel)reelOwnedMask=mask;
        else lineOwnedMask=mask;
    }
    public bool OwnsGear(GearKind kind,int tier) => tier>=0 && tier<=MaxTier(kind) && (GearMask(kind)&(1<<tier))!=0;

    public bool EnsureLures()
    {
        bool changed=false;
        if(lureQuantityVersion<1)
        {
            lureQuantities=new int[ShopCatalog.LureVariantCount];
            // Preserve each old unlock, including the starter Heavy Jig, as one copy.
            for(int i=0;i<lureQuantities.Length;i++)
                lureQuantities[i]=(i==0 || (lureOwnedMask&(1<<i))!=0)?1:0;
            lureQuantityVersion=1;changed=true;
        }
        if(lureQuantities==null || lureQuantities.Length<ShopCatalog.LureVariantCount)
        {Array.Resize(ref lureQuantities,ShopCatalog.LureVariantCount);changed=true;}
        int mask=0;
        for(int i=0;i<ShopCatalog.LureVariantCount;i++)if(LureQuantity(i)>0)mask|=1<<i;
        if(mask!=lureOwnedMask){lureOwnedMask=mask;changed=true;}
        if(lureEquipped<0 || lureEquipped>=ShopCatalog.LureVariantCount)
        {lureEquipped=0;changed=true;}
        if(baitEquipped==ShopCatalog.StarterLure && !OwnsLure(lureEquipped))
        {baitEquipped=0;changed=true;}
        return changed;
    }
    public int LureQuantity(int variant) => variant>=0 && lureQuantities!=null && variant<lureQuantities.Length ? lureQuantities[variant] : 0;
    public bool OwnsLure(int variant) => variant>=0 && variant<ShopCatalog.LureVariantCount && LureQuantity(variant)>0;
    public bool BuyLure(int variant)
    {
        EnsureLures();
        if(variant<0 || variant>=ShopCatalog.LureVariantCount || LureQuantity(variant)==int.MaxValue || !Spend(ShopCatalog.LurePrices[variant]))return false;
        lureQuantities[variant]++;lureOwnedMask|=1<<variant;
        return true; // Stock purchases do not change the selected bait/lure.
    }
    public bool EquipLure(int variant)
    {
        if(!OwnsLure(variant))return false;
        lureEquipped=variant;baitEquipped=ShopCatalog.StarterLure;return true;
    }
    public bool BeginLureFight(int variant)
    {
        if(lureFightPending || !OwnsLure(variant))return false;
        fightLureVariant=variant;lureFightPending=true;return true;
    }
    public bool ResolveLureFight(bool caught)
    {
        if(!lureFightPending)return false;
        lureFightPending=false;
        if(!caught && OwnsLure(fightLureVariant))lureQuantities[fightLureVariant]--;
        EnsureLures();return true;
    }

    public bool EnsureCatchStats()
    {
        bool changed=false;
        if(catchStatsVersion<1)
        {
            totalCaught=new int[FishCatalog.Count];personalBestKg=new float[FishCatalog.Count];
            catchStatsVersion=1;changed=true;
        }
        else
        {
            if(totalCaught==null || totalCaught.Length<FishCatalog.Count){Array.Resize(ref totalCaught,FishCatalog.Count);changed=true;}
            if(personalBestKg==null || personalBestKg.Length<FishCatalog.Count){Array.Resize(ref personalBestKg,FishCatalog.Count);changed=true;}
        }
        return changed;
    }
    public void RecordCatch(int species,float kg)
    {
        EnsureCatchStats();species=FishCatalog.CanonicalId(species);
        if(species<0 || species>=FishCatalog.Count || float.IsNaN(kg) || float.IsInfinity(kg) || kg<=0)return;
        if(totalCaught[species]<int.MaxValue)totalCaught[species]++;
        personalBestKg[species]=Math.Max(personalBestKg[species],kg);
    }
    public List<CaughtFishRecord> bag = new List<CaughtFishRecord>();
    public List<HabitatOwnership> habitats = new List<HabitatOwnership>();
    public bool legacyMigrated;
    public bool homeMigrated;
    public const int BagLimit=50;
    public bool BagFull => bag.Count>=BagLimit;
    public HabitatOwnership CurrentHabitat(bool pond) => habitats.Find(h=>ShopCatalog.Habitat(h.id).pond==pond);
    public int UpgradeCost(string id)
    {
        var d=ShopCatalog.Habitat(id); if(d==null)return -1;
        var current=CurrentHabitat(d.pond);
        int index=Array.IndexOf(ShopCatalog.Habitats,d);
        int previous=current==null?(d.pond?4:-1):Array.IndexOf(ShopCatalog.Habitats,ShopCatalog.Habitat(current.id));
        return index==previous+1 ? d.price-(current==null?0:ShopCatalog.Habitat(current.id).price) : -1;
    }
    public void MigrateHome()
    {
        if(homeMigrated)return;
        foreach(bool pond in new[]{false,true})
        {
            var old=habitats.FindAll(h=>ShopCatalog.Habitat(h.id).pond==pond);
            if(old.Count<2)continue;
            old.Sort((a,b)=>ShopCatalog.Habitat(a.id).price.CompareTo(ShopCatalog.Habitat(b.id).price));
            var keep=old[old.Count-1];
            for(int i=0;i<old.Count-1;i++)
            {
                keep.fish.AddRange(old[i].fish);
                coins=(int)Math.Min(int.MaxValue,(long)coins+ShopCatalog.Habitat(old[i].id).price);
                habitats.Remove(old[i]);
            }
        }
        homeMigrated=true;
    }
    public int Owned(GearKind kind) => kind == GearKind.Rod ? rodOwned : kind == GearKind.Reel ? reelOwned : lineOwned;
    public int Equipped(GearKind kind) => kind == GearKind.Rod ? rodEquipped : kind == GearKind.Reel ? reelEquipped : lineEquipped;
    public HabitatOwnership Habitat(string id) => habitats.Find(h => h.id == id);
    public bool Spend(int price) { if (price < 0 || price==int.MaxValue || coins < price) return false; coins -= price; return true; }
    public bool BuyGear(GearKind kind, int tier)
    {
        if(tier<1 || tier>MaxTier(kind) || OwnsGear(kind,tier))return false;
        int price=ShopCatalog.GearPrice(kind,tier);
        if(!Spend(price))return false;
        SetGearMask(kind,GearMask(kind)|LegacyGearMask(tier));
        int highest=HighestOwnedTier(GearMask(kind));
        if(kind==GearKind.Rod)rodOwned=highest;
        else if(kind==GearKind.Reel)reelOwned=highest;
        else lineOwned=highest;
        return Equip(kind,tier);
    }
    public bool Equip(GearKind kind, int tier)
    {
        if(!OwnsGear(kind,tier))return false;
        if (kind == GearKind.Rod) rodEquipped=tier;
        else if (kind == GearKind.Reel) reelEquipped=tier; else lineEquipped=tier;
        return true;
    }
    public bool BuyBait(int id)
    {
        if (id < 2 || id > 3 || bait[id] > 9990 || !Spend(ShopCatalog.BaitPrices[id])) return false;
        bait[id]+=10; baitEquipped=id; return true;
    }
    public int ConsumeBait()
    {
        int id=baitEquipped;
        if(id==ShopCatalog.StarterLure && OwnsLure(lureEquipped))return id;
        if (id < 2 || id > 3 || bait[id] <= 0) { baitEquipped=0; return 0; }
        bait[id]--; if (bait[id]==0) baitEquipped=0; return id;
    }
    public bool BuyHabitat(string id)
    {
        var definition=ShopCatalog.Habitat(id);
        int cost=UpgradeCost(id);
        if (definition==null || cost<0 || !Spend(cost)) return false;
        var current=CurrentHabitat(definition.pond);
        if(current==null) habitats.Add(new HabitatOwnership { id=id });
        else current.id=id;
        return true;
    }
    public string Admission(string id, CaughtFishRecord fish)
    {
        var owned=Habitat(id); var d=ShopCatalog.Habitat(id);
        if (owned==null || d==null) return "Purchase this habitat first.";
        if (fish==null || float.IsNaN(fish.weightKg) || float.IsInfinity(fish.weightKg) || fish.weightKg<=0) return "Invalid fish weight.";
        if (owned.fish.Count>=d.fishLimit) return "Fish count limit reached.";
        if (fish.weightKg>d.maxFishKg) return "This fish exceeds the individual weight limit.";
        float weight=fish.weightKg; foreach(var f in owned.fish) weight+=f.weightKg;
        if (weight>d.totalKg+0.0001f) return "This fish would exceed the total weight limit.";
        if (ShopCatalog.FishLength(fish.speciesId,fish.weightKg)>Math.Min(d.width,d.depth)*0.48f) return "This fish needs more turning space.";
        return null;
    }
    public bool Deposit(string id, CaughtFishRecord fish)
    {
        if (!bag.Contains(fish) || Admission(id,fish)!=null) return false;
        bag.Remove(fish); Habitat(id).fish.Add(fish); return true;
    }
    public bool Withdraw(string id, CaughtFishRecord fish)
    {
        var h=Habitat(id); if(BagFull || h==null || !h.fish.Remove(fish)) return false;
        bag.Add(fish); return true;
    }
    public bool Sell(CaughtFishRecord fish, int value)
    {
        if (value<1 || coins>int.MaxValue-value || !bag.Remove(fish)) return false;
        coins+=value; return true;
    }
}





