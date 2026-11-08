using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

// Same record fields as Unity's FishingInventory; these tests compile the real
// ShopLedger and ShopCatalog, without depending on Unity or third-party packages.
[Serializable] public sealed class CaughtFishRecord
{
    public int speciesId; public float weightKg; public long caughtUtcTicks;
}
internal static class Program
{
    private static int checks;
    private static void Check(bool condition,string message) { checks++; if(!condition)throw new Exception(message); }
    private static CaughtFishRecord Fish(float kg,int species=0) => new CaughtFishRecord { speciesId=species,weightKg=kg,caughtUtcTicks=DateTime.UtcNow.Ticks };
    private static void Main()
    {
        var d=new ShopLedger { coins=1999 };d.EnsureGearOwnership();
        Check(!d.BuyGear(GearKind.Rod,1) && d.coins==1999,"Unaffordable purchase changed balance");
        d.coins=2000; Check(d.BuyGear(GearKind.Rod,1) && d.coins==0 && d.rodEquipped==1,"Exact-price upgrade failed");
        Check(!d.BuyGear(GearKind.Rod,1),"Duplicate gear purchase");
        var direct=new ShopLedger {coins=6000};direct.EnsureGearOwnership();
        Check(!direct.BuyGear(GearKind.Rod,2) && direct.coins==6000 && direct.rodEquipped==0,"Skipped Level 2 rod prerequisite or changed balance/equipment");
        Check(!direct.OwnsGear(GearKind.Rod,1) && !direct.OwnsGear(GearKind.Rod,2),"Rejected purchase granted gear");
        foreach(GearKind kind in Enum.GetValues(typeof(GearKind)))
        {
            var gear=new ShopLedger {coins=1000000};gear.EnsureGearOwnership();
            int max=kind==GearKind.Rod?ShopCatalog.MaxRodTier:kind==GearKind.Reel?ShopCatalog.MaxReelTier:ShopCatalog.LineNames.Length-1;
            for(int tier=1;tier<=max;tier++)
            {
                int before=gear.coins;
                if(tier<max)
                    Check(!gear.BuyGear(kind,tier+1) && gear.coins==before && !gear.OwnsGear(kind,tier+1),"Skipped tier or charged for a locked item: "+kind);
                Check(gear.BuyGear(kind,tier) && gear.coins==before-ShopCatalog.GearPrice(kind,tier) && gear.Equipped(kind)==tier,"Sequential gear purchase failed: "+kind);
                before=gear.coins;
                Check(!gear.BuyGear(kind,tier) && gear.coins==before,"Duplicate gear purchase charged: "+kind);
            }
            Check(gear.Equip(kind,0) && gear.Equip(kind,max),"Owned gear could not be re-equipped: "+kind);
        }

        var reels=new ShopLedger {coins=6000};reels.EnsureGearOwnership();
        Check(ShopCatalog.MaxReelTier==2 && ShopCatalog.ReelNames[2]=="Level 3 Fishing Reel","Level 3 reel catalog entry missing");
        Check(ShopCatalog.ReelPrices[1]==1500 && ShopCatalog.ReelPrices[2]==4500,"Reel upgrade prices changed unexpectedly");
        Check(ShopCatalog.ReelPrefabResource(2)=="Fishing/FishingRodReelLevel3","Level 3 reel prefab resource mismatch");
        Check(reels.BuyGear(GearKind.Reel,1) && reels.coins==4500 && reels.reelEquipped==1,"Level 2 reel purchase failed");
        Check(reels.BuyGear(GearKind.Reel,2) && reels.coins==0 && reels.reelEquipped==2,"Level 3 reel purchase failed");
        Check(reels.OwnsGear(GearKind.Reel,0) && reels.OwnsGear(GearKind.Reel,1) && reels.OwnsGear(GearKind.Reel,2),"Reel ownership progression incomplete");

        Check(!d.Equip(GearKind.Reel,3),"Equipped unowned item");
        Check(d.Equip(GearKind.Rod,0),"Could not re-equip starter");
        d.coins=10000;
        Check(d.BuyBait(2) && d.bait[2]==10,"Bait pack size");
        for(int i=0;i<10;i++)Check(d.ConsumeBait()==2,"Wrong bait consumed");
        Check(d.baitEquipped==0 && d.ConsumeBait()==0 && d.bait[2]==0,"No infinite-worm fallback");
        Check(d.BuyHabitat("nano"),"Habitat purchase failed");int coins=d.coins;
        Check(!d.BuyHabitat("nano") && d.coins==coins,"Duplicate habitat charged");
        var heavy=Fish(0.81f);d.bag.Add(heavy);Check(!d.Deposit("nano",heavy) && d.bag.Contains(heavy),"Oversized fish lost");
        var a=Fish(0.8f);var b=Fish(0.8f);var c=Fish(0.3f);d.bag.AddRange(new[]{a,b,c});
        Check(d.Deposit("nano",a) && d.Deposit("nano",b),"Valid transfer failed");
        Check(!d.Deposit("nano",c) && d.bag.Contains(c),"Total mass limit bypassed");
        Check(!d.Deposit("nano",a),"Same fish deposited twice");
        Check(d.Withdraw("nano",a) && d.bag.Contains(a),"Withdrawal lost fish");
        Check(!d.Withdraw("nano",a),"Same fish withdrawn twice");
        var tiny=Fish(0.1f);var tiny2=Fish(0.1f);var tiny3=Fish(0.1f);
        var tiny4=Fish(0.1f);var tiny5=Fish(0.1f);var tiny6=Fish(0.1f);
        d.bag.AddRange(new[]{tiny,tiny2,tiny3,tiny4,tiny5,tiny6});
        Check(d.Deposit("nano",tiny) && d.Deposit("nano",tiny2) && d.Deposit("nano",tiny3) &&
              d.Deposit("nano",tiny4) && d.Deposit("nano",tiny5) && !d.Deposit("nano",tiny6),"Fish count limit bypassed");
        Check(!d.Deposit("lake",heavy),"Deposit into unowned lake");
        var invalid=Fish(float.NaN);d.bag.Add(invalid);Check(!d.Deposit("nano",invalid),"NaN admitted");d.bag.Remove(invalid);
        Check(d.Sell(a,20) && !d.Sell(a,20),"Duplicate sale paid twice");
        Check(Math.Abs(ShopCatalog.FishLength(0,0.5f)-0.3556f)<0.01f,"Mackerel length disagrees with source estimate");
        Check(ShopCatalog.FishLength(0,1000)<=1.0f && ShopCatalog.FishLength(6,1000)<=0.91f,"Extreme saved weights exceeded the open-ocean visual cap");
        for(int species=0;species<FishCatalog.Count;species++)
        {
            float previous=0;
            for(int i=1;i<=200;i++){float length=ShopCatalog.FishLength(species,i*0.1f);Check(length>=previous && !float.IsNaN(length),"Length table not monotone");previous=length;}
        }
        for(int i=0;i<150;i++)d.bag.Add(Fish(0.25f));
        var options=new JsonSerializerOptions { IncludeFields=true };
        string json=JsonSerializer.Serialize(d,options);var restored=JsonSerializer.Deserialize<ShopLedger>(json,options);
        Check(restored.coins==d.coins && restored.bag.Count==d.bag.Count && restored.bag.Count>150,"Full bag save lost fish");
        Check(restored.Habitat("nano").fish.Count==6 && restored.rodOwned==1 && restored.bait[2]==0,"Save lost ownership or bait");
        foreach(var habitat in ShopCatalog.Habitats)
        {
            Check(habitat.price>0 && habitat.fishLimit>0 && habitat.maxFishKg<=habitat.totalKg,"Invalid habitat limits");
            Check(ShopCatalog.FishLength(5,habitat.maxFishKg)<Math.Min(habitat.width,habitat.depth)*0.48f,"Catalog admits fish too long to turn");
        }
        var upgrades=new ShopLedger {coins=50000};
        Check(!upgrades.BuyHabitat("reef"),"Skipped aquarium prerequisite");
        Check(upgrades.BuyHabitat("nano") && upgrades.coins==49750,"Initial aquarium price");
        var resident=Fish(0.2f);upgrades.bag.Add(resident);upgrades.Deposit("nano",resident);
        Check(upgrades.UpgradeCost("reef")==450,"Upgrade should charge the difference");
        Check(upgrades.BuyHabitat("reef") && upgrades.Habitat("nano")==null && upgrades.Habitat("reef").fish.Contains(resident),"Upgrade lost resident or retained old aquarium");
        foreach(string id in new[]{"lagoon","grand","ocean"})Check(upgrades.BuyHabitat(id),"Aquarium progression failed");
        Check(upgrades.habitats.Count==1 && upgrades.coins==32000,"Aquarium total cost or single-slot ownership incorrect");
        Check(!upgrades.BuyHabitat("lake") && upgrades.BuyHabitat("pond"),"Pond prerequisite");
        Check(upgrades.BuyHabitat("garden") && upgrades.BuyHabitat("lake") && upgrades.habitats.Count==2 && upgrades.coins==17000,"Independent pond progression");
        for(int i=0;i<ShopLedger.BagLimit;i++)upgrades.bag.Add(Fish(1));
        Check(upgrades.BagFull && !upgrades.Withdraw("ocean",resident) && upgrades.Habitat("ocean").fish.Contains(resident),"Full-bag withdrawal lost resident");
        upgrades.bag.RemoveAt(0);Check(upgrades.Withdraw("ocean",resident) && upgrades.bag.Count==50,"Last bag slot rejected");
        var legacy=new ShopLedger {coins=100};
        legacy.habitats.Add(new HabitatOwnership{id="nano",fish=new(){Fish(0.2f)}});
        legacy.habitats.Add(new HabitatOwnership{id="reef",fish=new(){Fish(1)}});
        legacy.habitats.Add(new HabitatOwnership{id="garden",fish=new(){Fish(2)}});
        legacy.MigrateHome();
        Check(legacy.habitats.Count==2 && legacy.Habitat("reef").fish.Count==2 && legacy.coins==350,"Legacy consolidation lost fish or credit");
        legacy.MigrateHome();Check(legacy.coins==350 && legacy.Habitat("reef").fish.Count==2,"Migration applied twice");
        Check(legacy.UpgradeCost("lake")==10000,"Migrated pond cannot upgrade");
        Console.WriteLine($"PASS: {checks} commerce, gear, bait, capacity, transfer, weight scaling and save checks.");
    }
}

