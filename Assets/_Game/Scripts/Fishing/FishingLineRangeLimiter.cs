using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Enforces FishingSystem's effective maximum against the actual rendered line
/// after lure presentation, including the protected knockout limit.
/// </summary>
[DefaultExecutionOrder(1400)]
public sealed class FishingLineRangeLimiter : MonoBehaviour
{
    private FishingSystem fishing;
    private FieldInfo stateField;
    private FieldInfo rodEquippedField;
    private FieldInfo fishingLineField;
    private FieldInfo activeBaitField;
    private MethodInfo failFishingMethod;
    private MethodInfo snapLineMethod;
    private bool retracting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<FishingLineRangeLimiter>() == null)
                system.gameObject.AddComponent<FishingLineRangeLimiter>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(FishingSystem);
        stateField = type.GetField("state", flags);
        rodEquippedField = type.GetField("rodEquipped", flags);
        fishingLineField = type.GetField("fishingLine", flags);
        activeBaitField = type.GetField("activeBait", flags);
        failFishingMethod = type.GetMethod("FailFishing", flags);
        snapLineMethod = type.GetMethod("SnapLine", flags);

        if (stateField == null || rodEquippedField == null || fishingLineField == null ||
            activeBaitField == null || failFishingMethod == null ||
            snapLineMethod == null)
        {
            Debug.LogError("FishingLineRangeLimiter could not bind to FishingSystem and was disabled.");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (retracting || fishing == null)
            return;

        if (!(bool)rodEquippedField.GetValue(fishing))
            return;

        object stateValue = stateField.GetValue(fishing);
        string state = stateValue != null ? stateValue.ToString() : string.Empty;
        if (state == "Idle" || state == "Charging")
            return;

        LineRenderer line = fishingLineField.GetValue(fishing) as LineRenderer;
        if (line == null || !line.enabled || line.positionCount < 2)
            return;

        float maximum = Mathf.Max(0.01f, fishing.EffectiveLineDistanceLimit);
        float actualLength = RenderedLineLength(line);
        if (actualLength <= maximum + 0.001f)
            return;

        // Allow the hooked fish to exceed the equipped line length while the
        // once-per-fight passive is active. Other fishing states stay unchanged.
        if (state == "Fighting")
        {
            var immortality = fishing.GetComponent<FishingBurstDamageRuntime>();
            if (immortality != null &&
                (immortality.LineImmortalityActive || immortality.TryTriggerLineImmortality()))
                return;
        }

        retracting = true;
        bool lure = (int)activeBaitField.GetValue(fishing) == ShopCatalog.StarterLure;
        string message = lure
            ? $"Line exceeded {maximum:0} m — lure automatically returned to your tackle."
            : $"Line exceeded {maximum:0} m — line automatically retracted.";

        try
        {
            if (state == "Fighting")
                // Use the exact same snap pathway (including LineSnap audio)
                // as sustained high tension and FishingSystem's own max-distance check.
                snapLineMethod.Invoke(fishing,
                    new object[] { "SNAP! Maximum line distance reached." });
            else
                // Free lure/bait retrieval is not a line break and remains silent.
                failFishingMethod.Invoke(fishing, new object[] { message });
        }
        finally
        {
            retracting = false;
        }
    }

    private static float RenderedLineLength(LineRenderer line)
    {
        float length = 0f;
        Vector3 previous = LinePointWorld(line, 0);
        for (int i = 1; i < line.positionCount; i++)
        {
            Vector3 current = LinePointWorld(line, i);
            length += Vector3.Distance(previous, current);
            previous = current;
        }
        return length;
    }

    private static Vector3 LinePointWorld(LineRenderer line, int index)
    {
        Vector3 point = line.GetPosition(index);
        return line.useWorldSpace ? point : line.transform.TransformPoint(point);
    }
}
