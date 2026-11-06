using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Both supplied FBX rigs contain a single authored swim take. Held motion uses the shared controller.
[InitializeOnLoad]
public static class EmperorAngelfishInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/EmperorAngelfish.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static EmperorAngelfishInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { EnsureInstalled(); }
            catch (Exception e) { EditorApplication.isPlaying = false; Debug.LogException(e); }
        };
    }

    private static void AutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += AutoInstall; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { EnsureInstalled(); }
        catch (Exception e) { Debug.LogError("[EMPEROR ANGELFISH] Import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        if (Ready(PrefabPath) && Ready("Assets/Resources/Fishing/JuvenileEmperorAngelfish.prefab"))
        {
            EnsureDorsalTrackSkin("EmperorAngelfish");
            EnsureDorsalTrackSkin("JuvenileEmperorAngelfish");
            return;
        }
        Rebuild();
    }

    private static bool Ready(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null || prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        var animator=prefab.GetComponentInChildren<Animator>(true);
        return animator!=null && animator.runtimeAnimatorController!=null &&
            animator.runtimeAnimatorController.animationClips.Any(c=>c!=null && c.name=="Swim");
    }

    [MenuItem("Tools/Open World/Rebuild Emperor Angelfish")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/EmperorAngelfish.fbx") || !File.Exists(Source + "/EmperorAngelfishTexture.png") ||
                !File.Exists(Source + "/JuvenileEmperorAngelfish.fbx") || !File.Exists(Source + "/JuvenileEmperorAngelfishTexture.png"))
                throw new InvalidOperationException("Emperor Angelfish source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("EmperorAngelfish", PrefabPath, Source);
            SeaBassImporter.InstallModel("JuvenileEmperorAngelfish", "Assets/Resources/Fishing/JuvenileEmperorAngelfish.prefab", Source);
            EnsureDorsalTrackSkin("EmperorAngelfish");
            EnsureDorsalTrackSkin("JuvenileEmperorAngelfish");
            Debug.Log("[EMPEROR ANGELFISH] Adult and juvenile installed; dorsal fins now follow their body during train-track turns.");
        }
        finally { installing = false; }
    }

    // The supplied adult and juvenile FBXs have the same dorsal mesh (Plane.006).
    // Its authored weights pull even the back of the fin toward the head bone,
    // so it does not follow the shared five-bone train-track turn. Copy the
    // BODY's longitudinal weighting to the dorsal mesh only; leave the anal
    // fin, the body, authored clips, and all other species completely untouched.
    private static void EnsureDorsalTrackSkin(string species)
    {
        string prefabPath = "Assets/Resources/Fishing/" + species + ".prefab";
        string meshPath = Source + "/" + species + "DorsalTrackSkin.asset";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Missing " + species + " prefab.");

        Mesh repairedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        var existingFin = FindImportedSkin(prefab, "Plane.006", "Plane.002");
        if (existingFin != null && repairedMesh != null &&
            existingFin.sharedMesh == repairedMesh)
            return; // No prefab/asset churn on each editor reload.

        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var body = FindImportedSkin(contents, "Plane.004", "Plane");
            var dorsal = FindImportedSkin(contents, "Plane.006", "Plane.002");
            if (body == null || dorsal == null || body == dorsal ||
                body.sharedMesh == null || dorsal.sharedMesh == null)
                throw new InvalidOperationException(
                    "Cannot find the body and dorsal skinned meshes on " + species + ".");

            var weights = FollowBodyWeights(body, dorsal);
            Mesh corrected = UnityEngine.Object.Instantiate(dorsal.sharedMesh);
            corrected.name = species + " Dorsal Track Skin";
            corrected.hideFlags = HideFlags.None;
            corrected.boneWeights = weights;

            if (repairedMesh == null)
            {
                AssetDatabase.CreateAsset(corrected, meshPath);
                repairedMesh = corrected;
            }
            else
            {
                // Preserve this mesh asset's GUID across explicit prefab rebuilds.
                EditorUtility.CopySerialized(corrected, repairedMesh);
                repairedMesh.name = corrected.name;
                EditorUtility.SetDirty(repairedMesh);
                UnityEngine.Object.DestroyImmediate(corrected);
            }

            dorsal.sharedMesh = repairedMesh;
            if (PrefabUtility.SaveAsPrefabAsset(contents, prefabPath) == null)
                throw new InvalidOperationException("Could not save the corrected " + species + " prefab.");
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static SkinnedMeshRenderer FindImportedSkin(GameObject root, string meshName, string nodeName)
    {
        return root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.sharedMesh != null &&
                (r.sharedMesh.name == meshName || r.gameObject.name == nodeName));
    }

    private static BoneWeight[] FollowBodyWeights(SkinnedMeshRenderer body, SkinnedMeshRenderer dorsal)
    {
        Mesh bodyMesh = body.sharedMesh;
        Mesh finMesh = dorsal.sharedMesh;
        var bodyWeights = bodyMesh.boneWeights;
        var bodyVertices = bodyMesh.vertices;
        var finVertices = finMesh.vertices;
        Transform[] sourceBones = body.bones;
        Transform[] finBones = dorsal.bones;
        if (bodyWeights.Length != bodyVertices.Length ||
            finVertices.Length == 0 || finBones.Length == 0)
            throw new InvalidOperationException("Emperor dorsal or body skin weights are unavailable.");

        var head = sourceBones.FirstOrDefault(b => b != null && b.name == "Bone");
        var tail = sourceBones.FirstOrDefault(b => b != null && b.name == "Bone.004");
        if (head == null || tail == null || Vector3.Distance(head.position, tail.position) < 0.001f)
            throw new InvalidOperationException("Emperor body spine is missing.");

        // Measure in world space so the FBX mesh nodes may have different local
        // transforms. Fin tips use the spine weights of the body underneath.
        Vector3 axis = (head.position - tail.position).normalized;
        var positions = new float[bodyVertices.Length];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = Vector3.Dot(body.transform.TransformPoint(bodyVertices[i]), axis);

        var boneMap = new int[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; i++)
        {
            boneMap[i] = -1;
            // Some FBX importers omit unused joints from a fin renderer.
            // The closest available parent still follows the same trail.
            for (Transform joint = sourceBones[i]; joint != null && boneMap[i] < 0; joint = joint.parent)
                for (int j = 0; j < finBones.Length; j++)
                    if (finBones[j] == joint) { boneMap[i] = j; break; }
        }

        var fixedWeights = new BoneWeight[finVertices.Length];
        const int nearestCount = 8;
        for (int i = 0; i < finVertices.Length; i++)
        {
            float longitudinal = Vector3.Dot(dorsal.transform.TransformPoint(finVertices[i]), axis);
            var nearest = new int[nearestCount];
            var distances = new float[nearestCount];
            for (int n = 0; n < nearestCount; n++) { nearest[n] = -1; distances[n] = float.MaxValue; }
            for (int v = 0; v < positions.Length; v++)
            {
                float distance = Mathf.Abs(longitudinal - positions[v]);
                for (int n = 0; n < nearestCount; n++)
                {
                    if (distance >= distances[n]) continue;
                    for (int k = nearestCount - 1; k > n; k--)
                    {
                        nearest[k] = nearest[k - 1];
                        distances[k] = distances[k - 1];
                    }
                    nearest[n] = v;
                    distances[n] = distance;
                    break;
                }
            }

            var totals = new float[finBones.Length];
            for (int n = 0; n < nearestCount; n++)
            {
                if (nearest[n] < 0) continue;
                float amount = 1f / (0.01f + distances[n]);
                BoneWeight skin = bodyWeights[nearest[n]];
                AccumulateBone(totals, boneMap, skin.boneIndex0, skin.weight0 * amount);
                AccumulateBone(totals, boneMap, skin.boneIndex1, skin.weight1 * amount);
                AccumulateBone(totals, boneMap, skin.boneIndex2, skin.weight2 * amount);
                AccumulateBone(totals, boneMap, skin.boneIndex3, skin.weight3 * amount);
            }

            int[] strongest = Enumerable.Range(0, totals.Length)
                .OrderByDescending(index => totals[index]).Take(4).ToArray();
            float sum = strongest.Sum(index => totals[index]);
            if (sum <= 0.00001f)
                throw new InvalidOperationException("Cannot map " + i + "th dorsal vertex onto the spine.");
            BoneWeight result = new BoneWeight();
            if (strongest.Length > 0) { result.boneIndex0 = strongest[0]; result.weight0 = totals[strongest[0]] / sum; }
            if (strongest.Length > 1) { result.boneIndex1 = strongest[1]; result.weight1 = totals[strongest[1]] / sum; }
            if (strongest.Length > 2) { result.boneIndex2 = strongest[2]; result.weight2 = totals[strongest[2]] / sum; }
            if (strongest.Length > 3) { result.boneIndex3 = strongest[3]; result.weight3 = totals[strongest[3]] / sum; }
            fixedWeights[i] = result;
        }
        return fixedWeights;
    }

    private static void AccumulateBone(float[] totals, int[] sourceToFin, int sourceIndex, float weight)
    {
        if (weight <= 0f || sourceIndex < 0 || sourceIndex >= sourceToFin.Length)
            return;
        int index = sourceToFin[sourceIndex];
        if (index >= 0) totals[index] += weight;
    }

}

public sealed class EmperorAngelfishBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => EmperorAngelfishInstaller.EnsureInstalled();
}
