using UnityEngine;
using UnityEngine.UI;

// A single UI mesh, rebuilt by the Canvas only when its bounds change.
public sealed class AbyssalGemConnections : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        Vector2[] p={new Vector2(.5f,.84f),new Vector2(.22f,.57f),new Vector2(.32f,.16f),new Vector2(.68f,.16f),new Vector2(.78f,.57f)};
        for(int i=0;i<5;i++)
        {
            Vector2 a=new Vector2(r.xMin+p[i].x*r.width,r.yMin+p[i].y*r.height);
            var q=p[(i+1)%5];Vector2 b=new Vector2(r.xMin+q.x*r.width,r.yMin+q.y*r.height);
            Vector2 delta=b-a,normal=new Vector2(-delta.y,delta.x).normalized*1.5f;
            int n=vh.currentVertCount;var c=new Color(.2f,.9f,1f,.8f);
            vh.AddVert(a-normal,c,Vector2.zero);vh.AddVert(a+normal,c,Vector2.zero);
            vh.AddVert(b+normal,c,Vector2.zero);vh.AddVert(b-normal,c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
