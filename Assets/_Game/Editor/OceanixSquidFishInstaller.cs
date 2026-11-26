using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Imports Oceanix's squid-exclusive fish from the Unity computer's Downloads
/// directory, preserving original FBX animation, UV mapping and image atlases.
/// Missing sources are copied once; authored project files are never replaced.
/// </summary>
[InitializeOnLoad]
public static class OceanixSquidFishInstaller
{
    private const string Source="Assets/_Game/Reef/Source";
    private const string SourceBlend="ArtSources/Oceanix";
    private const string ResourcesFolder="Assets/Resources/Fishing";
    private static bool installing;

    private sealed class FishFiles
    {
        public readonly string Name,Fbx,Texture,Blend;
        public FishFiles(string name,string fbx,string texture,string blend)
        {Name=name;Fbx=fbx;Texture=texture;Blend=blend;}
    }

    // These original names (including the lower-case "shark" FBX suffix)
    // are intentionally distinct from their normalized Unity asset names.
    private static readonly FishFiles[] Fish={
        new FishFiles("AureliaMangroveSnapper",
            "Aurelia Mangrove Snapper.fbx",
            "Aurelia Mangrove Snapper Texture.png",
            "Aurelia Mangrove Snapper.blend"),
        new FishFiles("ChronoReefShark",
            "Chrono Reef Shark.fbx",
            "Chrono Reef Shark Texture.png",
            "Chrono Reef Shark.blend"),
        new FishFiles("CrimsonCybrosMakoShark",
            "Crimson Cybros Mako shark.fbx",
            "Crimson Cybros Mako Shark Texture.png",
            "Crimson Cybros Mako shark.blend")
    };

    static OceanixSquidFishInstaller()
    {
        EditorApplication.delayCall+=AutoInstall;
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.ExitingEditMode)return;
            try{EnsureInstalled();}
            catch(Exception error){EditorApplication.isPlaying=false;Debug.LogException(error);}
        };
    }

    private static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    private static string Absolute(string relative)=>
        Path.Combine(Root,relative.Replace('/',Path.DirectorySeparatorChar));

    private static IEnumerable<string> DownloadsFolders()
    {
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string profile=Environment.GetEnvironmentVariable("USERPROFILE");
        string cloud=Environment.GetEnvironmentVariable("OneDrive");
        string documents=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var paths=new List<string>();
        if(!string.IsNullOrWhiteSpace(home))
        {
            paths.Add(Path.Combine(home,"Downloads"));
            paths.Add(Path.Combine(home,"OneDrive","Downloads"));
        }
        if(!string.IsNullOrWhiteSpace(profile))paths.Add(Path.Combine(profile,"Downloads"));
        if(!string.IsNullOrWhiteSpace(cloud))paths.Add(Path.Combine(cloud,"Downloads"));
        if(!string.IsNullOrWhiteSpace(documents))
        {
            string parent=Path.GetDirectoryName(documents);
            if(!string.IsNullOrWhiteSpace(parent))paths.Add(Path.Combine(parent,"Downloads"));
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists);
    }

    // Restrict the search to these explicitly named files; do not scan other
    // project assets or overwrite manually edited source files.
    private static bool CopyMissing(string fileName,string target)
    {
        string destination=Absolute(target);
        if(File.Exists(destination))return false;
        string source=null;
        foreach(string downloads in DownloadsFolders())
        {
            source=Directory.EnumerateFiles(downloads)
                .FirstOrDefault(path=>string.Equals(Path.GetFileName(path),
                    fileName,StringComparison.OrdinalIgnoreCase));
            if(source!=null)break;
        }
        if(source==null)return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source,destination,false);
        Debug.Log("[OCEANIX SQUID FISH] Imported "+fileName+" to "+target);
        return true;
    }

    private static void CopySourceArt()
    {
        bool changed=false;
        foreach(FishFiles fish in Fish)
        {
            changed|=CopyMissing(fish.Fbx,Source+"/"+fish.Name+".fbx");
            changed|=CopyMissing(fish.Texture,Source+"/"+fish.Name+"Texture.png");
            CopyMissing(fish.Blend,SourceBlend+"/"+fish.Name+".blend");
        }
        if(changed)AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static bool Ready(FishFiles fish)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            ResourcesFolder+"/"+fish.Name+".prefab");
        if(prefab==null||prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        Animator animator=prefab.GetComponentInChildren<Animator>(true);
        if(animator==null||animator.runtimeAnimatorController==null||
           !animator.runtimeAnimatorController.animationClips.Any(c=>c!=null&&c.name=="Swim"))
            return false;
        if(!prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s=>
            s.sharedMesh!=null&&s.sharedMesh.vertexCount>0&&
            s.sharedMesh.uv.Length==s.sharedMesh.vertexCount))return false;
        foreach(Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach(Material material in renderer.sharedMaterials)
                if(material==null||!material.HasProperty("_BaseMap")||
                   material.GetTexture("_BaseMap")==null)return false;
        return prefab.GetComponentsInChildren<Transform>(true)
            .Any(t=>t.name=="AuthoredMouthAnchor");
    }

    private static void Install(FishFiles fish)
    {
        if(!File.Exists(Absolute(Source+"/"+fish.Name+".fbx")) ||
           !File.Exists(Absolute(Source+"/"+fish.Name+"Texture.png")))
            throw new FileNotFoundException("Missing source FBX or PNG for "+fish.Name+
                ". Keep the original files in this Unity computer's Downloads directory.");
        // The authored Mako export also contains unrelated Plane actions.
        // Always select the rig's ArmatureAction rather than the longest take.
        SeaBassImporter.InstallModel(fish.Name,ResourcesFolder+"/"+fish.Name+".prefab",
            Source,swimTakeContains:"ArmatureAction",
            discardEmptyRenderers:true,allowRightSuffixedRig:true);
        if(!Ready(fish))throw new InvalidOperationException(
            "Failed to validate authored mesh, animation, UV atlas or mouth anchor for "+fish.Name);
    }

    public static void EnsureInstalled()
    {
        if(installing)return;
        installing=true;
        try
        {
            CopySourceArt();
            foreach(FishFiles fish in Fish)if(!Ready(fish))Install(fish);
        }
        finally{installing=false;}
    }

    private static void AutoInstall()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)
        {EditorApplication.delayCall+=AutoInstall;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}
        catch(Exception error){Debug.LogError("[OCEANIX SQUID FISH] "+error);}
    }

    [MenuItem("Tools/Open World/Import Oceanix Squid Fish From Downloads")]
    public static void ImportFromDownloads()=>EnsureInstalled();

    [MenuItem("Tools/Open World/Rebuild Oceanix Squid Fish")]
    public static void Rebuild()
    {
        if(installing)return;
        installing=true;
        try
        {
            CopySourceArt();
            foreach(FishFiles fish in Fish)Install(fish);
            Debug.Log("[OCEANIX SQUID FISH] Three source FBXs, atlases, and swimming prefabs installed.");
        }
        finally{installing=false;}
    }
}

public sealed class OceanixSquidFishBuildCheck:IPreprocessBuildWithReport
{
    public int callbackOrder=>2;
    public void OnPreprocessBuild(BuildReport report)=>OceanixSquidFishInstaller.EnsureInstalled();
}
