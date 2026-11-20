using System;
namespace UnityEngine {
 public class Object {public static T FindFirstObjectByType<T>() where T:class=>null;}
 public class MonoBehaviour:Object {public bool enabled=true;public GameObject gameObject=new GameObject();public T GetComponent<T>() where T:class=>null;}
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int n){}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public float magnitude=>MathF.Sqrt(x*x+y*y);}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public class Transform {public Vector3 position;}
 public class GameObject {public Transform transform=new Transform();public T GetComponent<T>() where T:class=>null;public T AddComponent<T>() where T:new()=>new T();}
 public class Camera {public Vector3 WorldToScreenPoint(Vector3 p)=>p;}
 public static class Screen {public static int width=1920,height=1080;}
 public static class Time {public static float deltaTime;}
 public static class Random {public static float value=>.5f;}
 public static class Debug {public static void LogError(object o)=>throw new Exception(o.ToString());}
 public static class Mathf {
  public static float Clamp01(float v)=>Math.Clamp(v,0,1);public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
  public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
  public static int RoundToInt(float x)=>(int)MathF.Round(x);
  public static int FloorToInt(float x)=>(int)MathF.Floor(x);
 }
}
public class FishingSystem:UnityEngine.MonoBehaviour {
 public void CaptureProtectedKnockoutDistance(){}
 public bool PendingLegacyDamageCritical {get;set;}
 public AnglerProgression Progression=new AnglerProgression();
 private string state="Fighting";private int fishHealthPoints=1000,fishMaxHealth=1000,pendingDamage;
 private float fishHealth=1,damageFraction,damageDisplayTimer;private bool fishUnconscious;
 private UnityEngine.GameObject bobber;private UnityEngine.Camera playerCamera;
 public int SkillFeedbacks;public void PlaySkillFeedback(){SkillFeedbacks++;}
 public bool GemAngryState;public bool IsBossFight=>false;public int CurrentFishHealth=>fishHealthPoints;
 public void RefreshHardenedScalesStatus(){}public void PlaySirensCallSound(){}
 public int MitigateBossDamage(int damage,out bool hardened){hardened=false;return damage;}
 public int Stuns;public void ApplySkillStun(){Stuns++;}
 public int Presentations;private void EnsureUnconsciousFishVisual(){Presentations++;}
}
public class FishingHUD {public class Input {public bool IsHeld;}public Input ActionInput=new Input();public bool CombatInputVisible=true,FishingUiVisible=true;public int Damage;public float Charge;public bool Fighting,Live;
 public void ShowDamage(int n,UnityEngine.Vector3 p,bool lightning=false,bool hardened=false,bool critical=false,bool skill=false){Damage+=n;}
 public void SetDragFightUI(bool fighting,bool live,int mode,float charge,float cooldown=0f){Fighting=fighting;Live=live;Charge=charge;}}
public class ShopProgress {public class Ledger {public AbyssalGemCollection gems=new AbyssalGemCollection();public int rodEquipped;}public Ledger Data=new Ledger();}
public class Level2FishingRodRuntime:UnityEngine.MonoBehaviour {}

public static class ShopWorldHUD {public static bool MenuOpen;}


public class FishingBurnVisuals:UnityEngine.MonoBehaviour {}

public class AbyssalLeviathanEncounter:UnityEngine.MonoBehaviour {public void ReportDamage(int damage){}}
public static class FishingThirdPersonView {public static UnityEngine.Vector3 WorldToScreenPoint(UnityEngine.Camera camera,UnityEngine.Vector3 point)=>camera.WorldToScreenPoint(point);}

public sealed class CaughtFishRecord { public int speciesId; public float weightKg; public long caughtUtcTicks; }
public static class FishCatalog {public const int Count=100; public static int CanonicalId(int id)=>id;}
public static class FishSizeTable {public static float LengthMetres(int id,float kg)=>kg;}
