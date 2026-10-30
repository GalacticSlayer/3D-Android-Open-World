using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The supplied FBX contains one authored swim take. Held motion uses the shared controller.
[InitializeOnLoad]
public static class NapoleonWrasseInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/NapoleonWrasse.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static NapoleonWrasseInstaller()
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
        catch (Exception e) { Debug.LogError("[NAPOLEON WRASSE] Import failed: " + e); }
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

    [MenuItem("Tools/Open World/Rebuild Napoleon Wrasse")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/NapoleonWrasse.fbx") || !File.Exists(Source + "/NapoleonWrasseTexture.png"))
                throw new InvalidOperationException("Napoleon Wrasse source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("NapoleonWrasse", PrefabPath, Source);
            Debug.Log("[NAPOLEON WRASSE] Fish installed with shared animation, mouth anchor and held-fish presentation.");
        }
        finally { installing = false; }
    }
}

public sealed class NapoleonWrasseBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => NapoleonWrasseInstaller.EnsureInstalled();
}
