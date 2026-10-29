using System.Collections.Generic;
using UnityEngine;

public static class FishVisualFactory
{
    private static readonly Dictionary<int,Material> BodyMaterials=new Dictionary<int,Material>();
    private static readonly Dictionary<int,Material> AccentMaterials=new Dictionary<int,Material>();
    private static readonly Dictionary<int,GameObject> Prefabs=new Dictionary<int,GameObject>();
    private static Mesh tailMesh;

    public static GameObject CreateFish(string name,Transform parent,int speciesId,float scale)
    {
        return CreateFishInternal(name,parent,speciesId,scale,true);
    }

    public static GameObject CreateAmbientFish(string name,Transform parent,int speciesId,float scale)
    {
        return CreateFishInternal(name,parent,speciesId,scale,true);
    }

    private static GameObject CreateFishInternal(string name,Transform parent,int speciesId,float scale,bool allowAuthored)
    {
        speciesId=FishCatalog.CanonicalId(speciesId);
        FishSpeciesDefinition species=FishCatalog.Get(speciesId);
        if(allowAuthored)
        {
            GameObject authored=CreateAuthoredFish(name,parent,speciesId,scale);
            if(authored!=null)return authored;
        }

        GameObject root=new GameObject(name);
        if(parent!=null)root.transform.SetParent(parent,false);
        root.transform.localScale=Vector3.one*scale;
        GameObject body=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name="Body";body.transform.SetParent(root.transform,false);
        body.transform.localScale=new Vector3(.48f,.28f,.82f);
        body.GetComponent<Renderer>().sharedMaterial=GetBodyMaterial(speciesId);
        Collider bodyCollider=body.GetComponent<Collider>();if(bodyCollider!=null)Object.Destroy(bodyCollider);
        GameObject tail=new GameObject("Tail",typeof(MeshFilter),typeof(MeshRenderer));
        tail.transform.SetParent(root.transform,false);tail.transform.localPosition=new Vector3(0f,0f,-.48f);
        tail.GetComponent<MeshFilter>().sharedMesh=GetTailMesh();tail.GetComponent<MeshRenderer>().sharedMaterial=GetAccentMaterial(speciesId);
        GameObject fin=new GameObject("DorsalFin",typeof(MeshFilter),typeof(MeshRenderer));
        fin.transform.SetParent(root.transform,false);fin.transform.localPosition=new Vector3(0f,.18f,-.02f);
        fin.transform.localRotation=Quaternion.Euler(0f,0f,90f);fin.transform.localScale=Vector3.one*.52f;
        fin.GetComponent<MeshFilter>().sharedMesh=GetTailMesh();fin.GetComponent<MeshRenderer>().sharedMaterial=GetAccentMaterial(speciesId);
        return root;
    }

    private static GameObject CreateAuthoredFish(string name,Transform parent,int speciesId,float scale)
    {
        string resource=ResourcePath(speciesId);if(string.IsNullOrEmpty(resource))return null;
        GameObject prefab;
        if(!Prefabs.TryGetValue(speciesId,out prefab)||prefab==null)
        {prefab=Resources.Load<GameObject>(resource);Prefabs[speciesId]=prefab;}
        if(prefab==null)return null;
        GameObject instance=Object.Instantiate(prefab);instance.name=name;
        if(parent!=null)instance.transform.SetParent(parent,false);
        instance.transform.localScale=Vector3.one*scale;return instance;
    }

    private static string ResourcePath(int speciesId)
    {
        switch(speciesId)
        {
            case 0:return "Fishing/Mackerel";
            case FishCatalog.RedSnapperId:return "Fishing/RedSnapper";
            case 2:return "Fishing/SeaBass";
            case 3:return "Fishing/HeroYellowtail";
            case FishCatalog.YellowfinTunaId:return "Fishing/YellowfinTuna";
            case FishCatalog.YellowGoatfishId:return "Fishing/YellowGoatfish";
            case FishCatalog.BlackSpotGoatfishId:return "Fishing/BlackSpotGoatfish";
            case FishCatalog.BigeyeTunaId:return "Fishing/BigeyeTuna";
            case FishCatalog.BonitoId:return "Fishing/Bonito";
            case FishCatalog.BlackSeaBassId:return "Fishing/BlackSeaBass";
            case FishCatalog.StripedBassId:return "Fishing/StripedBass";
            case FishCatalog.AlbacoreId:return "Fishing/Albacore";
            case FishCatalog.YellowtailSnapperId:return "Fishing/YellowtailSnapper";
            case FishCatalog.BlacktipReefSharkId:return "Fishing/BlacktipReefShark";
            case FishCatalog.MakoSharkId:return "Fishing/MakoShark";
            case FishCatalog.BattleScarredMakoSharkId:return "Fishing/BattleScarredMakoShark";
            case FishCatalog.BlacktipSharkId:return "Fishing/BlacktipShark";
            case FishCatalog.GreaterAmberjackId:return "Fishing/GreaterAmberjack";
            case FishCatalog.SpottedSandBassId:return "Fishing/SpottedSandBass";
            case FishCatalog.MuttonSnapperId:return "Fishing/MuttonSnapper";
            case FishCatalog.MangroveSnapperId:return "Fishing/MangroveSnapper";
            case FishCatalog.BlackfinBarracudaId:return "Fishing/BlackfinBarracuda";
            case FishCatalog.GreatBarracudaId:return "Fishing/GreatBarracuda";
            case FishCatalog.BluefinTrevallyId:return "Fishing/BluefinTrevally";
            case FishCatalog.GoldenTrevallyId:return "Fishing/GoldenTrevally";
            case FishCatalog.GiantTrevallyId:return "Fishing/GiantTrevally";
            case FishCatalog.WinterFlounderId:return "Fishing/WinterFlounder";
            case FishCatalog.RoughtailStingrayId:return "Fishing/RoughtailStingray";
            case FishCatalog.LeopardStingrayId:return "Fishing/LeopardStingray";
            case FishCatalog.MahoganySnapperId:return "Fishing/MahoganySnapper";
            case FishCatalog.PicassoTriggerfishId:return "Fishing/PicassoTriggerfish";
            case FishCatalog.TitanTriggerfishId:return "Fishing/TitanTriggerfish";
            case FishCatalog.EmperorAngelfishId:return "Fishing/EmperorAngelfish";
            case FishCatalog.JuvenileEmperorAngelfishId:return "Fishing/JuvenileEmperorAngelfish";
            case FishCatalog.PantherGrouperId:return "Fishing/PantherGrouper";
            case FishCatalog.CoralGrouperId:return "Fishing/CoralGrouper";
            default:return null;
        }
    }

    private static Material GetBodyMaterial(int speciesId)
    {
        Material material;if(BodyMaterials.TryGetValue(speciesId,out material))return material;
        material=CreateLitMaterial(FishCatalog.Get(speciesId).BodyColor);BodyMaterials[speciesId]=material;return material;
    }
    private static Material GetAccentMaterial(int speciesId)
    {
        Material material;if(AccentMaterials.TryGetValue(speciesId,out material))return material;
        material=CreateLitMaterial(FishCatalog.Get(speciesId).AccentColor);AccentMaterials[speciesId]=material;return material;
    }
    private static Material CreateLitMaterial(Color color)
    {
        Shader shader=Resources.Load<Shader>("Fishing/FishingLit");
        if(shader==null){Debug.LogError("Missing Resources/Fishing/FishingLit shader.");return null;}
        Material material=new Material(shader);
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.34f);
        material.enableInstancing=true;return material;
    }
    private static Mesh GetTailMesh()
    {
        if(tailMesh!=null)return tailMesh;
        tailMesh=new Mesh{name="RuntimeFishTail"};
        tailMesh.vertices=new[]{new Vector3(0f,0f,0f),new Vector3(-.28f,.31f,-.22f),new Vector3(.28f,.31f,-.22f),new Vector3(-.28f,-.31f,-.22f),new Vector3(.28f,-.31f,-.22f)};
        tailMesh.triangles=new[]{0,1,2,0,2,1,0,4,3,0,3,4};tailMesh.RecalculateNormals();tailMesh.RecalculateBounds();return tailMesh;
    }
}

