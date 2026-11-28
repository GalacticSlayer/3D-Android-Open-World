using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds Oceanix's five base Synthetic fish from the originals in Downloads.
/// Only missing files are copied, preserving existing .meta files and GUIDs.
/// The FBX's actual UV atlas, skinning and authored swim are kept intact.
/// </summary>
[InitializeOnLoad]
public static class OceanixSyntheticFishInstaller
{
    private const string Source="Assets/_Game/Reef/Source";
    private const string BlendSource="ArtSources/Oceanix";
    private const string PrefabFolder="Assets/Resources/Fishing";
    private static bool installing;

    private static readonly string[] Names={
        "SyntheticMangroveSnapper","SyntheticTitanTriggerfish",
        "SyntheticCoralGrouper","SyntheticPantherGrouper",
        "SyntheticGreatBarracuda"
    };
    private static readonly string[] DownloadNames={
        "Synthetic Mangrove Snapper","Synthetic Titan Triggerfish",
        "Synthetic Coral Grouper","Synthetic Panther Grouper",
        "Synthetic Great Barracuda"
    };

    static OceanixSyntheticFishInstaller()
    {
        EditorApplication.delayCall+=AutoInstall;
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.ExitingEditMode)return;
            try{EnsureInstalled();}
            catch(Exception error){EditorApplication.isPlaying=false;Debug.LogException(error);}
        };
    }

    private static string ProjectRoot=>
        Path.GetFullPath(Path.Combine(Application.dataPath,".."));

    private static string Absolute(string relative)=>
        Path.Combine(ProjectRoot,relative.Replace('/',Path.DirectorySeparatorChar));

    private static IEnumerable<string> DownloadsFolders()
    {
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string profile=Environment.GetEnvironmentVariable("USERPROFILE");
        string oneDrive=Environment.GetEnvironmentVariable("OneDrive");
        string documents=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var paths=new List<string>();
        if(!string.IsNullOrWhiteSpace(home))
        {
            paths.Add(Path.Combine(home,"Downloads"));
            paths.Add(Path.Combine(home,"OneDrive","Downloads"));
        }
        if(!string.IsNullOrWhiteSpace(profile))
            paths.Add(Path.Combine(profile,"Downloads"));
        if(!string.IsNullOrWhiteSpace(oneDrive))
            paths.Add(Path.Combine(oneDrive,"Downloads"));
        if(!string.IsNullOrWhiteSpace(documents))
        {
            string parent=Path.GetDirectoryName(documents);
            if(!string.IsNullOrWhiteSpace(parent))
                paths.Add(Path.Combine(parent,"Downloads"));
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists);
    }

    private static bool CopyIfMissing(string fileName,string target)
    {
        string destination=Absolute(target);
        if(File.Exists(destination))return false;
        string source=null;
        foreach(string folder in DownloadsFolders())
        {
            source=Directory.EnumerateFiles(folder).FirstOrDefault(path=>
                string.Equals(Path.GetFileName(path),fileName,StringComparison.OrdinalIgnoreCase));
            if(source!=null)break;
        }
        if(source==null)return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source,destination,false);
        Debug.Log("[OCEANIX SYNTHETIC] Copied "+fileName+" to "+target);
        return true;
    }

    private static void CopyFromDownloads()
    {
        bool reimport=false;
        for(int i=0;i<Names.Length;i++)
        {
            string name=Names[i],download=DownloadNames[i];
            reimport|=CopyIfMissing(download+".fbx",Source+"/"+name+".fbx");
            reimport|=CopyIfMissing(download+" Texture.png",Source+"/"+name+"Texture.png");
            CopyIfMissing(download+".blend",BlendSource+"/"+name+".blend");
        }
        if(reimport)AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static bool Ready(string name)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/"+name+".prefab");
        if(prefab==null||prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        Animator animator=prefab.GetComponentInChildren<Animator>(true);
        if(animator==null||animator.runtimeAnimatorController==null||
           !animator.runtimeAnimatorController.animationClips.Any(c=>c!=null&&c.name=="Swim"))
            return false;
        if(!prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s=>
           s.sharedMesh!=null&&s.sharedMesh.vertexCount>0 &&
           s.sharedMesh.uv.Length==s.sharedMesh.vertexCount))return false;
        foreach(Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach(Material material in renderer.sharedMaterials)
                if(material==null||!material.HasProperty("_BaseMap")||
                   material.GetTexture("_BaseMap")==null)return false;
        return prefab.GetComponentsInChildren<Transform>(true)
            .Any(t=>t.name=="AuthoredMouthAnchor");
    }

    private static void Install(string name)
    {
        if(!File.Exists(Absolute(Source+"/"+name+".fbx")) ||
           !File.Exists(Absolute(Source+"/"+name+"Texture.png")))
            throw new FileNotFoundException(
                "Missing original FBX/PNG for "+name+
                ". Place the exact uploaded files in this computer's Downloads folder.");
        SeaBassImporter.InstallModel(name,PrefabFolder+"/"+name+".prefab",Source,
            swimTakeContains:"ArmatureAction",discardEmptyRenderers:true);
        if(!Ready(name))throw new InvalidOperationException(
            "Synthetic fish prefab validation failed (rig, UV, swim or mouth): "+name);
    }

    public static void EnsureInstalled()
    {
        if(installing)return;
        installing=true;
        try
        {
            CopyFromDownloads();
            foreach(string name in Names)if(!Ready(name))Install(name);
        }
        finally{installing=false;}
    }

    private static void AutoInstall()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)
        {EditorApplication.delayCall+=AutoInstall;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}
        catch(Exception error){Debug.LogWarning("[OCEANIX SYNTHETIC] "+error.Message);}
    }

    [MenuItem("Tools/Open World/Import Oceanix Synthetic Fish From Downloads")]
    public static void ImportFromDownloads()=>EnsureInstalled();

    [MenuItem("Tools/Open World/Rebuild Oceanix Synthetic Fish")]
    public static void Rebuild()
    {
        if(installing)return;
        installing=true;
        try
        {
            CopyFromDownloads();
            foreach(string name in Names)Install(name);
            Debug.Log("[OCEANIX SYNTHETIC] All five authored synthetic fish imported.");
        }
        finally{installing=false;}
    }
}

public sealed class OceanixSyntheticFishBuildCheck:IPreprocessBuildWithReport
{
    public int callbackOrder=>3;
    public void OnPreprocessBuild(BuildReport report)=>OceanixSyntheticFishInstaller.EnsureInstalled();
}
