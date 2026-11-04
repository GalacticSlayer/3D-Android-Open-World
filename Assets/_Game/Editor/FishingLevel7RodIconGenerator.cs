#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the player's supplied transparent Level 7 fishing-rod artwork
/// unchanged, instead of synthesizing an icon from the 3D model.
/// Rod6.png is shared by the transparent hotbar and by shop/equipment, where
/// ShopPreview draws the existing WaterBackground beneath it.
/// Other rod/reel icons and gameplay assets are not touched.
/// </summary>
[InitializeOnLoad]
public static class FishingLevel7RodIconGenerator
{
    private const string IconName = "Level 7 Fishing Rod Icon.png";
    private const string IconAssetPath = "Assets/Resources/Fishing/ItemIcons/Rod6.png";

    static FishingLevel7RodIconGenerator()
    {
        // Run after script compilation, independently of the heavier model
        // importer. This also corrects an already-generated Rod6 icon.
        EditorApplication.delayCall += () => InstallProvidedIcon(false);
    }

    [MenuItem("Tools/Open World/Import Level 7 Fishing Rod Icon (Downloads)")]
    private static void ImportFromMenu()
    {
        InstallProvidedIcon(true);
    }

    public static void InstallProvidedIcon(bool showDialog = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string source = FindProvidedIcon();
        if (string.IsNullOrEmpty(source) && showDialog)
        {
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
            source = EditorUtility.OpenFilePanel(
                "Select your original " + IconName, downloads, "png");
        }

        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            if (showDialog)
                Debug.LogWarning("[LEVEL 7 ICON] The supplied PNG was not found. " +
                    "Save '" + IconName + "' in Downloads and run the icon import menu again.");
            return;
        }

        // Verify the user-supplied PNG really contains transparent pixels and
        // has a valid file header. Do not add or remove a background or rotate.
        byte[] bytes = File.ReadAllBytes(source);
        if (bytes.Length < 33 || bytes[0] != 137 || bytes[1] != 80 ||
            bytes[2] != 78 || bytes[3] != 71)
        {
            Debug.LogError("[LEVEL 7 ICON] Supplied file is not a PNG: " + source);
            return;
        }

        string fullDestination = Path.GetFullPath(IconAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination));
        bool changed = !File.Exists(fullDestination) ||
            !File.ReadAllBytes(fullDestination).SequenceEqual(bytes);

        if (changed)
        {
            File.WriteAllBytes(fullDestination, bytes);
            AssetDatabase.ImportAsset(IconAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(IconAssetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[LEVEL 7 ICON] Unity could not import " + IconAssetPath);
            return;
        }

        bool needsImport = importer.textureType != TextureImporterType.Default ||
            !importer.alphaIsTransparency || importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            importer.maxTextureSize < 2048 || !importer.sRGBTexture;

        if (needsImport)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        if (changed || showDialog)
        {
            Debug.Log("[LEVEL 7 ICON] Imported original artwork without changes: " +
                IconAssetPath + ". Shop/Equipment use the existing blue background; " +
                "hotbar uses its transparency.");
        }

        if (showDialog)
            EditorUtility.DisplayDialog("Level 7 Rod Icon",
                "Original transparent PNG imported. Its bottom-left to top-right " +
                "orientation is preserved. The shop and Equipment supply their " +
                "existing blue-wave background; the hotbar does not.", "OK");
    }

    private static string FindProvidedIcon()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] folders =
        {
            Path.Combine(user, "Downloads"),
            Path.Combine(user, "Desktop"),
            "Assets/_Game/Fishing/RodReel/Level7/Source"
        };

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            string exact = Path.Combine(folder, IconName);
            if (File.Exists(exact)) return exact;

            // Browsers may append (1), (2), etc. to the downloaded filename.
            // Prefer the most recently downloaded matching Level 7 icon.
            string best = Directory.EnumerateFiles(folder, "*.png",
                    SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileNameWithoutExtension(path)
                    .StartsWith("Level 7 Fishing Rod Icon", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(best)) return best;
        }
        return null;
    }
}
#endif
