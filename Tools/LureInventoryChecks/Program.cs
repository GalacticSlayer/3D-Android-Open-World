using System;

// Compile with the real ShopLedger.cs and ShopCatalog.cs. Only unrelated fish
// sizing/progression dependencies are stubbed; lure behavior is production code.
[Serializable] public sealed class AnglerProgression { }
[Serializable] public sealed class CaughtFishRecord { public int speciesId; public float weightKg; }
public static class FishCatalog { public const int Count=100; public static int CanonicalId(int id)=>id; }
public static class FishSizeTable { public static float LengthMetres(int id,float kg)=>1f; }
internal static class Program
{
    private static int checks;
    private static void Check(bool condition,string message)
    { checks++; if(!condition)throw new Exception(message); }
    private static void Main()
    {
        int[] prices={650,400,400,200,650};
        var d=new ShopLedger { coins=10000,lureOwnedMask=31,baitEquipped=4,lureEquipped=3 };
        Check(d.EnsureLures(),"Legacy migration did not run");
        for(int v=0;v<5;v++)
        {
            Check(d.LureQuantity(v)==1,"Legacy unlock was not preserved");
            Check(ShopCatalog.LurePrices[v]==prices[v],"Wrong price");
            int coins=d.coins;
            int previousBait=d.baitEquipped,previousLure=d.lureEquipped;
            Check(d.BuyLure(v) && d.BuyLure(v),"Repeat purchase failed");
            Check(d.LureQuantity(v)==3 && d.coins==coins-2*prices[v],"Wrong purchase count/cost");
            Check(d.baitEquipped==previousBait && d.lureEquipped==previousLure,"Stock purchase changed equipment");
            Check(d.EquipLure(v),"Could not equip purchased lure");
            Check(d.ConsumeBait()==4 && d.LureQuantity(v)==3,"Cast consumed a lure");
            Check(!d.ResolveLureFight(false) && d.LureQuantity(v)==3,"No-bite retrieval lost a lure");
            Check(d.BeginLureFight(v) && !d.BeginLureFight(v),"Duplicate fight was allowed");
            Check(d.ResolveLureFight(true) && d.LureQuantity(v)==3,"Catch consumed a lure");
            Check(d.BeginLureFight(v) && d.ResolveLureFight(false) && d.LureQuantity(v)==2,"Escape did not consume one");
            Check(!d.ResolveLureFight(false) && d.LureQuantity(v)==2,"Repeated cleanup consumed two");
        }
        d.EquipLure(3);d.BeginLureFight(3);d.EquipLure(2);d.ResolveLureFight(false);
        Check(d.LureQuantity(3)==1 && d.LureQuantity(2)==2,"Changed equipment lost the wrong lure");
        d.EquipLure(3);d.BeginLureFight(3);d.ResolveLureFight(false);
        Check(!d.OwnsLure(3) && d.baitEquipped==0 && !d.EquipLure(3),"Exhausted lure remained usable");
        d.EnsureLures();Check(d.LureQuantity(3)==0,"Normalization resurrected a lure");
        d.EquipLure(0);d.BeginLureFight(0);d.ResolveLureFight(false);d.BeginLureFight(0);d.ResolveLureFight(false);d.EnsureLures();
        Check(d.LureQuantity(0)==0 && d.baitEquipped==0,"Starter lure regenerated");
        d.coins=650;Check(d.BuyLure(0) && d.coins==0 && d.LureQuantity(0)==1,"Starter lure could not be repurchased");
        Check(!d.BuyLure(0) && d.LureQuantity(0)==1,"Unaffordable purchase succeeded");
        Check(!d.BuyLure(-1) && !d.BuyLure(5),"Invalid lure accepted");
        d.EquipLure(0);d.BeginLureFight(0);
        // Simulate the persisted fields restored by Unity, then Awake's recovery.
        var restored=new ShopLedger { lureQuantityVersion=d.lureQuantityVersion,lureQuantities=(int[])d.lureQuantities.Clone(),lureFightPending=d.lureFightPending,fightLureVariant=d.fightLureVariant,baitEquipped=4 };
        restored.EnsureLures();restored.ResolveLureFight(false);restored.EnsureLures();
        Check(restored.LureQuantity(0)==0 && !restored.ResolveLureFight(false),"Interrupted fight did not settle exactly once");
        d.coins=90;Check(d.BuyBait(2) && d.bait[2]==10,"Shrimp pack changed");
        Check(d.ConsumeBait()==2 && d.bait[2]==9,"Bait consumption changed");
        d.baitEquipped=0;Check(d.ConsumeBait()==0,"Worm fallback changed");
        Console.WriteLine("Passed "+checks+" lure inventory checks.");
    }
}
