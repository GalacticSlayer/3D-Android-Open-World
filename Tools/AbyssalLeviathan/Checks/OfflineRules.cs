using System;

internal static class OfflineRules
{
    private static int checks;
    private static void Check(bool condition,string name)
    {if(!condition)throw new Exception(name);checks++;}
    public static void Main()
    {
        Check(FishingNetworkCompatibility.RodTier(5,false,4)==0,"unequipped Level 6 never sends rejected tier");
        Check(FishingNetworkCompatibility.RodTier(5,true,4)==4,"Level 6 presentation works on legacy server");
        Check(FishingNetworkCompatibility.RodTier(5,true,5)==5,"new server preserves Level 6 model");
        Check(FishingNetworkCompatibility.RodTier(-1,true,4)==0,"invalid saved tier cannot break opening");
        Check(FishingNetworkCompatibility.AdvertisedMaximum(0,5)==4,"old server without capability defaults safely");
        Check(FishingNetworkCompatibility.AdvertisedMaximum(5,5)==5,"new capability unlocks Level 6 replication");
        Check(FishingNetworkCompatibility.AdvertisedMaximum(99,5)==5,"server cannot exceed client catalog");

        var b=new AbyssalLeviathanLocalState(100,.5f);
        Check(b.Health==150000 && b.Weight==420 && b.SecondsLeft(100)==900,"offline summon stats");
        Check(!b.TryHook(1,.5f,100),"50 percent boundary is a miss");
        Check(!b.TryHook(1,.1f,100),"miss cannot reroll same cast");
        Check(b.TryHook(2,.499f,101),"roll below 50 percent hooks");
        string lease=b.Lease;
        Check(!b.TryHook(3,0,102),"cannot double hook");
        Check(!b.Damage("wrong",30000,102),"wrong lease cannot damage");
        Check(b.Damage(lease,30000,102) && b.Health==120000,"damage applied");
        Check(b.Damage(lease,30000,103) && b.Health==120000,"duplicate damage idempotent");
        b.Tick(200);Check(b.Health==120000,"no healing while hooked");
        Check(!b.Catch(lease,30000,200),"cannot land healthy boss");
        Check(b.Release(lease,30000,200),"release retains damage");
        b.Tick(204.999);Check(b.Health==120000,"five second heal grace");
        b.Tick(205);Check(b.Health==120000,"no heal at boundary");
        b.Tick(265);Check(b.Health==130000,"heals exactly 10000 per minute");
        Check(b.TryHook(4,0,265) && b.Health==130000,"rehook uses remaining HP");
        lease=b.Lease;Check(b.Damage(lease,130000,266) && b.Health==0,"zero HP before catch");
        Check(b.Catch(lease,130000,267) && b.Phase=="caught","offline catch completes");
        Check(!b.Catch(lease,130000,268),"catch cannot duplicate");
        var half=new AbyssalLeviathanLocalState(0,.5f);
        Check(half.TryHook(1,0,1),"first half-health cast hooks");
        string firstLease=half.Lease;
        Check(half.Damage(firstLease,75000,2) && half.Health==75000,"boss reaches half of 150000 HP");
        Check(half.Release(firstLease,75000,2),"damage persists when fish is lost");
        Check(half.TryHook(2,0,3) && half.Health==75000,"next hook begins at 75000 HP");
        Check(half.Damage(half.Lease,75000,4) && half.Health==0,"rehooked boss can be subdued");
        Check(half.Catch(half.Lease,75000,5) && half.Phase=="caught","subdued boss can be landed normally");
        var expiry=new AbyssalLeviathanLocalState(0,0);
        expiry.TryHook(1,0,1);lease=expiry.Lease;
        expiry.Tick(900);Check(expiry.Phase=="expired" && expiry.Lease=="","expiry releases hooked boss");
        Check(!expiry.Catch(lease,150000,901),"no catch after deadline");
        var cap=new AbyssalLeviathanLocalState(0,1);
        Check(cap.Weight==440,"maximum weight");
        cap.TryHook(1,0,0);cap.Release(cap.Lease,1000,0);cap.Tick(100);
        Check(cap.Health==150000,"healing capped");
        Check(new AbyssalLeviathanLocalState(0,0).Weight==400,"minimum weight");
        Console.WriteLine("PASS "+checks+" offline encounter and network compatibility assertions");
    }
}
