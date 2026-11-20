using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class AbyssalGem
{
    public string id;
    public int level=1;
    public int[] attributes=new int[4];
    public float[] values=new float[4];
    public float ScalePiercer => 1.5f+.05f*(level-1);
    public float Value(int attribute)
    {
        for(int i=0;i<4;i++)if(attributes[i]==attribute)return values[i];
        return 0;
    }
}

// Pure save/economy rules; sample integer tenths with an exclusive upper bound.
// Float values also read existing whole-number JSON saves without changing their bonuses.
[Serializable]
public sealed class AbyssalGemCollection
{
    public const int MaxLevel=15;
    public const int BaseBreakdownDust=10;
    public static readonly string[] Names={"Tension Control","Tension Recovery","Attack Percentage","Skill Damage","Crit Damage","Crit Chance","Extra Line","Lure Saver"};
    public static readonly int[] Minimum={1,1,1,1,1,1,1,4};
    public static readonly int[] Maximum={2,2,3,3,4,3,3,8};
    public List<AbyssalGem> items=new List<AbyssalGem>();
    public string equippedId="";
    public int dust;
    public bool resetDialUnlocked;
    public bool HasResetDial => resetDialUnlocked || items.Count>0;
    public IEnumerable<AbyssalGem> SortedItems => items.OrderByDescending(gem=>gem.level);
    public AbyssalGem Equipped => Find(equippedId);
    public AbyssalGem Find(string id)
    {
        if(string.IsNullOrEmpty(id))return null;
        for(int i=0;i<items.Count;i++)if(items[i].id==id)return items[i];
        return null;
    }
    public float Bonus(int attribute)=>(Equipped?.Value(attribute)??0)/100f;
    public static int UpgradeCost(int level)=>level>=MaxLevel?0:50+25*(Math.Max(1,level)-1);
    public static int CatchDust(int maximumHealth,bool leviathan)
        =>leviathan?30:Math.Max(2,Math.Min(25,2+(int)Math.Floor(Math.Sqrt(Math.Max(0,maximumHealth))/12d)));
    public AbyssalGem Create(Func<int,int,int> roll)
    {
        var gem=new AbyssalGem{id=Guid.NewGuid().ToString("N")};
        var pool=new List<int>{0,1,2,3,4,5,6,7};
        for(int i=0;i<4;i++)
        {
            int slot=roll(0,pool.Count),kind=pool[slot];pool.RemoveAt(slot);
            gem.attributes[i]=kind;gem.values[i]=RollIncrease(kind,roll);
        }
        items.Add(gem);resetDialUnlocked=true;return gem;
    }
    public bool Upgrade(string id,Func<int,int,int> roll,out int slot,out float increase)
    {
        slot=-1;increase=0;
        var gem=Find(id);
        if(gem==null || gem.level>=MaxLevel || dust<UpgradeCost(gem.level))return false;
        slot=roll(0,4);int kind=gem.attributes[slot];
        increase=RollIncrease(kind,roll);
        dust-=UpgradeCost(gem.level);gem.level++;gem.values[slot]=(float)Math.Round(gem.values[slot]+increase,1);
        return true;
    }
    private static float RollIncrease(int kind,Func<int,int,int> roll)
        =>roll(Minimum[kind]*10,Maximum[kind]*10+1)/10f;
    public bool Reset(string id,Func<int,int,int> roll)
    {
        var gem=Find(id);if(gem==null)return false;
        var values=new float[4];
        for(int i=0;i<4;i++)values[i]=RollIncrease(gem.attributes[i],roll);
        gem.values=values;gem.level=1;
        resetDialUnlocked=true;
        return true;
    }
    public bool Equip(string id)
    {
        if(Find(id)==null)return false;
        equippedId=equippedId==id?"":id;return true;
    }
    public static int BreakdownDust(int level)
    {
        // Every existing gem paid this same upgrade schedule, so its level
        // reconstructs the investment without changing the save format.
        int invested=0;
        for(int previous=1;previous<Math.Min(MaxLevel,level);previous++)
            invested+=UpgradeCost(previous);
        return BaseBreakdownDust+invested/5;
    }
    public bool BreakDown(string id)
    {
        var gem=Find(id);if(gem==null)return false;
        resetDialUnlocked=true;
        items.Remove(gem);if(equippedId==id)equippedId="";
        dust=(int)Math.Min(int.MaxValue,(long)dust+BreakdownDust(gem.level));return true;
    }
    public int AwardCatch(int maximumHealth,bool leviathan,Func<int,int,int> roll)
    {
        int reward=Equipped==null?0:CatchDust(maximumHealth,leviathan);
        dust=(int)Math.Min(int.MaxValue,(long)dust+reward);
        if(leviathan)Create(roll);
        return reward;
    }
    public void Validate()
    {
        if(items==null)items=new List<AbyssalGem>();
        if(dust<0)throw new InvalidOperationException("Invalid crystal dust balance");
        var ids=new HashSet<string>();
        foreach(var gem in items)
        {
            if(gem==null || string.IsNullOrEmpty(gem.id) || !ids.Add(gem.id) || gem.level<1 || gem.level>15 || gem.attributes==null || gem.values==null || gem.attributes.Length!=4 || gem.values.Length!=4)
                throw new InvalidOperationException("Invalid Abyssal Gem save");
            var kinds=new HashSet<int>();
            for(int i=0;i<4;i++)if(gem.attributes[i]<0 || gem.attributes[i]>=8 || !kinds.Add(gem.attributes[i]) || float.IsNaN(gem.values[i]) || float.IsInfinity(gem.values[i]) || gem.values[i]<1 || gem.values[i]>Maximum[gem.attributes[i]]*gem.level)
                throw new InvalidOperationException("Invalid Abyssal Gem attribute");
        }
        if(items.Count>0)resetDialUnlocked=true;
        if(Equipped==null)equippedId="";
    }
}
