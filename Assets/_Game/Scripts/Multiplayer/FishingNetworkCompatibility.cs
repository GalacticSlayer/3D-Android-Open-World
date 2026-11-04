using System;

public static class FishingNetworkCompatibility
{
    // Older deployed servers support zero-based tiers 0..4. They omit the
    // capability field; upgraded servers advertise 6 for the Level 7 rod.
    public const int LegacyMaximumRodTier=4;
    public static int AdvertisedMaximum(int advertised,int clientMaximum)
        =>advertised>0?Math.Min(advertised,clientMaximum):Math.Min(LegacyMaximumRodTier,clientMaximum);
    public static int RodTier(int equippedTier,bool rodVisible,int serverMaximum)
        =>rodVisible?Math.Max(0,Math.Min(equippedTier,serverMaximum)):0;
}
