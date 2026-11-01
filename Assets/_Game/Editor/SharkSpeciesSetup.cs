using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Prepared FBX + texture: automatic import, no Blender install or ZIP picker needed.
[InitializeOnLoad]
public static class SharkSpeciesSetup
{
    private static readonly string[] Models={"BlacktipReefShark","MakoShark","BattleScarredMakoShark","AbyssalLeviathan"};
    private static bool running;
    static SharkSpeciesSetup()
    {
        EditorApplication.delayCall+=InstallWhenReady;
        EditorApplication.playModeStateChanged+=OnPlayModeChanged;
    }
    private static void InstallWhenReady()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)
        {EditorApplication.delayCall+=InstallWhenReady;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}catch(Exception e){Debug.LogError("[SHARK SPECIES] "+e);}
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.ExitingEditMode)return;
        try{EnsureInstalled();}
        catch(Exception e){EditorApplication.isPlaying=false;Debug.LogError("[SHARK SPECIES] Setup must finish before Play: "+e);}
    }
    public static void EnsureInstalled()
    {
        if(running)return;
        if(Models.All(key=>IsReady(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Fishing/"+key+".prefab"))))return;
        Install();
    }
    private static bool IsReady(GameObject prefab)
    {
        if(prefab==null || prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        var animator=prefab.GetComponentInChildren<Animator>(true);
        return animator!=null && animator.runtimeAnimatorController!=null
            && prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="AuthoredMouthAnchor")
            && prefab.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null && m.GetTexture("_BaseMap")!=null));
    }
    [MenuItem("Tools/Open World/Install Reef and Mako Sharks")]
    public static void Install()
    {
        if(running)return;
        running=true;
        try
        {
            foreach(string key in Models)
            {
                string prefabPath="Assets/Resources/Fishing/"+key+".prefab";
                SeaBassImporter.InstallModel(key,prefabPath,swimTakeContains:key=="AbyssalLeviathan"?"ArmatureAction":null);
                if(!IsReady(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)))
                    throw new InvalidDataException(key+" model, swim animation, mouth anchor or texture is missing.");
            }
            FishingTuning.Reload();
            if(!FishingTuning.IsValid)throw new InvalidDataException(FishingTuning.ValidationError);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHARK SPECIES] Ready: reef and Mako sharks use shared aquarium swimming, train-track turns and held flopping.");
        }
        finally{running=false;}
    }
}

public sealed class SharkSpeciesBuildGate : IPreprocessBuildWithReport
{
    public int callbackOrder=>-900;
    public void OnPreprocessBuild(BuildReport report)
    {
        try{SharkSpeciesSetup.EnsureInstalled();}
        catch(Exception e){throw new BuildFailedException("Reef/Mako shark setup failed: "+e.Message);}
    }
}

