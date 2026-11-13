using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The original FBX and UV texture are checked in. Blender is not required by Unity.
[InitializeOnLoad]
public static class SpanishMackerelImporter
{
    const string Name = "SpanishMackerel";
    const string Source = "Assets/_Game/Reef/Source";
    const string PrefabPath = "Assets/Resources/Fishing/SpanishMackerel.prefab";

    static SpanishMackerelImporter()
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
        if (!Ready(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))) Install();
    }

    [MenuItem("Tools/Open World/Import Spanish Mackerel (One Click)")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before importing Spanish Mackerel.");

        if (!File.Exists(Source + "/" + Name + ".fbx") ||
            !File.Exists(Source + "/" + Name + "Texture.png"))
            throw new FileNotFoundException("Spanish Mackerel FBX or PNG is missing. Pull the complete update.");

        SeaBassImporter.InstallModel(Name, PrefabPath, Source, preserveWorldUp: true,
            swimTakeContains: "ArmatureAction.001");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!Ready(prefab))
            throw new InvalidDataException("Spanish Mackerel prefab is missing its rig, moving swim clip, UV texture or mouth anchor.");

        FishingTuning.Reload();
        if (!FishingTuning.IsValid) throw new InvalidDataException(FishingTuning.ValidationError);
        Debug.Log("[SPANISH MACKEREL] Authored rig, UV atlas, swim animation and held-fish mouth anchor installed.");
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

public sealed class SpanishMackerelBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => -900;
    public void OnPreprocessBuild(BuildReport report)
    {
        try { SpanishMackerelImporter.EnsureInstalled(); }
        catch (Exception e) { throw new BuildFailedException("Spanish Mackerel import failed: " + e.Message); }
    }
}
