using System;
using System.Text.Json;
class Program {
 static int count;
 static void Check(bool b,string msg){count++;if(!b)throw new Exception(msg);}
 static void Main(){
  foreach(GearKind kind in new[]{GearKind.Rod,GearKind.Reel}) {
   int price=kind==GearKind.Rod?1100000:900000;
   var fresh=new ShopLedger{coins=2000000};fresh.EnsureGearOwnership();
   Check(!fresh.BuyGear(kind,7)&&fresh.coins==2000000,"Cannot skip prerequisite");
   var saved=new ShopLedger{rodOwned=6,reelOwned=6,rodEquipped=6,reelEquipped=6,coins=price-1};saved.EnsureGearOwnership();
   Check(saved.OwnsGear(kind,6)&&!saved.OwnsGear(kind,7),"Legacy save must not grant Level 8");
   Check(!saved.BuyGear(kind,7)&&saved.coins==price-1,"Unaffordable purchase is atomic");
   saved.coins=price;Check(saved.BuyGear(kind,7)&&saved.coins==0&&saved.Equipped(kind)==7,"Exact-price Level 8 purchase");
   Check(!saved.BuyGear(kind,7),"Duplicate purchase rejected");
   var options=new JsonSerializerOptions{IncludeFields=true};
   var roundtrip=JsonSerializer.Deserialize<ShopLedger>(JsonSerializer.Serialize(saved,options),options);roundtrip.EnsureGearOwnership();
   Check(roundtrip.Equipped(kind)==7&&roundtrip.OwnsGear(kind,7),"Save/reload preserves Level 8");
   Check(roundtrip.Equip(kind,0)&&roundtrip.Equip(kind,7),"Re-equip Level 8");
  }
  Check(ShopCatalog.RodNames[7]=="Level 8 Fishing Rod"&&ShopCatalog.ReelNames[7]=="Level 8 Fishing Reel","Stable appended IDs");
  Check(ShopCatalog.RodTensionControlMultiplier(7)==1.13f&&ShopCatalog.RodTensionControlMultiplier(6)==1.09f,"Tension control");
  Check(ShopCatalog.ReelEscapeMultiplier(7)==.9f&&ShopCatalog.ReelEscapeMultiplier(6)==1f,"Escape reduction only on Level 8");
  int[] mins={2,4,6,10,25,35,55,80},maxs={4,8,12,20,35,50,75,95};
  float[] crit={0,.05f,.08f,.10f,.13f,.13f,.15f,.17f};
  for(int tier=0;tier<8;tier++) {
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
  var gemData=new ShopProgress();
  var gem=gemData.Data.gems.Create((min,max)=>min);gemData.Data.gems.Equip(gem.id);
  gem.attributes=new[]{2,3,4,5};gem.values=new[]{20,10,40,20};gem.level=15;
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
  Console.WriteLine($"PASS: {count} checks: Level 8 purchase, prerequisite, affordability, save/reload, re-equipping, all rod damage ranges, critical boundaries, tension, escape, network compatibility.");
 }
}
