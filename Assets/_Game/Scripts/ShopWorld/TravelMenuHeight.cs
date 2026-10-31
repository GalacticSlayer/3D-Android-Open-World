using UnityEngine;
using UnityEngine.UI;

// Keep the travel cards together when the canvas changes size or orientation.
public sealed class TravelMenuHeight : MonoBehaviour
{
    public RectTransform viewport;
    private LayoutElement slot;
    private void Awake(){slot=GetComponent<LayoutElement>();}
    private void LateUpdate()
    {
        if(viewport==null || slot==null)return;
        float height=Mathf.Max(560f,viewport.rect.height-4f);
        if(Mathf.Abs(slot.preferredHeight-height)>.5f)slot.preferredHeight=height;
    }
}
