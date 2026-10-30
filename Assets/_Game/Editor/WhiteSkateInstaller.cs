using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The rig is already exported and rolled onto its pale side. No Blender install
// is required after git pull; use the shared importer for every presentation path.
[InitializeOnLoad]
public static class WhiteSkateInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/WhiteSkate.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static WhiteSkateInstaller()
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
        catch (Exception e) { Debug.LogError("[WHITE SKATE] Import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<RedSnapperPresentation>() != null)
        {
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && clip.name == CaughtFishFlop.HeldStruggleStateName) { ApplySurface(); return; }
        }
        Rebuild();
    }

    private static void ApplySurface()
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(Source + "/WhiteSkate.mat");
        string path=Source + "/WhiteSkateMetallicRoughness.png";
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(material==null)throw new InvalidOperationException("White Skate material missing at " + Source + "/WhiteSkate.mat");
        if(importer==null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            importer=AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if(importer==null)throw new InvalidOperationException("White Skate texture importer missing at " + path);
        if(importer.sRGBTexture || importer.wrapMode!=TextureWrapMode.Repeat)
        { importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport(); }
        var shader=AuthoredLureReplacementImporter.LoadSurfaceShader();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        if(texture==null)throw new InvalidOperationException("White Skate texture could not be loaded at " + path);
        if(material.shader==shader && material.GetTexture("_MetallicRoughnessMap")==texture &&
            material.IsKeywordEnabled("_AUTHORED_METALLIC_ROUGHNESS"))return;
        material.shader=shader;material.SetTexture("_MetallicRoughnessMap",texture);
        material.EnableKeyword("_AUTHORED_METALLIC_ROUGHNESS");
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Open World/Rebuild White Skate")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/WhiteSkate.fbx") || !File.Exists(Source + "/WhiteSkateTexture.png"))
                throw new InvalidOperationException("White Skate source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("WhiteSkate", PrefabPath, Source, preserveWorldUp:true, importHeldStruggle:true);
            ApplySurface();
            Debug.Log("[WHITE SKATE] Textured ray installed with swimming and player-held struggle animations.");
        }
        finally { installing = false; }
    }
}

public sealed class WhiteSkateBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => WhiteSkateInstaller.EnsureInstalled();
}
