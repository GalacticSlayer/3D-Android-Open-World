using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Replaces the visible bobber with the equipped authored crankbait whenever a
/// permanent lure is being cast/retrieved. Every crankbait uses the same proven
/// line attachment, retrieve-facing behavior and animation logic; only the
/// selected visual prefab changes.
/// </summary>
[DefaultExecutionOrder(900)]
public sealed class LiplessCrankbaitWorldPresentation : MonoBehaviour
{
    private const float RetrieveDepth = 0.20f;
    private const float FloatDepth = 0.015f;

    private FishingSystem fishing;
    private ShopProgress shopProgress;
    private FishingHUD hud;
    private OceanWater oceanWater;

    private FieldInfo stateField;
    private FieldInfo activeBaitField;
    private FieldInfo bobberField;
    private FieldInfo fishingLineField;
    private FieldInfo castPointField;
    private FieldInfo oceanWaterField;

    private GameObject lureRoot;
    private Transform lineAttach;
    private Animator lureAnimator;
    private Renderer[] bobberRenderers;
    private int loadedVariant=-1;

    private bool showingLure;
    private float sinkDepth;
    private Quaternion authoredRootRotation = Quaternion.identity;
    private Quaternion localRetrieveFrame = Quaternion.identity;
    private Vector3 previousLogicalPosition;
    private bool hasPreviousWaitingPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<LiplessCrankbaitWorldPresentation>() == null)
                system.gameObject.AddComponent<LiplessCrankbaitWorldPresentation>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        shopProgress = GetComponent<ShopProgress>();
        CacheMembers();
    }

    private void LateUpdate()
    {
        if (fishing == null || stateField == null || bobberField == null)
            return;

        string state = GetStateName();
        GameObject bobber = bobberField.GetValue(fishing) as GameObject;
        if (bobber == null)
        {
            StopShowingLure();
            return;
        }

        if (!ShouldShowLure(state, bobber))
        {
            StopShowingLure();
            return;
        }

        if (!EnsureLure())
            return;

        if (!showingLure)
        {
            showingLure = true;
            lureRoot.transform.rotation = authoredRootRotation;
            hasPreviousWaitingPosition = false;
        }

        SetBobberRenderers(false);
        lureRoot.SetActive(true);

        bool waiting = string.Equals(state, "Waiting", StringComparison.Ordinal);
        bool reeling = waiting && IsReeling();

        Vector3 logicalPosition = waiting && castPointField != null
            ? (Vector3)castPointField.GetValue(fishing)
            : bobber.transform.position;

        float targetDepth = reeling ? RetrieveDepth : FloatDepth;
        float depthSpeed = reeling ? 0.70f : 0.38f;
        sinkDepth = Mathf.MoveTowards(sinkDepth, targetDepth, depthSpeed * Time.deltaTime);

        Vector3 position = logicalPosition;
        if (waiting && oceanWater != null)
        {
            float surface = oceanWater.GetSurfaceHeight(logicalPosition);
            position.y = surface - sinkDepth;
            if (!reeling)
                position.y += Mathf.Sin(Time.time * 2.6f) * 0.012f;
        }

        if (reeling)
        {
            // Follow the movement FishingSystem actually produced. The water
            // path can turn up to 72 degrees away from the player near shore.
            Vector3 retrieveDirection = hasPreviousWaitingPosition
                ? logicalPosition - previousLogicalPosition
                : Vector3.zero;
            retrieveDirection.y = 0f;
            if (retrieveDirection.sqrMagnitude < 0.00000001f)
            {
                retrieveDirection = fishing.transform.position - position;
                retrieveDirection.y = 0f;
            }

            if (retrieveDirection.sqrMagnitude > 0.00000001f)
            {
                // A complete forward/up frame stays upright even on a 180
                // degree turn. The child Animator still supplies the wobble.
                lureRoot.transform.rotation =
                    Quaternion.LookRotation(retrieveDirection, Vector3.up) *
                    // +Z points toward the player during retrieve: positive roll
                    // is clockwise from that end. Keep it outside the Animator.
                    Quaternion.AngleAxis(ShopCatalog.LurePresentationRoll(loadedVariant), Vector3.forward) *
                    Quaternion.Inverse(localRetrieveFrame);
            }
        }

        previousLogicalPosition = logicalPosition;
        hasPreviousWaitingPosition = waiting;

        // Put the line-tie/head itself at FishingSystem's logical lure point.
        // Every crankbait variant uses the same LineAttach contract.
        lureRoot.transform.position = position;
        if (lineAttach != null)
        {
            Vector3 headOffset = lineAttach.position - lureRoot.transform.position;
            lureRoot.transform.position -= headOffset;
        }

        bobber.transform.position = position;

        // The shared retrieve-speed presentation applies the current 2x retrieve
        // animation after this component. This value is the normal base state.
        if (lureAnimator != null)
            lureAnimator.speed = reeling ? 1f : 0f;

        LineRenderer line = fishingLineField != null
            ? fishingLineField.GetValue(fishing) as LineRenderer
            : null;
        if (line != null && line.enabled && line.positionCount >= 2)
            line.SetPosition(1, lineAttach != null ? lineAttach.position : position);
    }

    private bool ShouldShowLure(string state, GameObject bobber)
    {
        if (bobber == null || !bobber.activeSelf)
            return false;

        if (string.Equals(state, "Waiting", StringComparison.Ordinal))
        {
            if (activeBaitField == null) return false;
            return (int)activeBaitField.GetValue(fishing) == ShopCatalog.StarterLure;
        }

        if (string.Equals(state, "Casting", StringComparison.Ordinal))
            return shopProgress != null && shopProgress.Data.baitEquipped == ShopCatalog.StarterLure;

        return false;
    }

    private bool IsReeling()
    {
        if (hud == null)
            hud = FindFirstObjectByType<FishingHUD>();
        return hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
    }

    private bool EnsureLure()
    {
        int desiredVariant=shopProgress!=null
            ? Mathf.Clamp(shopProgress.Data.lureEquipped,0,ShopCatalog.LureVariantCount-1)
            : Mathf.Clamp(ShopCatalog.ActiveLureVariant,0,ShopCatalog.LureVariantCount-1);

        if (lureRoot != null && loadedVariant == desiredVariant)
            return true;

        if (lureRoot != null)
        {
            Destroy(lureRoot);
            lureRoot=null;
            lineAttach=null;
            lureAnimator=null;
        }

        string resource=ShopCatalog.LurePrefabResource(desiredVariant);
        GameObject prefab = Resources.Load<GameObject>(resource);
        if (prefab == null && desiredVariant!=ShopCatalog.NeonBreachLureVariant)
        {
            Debug.LogWarning("Missing lure prefab '"+resource+"'. Falling back to Heavy Jig until its model is imported.");
            prefab=Resources.Load<GameObject>(ShopCatalog.LurePrefabResource(ShopCatalog.NeonBreachLureVariant));
        }
        if (prefab == null)
            return false;

        lureRoot = Instantiate(prefab);
        loadedVariant=desiredVariant;
        // Keep one stable runtime name. LiplessCrankbaitRetrieveAnimationSpeed
        // deliberately finds this object and promotes its active retrieve from
        // 1x to the approved 2x animation speed for every lure variant.
        lureRoot.name = "ActiveLiplessCrankbait";
        authoredRootRotation = lureRoot.transform.rotation;
        showingLure = false;
        hasPreviousWaitingPosition = false;

        lineAttach = FindDeepChild(lureRoot.transform, "LineAttach");
        if (lineAttach == null)
        {
            GameObject attach = new GameObject("LineAttach");
            lineAttach = attach.transform;
            lineAttach.SetParent(lureRoot.transform, false);
        }

        lureAnimator = lureRoot.GetComponentInChildren<Animator>(true);
        if (lureAnimator != null)
        {
            lureAnimator.applyRootMotion = false;
            lureAnimator.speed = 0f;
            lureAnimator.Update(0f);
        }

        // Measure AFTER the initial animated pose is evaluated, not from the
        // FBX's different stored transform pose.
        localRetrieveFrame = MeasureLocalRetrieveFrame();

        lureRoot.SetActive(false);
        return true;
    }

    private Quaternion MeasureLocalRetrieveFrame()
    {
        // Models with a different authored axis convention provide an explicit
        // forward/up marker. This changes only visual alignment, never the path.
        Transform frame = lureRoot != null ? FindDeepChild(lureRoot.transform, "RetrieveFrame") : null;
        if (frame != null)
            return Quaternion.LookRotation(lureRoot.transform.InverseTransformDirection(frame.forward),
                lureRoot.transform.InverseTransformDirection(frame.up));

        if (lureRoot == null || lineAttach == null || lineAttach.parent == lureRoot.transform)
            return Quaternion.identity;

        // All five authored crankbaits share the import contract: LineAttach
        // is a child of the BODY, the nose is mesh -Y and the back is mesh +Z.
        // A hook's world AABB must never determine the body's heading.
        Transform body = lineAttach.parent;
        Vector3 head = lureRoot.transform.InverseTransformDirection(body.TransformVector(Vector3.down));
        Vector3 up = lureRoot.transform.InverseTransformDirection(body.TransformVector(Vector3.forward));
        return Quaternion.LookRotation(head, up);
    }

    private void StopShowingLure()
    {
        if (!showingLure)
            return;

        showingLure = false;
        hasPreviousWaitingPosition = false;
        sinkDepth = 0f;
        if (lureRoot != null)
        {
            lureRoot.transform.rotation = authoredRootRotation;
            lureRoot.SetActive(false);
        }
        if (lureAnimator != null) lureAnimator.speed = 0f;
        SetBobberRenderers(true);
    }

    private void SetBobberRenderers(bool visible)
    {
        GameObject bobber = bobberField != null ? bobberField.GetValue(fishing) as GameObject : null;
        if (bobber == null) return;
        if (bobberRenderers == null || bobberRenderers.Length == 0)
            bobberRenderers = bobber.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < bobberRenderers.Length; i++)
            if (bobberRenderers[i] != null) bobberRenderers[i].enabled = visible;
    }

    private string GetStateName()
    {
        object value = stateField.GetValue(fishing);
        return value != null ? value.ToString() : string.Empty;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private void CacheMembers()
    {
        Type type = typeof(FishingSystem);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        stateField = type.GetField("state", flags);
        activeBaitField = type.GetField("activeBait", flags);
        bobberField = type.GetField("bobber", flags);
        fishingLineField = type.GetField("fishingLine", flags);
        castPointField = type.GetField("castPoint", flags);
        oceanWaterField = type.GetField("oceanWater", flags);
        if (oceanWaterField != null && fishing != null)
            oceanWater = oceanWaterField.GetValue(fishing) as OceanWater;

        if (stateField == null || activeBaitField == null || bobberField == null ||
            fishingLineField == null || castPointField == null)
        {
            Debug.LogError("LiplessCrankbaitWorldPresentation could not bind to FishingSystem.");
            enabled = false;
        }
    }

    private void OnDisable()
    {
        StopShowingLure();
    }

    private void OnDestroy()
    {
        if (lureRoot != null) Destroy(lureRoot);
    }
}
