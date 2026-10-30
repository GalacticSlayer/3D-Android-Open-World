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
        if(material==null)throw new InvalidOperationException("White Skate material missing at " + Source + "/WhiteSkate.mat");
        var shader=AuthoredLureReplacementImporter.LoadSurfaceShader();
        var texture=LoadMetallicRoughness();
        if(material.shader==shader && material.GetTexture("_MetallicRoughnessMap")==texture &&
            material.IsKeywordEnabled("_AUTHORED_METALLIC_ROUGHNESS"))return;
        material.shader=shader;material.SetTexture("_MetallicRoughnessMap",texture);
        material.EnableKeyword("_AUTHORED_METALLIC_ROUGHNESS");
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
    }

    private static Texture2D LoadMetallicRoughness()
    {
        // Like SeaBassImporter.BuildAtlas, decode the supplied PNG directly.
        // A TextureImporter can exist while its imported Texture2D is unavailable.
        // Persist a native asset so the material also works in the built player.
        const string sourcePath = Source + "/WhiteSkateMetallicRoughness.png";
        const string assetPath = Source + "/WhiteSkateMetallicRoughness.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (saved != null && saved.width == 4096 && saved.height == 4096)
            return saved;
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("White Skate metallic/roughness PNG is missing. Pull the complete update.", sourcePath);
        // Data channels must stay linear: G = roughness, B = metallic.
        var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, true, true);
        try
        {
            if (!ImageConversion.LoadImage(decoded, File.ReadAllBytes(sourcePath), false) ||
                decoded.width != 4096 || decoded.height != 4096)
                throw new InvalidOperationException("Cannot decode White Skate metallic/roughness PNG at " + sourcePath);
            decoded.name = "White Skate Metallic Roughness";
            decoded.wrapMode = TextureWrapMode.Repeat;
            decoded.filterMode = FilterMode.Trilinear;
            decoded.anisoLevel = 2;
            decoded.Apply(true, false);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(decoded, assetPath);
                saved = decoded;
                decoded = null;
            }
            else EditorUtility.CopySerialized(decoded, saved);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssets();
            saved = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (saved == null || saved.width != 4096 || saved.height != 4096)
                throw new InvalidOperationException("Could not save White Skate metallic/roughness texture at " + assetPath);
            return saved;
        }
        finally { if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded); }
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
