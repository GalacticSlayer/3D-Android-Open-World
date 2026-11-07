using UnityEngine;

public static class BoatClearance
{
    // Measured from the stationary authored raft body in the exact v20 diagnostic.
    // Do NOT include the oars in the hull footprint: they visually swing outside the
    // raft but should never make the collision/shore-clearance box wider.
    private static readonly Vector3 AuthoredRaftHullSize = new Vector3(2.6749f,1.254236f,4.5634f);
    private const float AuthoredRaftDraft = .45f;

    // Keep navigation and placement tied to the same motorboat depth threshold.
    private static float MotorboatBrakingDepth(BoatData data) => data.Draft - .95f;
    private const float MotorboatPlacementMargin = .10f;

    // Strict placement checks must not be used to cancel thrust on open waves.
    // Navigation only checks the seabed; solid props still collide normally.
    public static bool ApproachingShallowWater(BoatData data, Vector3 position, Quaternion rotation, OceanWater water)
    {
        if(data==null || water==null)return false;
        bool raft=data.ID=="raft";
        Vector3 size=raft?AuthoredRaftHullSize:data.HullSize;
        float draft=raft?AuthoredRaftDraft:data.Draft;
        float halfX=size.x*.5f+(raft ? 0.18f : 0.6f);
        float halfZ=size.z*.5f+(raft ? 0.35f : 0.6f);
        // Motorboat may navigate water 1.6 m shallower before pre-braking.
        // Other boats retain their existing navigation margins.
        float requiredDepth=data.ID=="sailboat" ? MotorboatBrakingDepth(data) : draft+.65f;
        foreach(var terrain in WorldTerrain.Tiles)
        {
            if(terrain==null || !terrain.isActiveAndEnabled || terrain.terrainData==null)continue;
            Vector3 min=terrain.transform.position, dimensions=terrain.terrainData.size;
            // Four footprint corners plus the hull centre: stable through swells.
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
            {
                Vector3 p=position+rotation*new Vector3(x*halfX,0f,z*halfZ);
                Vector3 local=p-min;
                if(local.x<0f || local.z<0f || local.x>dimensions.x || local.z>dimensions.z)continue;
                float ground=terrain.SampleHeight(p)+min.y;
                if(water.BaseWaterLevel-ground<requiredDepth)return true;
            }
            Vector3 c=position-min;
            if(c.x>=0f && c.z>=0f && c.x<=dimensions.x && c.z<=dimensions.z
                && water.BaseWaterLevel-(terrain.SampleHeight(position)+min.y)<requiredDepth)return true;
        }
        // Open water outside finite terrain does not create an invisible wall.
        return false;
    }

    public static bool TryGetWaterTarget(Ray ray, OceanWater water, float maxDistance, out float distance)
    {
        distance = 0f;
        return water != null && ray.direction.y < -.0001f &&
            new Plane(Vector3.up, new Vector3(0f, water.BaseWaterLevel, 0f)).Raycast(ray, out distance) &&
            distance <= maxDistance;
    }

    public static bool Valid(BoatData data, Vector3 position, Quaternion rotation, OceanWater water, Transform ignored=null, Transform player=null)
    {
        if(data==null || water==null || PondWater.TrySurface(position,out _))return false;

        bool authoredRaft=data.ID=="raft";
        bool motorboat=data.ID=="sailboat";
        Vector3 hullSize=authoredRaft?AuthoredRaftHullSize:data.HullSize;
        float draft=authoredRaft?AuthoredRaftDraft:data.Draft;
        if(hullSize.x<=0 || hullSize.z<=0 || draft<0)return false;

        // The old raft value effectively used the oars as hull width and then added
        // another full metre on each side. That made the boat stop well before the
        // visible left/right edges. Keep a small safety margin on the authored raft;
        // preserve the original larger buffer for the other boats.
        Vector3 buffer=authoredRaft?new Vector3(.18f,0,.35f):new Vector3(1,0,1);
        Vector3 half=hullSize*.5f+buffer;
        float requiredDepth=motorboat ? MotorboatBrakingDepth(data)+MotorboatPlacementMargin : draft+.8f;
        var terrains=WorldTerrain.Tiles;
        for(float x=-half.x;x<=half.x+.01f;x+=Mathf.Max(.08f,half.x/3f))
        for(float z=-half.z;z<=half.z+.01f;z+=Mathf.Max(.08f,half.z/4f))
        {
            Vector3 p=position+rotation*new Vector3(x,0,z);
            bool found=false;
            foreach(var terrain in terrains)
            {
                if(terrain==null || !terrain.isActiveAndEnabled || terrain.terrainData==null)continue;
                Vector3 local=p-terrain.transform.position;Vector3 size=terrain.terrainData.size;
                if(local.x<0 || local.z<0 || local.x>size.x || local.z>size.z)continue;
                found=true;
                float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                if(water.BaseWaterLevel-ground<requiredDepth)return false;
            }
            // Beyond the edge of authored terrain is still open ocean.
            // Keep the depth test for every terrain tile intersecting the hull,
            // while solid-object checks below catch any man-made obstacle.
            // No fabricated invisible shoreline at the terrain-map edge.
        }
        half.y=(draft+2f)*.5f;
        foreach(var c in Physics.OverlapBox(position+Vector3.up*((2f-draft)*.5f),half,rotation,~0,QueryTriggerInteraction.Ignore))
        {
            // Terrain depth was checked over the footprint above. The old
            // full-draft overlap would otherwise override the new shallow limit.
            if(c is TerrainCollider)continue; // Every intersecting tile was depth-tested above.
            if(c.GetComponentInParent<OceanWater>() != null || c.name=="BoatPlacementWater")continue;
            var rider=c.GetComponentInParent<BoatPassenger>();
            if(rider!=null && rider.Boat!=null && ignored==rider.Boat.transform)continue;
            if(ignored!=null && c.transform.IsChildOf(ignored))continue;
            if(player!=null && c.transform.IsChildOf(player))continue;
            return false;
        }
        return true;
    }
}
