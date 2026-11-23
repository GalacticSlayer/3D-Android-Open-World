using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Imports Oceanix fish from their checked-in files OR the Unity computer's
/// Downloads folder. Exact user-authored filenames are copied into the
/// project on first open; Unity then builds the animated prefab normally.
/// Does not overwrite files that have already been imported.
/// </summary>
[InitializeOnLoad]
public static class OceanixAdditionalFishInstaller
{
    private const string Source="Assets/_Game/Reef/Source";
    private static readonly string[] Species={
        "ChloroCellTitanTriggerfish",
        "EmberCoreBarracuda",
        "MagmaVoltCoralGrouper",
        "CyberneticCoralGrouper"
    };
    private static readonly string[] DownloadNames={
        "Chloro-Cell Titan Triggerfish",
        "Ember Core Barracuda",
        "Magma Volt Coral Grouper",
        "Cybernetic Coral Grouper"
    };
    private static bool installing;

    static OceanixAdditionalFishInstaller()
    {
        EditorApplication.delayCall+=AutoInstall;
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.ExitingEditMode)return;
            try{EnsureInstalled();}
            catch(Exception error)
            {
                EditorApplication.isPlaying=false;
                Debug.LogException(error);
            }
        };
    }

    private static void AutoInstall()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)
        {
            EditorApplication.delayCall+=AutoInstall;
            return;
        }
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}
        catch(Exception error){Debug.LogError("[OCEANIX FISH] "+error);}
    }

    private static string ProjectRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath,".."));

    // Windows and macOS/Linux user profiles; also handle relocated OneDrive Downloads.
    private static IEnumerable<string> DownloadFolders()
    {
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string userProfile=Environment.GetEnvironmentVariable("USERPROFILE");
        string oneDrive=Environment.GetEnvironmentVariable("OneDrive");
        string documents=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var folders=new List<string>();
        if(!string.IsNullOrWhiteSpace(home))
        {
            folders.Add(Path.Combine(home,"Downloads"));
            folders.Add(Path.Combine(home,"OneDrive","Downloads"));
        }
        if(!string.IsNullOrWhiteSpace(userProfile))
            folders.Add(Path.Combine(userProfile,"Downloads"));
        if(!string.IsNullOrWhiteSpace(oneDrive))
            folders.Add(Path.Combine(oneDrive,"Downloads"));
        if(!string.IsNullOrWhiteSpace(documents))
        {
            string documentsParent=Path.GetDirectoryName(documents);
            if(!string.IsNullOrWhiteSpace(documentsParent))
                folders.Add(Path.Combine(documentsParent,"Downloads"));
        }
        return folders.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);
    }

    private static bool CopyIfMissing(string downloadName,string destination)
    {
        string target=Path.Combine(ProjectRoot,destination.Replace('/',Path.DirectorySeparatorChar));
        if(File.Exists(target))return false;
        string from=DownloadFolders()
            .Select(folder=>Path.Combine(folder,downloadName))
            .FirstOrDefault(File.Exists);
        if(string.IsNullOrEmpty(from))return false;
        string parent=Path.GetDirectoryName(target);
        if(!Directory.Exists(parent))Directory.CreateDirectory(parent);
        File.Copy(from,target,false);
        Debug.Log("[OCEANIX FISH] Imported "+downloadName+" from Downloads into "+destination);
        return true;
    }

    // Only copy the exact 12 user-provided source filenames; never overwrite
    // an existing Unity source or import anything unrelated from Downloads.
    private static void ImportSourcesFromDownloads()
    {
        bool refresh=false;
        for(int i=0;i<Species.Length;i++)
        {
            string name=Species[i],download=DownloadNames[i];
            refresh|=CopyIfMissing(download+".fbx",Source+"/"+name+".fbx");
            refresh|=CopyIfMissing(download+" Texture.png",Source+"/"+name+"Texture.png");
            CopyIfMissing(download+".blend","ArtSources/Oceanix/"+name+".blend");
        }
        if(refresh)AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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
        installing=true;
        try
        {
            ImportSourcesFromDownloads();
            foreach(var species in Species)
                if(!Ready(species))Install(species);
        }
        finally{installing=false;}
    }

    private static void Install(string species)
    {
        if(!File.Exists(Path.Combine(ProjectRoot,Source,species+".fbx"))||
           !File.Exists(Path.Combine(ProjectRoot,Source,species+"Texture.png")))
            throw new FileNotFoundException(
                "Missing "+species+" FBX or PNG. Put its original uploaded files in the computer's Downloads folder, "+
                "then reopen Unity or use Tools > Open World > Import Oceanix Fish From Downloads.");
        SeaBassImporter.InstallModel(species,"Assets/Resources/Fishing/"+species+".prefab",Source);
        if(!Ready(species))
            throw new InvalidOperationException("Imported fish failed validation: "+species);
    }

    [MenuItem("Tools/Open World/Import Oceanix Fish From Downloads")]
    public static void ImportFromDownloads()=>EnsureInstalled();

    [MenuItem("Tools/Open World/Rebuild Oceanix Cybernetic Fish")]
    public static void Rebuild()
    {
        if(installing)return;
        installing=true;
        try
        {
            ImportSourcesFromDownloads();
            foreach(var species in Species)Install(species);
            Debug.Log("[OCEANIX FISH] Four fish models, UV textures and swim prefabs installed.");
        }
        finally{installing=false;}
    }
}

public sealed class OceanixAdditionalFishBuildCheck:IPreprocessBuildWithReport
{
    public int callbackOrder=>1;
    public void OnPreprocessBuild(BuildReport report)=>OceanixAdditionalFishInstaller.EnsureInstalled();
}
