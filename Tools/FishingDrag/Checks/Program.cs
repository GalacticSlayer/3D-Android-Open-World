using System;
using System.Reflection;
using UnityEngine;
class Program {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static void Call(object o,string n)=>o.GetType().GetMethod(n,F).Invoke(o,null);
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(){
  var f=new FishingSystem();var h=new FishingHUD();var r=new FishingBurstDamageRuntime();
  Set(r,"fishing",f);Set(r,"hud",h);Set(r,"progress",new ShopProgress());
  void Tick(float dt){Time.deltaTime=dt;Call(r,"Update");}
  Tick(0);Check(r.SelectedDrag==1,"Default medium");h.ActionInput.IsHeld=true;Tick(.35f);Check(h.Damage==3,"Medium damage");
  r.SelectDrag(0);int before=h.Damage;Tick(.35f);Tick(.35f);Check(h.Damage-before==3,"Low exact half over odd rolls");
  r.SelectDrag(2);before=h.Damage;Tick(.35f);Check(h.Damage-before==6,"High double reel damage");
  h.ActionInput.IsHeld=false;before=h.Damage;Tick(.65f);Check(h.Damage-before==3,"High passive rod tick");
  for(int i=0;i<120;i++)Tick(.1f);
  Check(h.Charge>=.999f,"High fills meter");Tick(.1f);
  Check(!r.TrySkillLook(new Vector2(0,0)),"No stationary activation");
  ShopWorldHUD.MenuOpen=true;Check(!r.TrySkillLook(new Vector2(400,0)),"Reject menu swipe");before=h.Damage;Tick(1);Check(h.Damage==before,"No passive damage in covered menu");ShopWorldHUD.MenuOpen=false;
  r.SelectDrag(0);Check(r.TrySkillLook(new Vector2(0,.1f)),"Tiny upward look activates full meter");Check(!r.TrySkillLook(new Vector2(400,0)),"One spend only");
  before=h.Damage;Tick(0);Check(h.Damage-before==60 && h.Charge==0,"20x base rod strike, no Low multiplier");
  Check(h.Popups[^1].Amount==60 && h.Popups[^1].Label.fontSize==76 && h.Popups[^1].Label.color.g==.62f,
   "Charged skill popup has 2x orange text size without changing damage");
  Check(f.Stuns==1 && FishingDragRules.SkillStunSeconds==3f,"Surviving skill hit applies three-second stun");
  Set(r,"skillCooldown",0f);Set(r,"skillCharge",1f);Set(r,"authoritativeHp",5);Check(r.TrySkillLook(new Vector2(400,0)),"Finisher queued");Tick(0);
  Check(Get<int>(f,"fishHealthPoints")==0 && Get<bool>(f,"fishUnconscious") && f.Presentations>0,"Skill KO uses existing presentation");
  Check(f.SkillFeedbacks==2,"Skill feedback once for both surviving and lethal hits");
  Check(f.Stuns==1,"Lethal skill does not replace KO with temporary stun");
  Check(!r.TrySkillLook(new Vector2(400,0)),"No strikes on unconscious fish");
  Set(f,"state","Idle");Tick(0);Check(!h.Fighting && r.SelectedDrag==1 && Get<float>(r,"skillCharge")==0,"Fight cleanup");
  Set(f,"state","Fighting");Tick(0);Check(r.SelectedDrag==1 && Get<float>(r,"skillCharge")==0,"Fresh fight");
  Check(FishingDragRules.EscapeMultiplier(0)==2f && FishingDragRules.TensionMultiplier(0)==.5f,"Low escape/tension");
  Check(FishingDragRules.EscapeMultiplier(1)==1 && FishingDragRules.DamageMultiplier(1)==1 && FishingDragRules.TensionMultiplier(1)==1,"Medium unchanged");
  Check(FishingDragRules.ReelingTensionMultiplier(2)==1f && FishingDragRules.ReelingTensionMultiplier(1)==1f && FishingDragRules.ReelingTensionMultiplier(0)==.5f,"Reeling tension multipliers");
  Check(FishingDragRules.TensionMultiplier(2)==1f && FishingDragRules.HighTensionPerSecond==FishingDragRules.FragileTensionPerSecond*.5f,"High passive tension is half the purple ramp");
  Check(FishingDragRules.Charge(0,2,5)==1f && FishingDragRules.Charge(0,2,4)<1f,"Five-second charge");
  Check(FishingDragRules.RecoveryMultiplier(0)==1.3f && FishingDragRules.RecoveryMultiplier(1)==1f,"Low recovery");
  float danger=FishingDragRules.FragileTimer(0,true,1,1.99f);
  Check(danger<FishingDragRules.FragileWarningSeconds,"Purple grace period");
  Check(FishingDragRules.FragileTensionGain(0,2f)==0f,"No timer snap or extra tension at two seconds");
  Check(Math.Abs(FishingDragRules.FragileTensionGain(1.9f,2.1f)-.045f)<.0001f,"Only post-grace frame time adds tension");
  Check(Math.Abs(FishingDragRules.FragileTensionGain(2f,3f)-.45f)<.0001f,"Purple adds 45 tension points per second");
  Check(FishingDragRules.FragileTensionGain(3f,0f)==0f,"Low drag immediately stops extra tension");
  float integrated=0f;for(int i=0;i<300;i++)integrated+=FishingDragRules.FragileTensionGain(i*.01f,(i+1)*.01f);
  Check(Math.Abs(integrated-FishingDragRules.FragileTensionGain(0,3f))<.001f,"Frame-independent tension ramp");
  Check(FishingDragRules.FragileTimer(danger,true,0,.1f)==0 && FishingDragRules.FragileTimer(danger,false,2,.1f)==0,"Low or mood exit clears countdown");
  Check(FishingDragRules.FragileWarningDuration(false)==2f && FishingDragRules.SkillStunDuration(false)==3f,"Other zones retain default timing");
  float windgroveGrace=FishingDragRules.FragileWarningDuration(true);
  Check(windgroveGrace==1f && FishingDragRules.SkillStunDuration(true)==3f,"Windgrove keeps one-second warning and restores three-second skill stun");
  Check(FishingDragRules.FragileTensionGain(0,windgroveGrace,windgroveGrace)==0f,"Windgrove no extra tension before one second");
  Check(Math.Abs(FishingDragRules.FragileTensionGain(.9f,1.1f,windgroveGrace)-.045f)<.0001f,"Windgrove counts only post-grace frame time");
  Check(Math.Abs(FishingDragRules.FragileTensionGain(1f,2f,windgroveGrace)-.45f)<.0001f,"Windgrove retains purple tension rate");
  Check(FishingDragRules.FragileTensionGain(2f,0f,windgroveGrace)==0f,"Windgrove Low drag stops extra tension");
  integrated=0f;for(int i=0;i<300;i++)integrated+=FishingDragRules.FragileTensionGain(i*.01f,(i+1)*.01f,windgroveGrace);
  Check(Math.Abs(integrated-FishingDragRules.FragileTensionGain(0,3f,windgroveGrace))<.001f,"Windgrove frame-independent tension ramp");
  foreach(var d in new[]{new Vector2(.1f,0),new Vector2(-.1f,0),new Vector2(0,.1f),new Vector2(0,-.1f)})
  {
   Set(r,"skillCooldown",0f);Set(r,"skillCharge",1f);Check(r.TrySkillLook(d),"Any direction activates");Tick(0);
  }
  CheckPopupStyles();
  Console.WriteLine("PASS: live burst and popup regressions, including same-frame passive and gem crit hits, 2x charged-skill text, shield color, and reused labels.");
 }

 static void CheckPopupStyles(){
  var f=new FishingSystem();var h=new FishingHUD();var r=new FishingBurstDamageRuntime();var p=new ShopProgress();
  Set(r,"fishing",f);Set(r,"hud",h);Set(r,"progress",p);
  p.Data.rodEquipped=6; // Level 7, 15% base crit
  p.Data.gems.items.Add(new AbyssalGem{id="test",attributes=new[]{0,1,5,6},values=new[]{1f,1f,5f,1f}});
  p.Data.gems.Equip("test");
  Time.deltaTime=0;Call(r,"Update");
  r.SelectDrag(FishingDragRules.High);
  Set(r,"passiveSeconds",.9f);h.ActionInput.IsHeld=true;
  UnityEngine.Random.value=.17f; // Above rod-only crit, below the combined 20%.
  Time.deltaTime=.35f;Call(r,"Update");
  Check(h.Popups.Count==2,"Passive damage and a reel crit occur in one Update");
  var normal=h.Popups[0];var crit=h.Popups[1];
  Check(normal.Amount==58 && normal.Label.fontSize==32 && normal.Label.color.g==.85f,"Passive hit keeps normal styling");
  Check(crit.Amount==232 && crit.Label.fontSize==38 && crit.Label.color.g==.62f && crit.Label.color.b==.24f,"Only the gem-assisted reel crit is styled orange");

  f.Shielded=true;r.SelectDrag(FishingDragRules.Medium);Call(r,"Update");
  var shielded=h.Popups[^1];
  Check(shielded.Amount==23 && shielded.Label.fontSize==32 && shielded.Label.color.r==shielded.Label.color.g &&
   shielded.Label.color.g==shielded.Label.color.b,"Shielded crit remains gray and displays the mitigated amount");
  h.ActionInput.IsHeld=false;Set(r,"skillCharge",1f);
  Check(r.TrySkillLook(new Vector2(1,0)),"Shielded skill queued");
  Time.deltaTime=0;Call(r,"Update");
  var skill=h.Popups[^1];
  Check(skill.Amount==232 && skill.Label.fontSize==64 && skill.Label.color.r==skill.Label.color.g &&
   skill.Label.color.g==skill.Label.color.b && skill.Label.rectTransform.sizeDelta.x==360,"Shielded skill is gray and twice its old size");
  UnityEngine.Random.value=.5f;

  var label=skill.Label;
  FishingDamagePresentation.ConfigureDamageLabel(label,lightning:true,hardened:false,critical:false,skill:false);
  Check(label.fontSize==34 && label.color.b==.02f && label.fontStyle==FontStyle.Italic,"Lightning keeps its existing size, color and font style");
  FishingDamagePresentation.ConfigureDamageLabel(label,lightning:false,hardened:false,critical:false,skill:false);
  Check(label.fontSize==32 && label.color.g==.85f && label.fontStyle==FontStyle.Normal &&
   label.rectTransform.sizeDelta.x==180,"Reused label resets font, color, and dimensions");
 }
}
