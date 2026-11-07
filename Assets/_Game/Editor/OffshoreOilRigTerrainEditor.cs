#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time scene migration: the old offshore oil-rig island was replaced by
/// manually placed Windgrove turbines. Remove only the old rig terrain and
/// empty starter platform, retaining existing perimeter drop-offs and all
/// artist-placed scenery, nine turbine roots, and other island terrain.
/// This intentionally never installs the retired oil-rig assets again.
/// </summary>
[InitializeOnLoad]
public sealed class OffshoreOilRigTerrainEditor : IProcessSceneWithReport
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    private const string LegacyParent = "Offshore Ocean Additions";
    private const string LegacyTerrain = "Oil Rig Seabed (Editable)";
    private const string LegacyPlatform = "Offshore Oil Rig Starter Platform";

    public int callbackOrder => -900;

    static OffshoreOilRigTerrainEditor()
    {
        EditorApplication.delayCall += CleanOpenScenes;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CleanOpenScenes;
    }

    private static void CleanOpenScenes()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CleanOpenScenes;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene.path != ScenePath) continue;
            bool dirty = scene.isDirty;
            if (!RetireRig(scene)) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!dirty) EditorSceneManager.SaveScene(scene);
            SceneView.RepaintAll();
            Debug.Log("[WINDGROVE] Retired the obsolete oil-rig seabed and platform; all placed wind turbines preserved.");
        }
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (scene.path == ScenePath) RetireRig(scene);
    }

    private static bool RetireRig(Scene scene)
    {
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != LegacyParent || UserPlacedScenery.Contains(root.transform))
                continue;

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name != LegacyTerrain && child.name != LegacyPlatform) continue;
                if (UserPlacedScenery.Contains(child) ||
                    child.GetComponentInChildren<UserPlacedScenery>(true) != null) continue;
                Object.DestroyImmediate(child.gameObject);
                changed = true;
            }

            // Keep the "Perimeter Drop-offs" child and any unrelated additions.
            if (root.transform.childCount == 0)
            {
                Object.DestroyImmediate(root);
                changed = true;
            }
        }
        return changed;
    }
}
#endif
