using UnityEngine;

// Centralized 2D UI sound player for aquarium placement and shop transactions.
// Clips live in Resources/Fishing/Audio and load on first use, never per frame.
[DisallowMultipleComponent]
public sealed class ShopActionAudio : MonoBehaviour
{
    public enum Cue { AquariumStored, AquariumPickup, AquariumPlaced, StorePurchase }

    private static readonly string[] Paths={
        "Fishing/Audio/AquariumStored",
        "Fishing/Audio/AquariumPickup",
        "Fishing/Audio/AquariumPlaced",
        "Fishing/Audio/StorePurchase"
    };
    private readonly AudioClip[] clips=new AudioClip[4];
    private AudioSource output;

    public static void Play(Component owner,Cue cue)
    {
        if(owner==null)return;
        var player=owner.GetComponent<ShopActionAudio>();
        if(player==null)player=owner.gameObject.AddComponent<ShopActionAudio>();
        player.PlayCue(cue);
    }

    private void Awake()
    {
        output=gameObject.AddComponent<AudioSource>();
        output.playOnAwake=false;
        output.loop=false;
        output.spatialBlend=0f; // The player's UI action should not fade with distance.
        output.volume=.8f;
    }
    private void PlayCue(Cue cue)
    {
        int id=(int)cue;
        if(id<0 || id>=clips.Length)return;
        if(clips[id]==null)clips[id]=Resources.Load<AudioClip>(Paths[id]);
        if(output!=null && clips[id]!=null)output.PlayOneShot(clips[id],1f);
    }
    private void OnDestroy()
    {
        // AudioSource is on the same owner and is released with the player.
        for(int i=0;i<clips.Length;i++)clips[i]=null;
    }
}
