using System;
using System.Collections.Generic;
namespace UnityEngine {
 public enum FindObjectsSortMode {None}
 public enum RuntimeInitializeLoadType {AfterSceneLoad}
 public class RuntimeInitializeOnLoadMethod:Attribute {public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType mode){}}
 public class Object {public static T FindFirstObjectByType<T>() where T:class=>null;public static T[] FindObjectsByType<T>(FindObjectsSortMode mode)=>Array.Empty<T>();}
 public class MonoBehaviour:Object {public bool enabled=true;public GameObject gameObject=new GameObject();public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();}
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int n){}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public float magnitude=>MathF.Sqrt(x*x+y*y);public static Vector2 operator*(Vector2 v,float k)=>new Vector2(v.x*k,v.y*k);}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static float Distance(Vector3 a,Vector3 b)=>MathF.Sqrt(MathF.Pow(a.x-b.x,2)+MathF.Pow(a.y-b.y,2)+MathF.Pow(a.z-b.z,2));}
 public class Transform {public Vector3 position;public Vector3 TransformPoint(Vector3 p)=>p;}
 public class GameObject {public Transform transform=new Transform();private readonly Dictionary<Type,object> components=new();public T GetComponent<T>() where T:class=>components.GetValueOrDefault(typeof(T)) as T;public T AddComponent<T>() where T:new(){var c=new T();components[typeof(T)]=c;if(c is MonoBehaviour mb)mb.gameObject=this;return c;}}
 public class LineRenderer:MonoBehaviour {public int positionCount=2; public bool useWorldSpace=true;public float Length;public Transform transform=>gameObject.transform;public Vector3 GetPosition(int i)=>new(i==0?0:Length,0,0);}
 public class Camera {public Vector3 WorldToScreenPoint(Vector3 p)=>p;}
 public static class Screen {public static int width=1920,height=1080;}
 public static class Time {public static float deltaTime;}
 public static class Random {public static float value=.5f;}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public enum FontStyle {Normal,Italic}
 public class Font {}
 public class RectTransform {public Vector2 sizeDelta;}
 public static class Resources {public static T GetBuiltinResource<T>(string path) where T:new()=>new T();public static T Load<T>(string path) where T:new()=>new T();}
 public static class Debug {public static void LogError(object o)=>throw new Exception(o.ToString());}
 public static class Mathf {
  public static float Clamp01(float v)=>Math.Clamp(v,0,1);public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
  public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
  public static int RoundToInt(float x)=>(int)MathF.Round(x);
  public static int FloorToInt(float x)=>(int)MathF.Floor(x);
 }
}
namespace UnityEngine.UI {
 public class Text:MonoBehaviour {public Font font;public FontStyle fontStyle;public bool resizeTextForBestFit;public Color color;public int fontSize;public RectTransform rectTransform=new();}
 public class Outline {public Color effectColor;public Vector2 effectDistance;public bool useGraphicAlpha;}
}
public class FishingSystem:UnityEngine.MonoBehaviour {
 private bool rodEquipped=true;
 private int activeBait;
 private readonly UnityEngine.LineRenderer fishingLine=new();
 public float LineDistance {get=>fishingLine.Length;set=>fishingLine.Length=value;}
 public float EquippedLineLimit=40f;
 public float? CapturedKnockoutDistance {get;private set;}
 public float EffectiveLineDistanceLimit=>state=="Fighting" && fishUnconscious && CapturedKnockoutDistance.HasValue
  ? FishingDragRules.ProtectedKnockoutLineLimit(EquippedLineLimit,CapturedKnockoutDistance.Value):EquippedLineLimit;
 public void CaptureProtectedKnockoutDistance(){CapturedKnockoutDistance??=LineDistance;}
 public int Snaps;private void SnapLine(string message){Snaps++;state="Idle";}
 public int Retractions;private void FailFishing(string message){Retractions++;state="Idle";}
 public bool PendingLegacyDamageCritical {get;set;}
 public AnglerProgression Progression=new AnglerProgression();
 private string state="Fighting";private int fishHealthPoints=1000,fishMaxHealth=1000,pendingDamage;
 private float fishHealth=1,damageFraction,damageDisplayTimer;private bool fishUnconscious=false;
 private UnityEngine.GameObject bobber;private UnityEngine.Camera playerCamera;
 public int SkillFeedbacks;public void PlaySkillFeedback(){SkillFeedbacks++;}
 public bool GemAngryState;public bool IsBossFight=>false;public int CurrentFishHealth=>fishHealthPoints;
 public void RefreshHardenedScalesStatus(){}public void PlaySirensCallSound(){}
 public bool Shielded;
 public int MitigateBossDamage(int damage,out bool hardened){hardened=Shielded;return hardened?Math.Max(1,damage/5):damage;}
 public int Stuns;public void ApplySkillStun(){Stuns++;}
 public int Presentations;private void EnsureUnconsciousFishVisual(){Presentations++;}
}
public class FishingHUD {public class Input {public bool IsHeld;}public Input ActionInput=new Input();public bool CombatInputVisible=true,FishingUiVisible=true;public int Damage;public float Charge;public bool Fighting,Live;
 public readonly List<(int Amount, UnityEngine.UI.Text Label)> Popups=new();
 public void ShowDamage(int n,UnityEngine.Vector3 p,bool lightning=false,bool hardened=false,bool critical=false,bool skill=false){
  Damage+=n;
  var label=new UnityEngine.UI.Text();
  FishingDamagePresentation.ConfigureDamageLabel(label,lightning,hardened,critical,skill);
  Popups.Add((n,label));
 }
 public void SetDragFightUI(bool fighting,bool live,int mode,float charge,float cooldown=0f){Fighting=fighting;Live=live;Charge=charge;}}
public class ShopProgress {public class Ledger {public AbyssalGemCollection gems=new AbyssalGemCollection();public int rodEquipped;}public Ledger Data=new Ledger();}
public class Level2FishingRodRuntime:UnityEngine.MonoBehaviour {}
public static class ShopCatalog {public const int MaxRodTier=8,StarterLure=0;}
public static class ShopWorldHUD {public static bool MenuOpen;}


public class FishingBurnVisuals:UnityEngine.MonoBehaviour {}

public class AbyssalLeviathanEncounter:UnityEngine.MonoBehaviour {public void ReportDamage(int damage){}}
public static class FishingThirdPersonView {public static UnityEngine.Vector3 WorldToScreenPoint(UnityEngine.Camera camera,UnityEngine.Vector3 point)=>camera.WorldToScreenPoint(point);}
