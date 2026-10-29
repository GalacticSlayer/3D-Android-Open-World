using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The supplied FBX contains one authored swim take. Held motion uses the shared controller.
[InitializeOnLoad]
public static class BlueLinedGrouperInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/BlueLinedGrouper.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static BlueLinedGrouperInstaller()
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
        catch (Exception e) { Debug.LogError("[BLUE LINED GROUPER] Import failed: " + e); }
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
            animator.runtimeAnimatorController.animationClips.Any(c=>c!=null && c.name=="Swim");
    }

    [MenuItem("Tools/Open World/Rebuild Blue Lined Grouper")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/BlueLinedGrouper.fbx") || !File.Exists(Source + "/BlueLinedGrouperTexture.png"))
                throw new InvalidOperationException("Blue Lined Grouper source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("BlueLinedGrouper", PrefabPath, Source);
            Debug.Log("[BLUE LINED GROUPER] Fish installed with shared animation, mouth anchor and held-fish presentation.");
        }
        finally { installing = false; }
    }
}

public sealed class BlueLinedGrouperBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => BlueLinedGrouperInstaller.EnsureInstalled();
}
