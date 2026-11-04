#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Level 7 rod/reel prefabs from the player's UV-mapped FBXs. Copies the
/// proven Level 6 rigs, rod tip, animation hierarchy and reel action while
/// replacing only the authored visible meshes and their shared texture.
/// The Resources paths work in first-person, third-person, shop and multiplayer.
/// </summary>
[InitializeOnLoad]
public static class FishingLevel7GearImporter
{
    private const string Source = "Assets/_Game/Fishing/RodReel/Level7/Source/";
    private const string Generated = "Assets/_Game/Fishing/RodReel/Level7/Generated/";
    private const string RodSource = Source + "Level7FishingRod.fbx";
    private const string ReelSource = Source + "Level7FishingReel.fbx";
    private const string TextureSource = Source + "Level7EquipmentTexture.png";
    private const string RodPrefab = "Assets/Resources/Fishing/FishingRodReelLevel7Rod.prefab";
    private const string ReelPrefab = "Assets/Resources/Fishing/FishingRodReelLevel7.prefab";
    private static bool running;

    static FishingLevel7GearImporter()
    {
        EditorApplication.delayCall += ImportIfAvailable;
    }

    [MenuItem("Tools/Open World/Import Level 7 Fishing Rod and Reel (One Click)")]
    public static void ImportFromMenu()
    {
        // The source files can remain in the Windows Downloads folder. They
        // are copied (not moved) into Unity's Assets on the user's machine.
        // No ZIP extraction or renaming is needed.
        if (!AllSourcesExist() && !CopyOriginalFilesFromDownloads()) return;
        Build(true);
    }

    private static bool CopyOriginalFilesFromDownloads()
    {
        string downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string sourceDirectory = Path.Combine(Application.dataPath, "_Game", "Fishing",
            "RodReel", "Level7", "Source");

        string rod = Path.Combine(downloads, "Level 7 Fishing Rod.fbx");
        string reel = Path.Combine(downloads, "Level 7 Fishing Reel.fbx");
        string rodTexture = Path.Combine(downloads, "Level 7 Fishing Rod Texture.png");
        string reelTexture = Path.Combine(downloads, "Level 7 Fishing Reel Texture.png");
        string texture = File.Exists(rodTexture) ? rodTexture : reelTexture;

        if (!File.Exists(rod) || !File.Exists(reel) || !File.Exists(texture))
        {
            Debug.LogError("Level 7 gear: expected Level 7 Fishing Rod.fbx, " +
                "Level 7 Fishing Reel.fbx, and either Level 7 Fishing Rod Texture.png " +
                "or Level 7 Fishing Reel Texture.png in " + downloads);
            EditorUtility.DisplayDialog("Level 7 source files not found",
                "Keep the original .fbx and .png files in your Downloads folder " +
                "(the .blend files are optional).\n\nSearched: " + downloads +
                "\n\nIf Downloads is redirected, copy those files to " + Source, "OK");
            return false;
        }

        Directory.CreateDirectory(sourceDirectory);
        File.Copy(rod, Path.Combine(sourceDirectory, "Level7FishingRod.fbx"), false);
        File.Copy(reel, Path.Combine(sourceDirectory, "Level7FishingReel.fbx"), false);
        File.Copy(texture, Path.Combine(sourceDirectory, "Level7EquipmentTexture.png"), false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return AllSourcesExist();
    }

    public static void ImportIfAvailable()
    {
        if (running || !AllSourcesExist()) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RodPrefab) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(ReelPrefab) == null)
            Build(false);
    }

    private static bool AllSourcesExist()
        => AssetDatabase.LoadAssetAtPath<GameObject>(RodSource) != null &&
           AssetDatabase.LoadAssetAtPath<GameObject>(ReelSource) != null &&
           AssetDatabase.LoadAssetAtPath<Texture2D>(TextureSource) != null;

    private static void Build(bool announce)
    {
        if (running) return;
        if (!AllSourcesExist())
        {
            Debug.LogWarning("Level 7 gear: import both Level7Fishing*.fbx files and " +
                "Level7EquipmentTexture.png into " + Source + " first.");
            return;
        }
        running = true;
        try
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            PrepareModel(RodSource);
            PrepareModel(ReelSource);
            Material material = MakeMaterial(AssetDatabase.LoadAssetAtPath<Texture2D>(TextureSource));
            MakeRodPrefab(material);
            MakeReelPrefab(material);
            AssetDatabase.SaveAssets();
            Debug.Log("Level 7 gear prefabs ready for shop/equipment, local third person and remote multiplayer.");
            if (announce) EditorUtility.DisplayDialog("Level 7 Fishing Gear",
                "Level 7 rod and reel prefabs imported. Run Validate Level 7 Fishing Gear to check them.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError("Level 7 gear import failed: " + e);
            if (announce) EditorUtility.DisplayDialog("Level 7 Import Failed", e.Message, "OK");
        }
        finally { running = false; }
    }

    private static void PrepareModel(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Not an FBX model: " + path);
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
    }

    private static Material MakeMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No lit shader found.");
        var material = new Material(shader) { name = "Level7FishingGear", color = Color.white };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        const string path = Generated + "Level7FishingGear.mat";
        ReplaceAsset(path, material);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void MakeRodPrefab(Material material)
    {
        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(RodSource);
        MeshFilter source = imported.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(f => f.sharedMesh != null);
        if (source == null) throw new InvalidOperationException("Level 7 rod FBX contains no mesh.");
        GameObject clone = CloneTemplate("Assets/Resources/Fishing/FishingRodReelLevel6Rod.prefab",
            "FishingRodReelLevel7Rod");
        try
        {
            MeshFilter target = Find(clone.transform, "RodBlank")?.GetComponent<MeshFilter>();
            if (target == null) throw new InvalidOperationException("Rod template lacks RodBlank.");
            const string path = Generated + "Level7RodBlank.asset";
            ReplaceAsset(path, FitMesh(source, imported.transform, target, true));
            target.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer = target.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(null);
            SavePrefab(clone, RodPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }

    private static void MakeReelPrefab(Material material)
    {
        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(ReelSource);
        MeshFilter[] meshes = imported.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.sharedMesh != null).ToArray();
        if (meshes.Length < 5)
            throw new InvalidOperationException("Level 7 reel FBX must have five visual meshes.");
        // The imported Blender parts are fitted to the animated five-piece
        // ReelMount of the existing rig. This keeps the existing reel action.
        string[] parts = { "ReelFootAndBody", "Rotor", "ReelHousing", "Spool", "Handle" };
        string[] sources = { "Plane", "Cylinder", "Cylinder.002", "Cylinder.001", "Cylinder.003" };
        var used = new HashSet<MeshFilter>();
        GameObject clone = CloneTemplate("Assets/Resources/Fishing/FishingRodReelLevel6.prefab",
            "FishingRodReelLevel7");
        try
        {
            Transform mount = Find(clone.transform, "ReelMount");
            if (mount == null) throw new InvalidOperationException("Reel template lacks ReelMount.");
            for (int i = 0; i < parts.Length; i++)
            {
                MeshFilter target = mount.Find(parts[i])?.GetComponent<MeshFilter>();
                if (target == null) throw new InvalidOperationException("ReelMount lacks " + parts[i]);
                MeshFilter source = meshes.FirstOrDefault(f => !used.Contains(f) &&
                    (f.name == sources[i] || f.name.StartsWith(sources[i] + ".", StringComparison.Ordinal)));
                if (source == null) source = meshes.FirstOrDefault(f => !used.Contains(f));
                if (source == null) throw new InvalidOperationException("Missing reel mesh: " + parts[i]);
                used.Add(source);
                string path = Generated + "Level7" + parts[i] + ".asset";
                ReplaceAsset(path, FitMesh(source, imported.transform, target, false));
                target.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var renderer = target.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(null);
            }
            SavePrefab(clone, ReelPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }

    private static GameObject CloneTemplate(string path, string name)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (template == null) throw new InvalidOperationException("Missing template: " + path);
        var clone = PrefabUtility.InstantiatePrefab(template) as GameObject;
        if (clone == null) throw new InvalidOperationException("Cannot instantiate: " + path);
        clone.name = name;
        return clone;
    }

    private static void SavePrefab(GameObject clone, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(clone, path, out bool success);
        if (!success) throw new InvalidOperationException("Could not save " + path);
    }

    private static Mesh FitMesh(MeshFilter source, Transform root, MeshFilter target, bool rod)
    {
        // Fit UV-mapped imported geometry to the known working local mesh
        // bounds. Rod flex, reel animation, rod tip and line anchors stay put.
        Mesh mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
        mesh.name = "Level7" + target.name;
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length == 0) throw new InvalidOperationException("Empty mesh: " + source.name);
        Matrix4x4 conversion = root.worldToLocalMatrix * source.transform.localToWorldMatrix;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = conversion.MultiplyPoint3x4(vertices[i]);
        Bounds from = BoundsOf(vertices);
        Bounds to = target.sharedMesh.bounds;
        if (rod)
        {
            Quaternion orient = Quaternion.FromToRotation(LongestAxis(from.size), LongestAxis(to.size));
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = orient * (vertices[i] - from.center);
            from = BoundsOf(vertices);
        }
        Vector3 scale = new Vector3(to.size.x / Mathf.Max(.000001f, from.size.x),
            to.size.y / Mathf.Max(.000001f, from.size.y),
            to.size.z / Mathf.Max(.000001f, from.size.z));
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = to.center + Vector3.Scale(vertices[i] - from.center, scale);
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        if (mesh.uv != null && mesh.uv.Length == vertices.Length) mesh.RecalculateTangents();
        return mesh;
    }

    private static Bounds BoundsOf(Vector3[] vertices)
    {
        var b = new Bounds(vertices[0], Vector3.zero);
        foreach (var v in vertices) b.Encapsulate(v);
        return b;
    }
    private static Vector3 LongestAxis(Vector3 size)
        => size.x >= size.y && size.x >= size.z ? Vector3.right :
           size.y >= size.z ? Vector3.up : Vector3.forward;
    private static Transform Find(Transform root, string name)
        => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    private static void ReplaceAsset(string path, UnityEngine.Object asset)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    [MenuItem("Tools/Open World/Validate Level 7 Fishing Gear")]
    public static void Validate()
    {
        var rod = AssetDatabase.LoadAssetAtPath<GameObject>(RodPrefab);
        var reel = AssetDatabase.LoadAssetAtPath<GameObject>(ReelPrefab);
        if (rod == null || reel == null)
        {
            Debug.LogError("Level 7 gear resource prefabs are missing; run the importer.");
            return;
        }
        bool ready = Find(rod.transform, "RodBlank")?.GetComponent<MeshFilter>()?.sharedMesh != null;
        foreach (string part in new[] { "ReelFootAndBody", "Rotor", "ReelHousing", "Spool", "Handle" })
            ready &= Find(reel.transform, part)?.GetComponent<MeshFilter>()?.sharedMesh != null;
        // The Level 7 hotbar automatically renders the imported Rod6 model
        // when no separately authored 2D Rod6.png artwork is installed.
        ready &= AssetDatabase.LoadAssetAtPath<Texture2D>(TextureSource) != null;
        ready &= ShopCatalog.MaxRodTier == 6 && ShopCatalog.MaxReelTier == 6;
        ready &= FishingBurstDamageRuntime.NormalMinimumForTier(6) == 55 &&
                 FishingBurstDamageRuntime.NormalMaximumForTier(6) == 75 &&
                 Mathf.Approximately(Level2FishingReelRuntime.ReelMultiplier(6), 2.2f);
        if (ready) Debug.Log("Level 7 fishing gear: meshes, textures, artwork and stats OK.");
        else Debug.LogError("Level 7 fishing gear validation failed.");
    }
}

public sealed class FishingLevel7GearImportTrigger : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
            if (path.StartsWith("Assets/_Game/Fishing/RodReel/Level7/Source/",
                StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.delayCall += FishingLevel7GearImporter.ImportIfAvailable;
                break;
            }
    }
}
#endif
