using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Supplied generic takes use the existing indexed swim/held-struggle convention.
[InitializeOnLoad]
public static class NassauGrouperInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/NassauGrouper.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static NassauGrouperInstaller()
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
        catch (Exception e) { Debug.LogError("[NASSAU GROUPER] Import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        if (Ready(PrefabPath)) return;
        Rebuild();
    }

    private static bool Ready(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null || prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        var animator=prefab.GetComponentInChildren<Animator>(true);
        return animator!=null && animator.runtimeAnimatorController!=null &&
            animator.runtimeAnimatorController.animationClips.Any(c=>c!=null && c.name=="Swim") &&
            animator.runtimeAnimatorController.animationClips.Any(c=>c!=null && c.name==CaughtFishFlop.HeldStruggleStateName);
    }

    [MenuItem("Tools/Open World/Rebuild Nassau Grouper")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/NassauGrouper.fbx") || !File.Exists(Source + "/NassauGrouperTexture.png"))
                throw new InvalidOperationException("Nassau Grouper source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("NassauGrouper", PrefabPath, Source, importHeldStruggle:true, useIndexedSwimAndStruggle:true);
            Debug.Log("[NASSAU GROUPER] Fish installed with shared animation, mouth anchor and held-fish presentation.");
        }
        finally { installing = false; }
    }
}

public sealed class NassauGrouperBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => NassauGrouperInstaller.EnsureInstalled();
}
