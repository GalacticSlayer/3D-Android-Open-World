#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the new rod's transparent hotbar silhouette and blue-wave item
/// card using its ACTUAL imported UV-mapped mesh. Existing Rod0..Rod5 icons,
/// reels, equipment layout and all other artwork remain untouched.
/// </summary>
public static class FishingLevel7RodIconGenerator
{
    private const string Folder = "Assets/Resources/Fishing/ItemIcons/";
    private const int RenderSize = 512;
    private const int IconSize = 256;

    public static void Generate(Mesh mesh, Texture2D originalTexture)
    {
        if (mesh == null || originalTexture == null)
            throw new ArgumentNullException("The Level 7 mesh and original texture are both required.");
        Texture2D sampled = MakeReadable(originalTexture, originalTexture.width, originalTexture.height);
        Texture2D hotbar;
        try { hotbar = RenderIcon(mesh, sampled); }
        finally { UnityEngine.Object.DestroyImmediate(sampled); }
        try
        {
            Save(hotbar, Folder + "Rod6.png");
            Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "WaterBackground.png");
            Texture2D water = background != null ? MakeReadable(background, IconSize, IconSize) : null;
            try
            {
                Texture2D card = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                Color[] image = hotbar.GetPixels();
                Color[] backdrop = water != null ? water.GetPixels() : null;
                for (int i = 0; i < image.Length; i++)
                {
                    Color front = image[i];
                    Color back = backdrop != null ? backdrop[i] :
                        new Color(.11f, .34f, .47f, 1f);
                    image[i] = new Color(
                        Mathf.Lerp(back.r, front.r, front.a),
                        Mathf.Lerp(back.g, front.g, front.a),
                        Mathf.Lerp(back.b, front.b, front.a), 1f);
                }
                card.SetPixels(image);
                card.Apply(false, false);
                try { Save(card, Folder + "Rod6Card.png"); }
                finally { UnityEngine.Object.DestroyImmediate(card); }
            }
            finally { if (water != null) UnityEngine.Object.DestroyImmediate(water); }
        }
        finally { UnityEngine.Object.DestroyImmediate(hotbar); }
    }

    private static Texture2D MakeReadable(Texture texture, int width, int height)
    {
        RenderTexture old = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(texture,target);
            RenderTexture.active = target;
            Texture2D readable = new Texture2D(width,height,TextureFormat.RGBA32,false);
            readable.ReadPixels(new Rect(0,0,width,height),0,0,false);
            readable.Apply(false,false);
            return readable;
        }
        finally
        {
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static Texture2D RenderIcon(Mesh mesh, Texture2D texture)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        int[] tris = mesh.triangles;
        if (vertices.Length < 3 || uv.Length != vertices.Length)
            throw new InvalidDataException("Level 7 rod has invalid icon mesh UV coordinates.");

        // All rod icons appear diagonal inside their square, with the butt in
        // the lower-left and the tip in the upper-right.
        Quaternion pose = Quaternion.AngleAxis(-43f,Vector3.forward) *
                          Quaternion.AngleAxis(18f,Vector3.right);
        Vector3 centre = mesh.bounds.center;
        Vector3[] projected = new Vector3[vertices.Length];
        float minX=float.PositiveInfinity, minY=float.PositiveInfinity;
        float maxX=float.NegativeInfinity, maxY=float.NegativeInfinity;
        for (int i=0;i<vertices.Length;i++)
        {
            Vector3 p = pose*(vertices[i]-centre);
            projected[i]=p;
            minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);
            minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
        }
        float scale = .83f*RenderSize/Mathf.Max(maxX-minX,maxY-minY,.001f);
        Vector3[] screen = new Vector3[vertices.Length];
        float mx=(minX+maxX)*.5f,my=(minY+maxY)*.5f;
        for (int i=0;i<screen.Length;i++)
            screen[i] = new Vector3(
                (projected[i].x-mx)*scale+RenderSize*.5f,
                (projected[i].y-my)*scale+RenderSize*.5f,
                projected[i].z);

        Color[] pixels = new Color[RenderSize*RenderSize];
        float[] depths = new float[pixels.Length];
        for (int i=0;i<depths.Length;i++) depths[i]=float.NegativeInfinity;

        int texWidth=texture.width,texHeight=texture.height;
        Color32[] atlas=texture.GetPixels32();
        for (int t=0;t+2<tris.Length;t+=3)
        {
            int ia=tris[t],ib=tris[t+1],ic=tris[t+2];
            if (ia<0||ib<0||ic<0||ia>=screen.Length||ib>=screen.Length||ic>=screen.Length)
                continue;
            Vector3 a=screen[ia],b=screen[ib],c=screen[ic];
            float area=Edge(a,b,c.x,c.y);
            if (Mathf.Abs(area)<.000001f) continue;
            int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,RenderSize-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,RenderSize-1);
            int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,RenderSize-1);
            int y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,RenderSize-1);
            Vector3 normal=Vector3.Cross(projected[ib]-projected[ia],
                projected[ic]-projected[ia]).normalized;
            float lighting=Mathf.Clamp(1.1f+.20f*Vector3.Dot(normal,
                new Vector3(-.35f,.55f,.75f).normalized),.88f,1.36f);
            for (int y=y0;y<=y1;y++)
            for (int x=x0;x<=x1;x++)
            {
                float px=x+.5f,py=y+.5f;
                float wa=Edge(b,c,px,py)/area;
                float wb=Edge(c,a,px,py)/area;
                float wc=1f-wa-wb;
                if (wa<-.00001f || wb<-.00001f || wc<-.00001f) continue;
                int index=y*RenderSize+x;
                float depth=wa*a.z+wb*b.z+wc*c.z;
                if (depth<depths[index]) continue;
                depths[index]=depth;
                Vector2 st=wa*uv[ia]+wb*uv[ib]+wc*uv[ic];
                int tx=Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(st.x,1f)*texWidth),0,texWidth-1);
                int ty=Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(st.y,1f)*texHeight),0,texHeight-1);
                Color32 sample=atlas[ty*texWidth+tx];
                pixels[index]=new Color(
                    Mathf.Clamp01(sample.r/255f*lighting),
                    Mathf.Clamp01(sample.g/255f*lighting),
                    Mathf.Clamp01(sample.b/255f*lighting),sample.a/255f);
            }
        }

        // Two-times downsample gives a smooth, centered silhouette suitable
        // for the same 256px hotbar and square shop/equipment icon containers.
        Color[] smaller=new Color[IconSize*IconSize];
        for (int y=0;y<IconSize;y++)
        for (int x=0;x<IconSize;x++)
        {
            int sx=x*2,sy=y*2;
            Color a=pixels[sy*RenderSize+sx];
            Color b=pixels[sy*RenderSize+sx+1];
            Color c=pixels[(sy+1)*RenderSize+sx];
            Color d=pixels[(sy+1)*RenderSize+sx+1];
            float alpha=(a.a+b.a+c.a+d.a)*.25f;
            float weighted=Mathf.Max(.000001f,a.a+b.a+c.a+d.a);
            smaller[y*IconSize+x]=new Color(
                (a.r*a.a+b.r*b.a+c.r*c.a+d.r*d.a)/weighted,
                (a.g*a.a+b.g*b.a+c.g*c.a+d.g*d.a)/weighted,
                (a.b*a.a+b.b*b.a+c.b*c.a+d.b*d.a)/weighted,alpha);
        }

        Texture2D result=new Texture2D(IconSize,IconSize,TextureFormat.RGBA32,false);
        result.SetPixels(smaller);
        result.Apply(false,false);
        return result;
    }

    private static float Edge(Vector3 a,Vector3 b,float x,float y)
        => (x-a.x)*(b.y-a.y)-(y-a.y)*(b.x-a.x);

    private static void Save(Texture2D result,string path)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(path,result.EncodeToPNG());
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer==null) throw new InvalidDataException("Could not import generated icon: "+path);
        importer.textureType=TextureImporterType.Default;
        importer.alphaIsTransparency=true;
        importer.sRGBTexture=true;
        importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=512;
        importer.SaveAndReimport();
    }
}
#endif
