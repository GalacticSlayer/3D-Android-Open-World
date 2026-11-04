#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// Imports the ORIGINAL Level 7 Blender meshes and UVs with the same assembly
/// convention as FishingRodLevel5Importer / FishingReelLevel3Importer.
/// Never stretches individual reel parts to fit old bounds: their authored
/// relative positions, shared scale, pivot offsets and UVs are retained.
/// Gameplay and animation transforms come from the proven Level 6 prefabs.
/// </summary>
[InitializeOnLoad]
public static class FishingLevel7GearImporter
{
    private const string Source = "Assets/_Game/Fishing/RodReel/Level7/Source/";
    private const string Generated = "Assets/_Game/Fishing/RodReel/Level7/Generated/";
    private const string TextureSource = Source + "Level7EquipmentTexture.png";
    private const string RodTemplate = "Assets/Resources/Fishing/FishingRodReelLevel6Rod.prefab";
    private const string ReelTemplate = "Assets/Resources/Fishing/FishingRodReelLevel6.prefab";
    private const string RodPrefab = "Assets/Resources/Fishing/FishingRodReelLevel7Rod.prefab";
    private const string ReelPrefab = "Assets/Resources/Fishing/FishingRodReelLevel7.prefab";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";
    private const string RepairSessionKey = "OpenWorld.Level7Gear.CorrectAssembly.20261103.v2";
    private const string[] ReelParts =
        { "ReelFootAndBody", "Rotor", "ReelHousing", "Spool", "Handle" };
    private static bool running;

    [Serializable] private sealed class RodGeometry
    {
        public float[] vertices;
        public float[] uv;
        public int[] triangles;
    }

    [Serializable] private sealed class ReelGeometry
    {
        public ReelPart[] parts;
    }

    [Serializable] private sealed class ReelPart
    {
        public string name;
        public float[] position;
        public float[] vertices;
        public float[] normals;
        public float[] uv;
        public int[] triangles;
    }

    static FishingLevel7GearImporter()
    {
        EditorApplication.delayCall += RepairPreviousImport;
    }

    // One-time per Editor session: repairs even an EXISTING incorrectly imported
    // Level 7 prefab. The previous importer skipped it when the prefab existed.
    private static void RepairPreviousImport()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode ||
            Application.isBatchMode || SessionState.GetBool(RepairSessionKey, false))
            return;
        if (!HaveOriginalBlends()) return;
        SessionState.SetBool(RepairSessionKey, true);
        Build(false);
    }

    [MenuItem("Tools/Open World/Import Level 7 Fishing Rod and Reel (One Click)")]
    public static void ImportFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Level 7 gear", "Exit Play Mode before importing.", "OK");
            return;
        }
        Build(true);
    }

    public static void ImportIfAvailable()
    {
        if (!running && !EditorApplication.isPlayingOrWillChangePlaymode &&
            !SessionState.GetBool(RepairSessionKey, false))
            RepairPreviousImport();
    }

    private static string Downloads =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private static string FindOriginalBlend(string name)
    {
        string[] directories = {
            Downloads,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
            Path.GetFullPath(Source)
        };
        foreach (string dir in directories)
        {
            string path = Path.Combine(dir, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static bool HaveOriginalBlends() =>
        FindOriginalBlend("Level 7 Fishing Rod.blend") != null &&
        FindOriginalBlend("Level 7 Fishing Reel.blend") != null;

    // The user's .blend/.fbx/.png files remain in Downloads. The FBX and one
    // shared authored texture are COPIED into Assets; .blend files are read
    // directly from Downloads by Blender, so there is no ZIP to extract.
    private static void EnsureSourceAssets()
    {
        Directory.CreateDirectory(Source);
        foreach (string pair in new [] {
            "Level 7 Fishing Rod.fbx|Level7FishingRod.fbx",
            "Level 7 Fishing Reel.fbx|Level7FishingReel.fbx",
            "Level 7 Fishing Rod Texture.png|Level7EquipmentTexture.png"
        })
        {
            string[] tokens = pair.Split('|');
            string destination = Source + tokens[1];
            if (File.Exists(destination)) continue;
            string original = Path.Combine(Downloads, tokens[0]);
            if (!File.Exists(original) && tokens[0].Contains("Rod Texture"))
                original = Path.Combine(Downloads, "Level 7 Fishing Reel Texture.png");
            if (!File.Exists(original))
                throw new FileNotFoundException("Missing original Level 7 asset in Downloads: " + tokens[0], original);
            File.Copy(original, destination);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static string FindBlender(bool showDialog)
    {
        string blender = EditorPrefs.GetString(BlenderPrefsKey, "");
        if (File.Exists(blender)) return blender;
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string root = Path.Combine(programFiles, "Blender Foundation");
        if (Directory.Exists(root))
        {
            blender = Directory.EnumerateFiles(root, "blender.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (File.Exists(blender))
            {
                EditorPrefs.SetString(BlenderPrefsKey, blender);
                return blender;
            }
        }
        if (!showDialog) return null;
        blender = EditorUtility.OpenFilePanel("Locate Blender (used by the previous rod/reel importers)",
            programFiles, "exe");
        if (File.Exists(blender))
        {
            EditorPrefs.SetString(BlenderPrefsKey, blender);
            return blender;
        }
        return null;
    }

    private static void Build(bool showDialog)
    {
        if (running) return;
        running = true;
        try
        {
            string rodBlend = FindOriginalBlend("Level 7 Fishing Rod.blend");
            string reelBlend = FindOriginalBlend("Level 7 Fishing Reel.blend");
            if (rodBlend == null || reelBlend == null)
                throw new FileNotFoundException("Both original Level 7 Fishing Rod.blend and " +
                    "Level 7 Fishing Reel.blend are required to preserve their mesh transforms. " +
                    "Keep them in Downloads (no ZIP extraction).");
            string blender = FindBlender(showDialog);
            if (string.IsNullOrEmpty(blender))
                throw new FileNotFoundException("Blender is required for the same exact mesh import " +
                    "method used by earlier rods/reels. No approximate or distorted FBX import was performed.");

            EnsureSourceAssets();
            Directory.CreateDirectory(Generated);
            string scratch = Path.GetFullPath("Library/FishingLevel7Import");
            Directory.CreateDirectory(scratch);
            string rodJson = Path.Combine(scratch, "Rod.json");
            string reelJson = Path.Combine(scratch, "Reel.json");
            ExportBlender(blender, rodBlend, Path.Combine(scratch, "ExportRod.py"),
                Path.Combine(scratch, "Rod.json"), RodScript);
            ExportBlender(blender, reelBlend, Path.Combine(scratch, "ExportReel.py"),
                Path.Combine(scratch, "Reel.json"), ReelScript);

            RodGeometry rod = JsonUtility.FromJson<RodGeometry>(File.ReadAllText(rodJson));
            ReelGeometry reel = JsonUtility.FromJson<ReelGeometry>(File.ReadAllText(reelJson));
            ValidateGeometry(rod, reel);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureSource);
            if (texture == null)
                throw new InvalidDataException("Level 7 shared texture was not imported.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Resources/Fishing/FishingEquipment.shader");
            if (shader == null) throw new InvalidDataException("Existing FishingEquipment shader is missing.");

            Material rodMaterial = MatchExistingMaterial(
                Generated + "Level7FishingRod.mat", RodTemplate, "RodBlank", texture);
            Material reelMaterial = MatchExistingMaterial(
                Generated + "Level7FishingReel.mat", ReelTemplate, "Spool", texture);

            Mesh rodMesh = MakeRodMesh(rod, 1.80232f);
            Mesh savedRod = SaveMesh(Generated + "Level7RodBlank.asset", rodMesh);
            MakeRodPrefab(savedRod, rodMaterial);

            MakeReelPrefab(reel, reelMaterial);

            AssetDatabase.SaveAssets();
            FishingLevel7RodIconGenerator.Generate(savedRod, texture);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("[LEVEL 7 GEAR] Imported correctly: rigid rod dimensions, authored reel " +
                "assembly/UVs, working reel animation, third-person and multiplayer Resources paths.");
            SessionState.SetBool(RepairSessionKey, true);
            if (showDialog)
                EditorUtility.DisplayDialog("Level 7 Fishing Gear",
                    "Both models, their original UVs, the existing equipment animations " +
                    "and matching hotbar/shop/equipment icons have been rebuilt.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[LEVEL 7 GEAR IMPORT FAILED] " + ex);
            // Don't overwrite earlier assets when any source data is invalid.
            if (showDialog) EditorUtility.DisplayDialog("Level 7 Gear Import", ex.Message, "OK");
        }
        finally { running = false; }
    }

    private static void ExportBlender(string blender, string blend, string scriptPath,
        string resultPath, string python)
    {
        if (File.Exists(resultPath)) File.Delete(resultPath);
        File.WriteAllText(scriptPath, python);
        var info = new ProcessStartInfo {
            FileName = blender,
            Arguments = "--background " + Quote(blend) + " --python " + Quote(scriptPath) +
                        " -- " + Quote(resultPath),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        using (Process process = Process.Start(info))
        {
            if (process == null) throw new InvalidOperationException("Could not launch Blender.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(120000))
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException("Blender timed out while reading " + blend);
            }
            if (process.ExitCode != 0 || !File.Exists(resultPath))
                throw new InvalidOperationException("Blender failed to export " + blend +
                    "\n" + output + "\n" + error);
        }
    }

    // Same geometry export as FishingRodLevel5Importer: retains source UVs and
    // world transforms; source rod is oriented uniformly in the gameplay rig.
    private const string RodScript = @"import bpy, json, os, sys
out_path=sys.argv[sys.argv.index('--')+1]
scene=bpy.context.scene
scene.frame_set(1)
deps=bpy.context.evaluated_depsgraph_get()
meshes=[o for o in scene.objects if o.type=='MESH']
if not meshes: raise RuntimeError('Level 7 rod Blend contains no mesh.')
obj=max(meshes,key=lambda o:len(o.data.polygons))
eo=obj.evaluated_get(deps)
me=eo.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
try:
    me.calc_loop_triangles()
    uv_layer=me.uv_layers.active.data if me.uv_layers.active else None
    vertices=[]; uv=[]; triangles=[]
    world=obj.matrix_world
    flip=world.to_3x3().determinant()<0
    for tri in me.loop_triangles:
        ids=[]
        for li in tri.loops:
            loop=me.loops[li]
            p=world @ me.vertices[loop.vertex_index].co
            t=uv_layer[li].uv if uv_layer else (0.0,0.0)
            ids.append(len(vertices)//3)
            vertices.extend((p.x,p.y,p.z))
            uv.extend((float(t[0]),float(t[1])))
        if flip: ids[1],ids[2]=ids[2],ids[1]
        triangles.extend(ids)
    with open(out_path,'w',encoding='utf-8') as f:
        json.dump({'vertices':vertices,'uv':uv,'triangles':triangles},f,separators=(',',':'))
finally: eo.to_mesh_clear()
";

    // Same *assembled coordinate basis* as FishingReelLevel3Importer.
    // The names below are the original Blender part names, NOT arbitrary
    // mesh size rankings. This corrects the swapped spool/housing issue.
    private const string ReelScript = @"import bpy, json, os, sys
from mathutils import Matrix
out_path=sys.argv[sys.argv.index('--')+1]
name_map={'Plane':'ReelFootAndBody','Cylinder':'Rotor','Cylinder.001':'ReelHousing','Cylinder.002':'Spool','Cylinder.003':'Handle'}
scene=bpy.context.scene
scene.frame_set(1)
objs={o.name:o for o in scene.objects if o.type=='MESH'}
missing=[name for name in name_map if name not in objs]
if missing: raise RuntimeError('Missing original Level 7 reel mesh(es): '+', '.join(missing))
basis=Matrix(((1,0,0),(0,-1,0),(0,0,-1))) * 0.04
parts=[]
deps=bpy.context.evaluated_depsgraph_get()
for source_name,target_name in name_map.items():
    obj=objs[source_name]
    eo=obj.evaluated_get(deps)
    mesh=eo.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
    try:
        mesh.calc_loop_triangles()
        rest=obj.matrix_local.copy()
        linear=basis @ rest.to_3x3()
        normal_matrix=linear.inverted().transposed()
        pos=basis @ rest.translation
        uv_layer=mesh.uv_layers.active.data if mesh.uv_layers.active else None
        vertices=[]; normals=[]; uv=[]; triangles=[]
        for tri in mesh.loop_triangles:
            ids=[]
            for li in tri.loops:
                loop=mesh.loops[li]
                v=mesh.vertices[loop.vertex_index]
                p=linear @ v.co
                n=(normal_matrix @ loop.normal).normalized()
                t=uv_layer[li].uv if uv_layer else (0.0,0.0)
                ids.append(len(vertices)//3)
                vertices.extend((p.x,p.y,p.z))
                normals.extend((n.x,n.y,n.z))
                uv.extend((float(t[0]),float(t[1])))
            if linear.determinant()<0: ids[1],ids[2]=ids[2],ids[1]
            triangles.extend(ids)
        parts.append({'name':target_name,'position':[pos.x,pos.y,pos.z],
            'vertices':vertices,'normals':normals,'uv':uv,'triangles':triangles})
    finally: eo.to_mesh_clear()
with open(out_path,'w',encoding='utf-8') as f:
    json.dump({'parts':parts},f,separators=(',',':'))
";

    private static void ValidateGeometry(RodGeometry rod, ReelGeometry reel)
    {
        if (rod == null || rod.vertices == null || rod.vertices.Length < 9 ||
            rod.vertices.Length % 3 != 0 ||
            rod.uv == null || rod.uv.Length != rod.vertices.Length / 3 * 2 ||
            rod.triangles == null || rod.triangles.Length < 3 ||
            rod.triangles.Length % 3 != 0)
            throw new InvalidDataException("Level 7 rod mesh or UVs are incomplete.");
        if (reel == null || reel.parts == null || reel.parts.Length != 5 ||
            !new HashSet<string>(reel.parts.Select(p => p.name)).SetEquals(ReelParts))
            throw new InvalidDataException("Level 7 reel does not have the expected five named pieces.");
        foreach (ReelPart part in reel.parts)
        {
            if (part.position == null || part.position.Length != 3 ||
                part.vertices == null || part.vertices.Length < 9 ||
                part.vertices.Length % 3 != 0 ||
                part.normals == null || part.normals.Length != part.vertices.Length ||
                part.uv == null || part.uv.Length != part.vertices.Length / 3 * 2 ||
                part.triangles == null || part.triangles.Length < 3 ||
                part.triangles.Length % 3 != 0)
                throw new InvalidDataException("Bad reel geometry/UVs: " + part.name);
        }
    }

    private static Mesh MakeRodMesh(RodGeometry g, float targetLength)
    {
        int count = g.vertices.Length / 3;
        var from = new Vector3[count];
        Vector3 min = new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
        for (int i = 0; i < count; i++)
        {
            Vector3 p = new Vector3(g.vertices[i*3],g.vertices[i*3+1],g.vertices[i*3+2]);
            from[i] = p; min = Vector3.Min(min,p); max = Vector3.Max(max,p);
        }
        Vector3 span = max - min;
        int longAxis = span.x >= span.y && span.x >= span.z ? 0 :
                       span.y >= span.z ? 1 : 2;
        int sideA = longAxis == 0 ? 1 : 0, sideB = longAxis == 2 ? 1 : 2;
        if (longAxis == 1) { sideA = 0; sideB = 2; }
        float lo = Axis(min,longAxis), hi = Axis(max,longAxis);
        float baseValue = Mathf.Abs(lo) <= Mathf.Abs(hi) ? lo : hi;
        float sign = baseValue == lo ? 1f : -1f;
        float scale = targetLength / Mathf.Max(0.000001f,hi-lo);
        float ca = (Axis(min,sideA)+Axis(max,sideA))*.5f;
        float cb = (Axis(min,sideB)+Axis(max,sideB))*.5f;
        Vector3[] vertices = new Vector3[count];
        Vector2[] uv = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 p = from[i];
            vertices[i] = new Vector3(
                (Axis(p,sideA)-ca)*scale,
                (Axis(p,longAxis)-baseValue)*sign*scale,
                (Axis(p,sideB)-cb)*scale);
            uv[i] = new Vector2(g.uv[i*2],g.uv[i*2+1]);
        }
        int[] triangles = (int[])g.triangles.Clone();
        Vector3 tx = MapDirection(Vector3.right,longAxis,sideA,sideB,sign);
        Vector3 ty = MapDirection(Vector3.up,longAxis,sideA,sideB,sign);
        Vector3 tz = MapDirection(Vector3.forward,longAxis,sideA,sideB,sign);
        if (Vector3.Dot(Vector3.Cross(tx,ty),tz) < 0f)
            for (int i=0;i<triangles.Length;i+=3)
            { int tmp=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=tmp; }
        var mesh = new Mesh { name = "Level7FishingRod" };
        if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        if (mesh.bounds.size.y < 1.70f || mesh.bounds.size.y > 1.85f)
            throw new InvalidDataException("Level 7 rod length is not aligned to the existing RodTip.");
        return mesh;
    }

    private static Mesh MakeReelMesh(ReelPart part, Vector3 preservedPosition)
    {
        int count = part.vertices.Length / 3;
        Vector3[] vertices = new Vector3[count];
        Vector3[] normals = new Vector3[count];
        Vector2[] uv = new Vector2[count];
        Vector3 sourceOrigin = new Vector3(part.position[0], part.position[1], part.position[2]);
        Vector3 offset = sourceOrigin - preservedPosition;
        for (int i=0;i<count;i++)
        {
            int v=i*3,u=i*2;
            vertices[i] = new Vector3(part.vertices[v],part.vertices[v+1],part.vertices[v+2]) + offset;
            normals[i] = new Vector3(part.normals[v],part.normals[v+1],part.normals[v+2]);
            uv[i] = new Vector2(part.uv[u],part.uv[u+1]);
        }
        var mesh = new Mesh { name = "Level7" + part.name };
        if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv;
        mesh.triangles = part.triangles; mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    private static Mesh SaveMesh(string path, Mesh mesh)
    {
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, saved);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(saved);
        return saved;
    }

    private static Material MatchExistingMaterial(string path, string prefabPath,
        string partName, Texture2D texture)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Transform part = prefab != null ? Find(prefab.transform, partName) : null;
        Material template = part != null ? part.GetComponent<MeshRenderer>()?.sharedMaterial : null;
        if (template == null) throw new InvalidDataException("Missing existing material: " + prefabPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(template);
            material.name = "Level7" + partName;
            AssetDatabase.CreateAsset(material,path);
        }
        else material.CopyPropertiesFromMaterial(template);
        material.shader = template.shader;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        material.mainTexture = texture;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CloneTemplate(string path, string name)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (template == null) throw new InvalidDataException("Missing existing Level 6 prefab: " + path);
        GameObject clone = PrefabUtility.InstantiatePrefab(template) as GameObject;
        if (clone == null) clone = Object.Instantiate(template);
        if (PrefabUtility.IsPartOfPrefabInstance(clone))
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        clone.name = name;
        return clone;
    }

    private static void MakeRodPrefab(Mesh mesh, Material material)
    {
        GameObject clone = CloneTemplate(RodTemplate, "FishingRodReelLevel7Rod");
        try
        {
            Transform blank = Find(clone.transform,"RodBlank");
            Transform tip = Find(clone.transform,"RodTip");
            Transform mount = Find(clone.transform,"ReelMount");
            if (blank==null || tip==null || mount==null ||
                clone.GetComponent<FishingRodView>()==null)
                throw new InvalidDataException("Level 6 rod reference hierarchy is incomplete.");
            Vector3 tipPosition=tip.localPosition, mountPosition=mount.localPosition;
            Quaternion mountRotation=mount.localRotation;
            Animation clip = mount.GetComponent<Animation>();
            AnimationClip originalClip = clip!=null ? clip.clip : null;
            blank.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = blank.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(null);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (Vector3.Distance(tip.localPosition,tipPosition) > .000001f ||
                Vector3.Distance(mount.localPosition,mountPosition) > .000001f ||
                Quaternion.Angle(mount.localRotation,mountRotation) > .0001f ||
                (clip!=null && clip.clip!=originalClip))
                throw new InvalidOperationException("Rod tip, mount or animation unexpectedly changed.");
            SavePrefab(clone,RodPrefab);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void MakeReelPrefab(ReelGeometry reel, Material material)
    {
        GameObject clone = CloneTemplate(ReelTemplate, "FishingRodReelLevel7");
        try
        {
            Transform mount = Find(clone.transform,"ReelMount");
            if (mount == null) throw new InvalidDataException("Level 6 reel mount is missing.");
            Vector3 originalPosition = mount.localPosition;
            Quaternion originalRotation = mount.localRotation;
            Animation animation = mount.GetComponent<Animation>();
            AnimationClip clip = animation!=null ? animation.clip : null;
            foreach (string name in ReelParts)
            {
                ReelPart part = reel.parts.Single(p => p.name==name);
                Transform target = mount.Find(name);
                if (target == null) throw new InvalidDataException("Missing existing animated part: " + name);
                Vector3 localPosition = target.localPosition;
                // The original Level 7 FBX pivots exactly match these existing
                // reel pivots (to five decimal places after Blender axis/unit
                // conversion). Reject any future misassembled source instead
                // of quietly warping each part to look plausible.
                Vector3 authoredPosition = new Vector3(part.position[0],
                    part.position[1],part.position[2]);
                if (Vector3.Distance(authoredPosition,localPosition) > .002f)
                    throw new InvalidDataException("Level 7 " + name +
                        " authored pivot does not match the working reel mount: " +
                        authoredPosition + " / " + localPosition);
                Quaternion localRotation = target.localRotation;
                Vector3 localScale = target.localScale;
                Mesh mesh = MakeReelMesh(part, localPosition);
                // These meshes use the ONE original Blender assembly coordinate
                // system; NEVER independently stretch or re-centre the parts.
                Mesh saved = SaveMesh(Generated + "Level7" + name + ".asset", mesh);
                target.GetComponent<MeshFilter>().sharedMesh = saved;
                MeshRenderer renderer = target.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(null);
                if (Vector3.Distance(target.localPosition,localPosition) > .000001f ||
                    Quaternion.Angle(target.localRotation,localRotation) > .0001f ||
                    Vector3.Distance(target.localScale,localScale) > .000001f)
                    throw new InvalidOperationException("Reel animation pivot altered: " + name);
            }
            if (Vector3.Distance(mount.localPosition,originalPosition) > .000001f ||
                Quaternion.Angle(mount.localRotation,originalRotation) > .0001f ||
                (animation!=null && animation.clip!=clip))
                throw new InvalidOperationException("Existing reeling animation or mount altered.");
            SavePrefab(clone,ReelPrefab);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void SavePrefab(GameObject source, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(source,path);
        if (saved==null) throw new IOException("Unable to create " + path);
    }

    private static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
    private static float Axis(Vector3 v,int n)=>n==0?v.x:n==1?v.y:v.z;
    private static Vector3 MapDirection(Vector3 v,int longAxis,int a,int b,float sign)
        => new Vector3(Axis(v,a),Axis(v,longAxis)*sign,Axis(v,b));
    private static string Quote(string value)=>"\"" + value.Replace("\"","\\\"") + "\"";

    [MenuItem("Tools/Open World/Validate Level 7 Fishing Gear")]
    public static void Validate()
    {
        GameObject rod = AssetDatabase.LoadAssetAtPath<GameObject>(RodPrefab);
        GameObject reel = AssetDatabase.LoadAssetAtPath<GameObject>(ReelPrefab);
        if (rod==null || reel==null) { Debug.LogError("Level 7 prefabs are missing.");return; }
        MeshFilter rodBlank = Find(rod.transform,"RodBlank")?.GetComponent<MeshFilter>();
        bool good = rodBlank!=null && rodBlank.sharedMesh!=null &&
                    rodBlank.sharedMesh.bounds.size.y>1.7f;
        Transform mount = Find(reel.transform,"ReelMount");
        good &= mount!=null && mount.GetComponent<Animation>()?.clip!=null;
        foreach (string part in ReelParts)
        {
            Transform p=Find(reel.transform,part);
            good &= p!=null && p.GetComponent<MeshFilter>()?.sharedMesh!=null;
        }
        good &= AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/Fishing/ItemIcons/Rod6.png")!=null;
        good &= AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/Fishing/ItemIcons/Rod6Card.png")!=null;
        good &= ShopCatalog.MaxRodTier==6 && ShopCatalog.MaxReelTier==6;
        good &= FishingBurstDamageRuntime.NormalMinimumForTier(6)==55 &&
                FishingBurstDamageRuntime.NormalMaximumForTier(6)==75 &&
                Mathf.Approximately(Level2FishingReelRuntime.ReelMultiplier(6),2.20f);
        if (good) Debug.Log("Level 7 equipment: geometry, transforms, animations, icons and gear stats verified.");
        else Debug.LogError("Level 7 equipment validation failed; inspect the imported prefabs.");
    }
}
#endif
