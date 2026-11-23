using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class OceanixAdditionalFishInstaller
{
    private const string Source="Assets/_Game/Reef/Source";
    private static readonly string[] Species={"ChloroCellTitanTriggerfish",
        "EmberCoreBarracuda","MagmaVoltCoralGrouper","CyberneticCoralGrouper"};
    private static bool installing;
    static OceanixAdditionalFishInstaller()
    {
        EditorApplication.delayCall+=AutoInstall;
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.ExitingEditMode)return;
            try{EnsureInstalled();}
            catch(Exception error){EditorApplication.isPlaying=false;Debug.LogException(error);}
        };
    }
    private static void AutoInstall()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)
        {EditorApplication.delayCall+=AutoInstall;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}
        catch(Exception error){Debug.LogError("[OCEANIX FISH] "+error);}
    }
    private static bool Ready(string species)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Fishing/"+species+".prefab");
        if(prefab==null||prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        var animator=prefab.GetComponentInChildren<Animator>(true);
        if(animator==null||animator.runtimeAnimatorController==null||
            !animator.runtimeAnimatorController.animationClips.Any(c=>c!=null&&c.name=="Swim"))return false;
        if(!prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Any(s=>s.sharedMesh!=null&&s.sharedMesh.vertexCount>0&&
                s.sharedMesh.uv.Length==s.sharedMesh.vertexCount))return false;
        foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach(var material in renderer.sharedMaterials)
                if(material==null||!material.HasProperty("_BaseMap")||
                    material.GetTexture("_BaseMap")==null)return false;
        return prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="AuthoredMouthAnchor");
    }
    public static void EnsureInstalled()
    {
        if(installing)return;
        foreach(var species in Species)if(!Ready(species)){Rebuild();break;}
    }
    [MenuItem("Tools/Open World/Rebuild Oceanix Cybernetic Fish")]
    public static void Rebuild()
    {
        if(installing)return;
        installing=true;
        try
        {
            foreach(var species in Species)
            {
                if(!File.Exists(Source+"/"+species+".fbx")||
                   !File.Exists(Source+"/"+species+"Texture.png"))
                    throw new FileNotFoundException("Oceanix source FBX/PNG missing: "+species);
                SeaBassImporter.InstallModel(species,"Assets/Resources/Fishing/"+species+".prefab",Source);
                if(!Ready(species))throw new InvalidOperationException("Imported fish failed validation: "+species);
            }
        }
        finally{installing=false;}
    }
}
public sealed class OceanixAdditionalFishBuildCheck:IPreprocessBuildWithReport
{
    public int callbackOrder=>1;
    public void OnPreprocessBuild(BuildReport report)=>OceanixAdditionalFishInstaller.EnsureInstalled();
}
