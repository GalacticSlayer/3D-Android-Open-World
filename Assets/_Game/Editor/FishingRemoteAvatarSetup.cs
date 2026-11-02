using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Creates a Resources reference to the source FBX and its embedded clips.
// The original FBX importer settings, textures, materials and clips are unchanged.
[InitializeOnLoad]
public sealed class FishingRemoteAvatarSetup : IPreprocessBuildWithReport
{
    internal const string SourcePath = "Assets/Manually Added Assets/Female Character Model.fbx";
    public const string RunSourcePath = "Assets/Manually Added Assets/Female Running.fbx";
    public const string IdleSourcePath = "Assets/Manually Added Assets/Female Idle.fbx";
    public const string CastSourcePath = "Assets/Manually Added Assets/Female Casting Fishing Rod.fbx";
    public const string ReelSourcePath = "Assets/Manually Added Assets/Female Reeling.fbx";
    public const string PullSourcePath = "Assets/Manually Added Assets/Female Pulling on Rod.fbx";
    public const string WalkReelSourcePath = "Assets/Manually Added Assets/Female Reeling and Walking.fbx";
    public const string SmallFishPoseSourcePath = "Assets/Manually Added Assets/Female Holding a Small Fish Pose.fbx";
    public const string HoldFishSourcePath = "Assets/Manually Added Assets/Female Holding the Fish.fbx";
    public const string WalkHoldFishSourcePath = "Assets/Manually Added Assets/Female Holding the Fish while Walking.fbx";
    public const string DriveMotorboatSourcePath = "Assets/Manually Added Assets/Female Driving Motorboat.fbx";
    public const string PassengerMotorboatSourcePath = "Assets/Manually Added Assets/Female Being a Motorboat Passenger.fbx";
    private const string ResourceFolder = "Assets/Resources/Multiplayer";
    private const string CatalogPath = ResourceFolder + "/FemaleRemoteAvatar.asset";

    static FishingRemoteAvatarSetup()
    {
        EditorApplication.delayCall += SetupOnLoad;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode && !EnsureSetup(false))
            {
                Debug.LogError("Multiplayer avatar setup failed. Check the female model, idle and cast FBX import errors before entering Play Mode.");
            }
        };
    }

    public int callbackOrder => -200;

    private static void SetupOnLoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += SetupOnLoad;
            return;
        }
        EnsureSetup(false);
    }

    [MenuItem("Tools/Open World/Rebuild Female Multiplayer Avatar")]
    public static void Rebuild()
    {
        if (!EnsureSetup(true))
            EditorUtility.DisplayDialog("Female Multiplayer Avatar",
                "The model, walk, idle or casting animation is missing or has incompatible bone paths. Check the Console for details.", "OK");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!EnsureSetup(false))
            throw new BuildFailedException("Missing or incompatible female multiplayer avatar, walk, idle or casting clip. Open Tools/Open World/Rebuild Female Multiplayer Avatar and check the imported FBX animation.");
    }

    internal static bool EnsureSetup(bool verbose)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (model == null) return false;

        AnimationClip walk = null;
        AnimationClip idle = null;
        AnimationClip fallback = null;
        UnityEngine.Object[] embedded = AssetDatabase.LoadAllAssetsAtPath(SourcePath);
        for (int i = 0; i < embedded.Length; i++)
        {
            AnimationClip clip = embedded[i] as AnimationClip;
            if (clip == null || clip.length < .05f || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            if (clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 && idle == null)
                idle = clip;

            if ((clip.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 clip.name.IndexOf("walking", StringComparison.OrdinalIgnoreCase) >= 0) && walk == null)
                walk = clip;

            if (fallback == null && clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) < 0 &&
                clip.name.IndexOf("pose", StringComparison.OrdinalIgnoreCase) < 0)
                fallback = clip;
        }
        if (walk == null) walk = fallback; // FBX takes can have generic names, e.g. "Take 001".
        if (walk == null) return false;
        AnimationClip run = FindAnimation(RunSourcePath, "run");
        if (run == null)
        {
            Debug.LogError("Multiplayer: missing Female Running FBX animation.");
            return false;
        }
        if (!ValidateBindings(model, run)) return false;

        // Use the separately exported idle clip without replacing the original
        // textured FBX or its walking animation.
        AnimationClip separateIdle = FindAnimation(IdleSourcePath, "idle");
        if (separateIdle != null)
            idle = separateIdle;
        if (idle == null)
        {
            Debug.LogError("Multiplayer: missing idle animation at " + IdleSourcePath + ". Pull the FBX and its .meta file before building.");
            return false;
        }
        if (!ValidateBindings(model, idle)) return false;
        AnimationClip cast = FindAnimation(CastSourcePath, "cast");
        if (cast == null)
        {
            Debug.LogError("Multiplayer: missing casting animation at " + CastSourcePath);
            return false;
        }
        if (!ValidateBindings(model, cast)) return false;
        AnimationClip reel = FindAnimation(ReelSourcePath, "reel");
        AnimationClip pull = FindAnimation(PullSourcePath, "pull");
        if (reel == null || pull == null)
        {
            Debug.LogError("Multiplayer: missing Female Reeling or Female Pulling on Rod animation FBX.");
            return false;
        }
        if (!ValidateBindings(model, reel) || !ValidateBindings(model, pull)) return false;
        AnimationClip walkReel = FindAnimation(WalkReelSourcePath, "walk");
        AnimationClip holdFish = FindAnimation(HoldFishSourcePath, "hold");
        AnimationClip walkHoldFish = FindAnimation(WalkHoldFishSourcePath, "walk");
        if (walkReel == null || holdFish == null || walkHoldFish == null)
        {
            Debug.LogError("Multiplayer: missing walking/reeling, holding-fish or walking-with-fish FBX animation.");
            return false;
        }
        if (!ValidateBindings(model, walkReel) || !ValidateBindings(model, holdFish) || !ValidateBindings(model, walkHoldFish)) return false;
        AnimationClip smallPose = FindAnimation(SmallFishPoseSourcePath, "pose", true);
        if (smallPose == null || !ValidateBindings(model, smallPose))
        {
            Debug.LogError("Fish poses: missing or incompatible small fish pose FBX.");
            return false;
        }
        AnimationClip driveMotorboat = FindAnimation(DriveMotorboatSourcePath, "driv");
        if (driveMotorboat != null && !ValidateBindings(model, driveMotorboat)) return false;
        AnimationClip passengerMotorboat = FindAnimation(PassengerMotorboatSourcePath, "passenger");
        if (passengerMotorboat == null || !ValidateBindings(model, passengerMotorboat)) return false;
        if (FishingRemoteAvatarAsset.FindBone(model.transform, "Hand.R") == null)
        {
            Debug.LogError("Multiplayer: imported model has no right hand for the held fish.");
            return false;
        }
        Transform hand = FishingRemoteAvatarAsset.FindBone(model.transform, "Hand.L");
        if (hand == null)
        {
            Debug.LogError("Multiplayer: imported character has no left-hand bone. The rod cannot be attached.");
            return false;
        }
        string leftHandPath = AnimationUtility.CalculateTransformPath(hand, model.transform);
        if (Resources.Load<GameObject>(ShopCatalog.RodPrefabResource(0)) == null)
        {
            Debug.LogError("Multiplayer: missing Resources/Fishing/FishingRodReel prefab.");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(ResourceFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Multiplayer");

        FishingRemoteAvatarAsset catalog = AssetDatabase.LoadAssetAtPath<FishingRemoteAvatarAsset>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<FishingRemoteAvatarAsset>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        bool changed = catalog.SmallFishPoseClip != smallPose || catalog.ModelPrefab != model || catalog.WalkClip != walk || catalog.RunClip != run || catalog.IdleClip != idle || catalog.CastClip != cast || catalog.LeftHandPath != leftHandPath || catalog.ReelClip != reel || catalog.PullClip != pull || catalog.WalkReelClip != walkReel || catalog.HoldFishClip != holdFish || catalog.WalkHoldFishClip != walkHoldFish || catalog.DriveMotorboatClip != driveMotorboat || catalog.PassengerMotorboatClip != passengerMotorboat;
        if (changed)
        {
            catalog.ModelPrefab = model;
            catalog.WalkClip = walk;
            catalog.RunClip = run;
            catalog.IdleClip = idle;
            catalog.CastClip = cast;
            catalog.ReelClip = reel;
            catalog.PullClip = pull;
            catalog.WalkReelClip = walkReel;
            catalog.HoldFishClip = holdFish;
            catalog.SmallFishPoseClip = smallPose;
            catalog.WalkHoldFishClip = walkHoldFish;
            catalog.DriveMotorboatClip = driveMotorboat;
            catalog.PassengerMotorboatClip = passengerMotorboat;
            catalog.LeftHandPath = leftHandPath;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        if (verbose || changed)
            Debug.Log("Multiplayer: female FBX connected with walk animation '" + walk.name +
                      "' and " + (idle != null ? "idle animation '" + idle.name + "'." : "walk start pose while idle.") +
                      " Casting animation: '" + cast.name + "'. Existing FBX materials and animations are preserved.");
        return true;
    }

    private static bool ValidateBindings(GameObject model, AnimationClip idle)
    {
        int boneCurves = 0;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(idle))
        {
            if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)) continue;
            if (model.transform.Find(binding.path) == null)
            {
                Debug.LogError("Multiplayer: clip '" + idle.name + "' targets missing bone '" + binding.path +
                    "'. Reimport the animation FBX with Preserve Hierarchy enabled.");
                return false;
            }
            boneCurves++;
        }
        if (boneCurves == 0)
            Debug.LogError("Multiplayer: clip '" + idle.name + "' has no bone animation curves.");
        return boneCurves > 0;
    }

    private static AnimationClip FindAnimation(string path, string preferredName, bool allowStaticPose = false)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            return null;

        AnimationClip firstUsable = null;
        foreach (UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AnimationClip clip = item as AnimationClip;
            if (clip == null || (!allowStaticPose && clip.length < .05f) ||
                clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            if (clip.name.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0)
                return clip;
            if (firstUsable == null)
                firstUsable = clip; // This FBX calls its animation take "Scene".
        }
        return firstUsable;
    }
}

// Keep the character's imported materials and walking FBX untouched.
public sealed class FishingRemoteIdleModelImport : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (assetPath != FishingRemoteAvatarSetup.RunSourcePath &&
            assetPath != FishingRemoteAvatarSetup.IdleSourcePath &&
            assetPath != FishingRemoteAvatarSetup.CastSourcePath &&
            assetPath != FishingRemoteAvatarSetup.ReelSourcePath &&
            assetPath != FishingRemoteAvatarSetup.PullSourcePath &&
            assetPath != FishingRemoteAvatarSetup.WalkReelSourcePath &&
            assetPath != FishingRemoteAvatarSetup.SmallFishPoseSourcePath &&
            assetPath != FishingRemoteAvatarSetup.HoldFishSourcePath &&
            assetPath != FishingRemoteAvatarSetup.WalkHoldFishSourcePath &&
            assetPath != FishingRemoteAvatarSetup.DriveMotorboatSourcePath &&
            assetPath != FishingRemoteAvatarSetup.PassengerMotorboatSourcePath) return;
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        // Animation-only FBX has one root (Armature); the mesh FBX has several.
        // Keep Armature below the prefab root so both clips use Armature/... paths.
        importer.preserveHierarchy = true;
        importer.optimizeGameObjects = false;
        importer.optimizeBones = false;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
    }
}

// Keep the generated build reference current when either FBX is reimported.
public sealed class FishingRemoteAvatarImportWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (importedAssets[i] != FishingRemoteAvatarSetup.RunSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.SourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.IdleSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.CastSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.ReelSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.PullSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.WalkReelSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.SmallFishPoseSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.HoldFishSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.WalkHoldFishSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.DriveMotorboatSourcePath &&
                importedAssets[i] != FishingRemoteAvatarSetup.PassengerMotorboatSourcePath) continue;
            EditorApplication.delayCall += () => FishingRemoteAvatarSetup.EnsureSetup(false);
            break;
        }
    }
}
