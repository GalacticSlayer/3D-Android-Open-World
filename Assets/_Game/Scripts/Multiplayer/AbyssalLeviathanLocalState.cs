using System;

// Pure offline encounter rules. Uses the caller's monotonic real-time clock;
// menu pauses do not stop the deadline or healing. No server or HTTP dependency.
public sealed class AbyssalLeviathanLocalState
{
    public const int MaximumHealth=220000;
    public readonly string Id=Guid.NewGuid().ToString("N");
    public string Phase {get;private set;}="active";
    public string Lease {get;private set;}="";
    public double Health {get;private set;}=MaximumHealth;
    public readonly float Weight;
    private readonly double ends;
    private double updated,unhookedAt;
    private int lastCast=-1,cumulativeDamage;
    public AbyssalLeviathanLocalState(double now,float weightRoll)
    {
        ends=now+900;updated=unhookedAt=now;
        Weight=400f+40f*Math.Max(0f,Math.Min(1f,weightRoll));
    }
    public double SecondsLeft(double now)=>Math.Max(0,ends-now);
    public void Tick(double now)
    {
        if(Phase!="active")return;
        if(now>=ends){Phase="expired";Lease="";return;}
        if(string.IsNullOrEmpty(Lease))
            Health=Math.Min(MaximumHealth,Health+Math.Max(0,now-Math.Max(updated,unhookedAt+5))*20000/60);
        updated=now;
    }
    public bool TryHook(int cast,float roll,double now)
    {
        Tick(now);
        if(Phase!="active" || cast<1 || cast==lastCast || !string.IsNullOrEmpty(Lease))return false;
        lastCast=cast;
        if(roll>=.5f)return false;
        Lease=Guid.NewGuid().ToString("N");cumulativeDamage=0;return true;
    }
    public bool Damage(string lease,int total,double now)
    {
        Tick(now);
        if(Phase!="active" || string.IsNullOrEmpty(Lease) || Lease!=lease || total<0 || total>MaximumHealth)return false;
        if(total>cumulativeDamage){Health=Math.Max(0,Health-(total-cumulativeDamage));cumulativeDamage=total;}
        return true;
    }
    public bool Release(string lease,int total,double now)
    {
        if(!Damage(lease,total,now))return false;
        Lease="";unhookedAt=updated=now;return true;
    }
    public bool Catch(string lease,int total,double now)
    {
        if(!Damage(lease,total,now) || Health>0)return false;
        Phase="caught";Lease="";return true;
    }
}
