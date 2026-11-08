using System;

/// <summary>Per-hook boss defence. Times use the same clock as fishing statuses.</summary>
public sealed class AbyssalHardenedScales
{
    public const float ActivationChance = .2f;
    public int Status { get; private set; } = -1;
    private float activeUntil, flashUntil;
    private bool hit;
    private int damageRemainder;

    public void Reset(int initialStatus = -1)
    {
        Status = initialStatus;
        activeUntil = flashUntil = 0f;
        hit = false;
        damageRemainder = 0;
    }

    public bool IsActive(float now) => now < activeUntil;
    public float Opacity(float now) => now < flashUntil ? .9f : hit ? .5f : .4f;

    public void ObserveStatus(int status, float now, float chanceRoll, float duration)
    {
        if (status == Status) return;
        bool changed = Status >= 0;
        Status = status;
        // Existing protection is neither stacked nor extended: each activation
        // lasts 5-8 seconds even if the fish changes status several times.
        if (!changed || IsActive(now) || chanceRoll >= ActivationChance) return;
        activeUntil = now + Math.Max(5f, Math.Min(8f, duration));
        flashUntil = 0f;
        hit = false;
        damageRemainder = 0;
    }

    public int ReduceDamage(int damage, float now)
    {
        if (damage <= 0 || !IsActive(now)) return Math.Max(0, damage);
        hit = true;
        flashUntil = now + .2f;
        // Carry fifths across hits instead of forcing every tiny hit to deal 1 HP.
        // This preserves 80% mitigation even for low-level rods and passive hits.
        long scaled = (long)damage + damageRemainder;
        damageRemainder = (int)(scaled % 5);
        return (int)(scaled / 5);
    }
}
