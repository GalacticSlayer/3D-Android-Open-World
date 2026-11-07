#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fills the 30 saved placements once; never re-scatters or resets them.</summary>
[InitializeOnLoad]
public sealed class WindgroveTurbineSceneSetup : IProcessSceneWithReport
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    public int callbackOrder => -800;

    static WindgroveTurbineSceneSetup()
    {
        EditorApplication.delayCall += PrepareLoadedScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += PrepareLoadedScenes;
    }

    private static void PrepareLoadedScenes()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += PrepareLoadedScenes;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path != ScenePath || !scene.isLoaded) continue;
            bool wasDirty = scene.isDirty;
            if (!Prepare(scene, false)) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            SceneView.RepaintAll();
        }
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report != null && scene.path == ScenePath) Prepare(scene, true);
    }

    private static bool Prepare(Scene scene, bool building)
    {
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (WindgroveTurbine turbine in root.GetComponentsInChildren<WindgroveTurbine>(true))
        {
            // Existing visuals and every artist-owned placement remain untouched.
            if (turbine.Ready) continue;
            if (UserPlacedScenery.Contains(turbine.transform)) continue;
            if (turbine.EnsureVisual())
            {
                changed = true;
                EditorUtility.SetDirty(turbine);
            }
            if (!turbine.Ready && building)
                throw new BuildFailedException("Windgrove turbine import is incomplete: " + turbine.name);
        }
        return changed;
    }
}
#endif
