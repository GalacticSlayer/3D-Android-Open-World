using UnityEngine;

// Unity generates this Resources catalog from the user's original FBX.
// The FBX, embedded animation and material references are not modified.
public sealed class FishingRemoteAvatarAsset : ScriptableObject
{
    public GameObject ModelPrefab;
    public AnimationClip WalkClip;
    public AnimationClip RunClip;
    public AnimationClip IdleClip;
    public AnimationClip CastClip;
    public AnimationClip ReelClip;
    public AnimationClip PullClip;
    public AnimationClip WalkReelClip;
    public AnimationClip SmallFishPoseClip;
    public AnimationClip BigFishPoseClip;
    public AnimationClip HoldFishClip;
    public AnimationClip WalkHoldFishClip;
    public AnimationClip DriveMotorboatClip;
    public AnimationClip PassengerMotorboatClip;
    public string LeftHandPath;

    // FBX importers can sanitize punctuation or add an armature/namespace prefix.
    // Resolve the imported bone, rather than assuming the Blender hierarchy path.
    public static Transform FindBone(Transform root, string boneName)
    {
        if (root == null) return null;
        string wanted = BoneKey(boneName);
        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            if (BoneKey(bone.name) == wanted) return bone;
        return null;
    }

    private static string BoneKey(string name)
    {
        int separator = name.LastIndexOf(':');
        if (separator >= 0) name = name.Substring(separator + 1);
        return name.Replace(".", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
    }
}
