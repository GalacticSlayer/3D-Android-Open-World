using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The original FBX and UV texture are checked in. Blender is not required by Unity.
[InitializeOnLoad]
public static class QueenslandGrouperImporter
{
    const string Name = "QueenslandGrouper";
    const string Source = "Assets/_Game/Reef/Source";
    const string SourceModel = Source + "/" + Name + ".fbx";
    const string SourceTexture = Source + "/" + Name + "Texture.png";
    const string PrefabPath = "Assets/Resources/Fishing/QueenslandGrouper.prefab";

    static QueenslandGrouperImporter()
    {
        if (!Application.isBatchMode) EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { EnsureInstalled(); }
            catch (Exception e) { EditorApplication.isPlaying = false; Debug.LogException(e); }
        };
    }

    static void AutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoInstall;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { EnsureInstalled(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void EnsureInstalled()
    {
        var importer = AssetImporter.GetAtPath(PrefabPath);
        if (importer == null || importer.userData != SourceSignature() ||
            !Ready(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)))
            Install();
    }

    [MenuItem("Tools/Open World/Import Queensland Grouper (One Click)")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before importing Queensland Grouper.");

        var signature = SourceSignature();
        SeaBassImporter.InstallModel(Name, PrefabPath, Source, preserveWorldUp: true,
            swimTakeContains: "ArmatureAction.002", discardEmptyRenderers: true);

        CenterMouthAnchor();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!Ready(prefab))
            throw new InvalidDataException("Queensland Grouper prefab is missing its rig, moving swim clip, UV texture or mouth anchor.");

        FishingTuning.Reload();
        if (!FishingTuning.IsValid) throw new InvalidDataException(FishingTuning.ValidationError);

        // The generated prefab unpacks the FBX, and its atlas is a copy of the PNG.
        // A valid older prefab can therefore survive a source reimport and needs rebuilding.
        var importer = AssetImporter.GetAtPath(PrefabPath);
        if (importer.userData != signature)
        {
            importer.userData = signature;
            importer.SaveAndReimport();
        }
        Debug.Log("[QUEENSLAND GROUPER] Authored rig, UV atlas, swim animation and held-fish mouth anchor installed.");
    }

    // The shared importer anchors to the front of the lower lip. Move to
    // the measured midpoint between the jaws on the normalized 0.92 m mesh.
    static void CenterMouthAnchor()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var mouth = root.GetComponentsInChildren<Transform>(true)
                .First(t => t.name == "AuthoredMouthAnchor");
            var tip = root.transform.InverseTransformPoint(mouth.position);
            mouth.position = root.transform.TransformPoint(tip + new Vector3(0f, .0088f, -.02f));
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidDataException("Could not save the Queensland Grouper mouth anchor.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string SourceSignature()
    {
        if (!File.Exists(SourceModel) || !File.Exists(SourceTexture))
            throw new FileNotFoundException("Queensland Grouper FBX or PNG is missing. Pull the complete update.");

        using (var sha = SHA256.Create())
        using (var model = File.OpenRead(SourceModel))
        using (var texture = File.OpenRead(SourceTexture))
            return "QueenslandGrouper:" + Convert.ToBase64String(sha.ComputeHash(model)) + ":" +
                Convert.ToBase64String(sha.ComputeHash(texture));
    }

    static bool Ready(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponent<RedSnapperPresentation>() == null ||
            !prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AuthoredMouthAnchor"))
            return false;

        var animator = prefab.GetComponentInChildren<Animator>(true);
        var clip = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name == "Swim")
            : null;
        if (clip == null || clip.length <= 0f) return false;

        var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (!skins.Any(s => s.sharedMesh != null && s.bones.Length >= 5) ||
            skins.Any(s => s.sharedMesh == null || s.sharedMesh.uv.Length != s.sharedMesh.vertexCount ||
                s.sharedMaterials.Length == 0 ||
                s.sharedMaterials.Any(m => m == null || !m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") == null)))
            return false;

        return AnimationUtility.GetCurveBindings(clip).Any(binding =>
        {
            if (binding.type != typeof(Transform)) return false;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length < 2) return false;
            var keys = curve.keys;
            return keys.Any(key => Mathf.Abs(key.value - keys[0].value) > .00001f);
        });
    }
}

public sealed class QueenslandGrouperBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => -900;
    public void OnPreprocessBuild(BuildReport report)
    {
        try { QueenslandGrouperImporter.EnsureInstalled(); }
        catch (Exception e) { throw new BuildFailedException("Queensland Grouper import failed: " + e.Message); }
    }
}
