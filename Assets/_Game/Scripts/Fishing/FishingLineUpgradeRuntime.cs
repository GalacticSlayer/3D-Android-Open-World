using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the real line-length upgrades without changing any other line behavior.
/// Standard = 40 m, Reinforced = 45 m, Braided = 50 m, Elite Braid = 55 m.
/// Existing cast distance and line-tolerance bonuses remain unchanged.
/// </summary>
[DefaultExecutionOrder(970)]
public sealed class FishingLineUpgradeRuntime : MonoBehaviour
{
    private FishingSystem fishing;
    private ShopProgress progress;
    private FieldInfo maximumLineDistanceField;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if(system!=null && system.GetComponent<FishingLineUpgradeRuntime>()==null)
                system.gameObject.AddComponent<FishingLineUpgradeRuntime>();
        }

        foreach(ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
        {
            if(hud!=null && hud.GetComponent<FishingLineShopPresentation>()==null)
                hud.gameObject.AddComponent<FishingLineShopPresentation>();
        }
    }

    private void Awake()
    {
        fishing=GetComponent<FishingSystem>();
        progress=GetComponent<ShopProgress>();
        maximumLineDistanceField=typeof(FishingSystem).GetField(
            "maximumLineDistance",
            BindingFlags.Instance|BindingFlags.NonPublic);

        if(fishing==null || progress==null || maximumLineDistanceField==null)
        {
            Debug.LogError("FishingLineUpgradeRuntime could not bind to FishingSystem and was disabled.");
            enabled=false;
        }
    }

    private void LateUpdate()
    {
        if(fishing==null || progress==null || maximumLineDistanceField==null)return;

        int tier=Mathf.Clamp(progress.Data.lineEquipped,0,3);
        float maximum=40f+tier*5f+(progress.Data.gems.Equipped?.Value(6)??0);
        float current=(float)maximumLineDistanceField.GetValue(fishing);
        if(!Mathf.Approximately(current,maximum))
            maximumLineDistanceField.SetValue(fishing,maximum);
    }
}

/// <summary>
/// Keeps the Tackle Store/equipment description in sync with the equipped line
/// progression while preserving the existing cast range, tolerance percentages,
/// prices, names and purchase behavior.
/// </summary>
public sealed class FishingLineShopPresentation : MonoBehaviour
{
    private float nextScan;

    private void LateUpdate()
    {
        if(!ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+0.05f;

        foreach(Text text in GetComponentsInChildren<Text>(true))
        {
            if(text==null)continue;
            GameObject row=FindRow(text.transform);
            if(row==null)continue;

            int tier=LineTier(row);
            if(tier<0)continue;

            // ShopWorldHUD currently generates this line as
            // "40 m line / 30 m maximum cast; X% more line tolerance".
            // Only the line-length portion is replaced here.
            if(text.text!=null && text.text.Contains(" m line / 30 m maximum cast;"))
            {
                int separator=text.text.IndexOf(" m line / 30 m maximum cast;",System.StringComparison.Ordinal);
                if(separator>=0)
                {
                    string suffix=text.text.Substring(separator+3); // starts at "line / ..."
                    text.text=(40+tier*5)+" m "+suffix;
                }
            }
        }
    }

    private static int LineTier(GameObject row)
    {
        if(RowContains(row,"Standard Line"))return 0;
        if(RowContains(row,"Reinforced Line"))return 1;
        if(RowContains(row,"Braided Line"))return 2;
        if(RowContains(row,"Elite Braid"))return 3;
        return -1;
    }

    private static bool RowContains(GameObject row,string value)
    {
        foreach(Text t in row.GetComponentsInChildren<Text>(true))
            if(t!=null && string.Equals(t.text,value,System.StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private static GameObject FindRow(Transform start)
    {
        for(Transform t=start;t!=null;t=t.parent)
            if(t.GetComponent<LayoutElement>()!=null)return t.gameObject;
        return null;
    }
}
