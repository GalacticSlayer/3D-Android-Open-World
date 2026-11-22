using System.Collections.Generic;
using UnityEngine;

// Already attached to Oceanix_Datacenter.prefab. Shared source assets are never modified.
[DisallowMultipleComponent]
public sealed class OceanixLightCycle : MonoBehaviour
{
    [Min(0.1f)] public float cycleSeconds = 8f;
    [Min(0.1f)] public float rainbowCycleSeconds = 4f;
    [Min(0.1f)] public float bluePinkCycleSeconds = 8f / 1.5f;
    [Min(0f)] public float emissionIntensity = 4f;
    public Color blue = new Color(0.005f, 0.5f, 1f);
    public Color pink = new Color(1f, 0.015f, 0.3f);
    [Range(0f, 1f)] public float rainbowSaturation = 0.995f;
    [Min(0f)] public float coolingEmissionIntensity = 5f;
    readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
    readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();

    void OnEnable()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            Material[] original = (Material[])materials.Clone();
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || (!source.name.Contains("Pulse_BluePink") && !source.name.Contains("Cooling_BluePulse") && !source.name.Contains("Server_Rainbow") && !source.name.Contains("Constant_Blue"))) continue;
                if (!copies.TryGetValue(source, out Material instance))
                {
                    instance = new Material(source);
                    instance.name = source.name + " Runtime";
                    instance.EnableKeyword("_EMISSION");
                    copies.Add(source, instance);
                }
                materials[i] = instance;
                changed = true;
            }
            if (changed) { originals.Add(renderer, original); renderer.sharedMaterials = materials; }
        }
        Apply(Time.timeAsDouble);
    }

    void Update()
    {
        Apply(Time.timeAsDouble);
    }

    static float Phase(double seconds, float period) => (float)((seconds / System.Math.Max(0.1, period)) % 1.0);

    public Color Evaluate(string materialName, double seconds, out Color emission)
    {
        float phaseBP = Phase(seconds, bluePinkCycleSeconds);
        float blend = 0.5f - 0.5f * Mathf.Cos(phaseBP * Mathf.PI * 2f);
        Color color = Color.Lerp(blue, pink, blend);
            Color surface = color;
            emission = color * emissionIntensity;
            if (materialName.Contains("Server_Rainbow"))
            {
                float hue = Phase(seconds, rainbowCycleSeconds);
                surface = Color.HSVToRGB(hue, rainbowSaturation, 1f);
                emission = surface * emissionIntensity;
            }
            else if (materialName.Contains("Constant_Blue"))
            {
                surface = blue;
                emission = blue * emissionIntensity;
            }
            else if (materialName.Contains("Cooling_BluePulse"))
            {
                float phase = Phase(seconds, cycleSeconds / 3f);
                float level = phase < 0.25f ? 1f : phase < 0.5f ? 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.25f) * 4f) : phase < 0.75f ? 0f : Mathf.SmoothStep(0f, 1f, (phase - 0.75f) * 4f);
                surface = Color.Lerp(new Color(0.002f, 0.008f, 0.015f), blue, level);
                emission = blue * (coolingEmissionIntensity * level);
            }
        return surface;
    }

    void Apply(double seconds)
    {
        foreach (Material material in copies.Values)
        {
            Color surface = Evaluate(material.name, seconds, out Color emission);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", surface);
            if (material.HasProperty("_Color")) material.SetColor("_Color", surface);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        }
    }

    void OnDisable()
    {
        foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        foreach (Material material in copies.Values) Destroy(material);
        originals.Clear(); copies.Clear();
    }
}
