using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Automatically imports the player's own Ultraviolet Panther Grouper FBX
/// and original UV texture using the same proven Panther Grouper pipeline.
/// The prefab, authored atlas, material and animation controller are generated
/// from files already in the GitHub repository, without a manual import step.
/// </summary>
[InitializeOnLoad]
public static class UltravioletPantherGrouperInstaller
{
    private const string Source = "Assets/_Game/Reef/Source";
    private const string Model = Source + "/UltravioletPantherGrouper.fbx";
    private const string Texture = Source + "/UltravioletPantherGrouperTexture.png";
    private const string Prefab = "Assets/Resources/Fishing/UltravioletPantherGrouper.prefab";
    private static bool installing;

    static UltravioletPantherGrouperInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { EnsureInstalled(); }
            catch (Exception e)
            {
                EditorApplication.isPlaying = false;
                Debug.LogException(e);
            }
        };
    }

    private static void AutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoInstall;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { EnsureInstalled(); }
        catch (Exception e) { Debug.LogError("[ULTRAVIOLET PANTHER GROUPER] Import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing || Ready()) return;
        Rebuild();
    }

    private static bool Ready()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (prefab == null || prefab.GetComponent<RedSnapperPresentation>() == null) return false;
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null ||
            !animator.runtimeAnimatorController.animationClips.Any(c => c != null && c.name == "Swim"))
            return false;
        if (!prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(s => s.sharedMesh != null && s.sharedMesh.vertexCount > 0 &&
                          s.sharedMesh.uv.Length == s.sharedMesh.vertexCount))
            return false;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || !material.HasProperty("_BaseMap") ||
                    material.GetTexture("_BaseMap") == null)
                    return false;
        return prefab.GetComponentsInChildren<Transform>(true)
            .Any(t => t.name == "AuthoredMouthAnchor");
    }

    [MenuItem("Tools/Open World/Rebuild Ultraviolet Panther Grouper")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Model) || !File.Exists(Texture))
                throw new FileNotFoundException("Pull the Ultraviolet Panther Grouper FBX and PNG from GitHub.");
            SeaBassImporter.InstallModel("UltravioletPantherGrouper", Prefab, Source);
            if (!Ready())
                throw new InvalidOperationException("The authored fish animation, mesh, UV atlas or mouth anchor was not installed.");
            Debug.Log("[ULTRAVIOLET PANTHER GROUPER] Source model, texture, swim animation and held presentation installed.");
        }
        finally { installing = false; }
    }
}

public sealed class UltravioletPantherGrouperBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => UltravioletPantherGrouperInstaller.EnsureInstalled();
}
