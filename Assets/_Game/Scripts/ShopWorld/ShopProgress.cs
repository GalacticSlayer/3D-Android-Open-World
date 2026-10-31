using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class ShopProgress : MonoBehaviour
{
    public const string SaveKey = "OpenWorld.ShopWorld.v1";
    public ShopLedger Data { get; private set; }
    public event Action Changed;
    // Amount, previous level/XP, and saved new level/XP for catch-XP HUD animation.
    public event Action<int, int, double, int, double> FishingXpAwarded;
    // New level, rolled permanent stat index (-1 if capped), and resulting stat value.
    public event Action<int, int, int> FishingLevelAwarded;
    public string Notice { get; private set; }
    public bool ReadOnly { get; private set; }
    private void Awake()
    {
        try
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                try { Data=Read(PlayerPrefs.GetString(SaveKey)); }
                catch { Data=Read(PlayerPrefs.GetString(SaveKey+".backup")); Notice="Recovered the previous shop save."; }
            }
            else Migrate();
            bool migrated=Data.EnsureCatchStats();
            if(Data.angler==null){Data.angler=new AnglerProgression();migrated=true;}
            migrated|=Data.angler.Normalize();
            if(string.IsNullOrWhiteSpace(Data.playerName))
            {Data.playerName=NormalizePlayerName(PlayerPrefs.GetString("MultiplayerGuestName","Angler"));migrated=true;}
            migrated|=Data.EnsureLures();
            // An interrupted fight (including quitting the app) loses its lure once.
            migrated|=Data.ResolveLureFight(false);
            migrated|=Data.EnsureGearOwnership();
            if(!Data.infiniteWormsMigrated){Data.baitEquipped=0;Data.infiniteWormsMigrated=true;migrated=true;}
            if(Data.baitEquipped==1){Data.baitEquipped=0;migrated=true;}
            foreach(var fish in Data.bag)if(fish.speciesId==4){fish.speciesId=FishCatalog.YellowfinTunaId;migrated=true;}
            foreach(var habitat in Data.habitats)foreach(var fish in habitat.fish)
                if(fish.speciesId==4){fish.speciesId=FishCatalog.YellowfinTunaId;migrated=true;}
            ShopCatalog.SetActiveLureVariant(Data.lureEquipped);
            if(migrated)Save();
            if(!Data.homeMigrated) { Data.MigrateHome(); Save(); Notice="Your habitats now live at Home. Earlier smaller purchases were credited; all fish were preserved."; }
        }
        catch(Exception ex)
        {
            ReadOnly=true; Data=new ShopLedger(); Data.EnsureCatchStats(); Data.EnsureLures(); Data.EnsureGearOwnership(); ShopCatalog.SetActiveLureVariant(0); Notice="Save could not be loaded. Purchases and transfers are disabled; your saved data has not been overwritten.";
            Debug.LogError(Notice+" "+ex.Message);
        }
        ReefCatalog.BrinebreakDiscovered=Data!=null && Data.brinebreakDiscovered;
        ReefCatalog.BluewaterDiscovered=Data!=null && Data.bluewaterDiscovered;
        ReefCatalog.SnapperDiscovered=Data!=null && Data.snapperDiscovered;
    }
    private void Start()
    {
        // The popup is attached automatically to existing scenes and saves.
        if(GetComponent<FishingExperienceToast>()==null)
            gameObject.AddComponent<FishingExperienceToast>();
    }
    private static ShopLedger Read(string json)
    {
        if(string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Empty save");
        var d=JsonUtility.FromJson<ShopLedger>(json);
        if(d==null || d.version!=1 || d.bag==null || d.habitats==null || d.bait==null || d.bait.Length!=4) throw new InvalidOperationException("Invalid save");
        foreach(GearKind kind in Enum.GetValues(typeof(GearKind)))
        {
            int maxTier=kind==GearKind.Rod?ShopCatalog.MaxRodTier:kind==GearKind.Reel?ShopCatalog.MaxReelTier:ShopCatalog.LineNames.Length-1;
            if(d.Owned(kind)<0 || d.Owned(kind)>maxTier || d.Equipped(kind)<0 || d.Equipped(kind)>maxTier)
                throw new InvalidOperationException("Invalid saved equipment");
        }
        if(d.coins<0 || d.baitEquipped<0 || d.baitEquipped>ShopCatalog.StarterLure || d.lureEquipped<0 || d.lureEquipped>=ShopCatalog.LureVariantCount || d.lureOwnedMask<0) throw new InvalidOperationException("Invalid saved balance, bait, or lure");
        if(d.lureQuantities!=null)foreach(int amount in d.lureQuantities)
            if(amount<0)throw new InvalidOperationException("Invalid lure quantity");
        foreach(int amount in d.bait) if(amount<0) throw new InvalidOperationException("Invalid bait quantity");
        foreach(var habitat in d.habitats) if(habitat==null || ShopCatalog.Habitat(habitat.id)==null || habitat.fish==null)
            throw new InvalidOperationException("Invalid saved habitat");
        return d;
    }
    private void Migrate()
    {
        Data=new ShopLedger { coins=PlayerPrefs.GetInt("OpenWorld.Coins.v1",0), legacyMigrated=true };
        Data.EnsureLures();
        Data.EnsureGearOwnership();
        string fishJson=PlayerPrefs.GetString("OpenWorld.FishingInventory.v1","");
        if(!string.IsNullOrWhiteSpace(fishJson))
        {
            var old=JsonUtility.FromJson<FishingInventorySaveData>(fishJson);
            if(old?.fish!=null) Data.bag.AddRange(old.fish);
        }
        string tanks=PlayerPrefs.GetString("OpenWorld.Aquariums.v1","");
        int refunded=0, rescued=0;
        if(!string.IsNullOrWhiteSpace(tanks))
        {
            var old=JsonUtility.FromJson<AquariumSaveData>(tanks);
            if(old!=null)
            {
                refunded=Math.Max(0,old.unplacedTanks);
                if(old.tanks!=null) foreach(var tank in old.tanks)
                {
                    refunded++;
                    if(tank.fish!=null) { Data.bag.AddRange(tank.fish); rescued+=tank.fish.Count; }
                }
                Data.coins+=refunded*AquariumSystem.TankPrice;
            }
        }
        ShopCatalog.SetActiveLureVariant(Data.lureEquipped);
        Notice=refunded>0 ? $"Moved {rescued} fish to your bag and refunded {refunded*AquariumSystem.TankPrice} coins for old aquariums." : "Welcome to Tideglass Quay. Sell catches to fund your first upgrades.";
        Save();
    }
    public int AwardFishingXp(int health, float difficulty)
    {
        if(ReadOnly || Data==null)return 0;
        if(Data.angler==null)Data.angler=new AnglerProgression();
        int beforeLevel=Data.angler.level;
        double beforeXp=Data.angler.experience;
        int xp=AnglerProgression.CatchXp(health,difficulty);
        var levels=new List<int[]>();
        Data.angler.AddXp(xp,count=>UnityEngine.Random.Range(0,count),
            (level,stat,value)=>levels.Add(new[]{level,stat,value}));
        Save();
        // Notify only after the reward has been saved. Each per-level award
        // retains its exact randomly chosen attribute, even across multi-level catches.
        if(Data.angler.level!=beforeLevel || Math.Abs(Data.angler.experience-beforeXp)>.001d)
            FishingXpAwarded?.Invoke(xp,beforeLevel,beforeXp,Data.angler.level,Data.angler.experience);
        foreach(int[] award in levels)
            FishingLevelAwarded?.Invoke(award[0],award[1],award[2]);
        return xp;
    }
    public bool UpgradeFishingAbility(int ability)
    {
        if(ReadOnly || Data==null || Data.angler==null)return false;
        // Spend one level-earned point, not coins. Persist with the same save
        // mechanism as the existing XP and attribute upgrades.
        if(!Data.angler.UpgradeAbility(ability))return false;
        Save();
        return true;
    }
    public void Save()
    {
        if(ReadOnly) return;
        ShopCatalog.SetActiveLureVariant(Data.lureEquipped);
        string json=JsonUtility.ToJson(Data);
        if(PlayerPrefs.HasKey(SaveKey)) PlayerPrefs.SetString(SaveKey+".backup",PlayerPrefs.GetString(SaveKey));
        PlayerPrefs.SetString(SaveKey,json); PlayerPrefs.Save(); Changed?.Invoke();
    }
    public static string NormalizePlayerName(string value)
    {
        var name=new System.Text.StringBuilder();
        foreach(char c in value??"")
            if(c>='a' && c<='z' || c>='A' && c<='Z' || c>='0' && c<='9' || c==' ' || c=='_' || c=='-')name.Append(c);
        string clean=name.ToString().Trim();
        return clean.Length==0?"Angler":clean.Substring(0,Math.Min(20,clean.Length));
    }
    public bool SetPlayerName(string value)
    {
        if(ReadOnly || Data==null)return false;
        string name=NormalizePlayerName(value);
        if(Data.playerName==name)return true;
        string previous=Data.playerName;
        Data.playerName=name;
        try{Save();return true;}
        catch(Exception ex){Data.playerName=previous;Debug.LogError("Could not save player name: "+ex.Message);return false;}
    }
    public bool Commit(bool changed) { if(changed) Save(); return changed; }
    public bool CanTrade => !ReadOnly && ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.InShop;
    public bool BuyGear(GearKind kind,int tier) => CanTrade && ShopDimensionManager.Instance.Near("gear") && Commit(Data.BuyGear(kind,tier));
    public bool BuyBait(int id) => CanTrade && ShopDimensionManager.Instance.Near("gear") && Commit(Data.BuyBait(id));
    public bool BuyLure(int variant) => CanTrade && ShopDimensionManager.Instance.Near("gear") && Commit(Data.BuyLure(variant));
    // Equipping an already-owned lure is loadout management, just like selecting
    // owned bait from the BAIT / LURES shortcut. It must keep working after a
    // restart and does not require standing at the tackle counter.
    public bool BeginLureFight(int variant) => !ReadOnly && Commit(Data.BeginLureFight(variant));
    public bool ResolveLureFight(bool caught) => !ReadOnly && Commit(Data.ResolveLureFight(caught));
    public bool EquipLure(int variant) => !ReadOnly && Commit(Data.EquipLure(variant));
    public bool BuyHabitat(string id) => CanTrade && ShopDimensionManager.Instance.Near(id) && Commit(Data.BuyHabitat(id));
    public bool Sell(CaughtFishRecord fish) => CanTrade && ShopDimensionManager.Instance.Near("market") && Commit(Data.Sell(fish,FishCatalog.GetSellValue(fish.speciesId,fish.weightKg)));
    public bool CanManage => !ReadOnly && ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.InHome;
    public bool Deposit(string id,CaughtFishRecord fish) => CanManage && ShopDimensionManager.Instance.Near(id) && Commit(Data.Deposit(id,fish));
    public bool Withdraw(string id,CaughtFishRecord fish) => CanManage && ShopDimensionManager.Instance.Near(id) && Commit(Data.Withdraw(id,fish));
    public int TakeBait()
    {
        if(ReadOnly) return 0;
        int id=Data.ConsumeBait();
        if(id==ShopCatalog.StarterLure)ShopCatalog.SetActiveLureVariant(Data.lureEquipped);
        if(!ShopCatalog.PermanentBait(id)) Save();
        return id;
    }
}

