using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One cached, hollow LOD mesh outside the authored ocean. No physics or extra cameras.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class DistantOceanRuntime : MonoBehaviour
{
    private OceanWater ocean;
    private MeshRenderer source;
    private MeshRenderer outer;
    private Mesh mesh;
    private Material material;
    private Material sourceMaterial;
    private MaterialPropertyBlock properties;
    private bool failed;

    // UnderwaterVisuals suppresses only this camera's unused horizon draw.
    public MeshRenderer OuterRenderer => outer;

    private void LateUpdate()
    {
        if (outer == null && !failed) Initialize();
        if (outer == null) return;
        outer.enabled = ocean.isActiveAndEnabled && source.enabled;
        if (!outer.enabled) return;

        // Run after OceanWater and DeepOceanWaterRuntime: copy this frame's phases,
        // local storm parameters and live biome colour overrides, never another clock.
        if (sourceMaterial != source.sharedMaterial)
        {
            sourceMaterial = source.sharedMaterial;
            if (sourceMaterial != null) material.CopyPropertiesFromMaterial(sourceMaterial);
            material.renderQueue = 2995;
        }
        ocean.CopyWaveProperties(properties);
        outer.SetPropertyBlock(properties);
    }

    private void Initialize()
    {
        ocean = GetComponent<OceanWater>();
        source = GetComponent<MeshRenderer>();
        MeshFilter filter = GetComponent<MeshFilter>();
        Mesh original = filter != null ? filter.sharedMesh : null;
        if (ocean == null || source == null || original == null) return;

        Shader shader = Resources.Load<Shader>("DistantOcean");
        int row = Mathf.RoundToInt(Mathf.Sqrt(original.vertexCount));
        if (shader == null || !original.isReadable || row < 2 || row * row != original.vertexCount)
        {
            failed = true;
            Debug.LogWarning("Distant ocean requires its Resources shader and a readable square ocean grid.", this);
            return;
        }

        mesh = BuildMesh(original, row);
        material = new Material(shader) { name = "Distant Ocean (runtime)" };
        properties = new MaterialPropertyBlock();
        GameObject child = new GameObject("Distant Ocean (visual only)", typeof(MeshFilter), typeof(MeshRenderer));
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        child.GetComponent<MeshFilter>().sharedMesh = mesh;
        outer = child.GetComponent<MeshRenderer>();
        outer.sharedMaterial = material;
        outer.shadowCastingMode = ShadowCastingMode.Off;
        outer.receiveShadows = false;
        outer.lightProbeUsage = LightProbeUsage.Off;
        outer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        // Its shader projects beyond the normal far clip; never occlusion-cull the horizon.
        outer.allowOcclusionWhenDynamic = false;
    }

    private static Mesh BuildMesh(Mesh original, int row)
    {
        int segments = row - 1;
        Vector3[] grid = original.vertices;
        var vertices = new List<Vector3>();
        var fade = new List<Vector2>();
        var indices = new List<int>();
        // Read the actual perimeter instead of recomputing it: identical float values
        // and vertex spacing are necessary for a watertight, moving Gerstner seam.
        for (int i = 0; i < segments; i++) vertices.Add(grid[i * row]);
        for (int i = 0; i < segments; i++) vertices.Add(grid[segments * row + i]);
        for (int i = 0; i < segments; i++) vertices.Add(grid[(segments - i) * row + segments]);
        for (int i = 0; i < segments; i++) vertices.Add(grid[segments - i]);
        for (int i = 0; i < vertices.Count; i++) fade.Add(Vector2.zero);

        Vector3 center = (grid[0] + grid[grid.Length - 1]) * .5f;
        Vector3 half = (grid[grid.Length - 1] - grid[0]) * .5f;
        // Only the original edge animates. A narrow skirt joins it to a flat
        // surface; the far plane needs corners/very few edge points, not wave LODs.
        float[] distances = { 20f, 1200f, 100000f };
        int[] subdivisions = { Mathf.Max(4, segments / 4), 4, 4 };
        int previousStart = 0, previousCount = segments * 4;
        for (int ring = 0; ring < distances.Length; ring++)
        {
            float distance = distances[ring];
            int ringSegments = subdivisions[ring];
            int start = vertices.Count;
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = vertices[side * segments] - center;
                Vector3 b = vertices[((side + 1) % 4) * segments] - center;
                a.x *= (half.x + distance) / half.x;
                a.z *= (half.z + distance) / half.z;
                b.x *= (half.x + distance) / half.x;
                b.z *= (half.z + distance) / half.z;
                for (int j = 0; j < ringSegments; j++)
                {
                    vertices.Add(center + Vector3.Lerp(a, b, (float)j / ringSegments));
                    fade.Add(new Vector2(distance, 0f));
                }
            }
            int count = ringSegments * 4;
            int inner = 0, outside = 0;
            while (inner < previousCount || outside < count)
            {
                int a = previousStart + inner % previousCount;
                int b = start + outside % count;
                // Integer comparison also handles grids whose cell count is odd.
                if (outside == count || (inner < previousCount &&
                    (inner + 1) * count <= (outside + 1) * previousCount))
                {
                    indices.Add(a); indices.Add(b);
                    indices.Add(previousStart + (++inner % previousCount));
                }
                else
                {
                    indices.Add(a); indices.Add(b);
                    indices.Add(start + (++outside % count));
                }
            }
            previousStart = start;
            previousCount = count;
        }

        Mesh result = new Mesh { name = "Distant Ocean LOD Rings" };
        if (vertices.Count > 65535) result.indexFormat = IndexFormat.UInt32;
        result.SetVertices(vertices);
        result.SetUVs(0, fade);
        result.SetTriangles(indices, 0);
        // Generous vertical bounds include storm displacement and high viewpoints.
        result.bounds = new Bounds(center, new Vector3((half.x + 100000f) * 2f,
            20000f, (half.z + 100000f) * 2f));
        result.UploadMeshData(true);
        return result;
    }

    private void OnDisable()
    {
        if (outer != null) outer.enabled = false;
    }

    private void OnDestroy()
    {
        if (outer != null) Destroy(outer.gameObject);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
