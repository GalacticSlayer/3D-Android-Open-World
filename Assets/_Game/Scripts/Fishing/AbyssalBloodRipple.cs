using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Boss ring: supplied blood art, 80 m radius, wave-conforming fade.
/// </summary>
public sealed class AbyssalBloodRipple : MonoBehaviour
{
    private const string TexturePath = "Fishing/CastRipple/BloodRipple.png";
    private const string ShaderPath = "Fishing/Hotspots/HotspotParticles";
    private const float StartRadius = 0.08f;
    private const float MaximumRadius = 80f;
    private const float DurationSeconds = 4.5f;
    private const float SurfaceOffset = 0.06f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EffectAlphaId = Shader.PropertyToID("_EffectAlpha");
    private static readonly int FollowSurfaceId = Shader.PropertyToID("_FollowSurface");
    private static readonly int SeaLevelId = Shader.PropertyToID("_HotspotSeaLevel");
    private static Texture2D texture;
    private static Material material;
    private static Mesh quadMesh;
    private static Rect textureRegion;
    private static bool missingAssetsLogged;

    private OceanWater ocean;
    private Vector3 impactPosition;
    private MeshRenderer rippleRenderer;
    private MaterialPropertyBlock properties;
    private bool overPond;
    private float age;
    private float surfaceRotation;

    /// <summary>Emit at the current fish line endpoint during Lightning Strike.</summary>
    public static GameObject Spawn(Vector3 point, OceanWater water)
    {
        if (water == null || !PrepareSharedAssets()) return null;

        var gameObject = new GameObject("Abyssal Leviathan Blood Ripple");
        var ripple = gameObject.AddComponent<AbyssalBloodRipple>();
        ripple.ocean = water;
        ripple.surfaceRotation = Random.Range(0f, 360f);
        ripple.impactPosition = point;
        ripple.overPond = PondWater.TrySurface(point, out _);
        ripple.properties = new MaterialPropertyBlock();

        var filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = quadMesh;

        ripple.rippleRenderer = gameObject.AddComponent<MeshRenderer>();
        ripple.rippleRenderer.sharedMaterial = material;
        ripple.rippleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ripple.rippleRenderer.receiveShadows = false;
        ripple.rippleRenderer.lightProbeUsage = LightProbeUsage.Off;
        ripple.rippleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        ripple.rippleRenderer.sortingOrder = 10;

        ripple.Apply(0f);
        return gameObject;
    }

    private static bool PrepareSharedAssets()
    {
        if (texture == null)
        {
            var source = Resources.Load<TextAsset>(TexturePath);
            if (source != null)
            {
                var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (loaded.LoadImage(source.bytes))
                {
                    // UV-only trim: preserve the exact supplied PNG. Its ring is
                    // low on the canvas and pre-foreshortened; map that region to
                    // a square on the water so perspective is applied only once.
                    Color32[] pixels = loaded.GetPixels32();
                    int left=loaded.width, bottom=loaded.height, right=-1, top=-1;
                    for (int y=0; y<loaded.height; y++)
                    for (int x=0; x<loaded.width; x++)
                    {
                        if (pixels[y*loaded.width+x].a <= 12) continue;
                        left=Mathf.Min(left,x); right=Mathf.Max(right,x);
                        bottom=Mathf.Min(bottom,y); top=Mathf.Max(top,y);
                    }
                    textureRegion = right>=left
                        ? new Rect(left/(float)loaded.width, bottom/(float)loaded.height,
                            (right-left+1)/(float)loaded.width, (top-bottom+1)/(float)loaded.height)
                        : new Rect(0,0,1,1);
                    loaded.wrapMode = TextureWrapMode.Clamp;
                    loaded.filterMode = FilterMode.Bilinear;
                    loaded.Apply(false, true);
                    texture = loaded;
                }
                else Destroy(loaded);
                Resources.UnloadAsset(source);
            }
        }
        if (texture == null)
        {
            if (!missingAssetsLogged)
            {
                Debug.LogWarning("Blood ripple PNG missing from Resources/" + TexturePath);
                missingAssetsLogged = true;
            }
            return false;
        }

        if (material == null)
        {
            Shader shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                if (!missingAssetsLogged)
                {
                    Debug.LogWarning("Blood ripple could not load the fishing hotspot shader.");
                    missingAssetsLogged = true;
                }
                return false;
            }

            material = new Material(shader) { name = "Shared Blood Ripple" };
            material.SetTexture(MainTexId, texture);
            material.SetFloat(FollowSurfaceId, 1f);
            material.SetFloat(EffectAlphaId, 1f);
        }

        if (quadMesh == null)
            quadMesh = CreateSubdividedQuad();

        return true;
    }

    private static Mesh CreateSubdividedQuad()
    {
        // Eight subdivisions let the existing hotspot shader follow ocean waves,
        // rather than cutting straight through them as a single flat quad.
        const int subdivisions = 32;
        int edge = subdivisions + 1;
        var vertices = new Vector3[edge * edge];
        var uvs = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length];
        var triangles = new int[subdivisions * subdivisions * 6];

        int triangle = 0;
        for (int y = 0; y < edge; y++)
        for (int x = 0; x < edge; x++)
        {
            int index = y * edge + x;
            float u = x / (float)subdivisions;
            float v = y / (float)subdivisions;
            vertices[index] = new Vector3(u - 0.5f, 0f, v - 0.5f);
            uvs[index] = new Vector2(textureRegion.x + u * textureRegion.width,
                textureRegion.y + v * textureRegion.height);
            colors[index] = Color.white;

            if (x == subdivisions || y == subdivisions) continue;
            int a = index;
            int b = index + 1;
            int c = index + edge;
            int d = c + 1;
            triangles[triangle++] = a;
            triangles[triangle++] = c;
            triangles[triangle++] = b;
            triangles[triangle++] = b;
            triangles[triangle++] = c;
            triangles[triangle++] = d;
        }

        var mesh = new Mesh { name = "Blood Ripple Ocean-Following Mesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 20f, 1f));
        return mesh;
    }

    private void LateUpdate()
    {

        age += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(age / DurationSeconds);
        Apply(progress);

        if (progress >= 1f)
            Destroy(gameObject);
    }

    private void Apply(float progress)
    {
        if (rippleRenderer == null || ocean == null) return;

        // Ease out to a true eighty-metre visual radius; fade fully to zero alpha.
        float smooth = progress * progress * (3f - 2f * progress);
        float radius = Mathf.Lerp(StartRadius, MaximumRadius, smooth);
        float opacity = 1f - smooth;

        Vector3 surface = impactPosition;
        surface.y = ocean.GetSurfaceHeight(impactPosition) + SurfaceOffset;
        transform.position = surface;
        transform.rotation = Quaternion.Euler(0f, surfaceRotation, 0f);
        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, 1f, diameter);

        ocean.CopyWaveProperties(properties);
        properties.SetFloat(FollowSurfaceId, overPond ? 0f : 1f);
        properties.SetFloat(SeaLevelId, ocean.BaseWaterLevel);
        properties.SetFloat(EffectAlphaId, opacity);
        rippleRenderer.SetPropertyBlock(properties);
    }
}
