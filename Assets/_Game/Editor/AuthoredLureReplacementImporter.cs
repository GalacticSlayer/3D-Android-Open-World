using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Installs pre-exported authored geometry/UVs/actions; no Blender or user setup required.</summary>
[InitializeOnLoad]
public static class AuthoredLureReplacementImporter
{
    private static readonly string[] Names = { "HeavyJig", "LightJig", "Popper" };
    private static bool importing;
    [Serializable] private sealed class Export { public int version; public float duration, fps; public Vector3 lineAttach; public Part[] parts; }
    [Serializable] private sealed class Part { public string name; public Vector3[] vertices, normals; public Vector2[] uv; public int[] triangles; public Frame[] frames; public Color color; public float metallic, smoothness; }
    [Serializable] private sealed class Frame { public float time; public Vector3 position; public Quaternion rotation; }

    static AuthoredLureReplacementImporter()
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
        catch (Exception e) { Debug.LogError("[AUTHORED LURES] Install failed: " + e); }
    }

    [MenuItem("Tools/Open World/Rebuild Authored Lures")]
    public static void Rebuild() { EnsureInstalled(true); }

    public static void EnsureInstalled(bool force = false)
    {
        foreach (string name in Names) Install(name, force);
    }

    private static void Install(string name, bool force)
    {
        string Folder = "Assets/_Game/Fishing/" + name;
        string Prefab = "Assets/Resources/Fishing/" + name + ".prefab";
        if (importing) return;
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (!force && Ready(existing, name == "Popper" ? 3 : 2)) return;
        importing = true;
        GameObject root = null;
        try
        {
            string generated = Folder + "/Generated";
            if (!AssetDatabase.IsValidFolder(generated)) AssetDatabase.CreateFolder(Folder, "Generated");
            string albedoPath = PrepareAlbedo(Folder, generated);
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/AuthoredLure.json");
            ConfigureTexture(albedoPath, true);
            ConfigureTexture(Folder + "/MetallicRoughness.png", false);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D metallicRoughness = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/MetallicRoughness.png");
            if (source == null || texture == null || metallicRoughness == null) throw new InvalidDataException("Authored Lures source or texture is missing.");
            Export data = JsonUtility.FromJson<Export>(source.text);
            if (data.version != 1 || data.parts == null || data.parts.Length != (name == "Popper" ? 3 : 2))
                throw new InvalidDataException("Unexpected Authored Lures export.");
            Shader shader = Resources.Load<Shader>("Fishing/AuthoredLure");
            if (shader == null) throw new InvalidOperationException("AuthoredLure shader is missing.");
            root = new GameObject(name);
            Transform model = Child(root.transform, "AuthoredModel", Vector3.zero, Quaternion.identity);
            AnimationClip clip = new AnimationClip { name = name + "Retrieve", frameRate = data.fps, legacy = false };
            foreach (Part part in data.parts)
            {
                if (part.vertices.Length != part.uv.Length || part.vertices.Length != part.normals.Length || part.frames.Length < 2)
                    throw new InvalidDataException("Invalid authored mesh/animation for " + part.name);
                Mesh mesh = new Mesh { name = part.name, vertices = part.vertices, normals = part.normals,
                    uv = part.uv, triangles = part.triangles };
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                mesh = Save(mesh, generated + "/" + part.name + ".asset");
                Transform piece = Child(model, part.name, part.frames[0].position, part.frames[0].rotation);
                piece.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                Material material = Save(new Material(shader) { name = name + part.name }, generated + "/" + part.name + ".mat");
                material.SetColor("_BaseColor", part.color);
                material.SetFloat("_Metallic", part.metallic);
                material.SetFloat("_Smoothness", part.smoothness);
                if (part.name == "Body")
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTexture("_MetallicRoughnessMap", metallicRoughness);
                    material.EnableKeyword("_AUTHORED_METALLIC_ROUGHNESS");
                }
                EditorUtility.SetDirty(material);
                piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                for (int axis = 0; axis < 3; axis++)
                {
                    int component = axis;
                    SetCurve(clip, part, "m_LocalPosition." + "xyz"[axis], f => f.position[component]);
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    int component = axis;
                    SetCurve(clip, part, "m_LocalRotation." + "xyzw"[axis], f => f.rotation[component]);
                }
                if (part.name == "Body")
                {
                    // Export is relative to this body's pivot; anchor follows its authored motion.
                    Child(piece, "LineAttach", data.lineAttach, Quaternion.identity);
                    Child(piece, "RetrieveFrame", Vector3.zero, Quaternion.identity);
                    // Authored local +Z is upright; show the textured side in every UI preview.
                    Child(piece, "PreviewFrame", Vector3.zero, Quaternion.identity);
                }
            }
            clip.EnsureQuaternionContinuity();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.startTime = 0f; settings.stopTime = data.duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip = Save(clip, generated + "/Retrieve.anim");
            string controllerPath = generated + "/Retrieve.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Length > 0 ? machine.states[0].state : machine.AddState("Retrieve");
            state.motion = clip; state.speed = 1f; machine.defaultState = state;
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
            Animator animator = model.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            AssetDatabase.SaveAssets();
            if (!Ready(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), data.parts.Length))
                throw new InvalidOperationException("Authored Lures prefab failed validation.");
            Debug.Log("[AUTHORED LURES] Installed " + name + " with packed colour/metallic/roughness maps, original UVs and authored body/hook animation. Gameplay slots retained.");
        }
        finally { if (root != null) Object.DestroyImmediate(root); importing = false; }
    }

    // Split PNG bytes avoid transport limits without changing a single texture pixel.
    private static string PrepareAlbedo(string folder, string generated)
    {
        string first = folder + "/Albedo.part0.bytes";
        if (!File.Exists(first)) return folder + "/Albedo.png";
        string output = generated + "/Albedo.png";
        using (var bytes = new MemoryStream())
        {
            for (int i = 0; i < 2; i++)
            {
                byte[] part = File.ReadAllBytes(folder + "/Albedo.part" + i + ".bytes");
                bytes.Write(part, 0, part.Length);
            }
            byte[] png = bytes.ToArray();
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                string digest = BitConverter.ToString(hash.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
                if (digest != "5cebb95ed4c164c864f8b173366f8215a7d9482afae048340a7c8e1484bceb43")
                    throw new InvalidDataException("Popper texture parts are incomplete or corrupt. Pull the complete update.");
            }
            File.WriteAllBytes(output, png);
        }
        AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
        return output;
    }

    private static void ConfigureTexture(string path, bool srgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidDataException("Missing packed texture: " + path);
        if (importer.sRGBTexture == srgb && importer.wrapMode == TextureWrapMode.Repeat &&
            importer.filterMode == FilterMode.Bilinear && importer.maxTextureSize == 4096) return;
        importer.sRGBTexture = srgb;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    private static bool Ready(GameObject prefab, int count)
    {
        if (prefab == null) return false;
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null ||
            animator.runtimeAnimatorController.animationClips.Length == 0) return false;
        MeshFilter[] meshes = prefab.GetComponentsInChildren<MeshFilter>(true);
        return meshes.Length == count && meshes.All(m => m.sharedMesh != null) &&
            prefab.transform.Find("AuthoredModel/Body/LineAttach") != null &&
            prefab.transform.Find("AuthoredModel/Body/RetrieveFrame") != null &&
            prefab.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterial != null);
    }

    private static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false); child.localPosition = position; child.localRotation = rotation;
        return child;
    }

    private static T Save<T>(T value, string path) where T : Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
        EditorUtility.CopySerialized(value, existing); Object.DestroyImmediate(value);
        EditorUtility.SetDirty(existing); return existing;
    }

    private static void SetCurve(AnimationClip clip, Part part, string property, Func<Frame, float> value)
    {
        AnimationCurve curve = new AnimationCurve(part.frames.Select(f => new Keyframe(f.time, value(f))).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(part.name, typeof(Transform), property), curve);
    }
}

// Build & Run also installs/validates in batch mode before collecting Resources.
public sealed class AuthoredLureReplacementBuildPreparation : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report) { AuthoredLureReplacementImporter.EnsureInstalled(); }
}
