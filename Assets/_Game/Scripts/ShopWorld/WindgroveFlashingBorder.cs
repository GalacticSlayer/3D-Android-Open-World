using UnityEngine;

public sealed class WindgroveFlashingBorder : MonoBehaviour
{
    private CanvasGroup group;
    private void Awake() { group = GetComponent<CanvasGroup>(); }
    private void Update() { group.alpha = .25f + .75f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f)); }
}
