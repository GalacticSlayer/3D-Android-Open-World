#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Non-destructive offshore additions. Only creates new scene objects/assets;
/// existing TerrainData, scene transforms, shoreline paint and gameplay prefabs
/// are NEVER assigned to, resculpted or re-saved as generated assets.
/// The new oil rig floor is real Unity Terrain and remains sculpt/paint editable.
/// </summary>
[InitializeOnLoad]
public sealed class OffshoreOilRigTerrainEditor : IProcessSceneWithReport
{
    const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    const string Folder = "Assets/_Game/EditableFishingMap";
    const string RootName = "Offshore Ocean Additions";
    const string OilName = "Oil Rig Seabed (Editable)";
    const float SouthDistance = 3000f;
    const float OilWidth = 512f;
    const float OilBottom = 380f;
    const float OilVerticalRange = 420f;
    const int OilResolution = 129;
    const int SkirtRings = 4;
    static readonly float[] SkirtDistances = { 0f, 12f, 52f, 125f };
    static readonly float[] SkirtDepths = { 0f, 15f, 87f, 172f };

    enum Edge { West, East, North, South }
    public int callbackOrder => -900;

    static OffshoreOilRigTerrainEditor()
    {
        EditorApplication.delayCall += BakeLoadedScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += BakeLoadedScenes;
    }

    [MenuItem("Tools/Open World/Terrain/Install Offshore Dropoffs + Oil Rig Terrain")]
    static void InstallFromMenu()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        Install(scene, true);
    }

    static void BakeLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer ||
            EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == ScenePath && scene.isLoaded)
                Install(scene, true);
        }
    }

    // Also handles headless/batch builds where the scene was never opened in Edit Mode.
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report != null && scene.path == ScenePath) Install(scene, false);
    }

    static void Install(Scene scene, bool persist)
    {
        try
        {
            bool wasDirty = scene.isDirty;
            if (!Ensure(scene, persist)) return;
            if (!persist) return;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            // Never silently overwrite the player's unsaved Scene edits.
            if (!wasDirty && !EditorSceneManager.SaveScene(scene))
                Debug.LogWarning("[OFFSHORE] Created editable additions; save PrototypeWorld manually.");
            SceneView.RepaintAll();
            Debug.Log("[OFFSHORE] Added perimeter drop-offs and a separate editable Unity Terrain 3 km south of Bluewater Cay. No existing terrain or server assets changed.");
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    static bool Ensure(Scene scene, bool persist)
    {
        IslandExpansionWorld expansion = Find<IslandExpansionWorld>(scene);
        SouthSeabedTerrain south = Find<SouthSeabedTerrain>(scene);
        OceanWater ocean = Find<OceanWater>(scene);
        Terrain original = expansion != null ? expansion.Terrain : null;
        if (original == null || original.terrainData == null || ocean == null)
            throw new InvalidOperationException("The authored fishing map and ocean must be present before adding offshore terrain.");
        if (original.transform.rotation != Quaternion.identity || original.transform.lossyScale != Vector3.one)
            throw new InvalidOperationException("Offshore skirts require unrotated, unscaled source Terrain.");
        if (south != null && south.terrain != null &&
            (south.terrain.transform.rotation != Quaternion.identity || south.terrain.transform.lossyScale != Vector3.one))
            throw new InvalidOperationException("South seabed is not axis-aligned.");

        GameObject root = Root(scene);
        bool created = false;
        Material material = null;
        GameObject skirts = FindChild(root, "Perimeter Drop-offs");
        if (skirts == null)
        {
            skirts = new GameObject("Perimeter Drop-offs");
            skirts.transform.SetParent(root.transform, false);
            created = true;
        }
        // Reuse the exact existing edge heights but NEVER write to the original TerrainData.
        if (NeedSkirts(skirts, original, south != null ? south.terrain : null))
        {
            material = LoadOrCreateMaterial(persist);
            created |= EnsureSkirts(skirts, original, "Main", material, persist,
                south != null && south.terrain != null);
            if (south != null && south.terrain != null)
                created |= EnsureSkirts(skirts, south.terrain, "South", material, persist, false, true);
        }

        GameObject oil = FindChild(root, OilName);
        if (oil == null)
        {
            // Use Bluewater's SAVED island center, not a distance from the
            // origin, the main terrain edge, or the player's current location.
            Vector3 bluewater = expansion.PelagicCenter;
            float sea = ocean.BaseWaterLevel;
            Vector3 origin = new Vector3(bluewater.x - OilWidth * .5f,
                sea - OilBottom, bluewater.z - SouthDistance - OilWidth * .5f);
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/OilRigSeabed.asset");
            if (data == null)
            {
                data = CreateOilData(original.terrainData);
                if (persist) AssetDatabase.CreateAsset(data, Folder + "/OilRigSeabed.asset");
            }
            oil = Terrain.CreateTerrainGameObject(data);
            oil.name = OilName;
            SceneManager.MoveGameObjectToScene(oil, scene);
            oil.transform.SetParent(root.transform, false);
            oil.transform.position = origin;
            oil.layer = original.gameObject.layer;
            Terrain tile = oil.GetComponent<Terrain>();
            TerrainCollider collider = oil.GetComponent<TerrainCollider>();
            if (collider == null) collider = oil.AddComponent<TerrainCollider>();
            collider.terrainData = data;
            tile.materialTemplate = original.materialTemplate;
            tile.drawInstanced = true;
            tile.heightmapPixelError = 16f;
            tile.basemapDistance = 100f;
            tile.shadowCastingMode = ShadowCastingMode.Off;
            tile.drawTreesAndFoliage = false;
            tile.treeDistance = 0f;
            tile.detailObjectDistance = 0f;
            tile.groupingID = 9303;
            tile.allowAutoConnect = false; // There is a real deep-ocean gap.
            var visibility = oil.AddComponent<OffshoreOilRigTerrain>();
            visibility.terrain = tile;
            created = true;
        }
        return created;
    }

    static TerrainData CreateOilData(TerrainData source)
    {
        var data = new TerrainData
        {
            name = "Oil Rig Offshore Seabed - Editable",
            heightmapResolution = OilResolution,
            alphamapResolution = 128,
            baseMapResolution = 256,
            size = new Vector3(OilWidth, OilVerticalRange, OilWidth),
            terrainLayers = source.terrainLayers
        };
        var h = new float[OilResolution, OilResolution];
        for (int z = 0; z < OilResolution; z++)
        for (int x = 0; x < OilResolution; x++)
        {
            float u = x / (float)(OilResolution - 1);
            float v = z / (float)(OilResolution - 1);
            float radial = Mathf.Clamp01(Mathf.Sqrt(Mathf.Pow((u - .5f) * 2f, 2f) +
                                               Mathf.Pow((v - .5f) * 2f, 2f)));
            // 220-290 m below the ocean; nearly invisible from the surface.
            // The additional height range allows artists to raise the terrain in
            // Unity later, including for pilings or a shallow structure.
            float depth = 222f + 64f * Mathf.SmoothStep(0f, 1f, radial);
            depth += (Mathf.PerlinNoise(u * 5.3f + 12f, v * 5.7f + 31f) - .5f) * 10f;
            h[z, x] = Mathf.Clamp01((OilBottom - depth) / OilVerticalRange);
        }
        data.SetHeights(0, 0, h);
        TerrainLayer[] layers = data.terrainLayers;
        if (layers != null && layers.Length > 0)
        {
            int rock = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                string n = layers[i].name.ToLowerInvariant();
                if (n.Contains("underwater") || n.Contains("submerged")) rock = i;
            }
            var alpha = new float[128, 128, layers.Length];
            for (int z = 0; z < 128; z++)
            for (int x = 0; x < 128; x++) alpha[z, x, rock] = 1f;
            data.SetAlphamaps(0, 0, alpha);
        }
        return data;
    }

    static bool NeedSkirts(GameObject root, Terrain main, Terrain south)
    {
        if (FindChild(root, "Main - West") == null ||
            FindChild(root, "Main - East") == null ||
            FindChild(root, "Main - North") == null) return true;
        if (south == null) return FindChild(root, "Main - South") == null;
        return FindChild(root, "South - West") == null ||
               FindChild(root, "South - East") == null ||
               FindChild(root, "South - South") == null;
    }

    static bool EnsureSkirts(GameObject parent, Terrain source, string label,
        Material material, bool persist, bool skipSouth, bool skipNorth = false)
    {
        bool changed = false;
        foreach (Edge edge in Enum.GetValues(typeof(Edge)))
        {
            if ((skipSouth && edge == Edge.South) || (skipNorth && edge == Edge.North)) continue;
            string name = label + " - " + edge;
            if (FindChild(parent, name) != null) continue;
            string path = Folder + "/OffshoreDropoff" + label + edge + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = CreateSkirt(source, edge);
                mesh.name = "Offshore Dropoff " + name;
                if (persist) AssetDatabase.CreateAsset(mesh, path);
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // No collider: these are visual cliffs, not a costly extra seabed.
            changed = true;
        }
        return changed;
    }

    static Mesh CreateSkirt(Terrain terrain, Edge edge)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        float length = edge == Edge.North || edge == Edge.South ? data.size.x : data.size.z;
        int segments = Mathf.Max(2, Mathf.CeilToInt(length / 16f));
        var vertices = new Vector3[(segments + 1) * SkirtRings];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * (SkirtRings - 1) * 6];
        Vector3 direction = edge == Edge.West ? Vector3.left :
            edge == Edge.East ? Vector3.right :
            edge == Edge.North ? Vector3.forward : Vector3.back;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = edge == Edge.West ? 0f : edge == Edge.East ? data.size.x : data.size.x * t;
            float z = edge == Edge.South ? 0f : edge == Edge.North ? data.size.z : data.size.z * t;
            float y = origin.y + data.GetInterpolatedHeight(x / data.size.x, z / data.size.z);
            Vector3 edgePoint = new Vector3(origin.x + x, y, origin.z + z);
            for (int j = 0; j < SkirtRings; j++)
            {
                int id = i * SkirtRings + j;
                vertices[id] = edgePoint + direction * SkirtDistances[j] - Vector3.up * SkirtDepths[j];
                uv[id] = new Vector2(t * length / 24f, j * 2f);
            }
        }
        int at = 0;
        bool reverse = edge == Edge.West || edge == Edge.North;
        for (int i = 0; i < segments; i++)
        for (int j = 0; j < SkirtRings - 1; j++)
        {
            int a = i * SkirtRings + j, b = (i + 1) * SkirtRings + j;
            int c = a + 1, d = b + 1;
            if (reverse)
            {
                triangles[at++] = a; triangles[at++] = c; triangles[at++] = b;
                triangles[at++] = b; triangles[at++] = c; triangles[at++] = d;
            }
            else
            {
                triangles[at++] = a; triangles[at++] = b; triangles[at++] = c;
                triangles[at++] = b; triangles[at++] = d; triangles[at++] = c;
            }
        }
        var mesh = new Mesh();
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static Material LoadOrCreateMaterial(bool persist)
    {
        const string path = Folder + "/OffshoreDropoffRock.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No supported cliff shader is available.");
        m = new Material(shader) { name = "Offshore Dropoff Rock" };
        Color color = new Color(.18f, .225f, .245f, 1f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (persist) AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Root(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == RootName) return go;
        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    static GameObject FindChild(GameObject parent, string name)
    {
        if (parent == null) return null;
        Transform t = parent.transform.Find(name);
        return t != null ? t.gameObject : null;
    }

    static T Find<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }
}
#endif
