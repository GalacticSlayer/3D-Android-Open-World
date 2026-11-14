using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SeaBassImporter
{
    private const string Folder="Assets/_Game/Reef/Source";
    public const string PrefabPath="Assets/Resources/Fishing/SeaBass.prefab";
    public static void Install() => InstallModel("SeaBass",PrefabPath);
    public static void InstallModel(string assetName,string prefabPath,string sourceFolder=null,string texturePath=null,bool preserveWorldUp=false,bool importHeldStruggle=false,bool useIndexedSwimAndStruggle=false,string swimTakeContains=null,bool discardEmptyRenderers=false)
    {
        string folder=sourceFolder??Folder;
        string fbx=folder+"/"+assetName+".fbx", png=texturePath??(folder+"/"+assetName+"Texture.png");
        if(!File.Exists(png))png=folder+"/"+assetName+"Texture.jpg";
        AssetDatabase.ImportAsset(fbx,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(fbx) as ModelImporter;
        if(importer==null)throw new InvalidOperationException("Fish FBX was not imported.");
        importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;
        importer.importCameras=false;importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.optimizeGameObjects=false;importer.isReadable=true;importer.SaveAndReimport();
        var takes=importer.defaultClipAnimations;
        var take=swimTakeContains!=null ? takes.FirstOrDefault(c=>c.takeName.IndexOf(swimTakeContains,StringComparison.OrdinalIgnoreCase)>=0) : importHeldStruggle
            ? (useIndexedSwimAndStruggle ? takes.FirstOrDefault() : takes.FirstOrDefault(c=>c.takeName.IndexOf("Swim",StringComparison.OrdinalIgnoreCase)>=0))
            : takes.OrderByDescending(c=>c.lastFrame-c.firstFrame).FirstOrDefault();
        if(take==null)throw new InvalidOperationException("Fish authored swim take is missing.");
        take.name="Swim";take.loopTime=true;take.loopPose=true;
        if(importHeldStruggle)
        {
            var struggle=useIndexedSwimAndStruggle ? takes.Skip(1).FirstOrDefault() :
                takes.FirstOrDefault(c=>c.takeName.IndexOf("HeldStruggle",StringComparison.OrdinalIgnoreCase)>=0);
            if(struggle==null)throw new InvalidOperationException("Fish authored HeldStruggle take is missing.");
            // Some user-supplied FBX files contain two generic ArmatureAction takes.
            // The opt-in mode preserves their authored export order: swim first,
            // held struggle second. Existing species keep their named-take behavior.
            struggle.name=CaughtFishFlop.HeldStruggleStateName;struggle.loopTime=true;struggle.loopPose=true;
            importer.clipAnimations=new[]{take,struggle};
        }
        else importer.clipAnimations=new[]{take};
        importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c=>c.name=="Swim");
        string controllerPath=folder+"/"+assetName+".controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine=controller.layers[0].stateMachine;
        var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Swim")??machine.AddState("Swim");
        state.motion=clip;machine.defaultState=state;EditorUtility.SetDirty(controller);
        if(importHeldStruggle)
        {
            var heldClip=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .First(c=>c.name==CaughtFishFlop.HeldStruggleStateName);
            var heldState=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==CaughtFishFlop.HeldStruggleStateName)
                ??machine.AddState(CaughtFishFlop.HeldStruggleStateName);
            heldState.motion=heldClip;
            // No automatic transition: only ConfigurePlayerHeld may select this state.
            EditorUtility.SetDirty(controller);
        }
        string matPath=folder+"/"+assetName+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(material==null)
        {
            var shader=Resources.Load<Shader>("Fishing/FishingEquipment");
            if(shader==null)throw new InvalidOperationException("FishingEquipment shader missing.");
            material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);
        }
        // Explicit atlas sampling uses the same verified shader as the authored rod.
        material.shader=Resources.Load<Shader>("Fishing/FishingEquipment");
        if(material.shader==null)throw new InvalidOperationException("FishingEquipment shader missing.");
        var atlas=BuildAtlas(png,assetName,folder);
        material.SetTexture("_BaseMap",atlas);
        material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",0.4f);material.SetFloat("_Metallic",0.06f);
        EditorUtility.SetDirty(material);
        var root=new GameObject(assetName);
        try
        {
            var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),visual.transform,false);
            model.name="AuthoredModel";
            if(PrefabUtility.IsPartOfPrefabInstance(model))
                PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            // Some authored FBXs contain empty mesh placeholders. Keep the
            // hierarchy/animation paths, but exclude them from bounds and skin checks.
            if(discardEmptyRenderers)
            {
                foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if(skin.sharedMesh==null || skin.sharedMesh.vertexCount==0)
                        UnityEngine.Object.DestroyImmediate(skin);
                foreach(var meshRenderer in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter=meshRenderer.GetComponent<MeshFilter>();
                    if(filter==null || filter.sharedMesh==null || filter.sharedMesh.vertexCount==0)
                        UnityEngine.Object.DestroyImmediate(meshRenderer);
                }
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=Enumerable.Repeat(material,Mathf.Max(1,renderer.sharedMaterials.Length)).ToArray();
            var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            var head=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Bone");
            var tail=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Bone.004");
            if(head==null||tail==null)throw new InvalidOperationException("Fish five-bone rig is missing.");
            var forward=(head.position-tail.position).normalized;
            // Flatfish are exported lying on their side; keep world up instead of
            // undoing their authored roll via the FBX root/armature up vector.
            var up=Vector3.ProjectOnPlane(preserveWorldUp?Vector3.up:model.transform.up,forward).normalized;
            if(up.sqrMagnitude<0.01f)up=Vector3.up;
            visual.transform.rotation=Quaternion.Inverse(Quaternion.LookRotation(forward,up))*visual.transform.rotation;
            var bounds=BoundsOf(root);
            visual.transform.localScale*=0.92f/Mathf.Max(0.001f,bounds.size.z);
            bounds=BoundsOf(root);visual.transform.position+=root.transform.position-bounds.center;
            // Reuse the proven Red Snapper controller: aquarium trail updates,
            // held-fish mouth anchors, and ambient trails already support it.
            root.AddComponent<RedSnapperPresentation>().Configure(animator);
            // Bind an actual lip vertex to the animated head, instead of projecting
            // the snapper's longer snout beyond this model's mouth.
            var points=new System.Collections.Generic.List<Vector3>();
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(skin.sharedMesh.uv.Length==0)throw new InvalidOperationException("Fish UV0 missing.");
                // Evaluate skinning explicitly in world space. This avoids FBX
                // unit-scale ambiguity when baking a scaled renderer hierarchy.
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var bind=mesh.bindposes;var bones=skin.bones;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];Vector3 world=Vector3.zero;
                    int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                    float[] values={w.weight0,w.weight1,w.weight2,w.weight3};
                    for(int j=0;j<4;j++)if(values[j]>0)
                        world+=(bones[ids[j]].localToWorldMatrix*bind[ids[j]]).MultiplyPoint3x4(vertices[i])*values[j];
                    points.Add(root.transform.InverseTransformPoint(world));
                }
            }
            if(points.Count==0)throw new InvalidOperationException("Fish skinned mesh missing.");
            float front=points.Max(p=>p.z),back=points.Min(p=>p.z);
            var lips=points.Where(p=>p.z>=front-(front-back)*.012f).ToArray();
            Vector3 lip=Vector3.zero;foreach(var p in lips)lip+=p;lip/=lips.Length;
            var mouth=new GameObject("AuthoredMouthAnchor").transform;
            mouth.SetParent(head,false);mouth.position=root.transform.TransformPoint(lip);
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            if(!AssetDatabase.IsValidFolder("Assets/Resources/Fishing"))AssetDatabase.CreateFolder("Assets/Resources","Fishing");
            var saved=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            AssetDatabase.SaveAssets();
            if(saved==null || !AssetDatabase.GetDependencies(prefabPath,true).Contains(AssetDatabase.GetAssetPath(atlas)))
                throw new InvalidOperationException("Saved fish prefab lost its texture dependency.");
            foreach(var renderer in saved.GetComponentsInChildren<Renderer>(true))
                foreach(var mat in renderer.sharedMaterials)
                    if(mat==null || mat.GetTexture("_BaseMap")!=atlas)
                        throw new InvalidOperationException("Saved fish material is missing the authored atlas.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }
    // Decode the supplied PNG without depending on its TextureImporter artifact.
    // Native assets retain their GUID across reinstalls and are build dependencies.
    private static Texture2D BuildAtlas(string png,string assetName,string folder)
    {
        string source=Path.GetFullPath(Path.Combine(Application.dataPath,"..",png));
        if(!File.Exists(source))throw new FileNotFoundException("Fish source PNG is missing. Pull the complete update.",source);
        byte[] bytes=File.ReadAllBytes(source);
        var decoded=new Texture2D(2,2,TextureFormat.RGBA32,true,false);
        try
        {
            if(!ImageConversion.LoadImage(decoded,bytes,false) || decoded.width<2 || decoded.height<2)
                throw new InvalidOperationException("Cannot decode fish PNG: "+source+" ("+bytes.Length+" bytes). Pull the source PNG again.");
            decoded.name=assetName+" Authored Atlas";
            decoded.wrapMode=TextureWrapMode.Repeat;decoded.filterMode=FilterMode.Trilinear;
            decoded.anisoLevel=2;decoded.Apply(true,false);
            string path=folder+"/"+assetName+"Atlas.asset";
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(atlas==null)
            {
                AssetDatabase.CreateAsset(decoded,path);
                atlas=decoded;decoded=null;
            }
            else EditorUtility.CopySerialized(decoded,atlas);
            EditorUtility.SetDirty(atlas);AssetDatabase.SaveAssets();
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(saved==null || saved.width<2 || saved.height<2)
                throw new InvalidOperationException("Could not save the decoded fish atlas at "+path);
            Debug.Log("Fish atlas ready: "+saved.width+" x "+saved.height+" from the supplied PNG.");
            return saved;
        }
        finally {if(decoded!=null)UnityEngine.Object.DestroyImmediate(decoded);}
    }
    private static Bounds BoundsOf(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new InvalidOperationException("Fish mesh missing.");
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
    }
}

