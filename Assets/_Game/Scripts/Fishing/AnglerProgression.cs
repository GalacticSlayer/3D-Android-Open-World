using System;

/// <summary>Saved character progression. One permanent point per level; no equipment state.</summary>
[Serializable]
public sealed class AnglerProgression
{
    public const int MaxLevel = 601;
    public int level = 1;
    public double experience;
    // Raw damage, percent damage, reel speed, tension control, skill, charm.
    public int[] stats = new int[6];
    // One discretionary Special Ability upgrade is earned at every 20 levels.
    // The version marker preserves abilities unlocked by the previous automatic system.
    public int[] abilityRanks = new int[4];
    public bool abilityChoicesMigrated;
    public static readonly string[] Names = { "Raw Damage", "Percent Damage", "Reeling Speed", "Tension Control", "Skill", "Charm" };
    public static readonly string[] AbilityNames = { "LIGHTNING STRIKE", "HOOKSET POWER", "LINE IMMORTALITY", "SIREN'S CALL" };
    public static readonly string[] AbilityAssets = { "BurningFish", "HooksetPower", "LineImmortality", "SirensCall" };
    public int Value(int stat) => stats != null && stat >= 0 && stat < stats.Length ? Math.Max(0, Math.Min(100, stats[stat])) : 0;
    public float Multiplier(int stat) => 1f + Value(stat) * .01f;
    public int AbilityRank(int ability) => abilityRanks != null && ability >= 0 && ability < 4 && ability < abilityRanks.Length
        ? Math.Max(0, abilityRanks[ability]) : 0;
    public bool Unlocked(int ability) => AbilityRank(ability) > 0;
    public int AvailableAbilityPoints
    {
        get
        {
            int spent=0;
            if(abilityRanks!=null)for(int i=0;i<Math.Min(4,abilityRanks.Length);i++)spent+=Math.Max(0,abilityRanks[i]);
            return Math.Max(0,Math.Min(MaxLevel/20,level/20)-spent);
        }
    }
    public bool UpgradeAbility(int ability)
    {
        Normalize();
        if(ability<0 || ability>=4 || AvailableAbilityPoints<=0)return false;
        abilityRanks[ability]++;
        return true;
    }
    // Lightning Strike spreads a total of 5% max HP across five seconds at rank 1.
    // Each additional rank adds 0.5 percentage points to that TOTAL, and a
    // separate 2,000-damage hard cap is enforced when activated.
    public float BurningTotalFraction => .05f + .005f * Math.Max(0,AbilityRank(0)-1);
    public float BurningDamagePerHalfSecond => BurningTotalFraction / 10f;
    public float HooksetDamageFraction => .10f + .02f * Math.Max(0,AbilityRank(1)-1);
    public float ImmortalityDuration => 5f + Math.Max(0,AbilityRank(2)-1);
    public float SirenDuration => 4f + Math.Max(0,AbilityRank(3)-1);
    // The exponential base is unchanged. Each completed five-level bracket
    // adds 150 XP through level 100, 450 XP from 105 through 200,
    // and 1,500 XP from 205 onward. Previous brackets keep their original cost.
    public static double RequiredXp(int currentLevel)
    {
        int level = Math.Max(0, currentLevel);
        int standardBrackets = Math.Min(level, 100) / 5;
        int midBrackets = Math.Max(0, Math.Min(level, 200) - 100) / 5;
        int lateBrackets = Math.Max(0, level - 200) / 5;
        double baseXp = Math.Ceiling(250d * Math.Pow(1.015d, Math.Max(0, Math.Min(MaxLevel - 1, level - 1))));
        return baseXp + 150d * standardBrackets + 450d * midBrackets + 1500d * lateBrackets;
    }
    public static int CatchXp(int maximumHealth, float difficulty)
        => Math.Max(1, (int)Math.Round(4d * Math.Sqrt(Math.Max(1, maximumHealth)) * (1d + 2d * Math.Max(0d, Math.Min(2d, difficulty)))));

    public bool Normalize()
    {
        bool changed = false;
        if (level < 1 || level > MaxLevel) { level = Math.Max(1, Math.Min(MaxLevel, level)); changed = true; }
        if (stats == null || stats.Length != 6) { var old = stats; stats = new int[6]; if (old != null) Array.Copy(old, stats, Math.Min(6, old.Length)); changed = true; }
        for (int i = 0; i < 6; i++) { int value = Value(i); if (value != stats[i]) { stats[i] = value; changed = true; } }
        if (double.IsNaN(experience) || double.IsInfinity(experience) || experience < 0) { experience = 0; changed = true; }
        if (abilityRanks == null || abilityRanks.Length != 4)
        {
            int[] old=abilityRanks;
            abilityRanks=new int[4];
            if(old!=null)Array.Copy(old,abilityRanks,Math.Min(old.Length,4));
            changed=true;
        }
        if (!abilityChoicesMigrated)
        {
            // Existing players already owned one of the four abilities at
            // levels 20, 40, 60 and 80. Keep those as rank-one investments.
            for(int i=0;i<4;i++)if(level>=(i+1)*20 && abilityRanks[i]==0)abilityRanks[i]=1;
            abilityChoicesMigrated=true;
            changed=true;
        }
        int spent=0, earned=Math.Min(MaxLevel/20,level/20);
        for(int i=0;i<4;i++)
        {
            int safe=Math.Max(0,Math.Min(MaxLevel/20,abilityRanks[i]));
            if(safe!=abilityRanks[i]){abilityRanks[i]=safe;changed=true;}
            spent+=safe;
        }
        // Guard against malformed saves claiming more upgrades than levels earned.
        for(int i=3;i>=0 && spent>earned;i--)
        {
            int take=Math.Min(abilityRanks[i],spent-earned);
            abilityRanks[i]-=take;spent-=take;changed=true;
        }
        return changed;
    }

    // Optional per-level callback: new level, increased stat index, new stat value.
    // A stat index of -1 means all permanent attributes were already capped.
    public int AddXp(int amount, Func<int, int> choose, Action<int, int, int> onLevelUp = null)
    {
        Normalize();
        if (amount <= 0 || level >= MaxLevel) return 0;
        experience += amount;
        int gained = 0;
        while (level < MaxLevel && experience >= RequiredXp(level))
        {
            experience -= RequiredXp(level);
            int available = 0;
            for (int i = 0; i < 6; i++) if (stats[i] < 100) available++;
            int increasedStat = -1;
            if (available > 0)
            {
                int pick = Math.Max(0, Math.Min(available - 1, choose(available)));
                for (int i = 0; i < 6; i++)
                    if (stats[i] < 100 && pick-- == 0)
                    {
                        stats[i]++;
                        increasedStat = i;
                        break;
                    }
            }
            level++; gained++;
            if (onLevelUp != null)
                onLevelUp(level, increasedStat, increasedStat >= 0 ? stats[increasedStat] : 0);
        }
        if (level == MaxLevel) experience = 0;
        return gained;
    }
}
