#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Retires the original generated placements; never installs turbines.</summary>
[InitializeOnLoad]
public sealed class WindgroveTurbineSceneSetup : IProcessSceneWithReport
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    private const string GeneratedRoot = "The Windgrove Shoal - Wind Turbines";
    public int callbackOrder => -800;

    static WindgroveTurbineSceneSetup()
    {
        EditorApplication.delayCall += CleanLoadedScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += CleanLoadedScenes;
    }

    private static void CleanLoadedScenes()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CleanLoadedScenes;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path != ScenePath || !scene.isLoaded) continue;
            bool wasDirty = scene.isDirty;
            if (!RemoveGeneratedPlacements(scene)) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            SceneView.RepaintAll();
        }
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // Also covers entering Play Mode or building before the delayed cleanup.
        if (scene.path == ScenePath) RemoveGeneratedPlacements(scene);
    }

    private static bool RemoveGeneratedPlacements(Scene scene)
    {
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != GeneratedRoot || UserPlacedScenery.Contains(root.transform)) continue;
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                // Match only our original numbered placement wrappers. Directly
                // dragged FBX instances do not carry this component.
                if (child.GetComponent<WindgroveTurbine>() == null) continue;
                if (UserPlacedScenery.Contains(child)) continue;
                bool originalName = false;
                for (int number = 1; number <= 30; number++)
                    if (child.name == "Wind Turbine " + number.ToString("00")) originalName = true;
                if (!originalName) continue;
                Object.DestroyImmediate(child.gameObject);
                changed = true;
            }
            // Preserve any unrelated children added by the user.
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
