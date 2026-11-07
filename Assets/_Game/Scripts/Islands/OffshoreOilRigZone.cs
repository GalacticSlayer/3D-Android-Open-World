using UnityEngine;

/// <summary>
/// Retired compatibility symbol. The offshore oil rig was replaced by Windgrove.
/// No physical old zone, fish habitat or teleport is created from this class.
/// </summary>
[System.Obsolete("The offshore rig was replaced by WindgroveShoalZone.")]
public static class OffshoreOilRigZone
{
    public const int BiomeId = WindgroveShoalZone.BiomeId;
    public const string Id = WindgroveShoalZone.Id;
    public const string Name = WindgroveShoalZone.Name;
    public static bool Contains(Vector3 point, float inset = 0f) => false;
}
