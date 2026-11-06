using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the user's actual Black Marlin.fbx and Black Marlin Texture.png from
/// Downloads (or project root). Reuses the proven five-bone SeaBassImporter:
/// animated swim, train-track aquarium steering, caught-fish flop, and authored
/// head-bone mouth anchor. Does NOT require Blender or regenerate the original.
/// </summary>
public static class BlackMarlinImporter
{
    const string SourceFolder = "Assets/_Game/Reef/Source";
    const string FbxPath = SourceFolder + "/BlackMarlin.fbx";
    const string TexturePath = SourceFolder + "/BlackMarlinTexture.png";
    const string PrefabPath = "Assets/Resources/Fishing/BlackMarlin.prefab";
    const string CandidatePath = SourceFolder + "/BlackMarlin_ImportCandidate.prefab";
    const string SessionKey = "OpenWorld.AutoImport.BlackMarlin.20261106.v1";

    [InitializeOnLoadMethod]
    static void AutoImport()
    {
        if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (IsReady(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))) return;
                string fbx = Find("Black Marlin.fbx");
                string texture = Find("Black Marlin Texture.png", fbx != null ? Path.GetDirectoryName(fbx) : null);
                if (fbx == null || texture == null)
                {
                    Debug.LogWarning("[BLACK MARLIN] Downloaded FBX and texture not found. Put both original files in Downloads, or use Tools > Open World > Import Black Marlin (One Click) to select them.");
                    return;
                }
                Import(fbx, texture);
            }
            catch (Exception e) { Debug.LogError("[BLACK MARLIN] Import failed: " + e); }
        };
    }

    [MenuItem("Tools/Open World/Import Black Marlin (One Click)")]
    public static void ImportOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Black Marlin Import", "Exit Play Mode first.", "OK");
            return;
        }
        try
        {
            string fbx = Find("Black Marlin.fbx");
            if (fbx == null)
                fbx = EditorUtility.OpenFilePanel("Select the Black Marlin.fbx supplied with the fish", UserHome(), "fbx");
            if (string.IsNullOrEmpty(fbx)) return;
            string png = Find("Black Marlin Texture.png", Path.GetDirectoryName(fbx));
            if (png == null)
                png = EditorUtility.OpenFilePanel("Select the Black Marlin Texture.png", Path.GetDirectoryName(fbx), "png");
            if (string.IsNullOrEmpty(png)) return;
            Import(fbx, png);
            EditorUtility.DisplayDialog("Black Marlin Ready",
                "The authored skinned model, swim animation and texture are installed. Fish index, catching, holding and aquariums use its prefab.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Black Marlin Import Failed", e.Message + "\nSee Console for details.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static void Import(string sourceFbx, string sourceTexture)
    {
        if (!File.Exists(sourceFbx) || !File.Exists(sourceTexture))
            throw new FileNotFoundException("Both Black Marlin FBX and texture must exist.");
        if (new FileInfo(sourceFbx).Length < 4096L)
            throw new InvalidDataException("Black Marlin FBX is empty or damaged.");
        EnsureFolder(SourceFolder);
        EnsureFolder("Assets/Resources/Fishing");
        EditorUtility.DisplayProgressBar("Black Marlin", "Installing the original FBX and texture...", .15f);
        CopyIfDifferent(sourceFbx, FbxPath);
        CopyIfDifferent(sourceTexture, TexturePath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        EditorUtility.DisplayProgressBar("Black Marlin", "Importing the five-bone animated rig...", .45f);
        // Build at a staging path first; a failed animation or UV check can
        // never replace a previously working Black Marlin gameplay prefab.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePath) != null)
            AssetDatabase.DeleteAsset(CandidatePath);
        SeaBassImporter.InstallModel("BlackMarlin", CandidatePath, SourceFolder, TexturePath);
        GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePath);
        Verify(candidate);
        EditorUtility.DisplayProgressBar("Black Marlin", "Saving verified gameplay prefab...", .85f);
        GameObject root = PrefabUtility.LoadPrefabContents(CandidatePath);
        try
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("Could not save the Black Marlin prefab.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.DeleteAsset(CandidatePath);
        AssetDatabase.SaveAssets();
        Verify(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        FishingTuning.Reload();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[BLACK MARLIN] Verified skinned rig, moving swim curves, matching atlas and head-bone mouth. Saved " + PrefabPath);
    }

    static void Verify(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponent<RedSnapperPresentation>() == null)
            throw new InvalidDataException("Black Marlin must use the shared animated fish presentation.");
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
            throw new InvalidDataException("Black Marlin FBX has no usable Animator/controller.");
        var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skins.Length == 0 || !skins.Any(s => s.sharedMesh != null && s.bones.Length >= 5))
            throw new InvalidDataException("Black Marlin FBX must retain its weighted five-bone skinned mesh.");
        if (skins.Any(s => s.sharedMesh == null || s.sharedMesh.uv == null ||
            s.sharedMesh.uv.Length != s.sharedMesh.vertexCount))
            throw new InvalidDataException("Black Marlin skinned meshes are missing the authored UV atlas.");
        if (!prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AuthoredMouthAnchor"))
            throw new InvalidDataException("Black Marlin mouth anchor was not created on its animated head.");
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || !material.HasProperty("_BaseMap") ||
                    material.GetTexture("_BaseMap") == null)
                    throw new InvalidDataException("Black Marlin's authored UV texture is not assigned.");
        bool animated = false;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null || clip.length <= 0f) continue;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform)) continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;
                float first = curve.keys[0].value;
                if (curve.keys.Any(key => Mathf.Abs(key.value - first) > .00001f))
                { animated = true; break; }
            }
            if (animated) break;
        }
        if (!animated)
            throw new InvalidDataException("Black Marlin has an Animator but no moving bone transform curves. Re-export the authored rig/animation with animation baking enabled.");
    }

    static void CopyIfDifferent(string source, string destination)
    {
        string to = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(to));
        string from = Path.GetFullPath(source);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return;
        var bytes = File.ReadAllBytes(from);
        if (File.Exists(to) && bytes.SequenceEqual(File.ReadAllBytes(to))) return;
        File.WriteAllBytes(to, bytes);
    }

    static string UserHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static string Find(string filename, string preferred = null)
    {
        string home = UserHome();
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var roots = new[]
        {
            preferred, Path.Combine(home, "Downloads"),
            Path.Combine(home, "OneDrive", "Downloads"), project,
            Path.Combine(project, "SourceAssets"), Path.Combine(project, "ArtSources"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };
        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            string direct = Path.Combine(root, filename);
            if (File.Exists(direct)) return direct;
            // Search immediate subfolders and one nested level only. Avoid
            // expensive scanning across the user's whole drive.
            try
            {
                foreach (string sub in Directory.EnumerateDirectories(root))
                {
                    direct = Path.Combine(sub, filename);
                    if (File.Exists(direct)) return direct;
                    foreach (string nested in Directory.EnumerateDirectories(sub))
                    {
                        direct = Path.Combine(nested, filename);
                        if (File.Exists(direct)) return direct;
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return null;
    }

    static bool IsReady(GameObject prefab) =>
        prefab != null &&
        prefab.GetComponent<RedSnapperPresentation>() != null &&
        prefab.GetComponentInChildren<Animator>(true) != null &&
        prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0 &&
        prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AuthoredMouthAnchor");

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
