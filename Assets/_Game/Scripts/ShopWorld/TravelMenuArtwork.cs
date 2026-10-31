using UnityEngine;
using UnityEngine.UI;

// Original supplied destination artwork, decoded once and clipped without stretching.
public static class TravelMenuArtwork
{
    private static readonly Texture2D[] cache=new Texture2D[2];
    public static void Place(Transform parent,string name)
    {
        int index=name=="Home"?0:1;
        if(cache[index]==null)
        {
            var source=Resources.Load<TextAsset>("TravelMenuArtwork/"+name+".png");
            if(source==null)return;
            var texture=new Texture2D(2,2,TextureFormat.RGB24,false);
            if(!texture.LoadImage(source.bytes,true)){Object.Destroy(texture);return;}
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
            cache[index]=texture;Resources.UnloadAsset(source);
        }
        var frame=new GameObject("DestinationArtwork",typeof(RectTransform),typeof(Image),typeof(Mask));
        frame.transform.SetParent(parent,false);
        var rect=frame.GetComponent<RectTransform>();
        rect.anchorMin=new Vector2(0,.49f);rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(18,0);rect.offsetMax=new Vector2(-18,-18);
        var mask=frame.GetComponent<Image>();FishingHudTheme.Fill(mask);mask.raycastTarget=false;
        frame.GetComponent<Mask>().showMaskGraphic=false;
        var image=new GameObject(name,typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
        image.transform.SetParent(frame.transform,false);
        image.GetComponent<RawImage>().texture=cache[index];image.GetComponent<RawImage>().raycastTarget=false;
        var fit=image.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio=(float)cache[index].width/cache[index].height;
    }
}
