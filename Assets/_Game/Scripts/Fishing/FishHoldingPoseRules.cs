using UnityEngine;

public static class FishHoldingPoseRules
{
    public static bool UseBigPose(int species, float lengthMetres)
    {
        return lengthMetres >= 1.6f && species >= 0 &&
            species != FishCatalog.RoughtailStingrayId &&
            species != FishCatalog.LeopardStingrayId &&
            species != FishCatalog.WhiteSkateId;
    }

    // Measure once when creating the held visual, never bake a mesh every frame.
    // Sampling the middle of the body avoids a tail or long snout shifting the
    // support height. Percentiles exclude thin dorsal/pelvic fins from the belly.
    public static Vector3 SupportPoint(GameObject fish)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        var mesh = new Mesh();
        Bounds visibleBounds = default;
        bool hasVisibleBounds = false;
        foreach (var renderer in fish.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
            // Use the rendered envelope for centering. Baked vertices can have
            // different FBX scale/origin conventions from the visible bounds.
            // Transform local corners, avoiding rotation-dependent world AABBs.
            Bounds envelope = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = envelope.center + Vector3.Scale(envelope.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 local = fish.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                if (!hasVisibleBounds) { visibleBounds = new Bounds(local, Vector3.zero); hasVisibleBounds = true; }
                else visibleBounds.Encapsulate(local);
            }
            Mesh source = null;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.BakeMesh(mesh);
                source = mesh;
            }
            else if (renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    if (filter.sharedMesh.isReadable) source = filter.sharedMesh;
                    else
                    {
                        Bounds local = filter.sharedMesh.bounds;
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 corner = local.center + Vector3.Scale(local.extents,
                                new Vector3((i & 1) == 0 ? -1f : 1f,
                                    (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                            points.Add(fish.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner)));
                        }
                    }
                }
            }
            if (source != null)
            {
                foreach (Vector3 vertex in source.vertices)
                    points.Add(fish.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)));
            }
        }
        Object.Destroy(mesh);
        if (points.Count == 0)
            return hasVisibleBounds ? new Vector3(visibleBounds.center.x, visibleBounds.min.y, visibleBounds.center.z) : Vector3.zero;
        Bounds bounds = new Bounds(points[0], Vector3.zero);
        foreach (var point in points) bounds.Encapsulate(point);
        var heights = new System.Collections.Generic.List<float>();
        foreach (var point in points)
            if (Mathf.Abs(point.z - bounds.center.z) <= bounds.size.z * .15f)
                heights.Add(point.y);
        heights.Sort();
        float belly = heights.Count > 0 ? heights[(heights.Count - 1) / 10] : bounds.min.y;
        Vector3 center = hasVisibleBounds ? visibleBounds.center : bounds.center;
        return new Vector3(center.x, belly, center.z);
    }
}
