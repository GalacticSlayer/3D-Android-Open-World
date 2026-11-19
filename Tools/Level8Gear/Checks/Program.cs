using System;
using System.Text.Json;
class Program {
 static int count;
 static void Check(bool b,string msg){count++;if(!b)throw new Exception(msg);}
 static void Main(){
  foreach(GearKind kind in new[]{GearKind.Rod,GearKind.Reel}) {
   int[] prices=kind==GearKind.Rod?new[]{1100000,7800000}:new[]{900000,6800000};
   for(int tier=7;tier<=8;tier++) {
    int price=prices[tier-7];
    int previous=tier-1;
    Check(ShopCatalog.GearPrice(kind,tier)==price,"Catalog price");
    var skip=new ShopLedger{
     rodOwned=kind==GearKind.Rod?previous-1:previous,
     reelOwned=kind==GearKind.Reel?previous-1:previous,
     coins=price
    };
    skip.EnsureGearOwnership();
    Check(!skip.BuyGear(kind,tier)&&skip.coins==price,"Cannot skip the same gear's prerequisite");
    var saved=new ShopLedger{rodOwned=previous,reelOwned=previous,rodEquipped=previous,reelEquipped=previous,coins=price-1};
    saved.EnsureGearOwnership();
    Check(saved.OwnsGear(kind,previous)&&!saved.OwnsGear(kind,tier)&&saved.Equipped(kind)==previous,"Legacy saves preserve gear without granting the new tier");
    Check(!saved.BuyGear(kind,tier)&&saved.coins==price-1,"Unaffordable purchase is atomic");
    saved.coins=price;Check(saved.BuyGear(kind,tier)&&saved.coins==0&&saved.Equipped(kind)==tier,"Exact-price purchase auto-equips");
    Check(!saved.BuyGear(kind,tier),"Duplicate purchase rejected");
    var options=new JsonSerializerOptions{IncludeFields=true};
    var roundtrip=JsonSerializer.Deserialize<ShopLedger>(JsonSerializer.Serialize(saved,options),options);roundtrip.EnsureGearOwnership();
    Check(roundtrip.Equipped(kind)==tier&&roundtrip.OwnsGear(kind,tier)&&roundtrip.OwnsGear(kind,previous),"Save/reload preserves both tiers");
    Check(roundtrip.Equip(kind,0)&&roundtrip.Equip(kind,tier),"Switch away and re-equip");
   }
  }
  Check(ShopCatalog.RodNames[7]=="Level 8 Fishing Rod"&&ShopCatalog.ReelNames[7]=="Level 8 Fishing Reel","Stable appended IDs");
  Check(ShopCatalog.RodNames[8]=="Level 9 Fishing Rod"&&ShopCatalog.ReelNames[8]=="Level 9 Fishing Reel","Level 9 appended below Level 8");
  Check(ShopCatalog.RodTensionControlMultiplier(8)==1.16f&&ShopCatalog.RodTensionControlMultiplier(7)==1.13f&&ShopCatalog.RodTensionControlMultiplier(6)==1.09f,"Tension control");
  Check(ShopCatalog.ReelRetrievalMultiplier(8)==2.6f&&ShopCatalog.ReelRetrievalMultiplier(7)==2.4f,"Retrieval is separately 160% faster");
  Check(ShopCatalog.ReelEscapeMultiplier(8)==.85f,"Level 9 replaces Level 8's slowdown with 15%, not an additional stacked slowdown");
  Check(ShopCatalog.ReelEscapeMultiplier(7)==.9f&&ShopCatalog.ReelEscapeMultiplier(6)==1f,"Escape reduction only on Level 8");
  int[] mins={2,4,6,10,25,35,55,80,115},maxs={4,8,12,20,35,50,75,95,135};
  float[] crit={0,.05f,.08f,.10f,.13f,.13f,.15f,.17f,.19f};
  for(int tier=0;tier<=ShopCatalog.MaxRodTier;tier++) {
   Check(FishingBurstDamageRuntime.NormalMinimumForTier(tier)==mins[tier]&&FishingBurstDamageRuntime.NormalMaximumForTier(tier)==maxs[tier],"Damage ranges");
   Check(FishingBurstDamageRuntime.CriticalChanceForTier(tier)==crit[tier],"Critical chance");
   for(int i=0;i<10000;i++) {
    bool critical;float roll=i/9999f;
    int damage=FishingBurstDamageRuntime.RollBurstForTier(tier,roll,1f,out critical);
    Check(!critical&&damage>=mins[tier]&&damage<=maxs[tier],"Normal roll bounds");
   }
  }
  Check(FishingBurstDamageRuntime.RollBurstForTier(7,0,0,out bool c)==160&&c,"Minimum critical");
  Check(FishingBurstDamageRuntime.RollBurstForTier(7,1,0,out c)==190&&c,"Maximum critical");
  Check(FishingNetworkCompatibility.RodTier(7,true,6)==6&&FishingNetworkCompatibility.RodTier(7,true,7)==7,"Legacy and updated server tiers");
  Check(FishingBurstDamageRuntime.RollBurstForTier(8,0,.1899f,out c)==230&&c,"Level 9 minimum critical");
  Check(FishingBurstDamageRuntime.RollBurstForTier(8,1,0,out c)==270&&c,"Level 9 maximum critical");
  Check(FishingBurstDamageRuntime.RollBurstForTier(8,1,.19f,out c)==135&&!c,"Level 9 exact critical threshold");
  Check(FishingNetworkCompatibility.RodTier(8,true,7)==7&&FishingNetworkCompatibility.RodTier(8,true,8)==8,"Level 8 server fallback and Level 9 support");
  Check(FishingNetworkCompatibility.AdvertisedMaximum(8,7)==7&&FishingNetworkCompatibility.AdvertisedMaximum(0,8)==4,"Legacy client and server compatibility");
  var gemData=new ShopProgress();
  var gem=gemData.Data.gems.Create((min,max)=>min);gemData.Data.gems.Equip(gem.id);
  gem.attributes=new[]{2,3,4,5};gem.values=new[]{20f,10f,40f,20f};gem.level=15;
  var system=new FishingSystem();var runtime=new FishingBurstDamageRuntime();
  const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  typeof(FishingBurstDamageRuntime).GetField("fishing",flags).SetValue(runtime,system);
  typeof(FishingBurstDamageRuntime).GetField("progress",flags).SetValue(runtime,gemData);
  var skill=typeof(FishingBurstDamageRuntime).GetMethod("GemSkillMultiplier",flags);
  system.GemAngryState=false;
  Check(Math.Abs((float)skill.Invoke(runtime,null)-1.1f)<.0001f,"Gem skill bonus while calm");
  system.GemAngryState=true;
  Check(Math.Abs((float)skill.Invoke(runtime,null)-2.42f)<.0001f,"Angry primary stacks with skill bonus");
  var boost=typeof(FishingBurstDamageRuntime).GetMethod("BoostDamage",flags);
  Check((int)boost.Invoke(runtime,new object[]{100,false})==120,"Attack bonus applies once");
  Check((int)boost.Invoke(runtime,new object[]{200,true})==288,"Critical bonus adds to 2x multiplier");
  gemData.Data.gems.Equip(gem.id);
  Check(Math.Abs((float)skill.Invoke(runtime,null)-1f)<.0001f && (int)boost.Invoke(runtime,new object[]{100,false})==100,"Unequipping removes all gem combat bonuses");
  Console.WriteLine($"PASS: {count} checks: Level 8 and 9 purchase, prerequisite, affordability, save/reload, re-equipping, all rod damage ranges, critical boundaries, tension, escape, network compatibility.");
 }
}
