using UnityEngine;

/// <summary>
/// Remote, save-stable sixth fishing biome. The rig's 350 m fishing radius is
/// independent of the much smaller editable seabed tile; no terrain is generated
/// through the empty ocean between Bluewater and the rig.
/// </summary>
public static class OffshoreOilRigZone
{
    public const int BiomeId = 5;
    public const string Id = "offshore-oil-rig";
    public const string Name = "Offshore Oil Rig";
    public const float Radius = 350f;
    public const float SouthOfBluewater = 3000f;
    public const string ArrivalName = "Offshore Oil Rig Arrival";

    public static bool TryGetCenter(out Vector3 center)
    {
        IslandExpansionWorld world = IslandExpansionWorld.Active;
        if (world == null || !world.Ready)
        {
            center = Vector3.zero;
            return false;
        }
        center = world.PelagicCenter + Vector3.back * SouthOfBluewater;
        return true;
    }

    public static bool Contains(Vector3 point, float inset = 0f)
    {
        if (WindgroveShoalZone.Contains(point)) return false;
        if (!TryGetCenter(out Vector3 center)) return false;
        float dx = point.x - center.x, dz = point.z - center.z;
        float radius = Mathf.Max(0f, Radius - inset);
        return dx * dx + dz * dz <= radius * radius;
    }

    public static bool TryGetArrival(out Transform arrival)
    {
        GameObject marker = GameObject.Find(ArrivalName);
        arrival = marker != null ? marker.transform : null;
        return arrival != null;
    }
}
