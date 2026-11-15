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
public static class StripedMarlinImporter
{
    const string Name = "StripedMarlin";
    const string Source = "Assets/_Game/Reef/Source";
    const string SourceModel = Source + "/" + Name + ".fbx";
    const string SourceTexture = Source + "/" + Name + "Texture.png";
    const string PrefabPath = "Assets/Resources/Fishing/StripedMarlin.prefab";

    static StripedMarlinImporter()
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

    [MenuItem("Tools/Open World/Import Striped Marlin (One Click)")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before importing Striped Marlin.");

        var signature = SourceSignature();
        SeaBassImporter.InstallModel(Name, PrefabPath, Source, preserveWorldUp: true,
            swimTakeContains: "ArmatureAction", discardEmptyRenderers: true);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!Ready(prefab))
            throw new InvalidDataException("Striped Marlin prefab is missing its rig, moving swim clip, UV texture or mouth anchor.");

        PlaceMouthBehindBill();
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
        Debug.Log("[STRIPED MARLIN] Authored rig, UV atlas, swim animation and held-fish mouth anchor installed.");
    }

    // The shared importer finds the tip of the bill. On the authored mesh,
    // the mouth opening sits about 0.15 m back and 0.025 m below that point
    // at the shared 0.92 m prefab scale. Keep the anchor parented to the head.
    static void PlaceMouthBehindBill()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var mouth = root.GetComponentsInChildren<Transform>(true)
                .First(t => t.name == "AuthoredMouthAnchor");
            var local = root.transform.InverseTransformPoint(mouth.position);
            local.z -= .15f;
            local.y -= .025f;
            mouth.position = root.transform.TransformPoint(local);
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidDataException("Could not save the Striped Marlin mouth anchor.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string SourceSignature()
    {
        if (!File.Exists(SourceModel) || !File.Exists(SourceTexture))
            throw new FileNotFoundException("Striped Marlin FBX or PNG is missing. Pull the complete update.");

        using (var sha = SHA256.Create())
        using (var model = File.OpenRead(SourceModel))
        using (var texture = File.OpenRead(SourceTexture))
            return "StripedMarlin:" + Convert.ToBase64String(sha.ComputeHash(model)) + ":" +
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

public sealed class StripedMarlinBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => -900;
    public void OnPreprocessBuild(BuildReport report)
    {
        try { StripedMarlinImporter.EnsureInstalled(); }
        catch (Exception e) { throw new BuildFailedException("Striped Marlin import failed: " + e.Message); }
    }
}
