#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class WindgroveTurbineImporter : AssetPostprocessor
{
    private const string ModelPath = "Assets/Resources/Environment/Windgrove/WindTurbine.fbx";

    // Changing the postprocessor version causes Unity to reimport the source
    // FBX and update every scene instance without editing its root transform.
    public override uint GetVersion() => 2;

    private static Transform Part(Transform root, string name)
    {
        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(part.name, name, System.StringComparison.OrdinalIgnoreCase))
                return part;
        return null;
    }

    private void OnPostprocessModel(GameObject importedModel)
    {
        if (assetPath != ModelPath) return;
        // The original FBX has independent Anemometer mast and Top service cap
        // mesh objects. Seat the mast's visible base on the cap's visible top.
        // Bounds are already evaluated in imported model coordinates, so the
        // correction automatically inherits each scene instance's manual scale.
        Transform mast = Part(importedModel.transform, "Anemometer mast");
        Transform cap = Part(importedModel.transform, "Top service cap");
        if (mast == null || cap == null) return;
        Renderer mastMesh = mast.GetComponentInChildren<Renderer>(true);
        Renderer capMesh = cap.GetComponentInChildren<Renderer>(true);
        if (mastMesh == null || capMesh == null) return;
        float elevation = capMesh.bounds.max.y - mastMesh.bounds.min.y;
        if (Mathf.Abs(elevation) > .0001f)
            mast.position += Vector3.up * elevation;
    }

    private void OnPreprocessModel()
    {
        if (assetPath != ModelPath) return;
        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.animationWrapMode = WrapMode.Loop;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.addCollider = false;
    }

    private void OnPostprocessMaterial(Material material)
    {
        if (assetPath != ModelPath) return;
        Color color;
        // Blender linear base colors, preserved from the supplied FBX.
        switch (material.name)
        {
            case "Blade white": color = new Color(.90f, .92f, .96f); break;
            case "Hub steel": color = new Color(.58f, .64f, .72f); break;
            case "Porcelain white": color = new Color(.78f, .82f, .88f); break;
            case "Safety yellow.001": color = new Color(1f, .64f, .015f); break;
            case "Signal red.001": color = new Color(.90f, .018f, .012f); break;
            case "Vent charcoal": color = new Color(.16f, .19f, .22f); break;
            default: color = material.color; break;
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null) material.shader = shader;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color.gamma);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color.gamma);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .25f);
        material.enableInstancing = true;
    }
}
#endif
