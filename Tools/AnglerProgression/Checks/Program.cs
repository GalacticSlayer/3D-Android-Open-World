using System;
using System.Reflection;
using UnityEngine;
class Program
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main()
 {
  var p=new AnglerProgression();
  Check(p.level==1 && p.Value(0)==0,"Fresh save");
  Check(p.AddXp(249,n=>0)==0 && p.experience==249,"XP threshold");
  Check(p.AddXp(1,n=>0)==1 && p.level==2 && p.stats[0]==1,"First point");
  var random=new System.Random(42);p.AddXp(int.MaxValue,n=>random.Next(n));
  Check(p.level==601 && p.experience==0,"Maximum level and overflow");
  foreach(int stat in p.stats)Check(stat==100,"Every stat capped without wasted rolls");
  for(int l=1;l<601;l++)Check(AnglerProgression.RequiredXp(l+1)>AnglerProgression.RequiredXp(l),"Increasing XP curve");
  // Verify the full curve against cumulative five-level steps.
  double BaseXp(int level)=>Math.Ceiling(250d*Math.Pow(1.015d,level-1));
  int surcharge=0;
  for(int l=1;l<=600;l++)
  {
    if(l%5==0)surcharge+=l<=100 ? 150 : l<=200 ? 450 : 1500;
    Check(AnglerProgression.RequiredXp(l)==BaseXp(l)+surcharge,"Correct cumulative XP surcharge at level "+l);
  }
  foreach(int l in new[]{5,100,105,110,200,205,210})
  {
    // Existing save XP is unchanged and the new thresholds apply on level-up.
    var profile=new AnglerProgression{level=l,experience=120,abilityChoicesMigrated=true};
    profile.Normalize();
    Check(profile.level==l && profile.experience==120,"Existing save XP preserved at level "+l);
    int remaining=(int)AnglerProgression.RequiredXp(l)-120;
    Check(profile.AddXp(remaining-1,n=>0)==0 && profile.level==l,"Bracket threshold not bypassed at level "+l);
    Check(profile.AddXp(2,n=>0)==1 && profile.level==l+1 && profile.experience==1,
      "Bracket threshold met with overflow XP carried at level "+l);
  }
  Check(AnglerProgression.CatchXp(400,.5f)>AnglerProgression.CatchXp(100,.5f),"HP affects XP");
  Check(AnglerProgression.CatchXp(100,1)>AnglerProgression.CatchXp(100,.2f),"Difficulty affects XP");
  // New profiles choose where to spend one upgrade point per 20 levels.
  p=new AnglerProgression();p.Normalize();
  p.level=19;Check(p.AvailableAbilityPoints==0,"No early Special Ability upgrades");
  p.level=20;Check(p.AvailableAbilityPoints==1 && !p.Unlocked(0),"Level 20 earns unspent choice");
  Check(p.UpgradeAbility(2) && p.Unlocked(2) && p.AbilityRank(2)==1 && p.AvailableAbilityPoints==0,"Choose any ability at 20");
  Check(!p.UpgradeAbility(1),"Cannot spend nonexistent upgrade");
  p.level=40;Check(p.UpgradeAbility(2) && p.AbilityRank(2)==2 && p.ImmortalityDuration==6f,"Ranks strengthen abilities");
  Check(p.AvailableAbilityPoints==0,"Second upgrade spends second point");
  // A pre-upgrade-era save owns the same four abilities it had already unlocked.
  var legacy=new AnglerProgression{level=80};Check(legacy.Normalize(),"Old ability save migration");
  for(int i=0;i<4;i++)Check(legacy.Unlocked(i) && legacy.AbilityRank(i)==1,"Legacy unlock retained");
  Check(legacy.AvailableAbilityPoints==0 && !legacy.Normalize(),"Legacy migration only once");
  legacy.level=100;Check(legacy.AvailableAbilityPoints==1,"Further milestones give a new choice");
  Check(legacy.UpgradeAbility(0) && legacy.BurningTotalFraction>.05f,"Repeat upgrade strengthens fire");
  p=new AnglerProgression{level=0,stats=null,experience=double.NaN};p.Normalize();Check(p.level==1 && p.stats.Length==6 && p.experience==0,"Old save migration");
  var f=new FishingSystem();var hud=new FishingHUD();var owner=new FishingBurstDamageRuntime();
  Set(owner,"fishing",f);Set(owner,"hud",hud);Set(owner,"progress",new ShopProgress());
  void Tick(float dt){Time.deltaTime=dt;typeof(FishingBurstDamageRuntime).GetMethod("Update",F).Invoke(owner,null);}
  Tick(0);Check(!owner.CanUseAbility(0),"Locked ability rejected");
  f.Progression.level=80;
  f.Progression.Normalize(); // Exercise the old save's four granted abilities.
  Set(f,"state","Idle");Tick(0);Set(f,"state","Fighting");Tick(0);
  Check(Get<int>(f,"fishHealthPoints")==900,"Hookset once after max HP known");Tick(0);Check(Get<int>(f,"fishHealthPoints")==900,"No repeated hookset");
  Set(owner,"skillQueued",true);Tick(0);
  int afterSkill=Get<int>(f,"fishHealthPoints");
  Check(owner.BurningSeconds==4f && Get<bool[]>(owner,"abilityUsed")[0],"Charged Skill automatically starts Burning Fish");
  Check(!owner.CanUseAbility(0),"Burning Fish is not a manual button");
  Check(owner.TryTakeLightningZap(out int zap) && zap==0,"First zap event is immediate");
  Tick(.25f);Check(Get<int>(f,"fishHealthPoints")==afterSkill,"Burn ticks in authoritative HP");
  ShopWorldHUD.MenuOpen=true;Tick(5);Check(Get<int>(f,"fishHealthPoints")==afterSkill,"Menu pauses burn");ShopWorldHUD.MenuOpen=false;
  Tick(3.75f);Check(Get<int>(f,"fishHealthPoints")==afterSkill-25,"Burn total 5 percent");Tick(1);Check(Get<int>(f,"fishHealthPoints")==afterSkill-25,"Burn ends");
  for(int i=1;i<8;i++)Check(owner.TryTakeLightningZap(out zap) && zap==i,"Exactly ordered later zaps");
  Check(!owner.TryTakeLightningZap(out zap),"No ninth zap");
  Set(owner,"skillQueued",true);Tick(0);
  Check(owner.TryTakeLightningZap(out zap) && zap==0 && owner.BurningSeconds==4f,"Same fight skill retriggers lightning");
  owner.UseAbility(2);Check(owner.ImmortalitySeconds==0 && !owner.CanUseAbility(2),"Line immortality cannot be clicked");
  Check(owner.TryTriggerLineImmortality() && owner.LineImmortalityActive && owner.ImmortalitySeconds==5,
      "First line-limit hit automatically triggers five seconds of immunity");
  Check(!owner.TryTriggerLineImmortality(),"Line immortality cannot retrigger in the same fight");
  owner.UseAbility(3);Check(owner.SirenSeconds==4 && f.SirenSoundPlays==1,"Siren's Call lasts four seconds and sounds on click");
  owner.UseAbility(3);Check(f.SirenSoundPlays==1,"Siren activation audio plays only once per fight");
  Tick(2);Check(owner.ImmortalitySeconds==3 && owner.SirenSeconds==2,"Timed abilities count down");
  Tick(3);Check(!owner.LineImmortalityActive && !owner.TryTriggerLineImmortality(),"Immortality ends and cannot retrigger after expiry");
  Set(f,"state","Idle");Tick(0);Check(owner.ImmortalitySeconds==0 && owner.SirenSeconds==0,"Cancel clears timers");
  Set(f,"state","Fighting");Tick(0);Check(!Get<bool[]>(owner,"abilityUsed")[0],"Fresh fight resets automatic burn");
  f.Progression.stats[0]=10;f.Progression.stats[1]=100;hud.ActionInput.IsHeld=true;
  int hp=Get<int>(f,"fishHealthPoints");Tick(.35f);Check(hp-Get<int>(f,"fishHealthPoints")==26,"Raw then percent burst damage");
  hud.ActionInput.IsHeld=false;Set(owner,"authoritativeHp",5);
  typeof(FishingBurstDamageRuntime).GetMethod("StartBurn",F).Invoke(owner,null);Tick(.5f);
  Check(Get<int>(f,"fishHealthPoints")==0 && Get<bool>(f,"fishUnconscious") && f.Presentations>0,"Burn KO presentation");
  Check(!owner.CanUseAbility(2),"No ability on unconscious fish");
  Set(f,"state","Idle");Tick(0);Set(f,"state","Fighting");Tick(0);
  typeof(FishingBurstDamageRuntime).GetMethod("StartBurn",F).Invoke(owner,null);
  for(int i=0;i<241;i++)Tick(1f/60f);
  Check(Get<int>(f,"fishHealthPoints")==850,"Lightning completes eight zaps at 60 FPS");
  Set(f,"fishMaxHealth",100000);
  typeof(FishingBurstDamageRuntime).GetMethod("StartBurn",F).Invoke(owner,null);
  Check(Get<int>(owner,"burnTotalDamage")==2000,"Burn never exceeds 2000 total damage");
  Set(f,"state","Idle");Tick(0);Set(f,"state","Fighting");Tick(0);
  Set(owner,"authoritativeHp",100000);
  typeof(FishingBurstDamageRuntime).GetMethod("StartBurn",F).Invoke(owner,null);
  Check(Get<int>(owner,"authoritativeHp")==99000,"Capped initial zap = 1000");
  int[] expectedHp={98857,98714,98571,98429,98286,98143,98000};
  foreach(int expected in expectedHp){Tick(4f/7f);Check(Get<int>(owner,"authoritativeHp")==expected,"Seven later zaps evenly share the other 1000");}
  // Two overlapping activations retain their complete independent damage budgets.
  Set(f,"state","Idle");Tick(0);Set(f,"state","Fighting");Tick(0);
  Set(owner,"authoritativeHp",100000);
  typeof(FishingBurstDamageRuntime).GetMethod("StartBurn",F).Invoke(owner,null);
  Check(Get<int>(owner,"authoritativeHp")==99000,"First zap pays half of capped total immediately");
  Tick(1);
  typeof(FishingBurstDamageRuntime).GetMethod("StartBurn",F).Invoke(owner,null);
  Check(Get<int>(owner,"authoritativeHp")==97857,"Overlapping first zap pays its own half");
  Tick(3);Tick(1);
  Check(Get<int>(owner,"authoritativeHp")==96000,"Both activations pay full capped total");
  int events=0;while(owner.TryTakeLightningZap(out zap))events++;
  Check(events==16,"Two activations have exactly sixteen zap events");
  Console.WriteLine("PASS: progression, caps, save migration, chosen ability ranks, XP curve, unlocks, authoritative damage, eight-zap damage split, repeated skills, overlap, cap, KO, menu pause and fight reset.");
 }
}
