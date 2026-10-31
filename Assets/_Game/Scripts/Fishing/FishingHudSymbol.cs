using UnityEngine;
using UnityEngine.UI;

// Resolution-independent HUD artwork, drawn without font-dependent glyphs.
public sealed class FishingHudSymbol : MaskableGraphic
{
    public enum Kind { Reel, Bolt, Pin, Island, Gear, Cart, Camera, Fish, Rod, Stats, People, Home, Boat }
    public Kind kind;
    public static void Add(Transform parent, Kind kind, Vector2 anchor, Vector2 position, float size)
    {
        var go = new GameObject("Hud" + kind, typeof(RectTransform), typeof(FishingHudSymbol));
        go.transform.SetParent(parent, false);
        var graphic = go.GetComponent<FishingHudSymbol>();
        graphic.kind = kind; graphic.raycastTarget = false;
        graphic.color = kind == Kind.Bolt ? FishingHudTheme.Cyan : new Color(.85f,.97f,1f,1f);
        var r = graphic.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.anchoredPosition = position; r.sizeDelta = Vector2.one * size;
    }
    private Vector3 P(float x, float y)
    {
        Rect r = rectTransform.rect;
        return new Vector3(r.xMin + x*r.width, r.yMin + y*r.height, 0);
    }
    private void Quad(VertexHelper vh, float x, float y, float w, float h)
    {
        int n = vh.currentVertCount;
        vh.AddVert(P(x,y),color,Vector2.zero); vh.AddVert(P(x+w,y),color,Vector2.zero);
        vh.AddVert(P(x+w,y+h),color,Vector2.zero); vh.AddVert(P(x,y+h),color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
    }
    private void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
    {
        int n = vh.currentVertCount;
        vh.AddVert(P(a.x,a.y),color,Vector2.zero); vh.AddVert(P(b.x,b.y),color,Vector2.zero);
        vh.AddVert(P(c.x,c.y),color,Vector2.zero); vh.AddTriangle(n,n+1,n+2);
    }
    private void Ring(VertexHelper vh, float x, float y, float radius, float width, float fraction = 1f)
    {
        for(int i=0;i<64*fraction;i++)
        {
            float a=i*Mathf.PI*2/64, b=(i+1)*Mathf.PI*2/64;
            Vector2 center = new Vector2(x,y), u=new Vector2(Mathf.Sin(a),Mathf.Cos(a)), v=new Vector2(Mathf.Sin(b),Mathf.Cos(b));
            Tri(vh,center+u*radius,center+v*radius,center+v*(radius-width));
            Tri(vh,center+u*radius,center+v*(radius-width),center+u*(radius-width));
        }
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        switch(kind)
        {
            case Kind.Home:
                Tri(vh,new Vector2(.02f,.52f),new Vector2(.98f,.52f),new Vector2(.5f,.96f));
                Quad(vh,.17f,.08f,.25f,.49f);Quad(vh,.59f,.08f,.24f,.49f);Quad(vh,.40f,.39f,.21f,.18f);break;
            case Kind.Fish:
                Tri(vh,new Vector2(.04f,.26f),new Vector2(.04f,.75f),new Vector2(.40f,.5f));
                Ring(vh,.57f,.5f,.29f,.29f);Tri(vh,new Vector2(.43f,.73f),new Vector2(.58f,.91f),new Vector2(.69f,.7f));break;
            case Kind.Rod:
                Tri(vh,new Vector2(.08f,.08f),new Vector2(.17f,.06f),new Vector2(.82f,.91f));
                Quad(vh,.80f,.18f,.018f,.73f);Ring(vh,.76f,.18f,.05f,.018f,.75f);break;
            case Kind.Stats:
                for(int i=0;i<4;i++)Quad(vh,.08f+i*.23f,.15f,.15f,.2f+i*.17f);
                Quad(vh,.06f,.05f,.88f,.04f);break;
            case Kind.People:
                Ring(vh,.5f,.74f,.16f,.16f);Ring(vh,.17f,.65f,.12f,.12f);Ring(vh,.83f,.65f,.12f,.12f);
                Quad(vh,.30f,.12f,.40f,.39f);Quad(vh,.03f,.17f,.22f,.29f);Quad(vh,.75f,.17f,.22f,.29f);break;
            case Kind.Boat:
                Tri(vh,new Vector2(.04f,.40f),new Vector2(.92f,.40f),new Vector2(.73f,.12f));
                Tri(vh,new Vector2(.04f,.40f),new Vector2(.73f,.12f),new Vector2(.21f,.12f));
                Quad(vh,.32f,.44f,.38f,.20f);Quad(vh,.42f,.67f,.03f,.18f);break;
            case Kind.Camera:
                Quad(vh,.10f,.25f,.80f,.06f); Quad(vh,.10f,.70f,.80f,.06f);
                Quad(vh,.10f,.25f,.06f,.51f); Quad(vh,.84f,.25f,.06f,.51f);
                Quad(vh,.23f,.76f,.22f,.09f); Ring(vh,.52f,.50f,.17f,.055f); break;
            case Kind.Reel:
                Ring(vh,.5f,.5f,.44f,.065f,.8f);
                Quad(vh,.30f,.25f,.31f,.5f); Quad(vh,.25f,.22f,.08f,.56f); Quad(vh,.60f,.22f,.08f,.56f);
                Quad(vh,.67f,.40f,.16f,.06f); Quad(vh,.79f,.28f,.065f,.16f); Ring(vh,.82f,.26f,.08f,.08f);
                break;
            case Kind.Bolt:
                Tri(vh,new Vector2(.49f,.96f),new Vector2(.18f,.43f),new Vector2(.73f,.43f));
                Tri(vh,new Vector2(.36f,.02f),new Vector2(.80f,.59f),new Vector2(.36f,.59f)); break;
            case Kind.Pin:
                Ring(vh,.5f,.66f,.29f,.14f);
                Tri(vh,new Vector2(.22f,.59f),new Vector2(.78f,.59f),new Vector2(.5f,.06f)); break;
            case Kind.Island:
                Tri(vh,new Vector2(.04f,.12f),new Vector2(.94f,.12f),new Vector2(.54f,.30f));
                Quad(vh,.47f,.26f,.065f,.47f);
                Tri(vh,new Vector2(.5f,.72f),new Vector2(.12f,.65f),new Vector2(.27f,.87f));
                Tri(vh,new Vector2(.5f,.72f),new Vector2(.87f,.62f),new Vector2(.75f,.86f));
                Tri(vh,new Vector2(.5f,.72f),new Vector2(.38f,.98f),new Vector2(.65f,.91f)); break;
            case Kind.Cart:
                Quad(vh,.07f,.78f,.14f,.07f);Quad(vh,.19f,.30f,.07f,.53f);
                Tri(vh,new Vector2(.25f,.72f),new Vector2(.92f,.79f),new Vector2(.78f,.39f));
                Tri(vh,new Vector2(.25f,.72f),new Vector2(.78f,.39f),new Vector2(.30f,.39f));
                Quad(vh,.24f,.27f,.56f,.06f);
                Ring(vh,.32f,.14f,.065f,.065f);Ring(vh,.74f,.14f,.065f,.065f);break;
            case Kind.Gear:
                Ring(vh,.5f,.5f,.32f,.13f);
                for(int i=0;i<8;i++) {float a=i*Mathf.PI/4; Quad(vh,.45f+Mathf.Sin(a)*.32f,.45f+Mathf.Cos(a)*.32f,.1f,.1f);} break;
        }
    }
}

