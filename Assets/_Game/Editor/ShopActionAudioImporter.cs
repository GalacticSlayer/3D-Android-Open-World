using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// The four short sound files supplied with the game are kept as normal
// AudioClips, not runtime-decoded bytes or remote network requests.
// On the developer's Windows machine the uploaded originals are in Downloads.
// Import them once, without overwriting any existing audio asset.
[InitializeOnLoad]
internal static class ShopActionAudioImporter
{
    private const string Destination="Assets/Resources/Fishing/Audio";
    private static readonly string[] Files={
        "Aquarium Move to Storage.mp3",
        "Picking Up Aquarium Noise.mp3",
        "Placing Down Aquarium.mp3",
        "Bought Item From Store.mp3"
    };
    private static readonly string[] Names={
        "AquariumStored.mp3","AquariumPickup.mp3",
        "AquariumPlaced.mp3","StorePurchase.mp3"
    };

    static ShopActionAudioImporter()
    {
        EditorApplication.delayCall+=()=>{ Import(); };
    }

    [MenuItem("Tools/Open World/Import Aquarium and Shop Sounds")]
    private static void ImportManually()
    {
        int count=Import();
        if(count==0)
            Debug.Log("No missing aquarium/store audio found. Place the four supplied MP3s in Downloads or the project's Shop Audio folder, then retry.");
    }

    private static int Import()
    {
        string downloads=Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string supplied=Path.Combine(project,"Shop Audio");
        int imported=0;
        for(int i=0;i<Names.Length;i++)
        {
            string target=Destination+"/"+Names[i];
            if(File.Exists(target))continue;
            string original=Path.Combine(downloads,Files[i]);
            if(!File.Exists(original))original=Path.Combine(supplied,Files[i]);
            if(!File.Exists(original))continue; // No missing-audio error for other developers.
            try
            {
                Directory.CreateDirectory(Destination);
                File.Copy(original,target,false);
                AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceUpdate);
                imported++;
            }
            catch(Exception error)
            {
                Debug.LogWarning("Could not import shop audio "+Files[i]+": "+error.Message);
            }
        }
        if(imported>0)
        {
            AssetDatabase.Refresh();
            Debug.Log("Imported "+imported+" aquarium/shop sounds into "+Destination+".");
        }
        return imported;
    }
}
