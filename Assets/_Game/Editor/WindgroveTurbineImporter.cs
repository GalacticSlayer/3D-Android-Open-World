#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class WindgroveTurbineImporter : AssetPostprocessor
{
    private const string ModelPath = "Assets/Resources/Environment/Windgrove/WindTurbine.fbx";

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
