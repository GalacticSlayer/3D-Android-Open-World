using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Legacy Level 5 fallback, restricted to the Fishing Rods equipment category.
/// The current equipment builder already lists every owned rod; this fallback
/// only inserts Level 5 if it is missing from that category.
/// </summary>
[DefaultExecutionOrder(1800)]
public sealed class Level5FishingRodShopRuntime : MonoBehaviour
{
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly MethodInfo RowMethod=typeof(ShopWorldHUD).GetMethod("Row",Flags);
    private static readonly MethodInfo ResultMethod=typeof(ShopWorldHUD).GetMethod("Result",Flags);

    private ShopWorldHUD hud;
    private ShopProgress progress;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(target!=null && target.GetComponent<Level5FishingRodShopRuntime>()==null)
                target.gameObject.AddComponent<Level5FishingRodShopRuntime>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        if(hud==null || RowMethod==null || ResultMethod==null)
        {
            Debug.LogError("Level5FishingRodShopRuntime could not bind the existing equipment UI and was disabled.");
            enabled=false;
        }
    }

    private void LateUpdate()
    {
        if(!enabled || !ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+.08f;
        if(progress==null)progress=FindFirstObjectByType<ShopProgress>();
        if(progress==null)return;

        // The fallback must never insert a rod into Reels and Lines or Gems.
        if(!hud.ShowingFishingRods)return;

        bool owned=progress.Data.OwnsGear(GearKind.Rod,4);
        if(!owned || HasLevel5Row())return;

        bool equipped=progress.Data.rodEquipped==4;
        string stats=FishingBurstDamageRuntime.NormalMinimumForTier(4)+"–"+FishingBurstDamageRuntime.NormalMaximumForTier(4)+
                     " damage / "+(FishingBurstDamageRuntime.CriticalChanceForTier(4)*100f).ToString("0")+"% critical (2×)";
        string action=equipped?"EQUIPPED":"EQUIP";

        Action callback=()=>
        {
            bool result=!progress.ReadOnly && progress.Commit(progress.Data.Equip(GearKind.Rod,4));
            ResultMethod.Invoke(hud,new object[]{result,"Equipment updated."});
        };

        RowMethod.Invoke(hud,new object[]{
            ShopCatalog.RodNames[4],
            stats,
            action,
            callback,
            !equipped && !progress.ReadOnly,
            null,
            GearKind.Rod.ToString()
        });
    }

    private bool HasLevel5Row()
    {
        foreach(Text label in hud.GetComponentsInChildren<Text>(true))
            if(label!=null && string.Equals(label.text,ShopCatalog.RodNames[4],StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
}
