using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Installs both supplied, already-UV-mapped and animated five-bone FBXs.
/// Prefabs use the same presentation, swim and holding behavior as Black Marlin.
/// Source files may be placed directly in Assets/_Game/Reef/Source or
/// provided from ArtSources/MahiMahi or Downloads.
/// </summary>
public static class MahiMahiImporter
{
    const string Source = "Assets/_Game/Reef/Source";
    const string Prefabs = "Assets/Resources/Fishing";
    const string SessionKey = "OpenWorld.AutoImport.MahiMahi.20261109.v1";
    static readonly string[] Names = { "MahiMahi", "BlueMahiMahi" };
    static readonly string[] SourceFbxNames = { "Mahi Mahi.fbx", "Blue Mahi Mahi.fbx" };
    static readonly string[] SourcePngNames = { "Mahi Mahi Texture.png", "Blue Mahi Mahi Texture.png" };

    [InitializeOnLoadMethod]
    static void AutoImport()
    {
        if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Ready()) return;
            try
            {
                if (Prepared() || CopyFromKnownLocations(false)) InstallPrepared();
            }
            catch (Exception e) { Debug.LogError("[MAHI MAHI] Automatic import failed: " + e); }
        };
    }

    [MenuItem("Tools/Open World/Import Mahi Mahi + Blue Variant (One Click)")]
    public static void OneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Mahi Mahi", "Exit Play Mode before importing.", "OK");
            return;
        }
        try
        {
            if (!Prepared() && !CopyFromKnownLocations(true))
                throw new FileNotFoundException("Both original FBX files and their PNG textures are required.");
            InstallPrepared();
            EditorUtility.DisplayDialog("Mahi Mahi Ready",
                "Both animated variants are installed. Each has its own prefab, atlas, and saved catch ID.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Mahi Mahi Import Failed", e.Message, "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static bool Ready()
    {
        return Names.All(name =>
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab");
            return go != null && go.GetComponent<RedSnapperPresentation>() != null
                && go.GetComponentInChildren<Animator>(true) != null
                && go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0
                && go.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AuthoredMouthAnchor");
        });
    }

    static bool Prepared()
    {
        return Names.All(name => File.Exists(Source + "/" + name + ".fbx")
            && File.Exists(Source + "/" + name + "Texture.png"));
    }

    static bool CopyFromKnownLocations(bool prompt)
    {
        var originals = new string[2];
        var textures = new string[2];
        for (int i = 0; i < Names.Length; i++)
        {
            originals[i] = Find(SourceFbxNames[i]);
            textures[i] = Find(SourcePngNames[i]);
            if (prompt && originals[i] == null)
                originals[i] = EditorUtility.OpenFilePanel("Select " + SourceFbxNames[i],
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fbx");
            if (prompt && textures[i] == null)
                textures[i] = EditorUtility.OpenFilePanel("Select " + SourcePngNames[i],
                    originals[i] != null ? Path.GetDirectoryName(originals[i]) :
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "png");
            if (string.IsNullOrEmpty(originals[i]) || string.IsNullOrEmpty(textures[i])) return false;
        }

        EnsureFolder(Source);
        for (int i = 0; i < Names.Length; i++)
        {
            CopyIfDifferent(originals[i], Source + "/" + Names[i] + ".fbx");
            CopyIfDifferent(textures[i], Source + "/" + Names[i] + "Texture.png");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return Prepared();
    }

    static void InstallPrepared()
    {
        if (!Prepared()) throw new FileNotFoundException("Mahi Mahi models or textures not found in " + Source);
        EnsureFolder(Prefabs);
        for (int i = 0; i < Names.Length; i++)
        {
            string name = Names[i];
            EditorUtility.DisplayProgressBar("Mahi Mahi", "Importing " + name + "...", (i + .25f) / 2f);
            string candidate = Source + "/" + name + "_ImportCandidate.prefab";
            string prefabPath = Prefabs + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(candidate) != null)
                AssetDatabase.DeleteAsset(candidate);
            SeaBassImporter.InstallModel(name, candidate, Source, Source + "/" + name + "Texture.png");
            Verify(AssetDatabase.LoadAssetAtPath<GameObject>(candidate), name);
            var root = PrefabUtility.LoadPrefabContents(candidate);
            try
            {
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException("Could not write " + prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.DeleteAsset(candidate);
            Verify(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), name);
        }
        AssetDatabase.SaveAssets();
        FishingTuning.Reload();
        Debug.Log("[MAHI MAHI] Both original skinned, animated UV-mapped variants imported successfully.");
    }

    static void Verify(GameObject prefab, string name)
    {
        if (prefab == null || prefab.GetComponent<RedSnapperPresentation>() == null)
            throw new InvalidDataException(name + ": fish presentation missing.");
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
            throw new InvalidDataException(name + ": animation controller missing.");
        var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skins.Length == 0 || !skins.Any(s => s.sharedMesh != null && s.bones.Length >= 5))
            throw new InvalidDataException(name + ": five-bone skinned mesh missing.");
        if (skins.Any(s => s.sharedMesh == null || s.sharedMesh.uv.Length != s.sharedMesh.vertexCount))
            throw new InvalidDataException(name + ": authored UV map missing.");
        if (!prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AuthoredMouthAnchor"))
            throw new InvalidDataException(name + ": animated mouth anchor missing.");
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material mat in renderer.sharedMaterials)
                if (mat == null || !mat.HasProperty("_BaseMap") || mat.GetTexture("_BaseMap") == null)
                    throw new InvalidDataException(name + ": authored texture not assigned.");
        bool animated = animator.runtimeAnimatorController.animationClips.Any(clip =>
            clip != null && clip.length > 0f &&
            AnimationUtility.GetCurveBindings(clip).Any(binding =>
            {
                if (binding.type != typeof(Transform)) return false;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                return curve != null && curve.length > 1 &&
                    curve.keys.Any(key => Mathf.Abs(key.value - curve.keys[0].value) > .00001f);
            }));
        if (!animated) throw new InvalidDataException(name + ": FBX has no moving skeletal animation curves.");
    }

    static void CopyIfDifferent(string from, string to)
    {
        from = Path.GetFullPath(from); to = Path.GetFullPath(to);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(to));
        var bytes = File.ReadAllBytes(from);
        if (!File.Exists(to) || !bytes.SequenceEqual(File.ReadAllBytes(to))) File.WriteAllBytes(to, bytes);
    }

    static string Find(string filename)
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots =
        {
            Path.Combine(project, "ArtSources", "MahiMahi"),
            Path.Combine(project, "ArtSources"), project,
            Path.Combine(home, "Downloads"), Path.Combine(home, "OneDrive", "Downloads"),
            Path.Combine(project, "SourceAssets")
        };
        foreach (string root in roots)
        {
            if (!Directory.Exists(root)) continue;
            string direct = Path.Combine(root, filename);
            if (File.Exists(direct)) return direct;
            try
            {
                foreach (string folder in Directory.EnumerateDirectories(root))
                {
                    direct = Path.Combine(folder, filename);
                    if (File.Exists(direct)) return direct;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
