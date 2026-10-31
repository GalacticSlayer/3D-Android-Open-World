using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

// Assets only: never rebuilds the marina, edits a scene, or changes fishing CSVs.
[InitializeOnLoad]
public static class RowboatSetup
{
    private const string Root="Assets/_Game/Boats/Rowboat";
    private const string Generated=Root+"/Generated";
    private const string PrefabPath="Assets/Resources/Boats/Rowboat.prefab";
    private const string DataPath="Assets/Resources/Boats/Rowboat.asset";
    [Serializable] private sealed class CollisionSource {public CollisionPiece[] pieces;}
    [Serializable] private sealed class CollisionPiece {public string name;public Vector3[] vertices;public int[] triangles;}
    [Serializable] private sealed class Source {public float fps,duration;public Part[] parts;}
    [Serializable] private sealed class Part
    {
        public string name;public bool moving;
        public Vector3[] vertices,normals,positions;public Vector2[] uv;
        public int[] triangles;public Quaternion[] rotations;
    }
    static RowboatSetup(){EditorApplication.delayCall+=InstallWhenReady;}
    private static void InstallWhenReady()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating){EditorApplication.delayCall+=InstallWhenReady;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}catch(Exception e){Debug.LogError("[ROWBOAT] Install failed; existing world was not changed. "+e);}
    }
    public static void EnsureInstalled()
    {
        var old=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var existing=AssetDatabase.LoadAssetAtPath<BoatData>(DataPath);
        if(old!=null && existing!=null && existing.Prefab==old && old.GetComponent<RowboatPaddleAnimator>()?.SourceVersion==3 && old.transform.localScale==Vector3.one*.72f)return;
        Directory.CreateDirectory(Generated);Directory.CreateDirectory("Assets/Resources/Boats");AssetDatabase.Refresh();
        Source solo=Read("Solo",3),tandem=Read("Tandem",5);
        var hull=MakeMaterial("Hull","Rowboat Textures.png");
        var oars=MakeMaterial("Oars","Oar Texture.jpg");
        var data=existing;
        if(data==null)
        {
            data=ScriptableObject.CreateInstance<BoatData>();data.name="Rowboat";data.ID="rowboat";data.Cost=1500;data.Speed=4.5f;
            data.MaxCapacity=2;
            AssetDatabase.CreateAsset(data,DataPath);
        }
        // World-space physics/clearance dimensions match the uniformly scaled prefab.
        data.HullSize=new Vector3(1.8216f,.6696f,5.6592f);data.Draft=.324f;
        var root=new GameObject("Rowboat");
        root.transform.localScale=Vector3.one*.72f;
        try
        {
            var rb=root.AddComponent<Rigidbody>();rb.useGravity=false;rb.mass=300;rb.linearDamping=.65f;rb.angularDamping=3;
            rb.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            var controller=root.AddComponent<BoatController>();controller.Data=data;
            var model=Child(root.transform,"ModelContainer");
            BuildMesh(model,tandem.parts.Single(p=>!p.moving),hull,"Hull");
            var anim=root.AddComponent<RowboatPaddleAnimator>();
            anim.SoloOars=Child(model,"SoloOars").gameObject;
            anim.TandemOars=Child(model,"TandemOars").gameObject;
            anim.SoloParts=BuildOars(anim.SoloOars.transform,solo,oars,"Solo");
            anim.TandemParts=BuildOars(anim.TandemOars.transform,tandem,oars,"Tandem");
            anim.SoloStroke=BuildStroke(solo,"Solo");anim.TandemStroke=BuildStroke(tandem,"Tandem");
            anim.TandemOars.SetActive(true);
            for(int i=0;i<2;i++)anim.TandemParts[i].gameObject.SetActive(false);
            // Convex pieces follow the authored floor, curved shell, benches and
            // stationary mounts. No broad box fills the walkable hollow interior.
            BuildCollision(root.transform);
            controller.DriverSeat=Child(root.transform,"FrontRowingSeat",new Vector3(0,-.22f,1.25f));
            controller.SecondRowingSeat=Child(root.transform,"RearRowingSeat",new Vector3(0,-.22f,-1.12f));
            controller.DeckExit=Child(root.transform,"DeckExit",new Vector3(0,-.20f,-2.65f));
            data.Prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        }
        finally{Object.DestroyImmediate(root);}
        Debug.Log("[ROWBOAT] Installed: raft 1 seat, rowboat 2 seats; 4.5 m/s solo, 9 m/s tandem defaults. No scene setup needed.");
    }
    private static void BuildCollision(Transform root)
    {
        var source=JsonUtility.FromJson<CollisionSource>(File.ReadAllText(Root+"/Source/Collision.json"));
        if(source?.pieces==null || source.pieces.Length==0)throw new InvalidOperationException("Missing authored rowboat collision shapes.");
        var parent=Child(root,"HullCollision");
        foreach(var part in source.pieces)
        {
            string path=Generated+"/Collision-"+part.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name="Collision-"+part.name;mesh.vertices=part.vertices;mesh.triangles=part.triangles;
            mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var collider=Child(parent,part.name).gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=mesh;collider.convex=true;
        }
    }

    private static Source Read(string name,int parts)
    {
        var result=JsonUtility.FromJson<Source>(File.ReadAllText(Root+"/Source/"+name+".json"));
        if(result==null || result.parts==null || result.parts.Length!=parts || result.fps!=96 || result.duration<=0)
            throw new InvalidOperationException(name+" source is incomplete.");
        foreach(var p in result.parts)
            if(p.vertices.Length!=p.uv.Length || p.positions.Length!=145 || p.rotations.Length!=145)
                throw new InvalidOperationException(name+" mesh/UV/animation mismatch: "+p.name);
        return result;
    }
    private static Material MakeMaterial(string name,string file)
    {
        string texturePath=Root+"/Source/"+file;
        AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(texture==null || shader==null)throw new InvalidOperationException("Missing rowboat texture or URP shader: "+file);
        string path=Generated+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.shader=shader;mat.name=name;mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);
        mat.SetFloat("_Smoothness",.18f);mat.SetFloat("_Metallic",0);EditorUtility.SetDirty(mat);return mat;
    }
    private static Transform BuildMesh(Transform parent,Part p,Material material,string key)
    {
        string path=Generated+"/"+key+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.name=key;mesh.vertices=p.vertices;mesh.normals=p.normals;mesh.uv=p.uv;mesh.triangles=p.triangles;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var part=Child(parent,p.name,p.positions[0]);part.localRotation=p.rotations[0];
        part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        part.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return part;
    }
    private static Transform[] BuildOars(Transform parent,Source source,Material material,string key)
    {return source.parts.Where(p=>p.moving).Select(p=>BuildMesh(parent,p,material,key+"-"+p.name)).ToArray();}
    private static RowboatStrokeData BuildStroke(Source source,string key)
    {
        string path=Generated+"/"+key+"Stroke.asset";
        var data=AssetDatabase.LoadAssetAtPath<RowboatStrokeData>(path);
        if(data==null){data=ScriptableObject.CreateInstance<RowboatStrokeData>();AssetDatabase.CreateAsset(data,path);}
        data.SamplesPerSecond=source.fps;data.Duration=source.duration;
        data.Tracks=source.parts.Where(p=>p.moving).Select(p=>new RowboatStrokeData.Track{Positions=p.positions,Rotations=p.rotations}).ToArray();
        EditorUtility.SetDirty(data);return data;
    }
    private static Transform Child(Transform parent,string name,Vector3 position=default)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
    private static Transform Box(Transform parent,string name,Vector3 center,Vector3 size)
    {var t=Child(parent,name,center);t.gameObject.AddComponent<BoxCollider>().size=size;return t;}

    [MenuItem("Tools/Open World/Check Rowboat")]
    public static void Check()
    {
        EnsureInstalled();var d=AssetDatabase.LoadAssetAtPath<BoatData>(DataPath);
        var p=d.Prefab.GetComponent<RowboatPaddleAnimator>();
        string report="[ROWBOAT] ID="+d.ID+"; capacity="+d.MaxCapacity+"; solo="+d.Speed+"; tandem="+(d.Speed*2)+
            "; solo meshes="+p.SoloParts.Length+"; tandem meshes="+p.TandemParts.Length+
            "; stroke seconds="+p.SoloStroke.Duration+"; materials: hull="+
            d.Prefab.transform.Find("ModelContainer/Plane").GetComponent<Renderer>().sharedMaterial.mainTexture.name+
            "; oars="+p.SoloParts[0].GetComponent<Renderer>().sharedMaterial.mainTexture.name+
            ". Second-human gameplay requires a multiplayer implementation; previews are editor-only.";
        Debug.Log(report);EditorGUIUtility.systemCopyBuffer=report;
    }
}
public sealed class RowboatBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    public void OnPreprocessBuild(BuildReport report){RowboatSetup.EnsureInstalled();}
}



