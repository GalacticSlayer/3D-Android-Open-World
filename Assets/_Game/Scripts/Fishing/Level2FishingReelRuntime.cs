using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime support for all seven fishing reels. Reel upgrades keep the starter
/// reel's mechanics, hierarchy and authored animation; only the visible five reel
/// meshes/materials and the reeling-speed multiplier change.
/// Starter = 1.00x, Level 2 = 1.20x, Level 3 = 1.40x, Level 4 = 1.60x, Level 5 = 1.80x, Level 6 = 2.00x, Level 7 = 2.20x.
/// </summary>
[DefaultExecutionOrder(-420)]
public sealed class Level2FishingReelRuntime : MonoBehaviour
{
    private static readonly string[] ReelParts={"ReelFootAndBody","Rotor","ReelHousing","Spool","Handle"};
    private FishingSystem fishing;
    private ShopProgress progress;
    private FieldInfo reelPowerField;
    private FieldInfo rodRootField;
    private int appliedTier=-1;
    private int warnedTier=-1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<Level2FishingReelRuntime>()==null)
                system.gameObject.AddComponent<Level2FishingReelRuntime>();
        EnsureShopCleanup();
    }

    private static void EnsureShopCleanup()
    {
        foreach(ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(hud!=null && hud.GetComponent<ReelShopCleanup>()==null)
                hud.gameObject.AddComponent<ReelShopCleanup>();
    }

    private void Awake()
    {
        fishing=GetComponent<FishingSystem>();
        progress=GetComponent<ShopProgress>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        reelPowerField=typeof(FishingSystem).GetField("reelPower",flags);
        rodRootField=typeof(FishingSystem).GetField("rodRoot",flags);
    }

    private void Start()
    {
        EnsureShopCleanup();
        if(progress==null || fishing==null || reelPowerField==null || rodRootField==null){enabled=false;return;}
        bool migrated=false;
        if(progress.Data.reelOwned>ShopCatalog.MaxReelTier){progress.Data.reelOwned=ShopCatalog.MaxReelTier;migrated=true;}
        if(progress.Data.reelEquipped>ShopCatalog.MaxReelTier){progress.Data.reelEquipped=ShopCatalog.MaxReelTier;migrated=true;}
        if(progress.Data.reelEquipped>progress.Data.reelOwned){progress.Data.reelEquipped=progress.Data.reelOwned;migrated=true;}
        if(migrated && !progress.ReadOnly)progress.Save();
        Apply();
    }

    // This runs before FishingSystem.Update so lure retrieval, fish fighting and
    // visible reel animation all read the exact tier multiplier in the same frame.
    private void Update(){Apply();}

    // Defensive cleanup for stale property blocks from older equipment code.
    // Authored materials retain their original colours across saves and fights.
    private void LateUpdate(){ClearReelTint();}

    private void Apply()
    {
        if(progress==null || fishing==null || reelPowerField==null || rodRootField==null)return;
        int tier=Mathf.Clamp(progress.Data.reelEquipped,0,ShopCatalog.MaxReelTier);
        reelPowerField.SetValue(fishing,ReelMultiplier(tier));
        ApplyVisual(tier);
    }

    public static float ReelMultiplier(int tier)
    {
        if(tier>=6)return 2.20f;
        if(tier>=5)return 2.00f;
        if(tier==4)return 1.80f;
        if(tier>=3)return 1.60f;
        if(tier==2)return 1.40f;
        if(tier==1)return 1.20f;
        return 1f;
    }

    private void ApplyVisual(int tier)
    {
        GameObject rodRoot=rodRootField.GetValue(fishing) as GameObject;
        if(rodRoot==null || appliedTier==tier)return;

        GameObject sourcePrefab=Resources.Load<GameObject>(ShopCatalog.ReelPrefabResource(tier));
        if(sourcePrefab==null)
        {
            if(tier>0 && warnedTier!=tier)
            {
                warnedTier=tier;
                string level="Level "+(tier+1);
                Debug.LogWarning(level+" reel model is not installed yet. Run Tools > Open World > Import "+level+" Fishing Reel (One Click). The previous visual is being used temporarily.");
            }
            return;
        }

        Transform sourceMount=FindDeepChild(sourcePrefab.transform,"ReelMount");
        Transform targetMount=FindDeepChild(rodRoot.transform,"ReelMount");
        if(sourceMount==null || targetMount==null)return;

        foreach(string partName in ReelParts)
        {
            Transform source=sourceMount.Find(partName);
            Transform target=targetMount.Find(partName);
            if(source==null || target==null)continue;
            MeshFilter sourceFilter=source.GetComponent<MeshFilter>();
            MeshFilter targetFilter=target.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer=source.GetComponent<MeshRenderer>();
            MeshRenderer targetRenderer=target.GetComponent<MeshRenderer>();
            if(sourceFilter!=null && targetFilter!=null)targetFilter.sharedMesh=sourceFilter.sharedMesh;
            if(sourceRenderer!=null && targetRenderer!=null)
            {
                targetRenderer.sharedMaterials=sourceRenderer.sharedMaterials;
                targetRenderer.SetPropertyBlock(null);
            }
        }
        appliedTier=tier;
        warnedTier=-1;
    }

    private void ClearReelTint()
    {
        if(fishing==null || rodRootField==null)return;
        GameObject rodRoot=rodRootField.GetValue(fishing) as GameObject;
        if(rodRoot==null)return;
        Transform targetMount=FindDeepChild(rodRoot.transform,"ReelMount");
        if(targetMount==null)return;

        foreach(string partName in ReelParts)
        {
            Transform target=targetMount.Find(partName);
            MeshRenderer renderer=target!=null?target.GetComponent<MeshRenderer>():null;
            if(renderer!=null)renderer.SetPropertyBlock(null);
        }
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t!=null && t.name==name)return t;
        return null;
    }
}

/// <summary>Hides retired reel rows and presents the exact reel-speed bonuses.</summary>
public sealed class ReelShopCleanup : MonoBehaviour
{
    private float nextScan;
    private void LateUpdate()
    {
        if(!ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+0.05f;
        Text[] texts=GetComponentsInChildren<Text>(true);
        foreach(Text text in texts)
        {
            if(text==null)continue;
            string value=text.text??string.Empty;
            if(value=="REMOVED REEL" || value=="Smooth Drag" || value=="Precision Drag" || value=="Deepwater Pro")
            {
                GameObject row=FindRow(text.transform);
                if(row!=null)row.SetActive(false);
                continue;
            }
            if(value.Contains("22% faster tiring and retrieval"))
                text.text=value.Replace("22% faster tiring and retrieval","20% faster reeling").Replace(" / Requires previous tier",string.Empty);
            else if(value.Contains("44% faster tiring and retrieval"))
                text.text=value.Replace("44% faster tiring and retrieval","40% faster reeling").Replace(" / Requires previous tier",string.Empty);
        }
    }

    private static GameObject FindRow(Transform start)
    {
        for(Transform t=start;t!=null;t=t.parent)
            if(t.GetComponent<LayoutElement>()!=null)return t.gameObject;
        return null;
    }
}
