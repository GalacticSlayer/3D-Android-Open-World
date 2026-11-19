using System;
using System.Collections.Generic;
using System.Text.Json;
namespace UnityEngine
{
 public class MonoBehaviour { public object component; public T GetComponent<T>() where T:class=>component as T; }
 public class SerializeField:Attribute {}
 public static class Random {static readonly System.Random rng=new System.Random(998); public static int Range(int a,int b)=>rng.Next(a,b);}
 public static class JsonUtility
 {
  public static readonly JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
  public static string ToJson<T>(T value)=>JsonSerializer.Serialize(value,Options);
  public static T FromJson<T>(string value)=>JsonSerializer.Deserialize<T>(value,Options);
 }
 public static class PlayerPrefs
 {
  static readonly Dictionary<string,string> values=new Dictionary<string,string>();
  public static bool HasKey(string key)=>values.ContainsKey(key);
  public static string GetString(string key,string fallback)=>values.TryGetValue(key,out var value)?value:fallback;
  public static void SetString(string key,string value)=>values[key]=value;
  public static void Save(){}
 }
}
public static class FishCatalog {public const int AbyssalLeviathanId=43;public static int CanonicalId(int id)=>id;}
public static class ShopLedger {public const int BagLimit=50;}
public class TestLedger
{
 public List<CaughtFishRecord> bag=new List<CaughtFishRecord>();
 public AbyssalGemCollection gems=new AbyssalGemCollection();
 public int catches;
 public void RecordCatch(int species,float weight){catches++;}
}
public class ShopProgress
{
 public TestLedger Data=new TestLedger(),LastSaved;
 public bool ReadOnly; public event Action Changed;
 public int SaveCount,NotifiedDust,GemNotifications;
 public void NotifyAbyssalGemAcquired(){if(LastSaved==null || LastSaved.gems.items.Count<=GemNotifications)throw new Exception("Gem notice before save");GemNotifications++;}
 public void Save(){SaveCount++;LastSaved=UnityEngine.JsonUtility.FromJson<TestLedger>(UnityEngine.JsonUtility.ToJson(Data));Changed?.Invoke();}
 public void NotifyCrystalDust(int amount)
 {
  if(LastSaved==null || LastSaved.gems.dust!=Data.gems.dust)throw new Exception("Dust notification arrived before save");
  NotifiedDust+=amount;
 }
}
