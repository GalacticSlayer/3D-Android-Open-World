using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class BoatSystemSetup
{
    private const string Root="Assets/Resources/Boats";
    private static Material wood,white,teal,metal;
    [MenuItem("Tools/Setup Boat System")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        var player=Object.FindFirstObjectByType<FirstPersonController>();
        var reef=Object.FindFirstObjectByType<ReefZone>();
        var ocean=Object.FindFirstObjectByType<OceanWater>();
        if(player==null || reef==null || ocean==null || string.IsNullOrEmpty(scene.path))
            throw new InvalidOperationException("Open the saved Suncrest Reef island scene before setup.");
        if(reef.gameObject.scene!=scene || player.gameObject.scene!=scene)
            throw new InvalidOperationException("Make the Suncrest Reef player scene active before setup.");
        if(player.GetComponent<ShopProgress>()==null)throw new InvalidOperationException("Install the existing shop/economy system first.");
        var old=GameObject.Find("MarinaShop");
        // Validate before changing anything: include non-collider decorative meshes.
        Physics.SyncTransforms();
        if(!FindSite(reef,ocean,old!=null?old.transform:null,out Vector3 site,out Quaternion rotation))
            throw new InvalidOperationException("No unobstructed shoreline site found. No marina was placed. Clear a 12 x 26 m shoreline strip and run setup again.");
        EnsureLayer("Water");int terrainLayer=EnsureLayer("Terrain");
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        wood=Material("Teak",new Color(.43f,.25f,.1f));white=Material("Ivory Fiberglass",new Color(.9f,.88f,.75f));
        teal=Material("Marina Teal",new Color(.04f,.34f,.39f));metal=Material("Brass",new Color(.64f,.49f,.22f));
        BoatData[] boats=new BoatData[3];
        string[] names={"Raft","Sailboat","Yacht"};int[] costs={500,12000,15000};float[] speeds={3.5f,27,9};int[] capacities={1,4,8};
        Vector3[] sizes={new Vector3(3,1,5),new Vector3(4,1.2f,7),new Vector3(5,1.4f,10)};
        for(int i=0;i<3;i++)
        {
            string path=Root+"/"+names[i]+".asset";
            var data=AssetDatabase.LoadAssetAtPath<BoatData>(path);
            if(data==null)
            {
                data=ScriptableObject.CreateInstance<BoatData>();data.ID=names[i].ToLowerInvariant();data.Cost=costs[i];data.Speed=speeds[i];data.MaxCapacity=capacities[i];data.HullSize=sizes[i];data.Draft=.45f+i*.25f;
                AssetDatabase.CreateAsset(data,path);
            }
            string prefabPath=Root+"/"+(i==0?"BaseBoat":names[i])+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            // Preserve user replacement meshes and tuning on repeated setup.
            if(prefab==null)
            {
                var model=BuildBoat(data,i);prefab=PrefabUtility.SaveAsPrefabAsset(model,prefabPath);Object.DestroyImmediate(model);
            }
            data.Prefab=prefab;EditorUtility.SetDirty(data);boats[i]=data;
        }
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Setup Boat System");
        if(old!=null)Undo.DestroyObjectImmediate(old);
        var marina=new GameObject("MarinaShop");Undo.RegisterCreatedObjectUndo(marina,"Create Marina");
        marina.transform.SetPositionAndRotation(site,rotation);
        Part(marina.transform,"Boardwalk",new Vector3(0,0,4),new Vector3(8,.3f,18),wood,true);
        for(int z=-4;z<=12;z+=4)for(int x=-1;x<=1;x+=2)
            Part(marina.transform,"Dock piling",new Vector3(x*3.65f,-1,z),new Vector3(.3f,2.5f,.3f),wood,true);
        Part(marina.transform,"Counter",new Vector3(0,.75f,-1),new Vector3(3,1.2f,.8f),teal,true);
        Part(marina.transform,"Countertop",new Vector3(0,1.4f,-1),new Vector3(3.3f,.15f,1),wood,true);
        foreach(float x in new[]{-2.7f,2.7f})foreach(float z in new[]{-3f,1.5f})
            Part(marina.transform,"Canopy post",new Vector3(x,1.7f,z),new Vector3(.18f,3.4f,.18f),white,true);
        Part(marina.transform,"Canopy",new Vector3(0,3.5f,-.75f),new Vector3(6.5f,.3f,5.5f),teal,true);
        var sign=Part(marina.transform,"Marina sign",new Vector3(0,2.6f,1.65f),new Vector3(4.5f,.65f,.12f),wood,true);
        var letters=new GameObject("Marina lettering",typeof(TextMesh));letters.transform.SetParent(sign.transform,false);
        // TextMesh faces its local -Z; label is on the land-facing side.
        letters.transform.localPosition=new Vector3(0,0,-.55f);letters.transform.localScale=new Vector3(1/4.5f,1/.65f,1/.12f);
        var text=letters.GetComponent<TextMesh>();text.text="SUNCREST MARINA";text.anchor=TextAnchor.MiddleCenter;text.characterSize=.14f;text.fontSize=48;text.color=Color.white;
        var counter=new GameObject("MarinaInteraction");counter.transform.SetParent(marina.transform,false);counter.transform.localPosition=new Vector3(0,.4f,-2.5f);
        var arrival=new GameObject("IslandDockArrival");arrival.transform.SetParent(marina.transform,false);arrival.transform.localPosition=new Vector3(0,.2f,5);
        // Wide low-angle ramp to the land; no impassable raised edge.
        Vector3 inland=site+rotation*new Vector3(0,0,-10);float ground=Ground(inland);
        Vector3 top=site+rotation*new Vector3(0,.16f,-5);Vector3 bottom=new Vector3(inland.x,ground+.08f,inland.z);
        Vector3 delta=top-bottom;
        var ramp=Part(marina.transform,"Beach access ramp",Vector3.zero,new Vector3(4,.15f,delta.magnitude),wood,true);
        ramp.transform.position=(top+bottom)*.5f;ramp.transform.rotation=Quaternion.LookRotation(delta,Vector3.up);
        var system=player.GetComponent<BoatSystem>();if(system==null)system=Undo.AddComponent<BoatSystem>(player.gameObject);
        if(player.GetComponent<BoatPassenger>()==null)Undo.AddComponent<BoatPassenger>(player.gameObject);
        Undo.RecordObject(system,"Configure boats");system.Catalog=boats;system.Water=ocean;system.MarinaCounter=counter.transform;system.IslandDock=arrival.transform;
        var travel=player.GetComponent<ShopDimensionManager>();if(travel!=null){Undo.RecordObject(travel,"Dock arrival");travel.islandArrival=arrival.transform;}
        foreach(var terrain in Terrain.activeTerrains){Undo.RecordObject(terrain.gameObject,"Terrain layer");terrain.gameObject.layer=terrainLayer;}
        // The rendered ocean follows the camera and has no physics surface. Use a
        // separate trigger only for placement rays, covering the known terrain.
        var oldWater=GameObject.Find("BoatPlacementWater");if(oldWater!=null)Undo.DestroyObjectImmediate(oldWater);
        var water=new GameObject("BoatPlacementWater");Undo.RegisterCreatedObjectUndo(water,"Water query surface");
        water.layer=LayerMask.NameToLayer("Water");
        var bounds=new Bounds(reef.center,Vector3.zero);foreach(var t in Terrain.activeTerrains){bounds.Encapsulate(t.transform.position);bounds.Encapsulate(t.transform.position+t.terrainData.size);}
        water.transform.position=new Vector3(bounds.center.x,ocean.BaseWaterLevel-.025f,bounds.center.z);
        var box=water.AddComponent<BoxCollider>();box.size=new Vector3(bounds.size.x,.05f,bounds.size.z);box.isTrigger=true;
        EditorUtility.SetDirty(system);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject=marina;
        Debug.Log("Boat system ready. Play, visit Suncrest Marina, buy a boat, and select hotbar slot 2. Setup saved the island scene.");
    }
    private static GameObject BuildBoat(BoatData data,int style)
    {
        var root=new GameObject(data.name);root.AddComponent<Rigidbody>();var controller=root.AddComponent<BoatController>();controller.Data=data;
        var model=new GameObject("ModelContainer");model.transform.SetParent(root.transform,false);
        float w=data.HullSize.x,l=data.HullSize.z;
        Part(model.transform,"Rounded hull",new Vector3(0,-.12f,0),new Vector3(w,.9f,l),style==0?wood:white,false,PrimitiveType.Sphere);
        // Stable flat deck and perimeter rails live outside replaceable visuals.
        Part(root.transform,"Walkable deck",new Vector3(0,.4f,0),new Vector3(w,.3f,l),wood,true);
        Part(root.transform,"Hull collider",new Vector3(0,-.15f,0),new Vector3(w,.8f,l),null,true).GetComponent<Renderer>().enabled=false;
        foreach(float side in new[]{-1f,1f})
            Part(root.transform,"Low gunwale",new Vector3(side*(w*.5f-.08f),.72f,0),new Vector3(.16f,.34f,l),teal,true);
        Part(model.transform,"Driver seat",new Vector3(0,.78f,l*.18f),new Vector3(.85f,.45f,.85f),teal,false);
        Part(model.transform,"Seat back",new Vector3(0,1.1f,l*.18f-.4f),new Vector3(.85f,.7f,.15f),white,false);
        Part(model.transform,"Helm console",new Vector3(0,1.05f,l*.18f+.75f),new Vector3(.7f,.9f,.35f),wood,false);
        if(style==1)
        {
            Part(model.transform,"Mast",new Vector3(0,3,-l*.18f),new Vector3(.12f,5,.12f),wood,false);
            Part(model.transform,"Furled sail",new Vector3(.25f,3.6f,-l*.18f),new Vector3(.45f,3,.15f),white,false);
        }
        if(style==2)
        {
            Part(model.transform,"Sun canopy",new Vector3(0,2.8f,-2),new Vector3(w*.8f,.15f,3),teal,false);
            foreach(float x in new[]{-w*.35f,w*.35f})Part(model.transform,"Canopy support",new Vector3(x,1.7f,-2.9f),new Vector3(.1f,2.2f,.1f),metal,false);
        }
        controller.DriverSeat=new GameObject("DriverSeat").transform;controller.DriverSeat.SetParent(root.transform,false);controller.DriverSeat.localPosition=new Vector3(0,.57f,l*.18f);
        controller.DeckExit=new GameObject("DeckExit").transform;controller.DeckExit.SetParent(root.transform,false);controller.DeckExit.localPosition=new Vector3(0,.57f,-l*.25f);
        return root;
    }
    private static bool FindSite(ReefZone reef,OceanWater ocean,Transform ignore,out Vector3 site,out Quaternion rotation)
    {
        var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for(int a=0;a<180;a++)
        {
            float angle=a*Mathf.PI/90;Vector3 outward=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
            Quaternion yaw=Quaternion.LookRotation(outward);
            for(float scale=.8f;scale<1.45f;scale+=.015f)
            {
                Vector3 p=reef.center+new Vector3(outward.x*reef.islandRadiusX*scale,0,outward.z*reef.islandRadiusZ*scale);
                float g=Ground(p);if(g<ocean.BaseWaterLevel+.15f || g>ocean.BaseWaterLevel+1.2f)continue;
                if(Ground(p+outward*13)>ocean.BaseWaterLevel-.3f || Ground(p-outward*10)<ocean.BaseWaterLevel+.1f)continue;
                float max=g,min=g;
                for(int x=-4;x<=4;x+=2)for(int z=-5;z<=3;z+=2){float h=Ground(p+yaw*new Vector3(x,0,z));max=Mathf.Max(max,h);min=Mathf.Min(min,h);}
                if(max-min>1.5f)continue;
                p.y=max+.15f;
                // Conservative world AABB encloses dock, ramp, canopy and approach.
                var area=new Bounds(p,Vector3.zero);
                foreach(float x in new[]{-5.5f,5.5f})foreach(float z in new[]{-11f,14f})area.Encapsulate(p+yaw*new Vector3(x,0,z));
                area.SetMinMax(new Vector3(area.min.x,ocean.BaseWaterLevel-3,area.min.z),new Vector3(area.max.x,p.y+5,area.max.z));
                bool blocked=false;
                foreach(var r in renderers)
                {
                    if(!r.enabled || !r.gameObject.activeInHierarchy || r.GetComponentInParent<OceanWater>()!=null || r.GetComponentInParent<FirstPersonController>()!=null || (ignore!=null && r.transform.IsChildOf(ignore)))continue;
                    if(area.Intersects(r.bounds)){blocked=true;break;}
                }
                if(blocked)continue;
                foreach(var c in Physics.OverlapBox(area.center,area.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                    if(!(c is TerrainCollider) && c.GetComponentInParent<FirstPersonController>()==null && (ignore==null || !c.transform.IsChildOf(ignore))){blocked=true;break;}
                if(blocked)continue;
                site=p;rotation=yaw;return true;
            }
        }
        site=Vector3.zero;rotation=Quaternion.identity;return false;
    }
    private static float Ground(Vector3 p)
    {
        foreach(var t in Terrain.activeTerrains){var q=p-t.transform.position;var s=t.terrainData.size;if(q.x>=0 && q.z>=0 && q.x<=s.x && q.z<=s.z)return t.SampleHeight(p)+t.transform.position.y;}
        return float.PositiveInfinity;
    }
    private static GameObject Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool solid,PrimitiveType type=PrimitiveType.Cube)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        if(material!=null)go.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    private static Material Material(string name,Color color)
    {
        string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit shader is missing.");
        m=new Material(shader);m.color=color;AssetDatabase.CreateAsset(m,path);return m;
    }
    private static int EnsureLayer(string name)
    {
        int existing=LayerMask.NameToLayer(name);if(existing>=0)return existing;
        var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");
        for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue=name;tags.ApplyModifiedProperties();return i;}
        throw new InvalidOperationException("No free layer slot for "+name);
    }
}

