#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// The staircase is an authored static .blend model, not a box or a slope.
/// Keep one accurate NON-CONVEX static MeshCollider per rendered mesh so every
/// step and platform stays walkable when the user scales its scene instance.
/// No Rigidbody, animated collider or duplicate collision geometry is created.
/// </summary>
public sealed class WindgroveStaircaseColliderImporter : AssetPostprocessor
{
    private const string StaircasePath =
        "Assets/Manually Added Assets/Wind Turbine Staircase.blend";

    public override uint GetVersion() => 1;

    private void OnPreprocessModel()
    {
        if (assetPath != StaircasePath) return;
        ModelImporter importer = (ModelImporter)assetImporter;
        importer.addCollider = true;
    }

    private void OnPostprocessModel(GameObject importedModel)
    {
        if (assetPath != StaircasePath) return;
        foreach (MeshFilter filter in importedModel.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null)
                collider = filter.gameObject.AddComponent<MeshCollider>();
            // Unity's importer normally creates these automatically. Reuse
            // them; only repair missing/stale mesh references.
            if (collider.sharedMesh != filter.sharedMesh)
                collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            collider.enabled = true;
        }
    }
}
#endif
