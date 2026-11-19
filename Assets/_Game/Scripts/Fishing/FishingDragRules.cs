using UnityEngine;

// Central balance values shared by live combat, UI and regression checks.
public static class FishingDragRules
{
    public const int Low=0, Medium=1, High=2;
    public const float SkillChargeSeconds=5f;
    public const float PassiveDamageSeconds=1f;
    public const float HighTensionPerSecond=FragileTensionPerSecond * .5f;
    public const float FragileWarningSeconds=2f;
    public const float FragileTensionPerSecond=.45f;
    public static float FragileWarningDuration(bool windgrove)=>FragileWarningSeconds*(windgrove?.5f:1f);
    public static float FragileTensionGain(float previous,float elapsed,float warningSeconds=FragileWarningSeconds)=>
        Mathf.Max(0f,Mathf.Max(0f,elapsed-warningSeconds)-Mathf.Max(0f,previous-warningSeconds))*FragileTensionPerSecond;
    public static float RecoveryMultiplier(int mode)=>mode==Low?1.3f:1f;
    public static float FragileTimer(float elapsed,bool fragile,int mode,float dt)=>fragile && mode!=Low?elapsed+Mathf.Max(0f,dt):0f;
    public static float EscapeMultiplier(int mode)=>mode==Low?2f:1f;
    public static float TensionMultiplier(int mode)=>mode==Low?.5f:1f;
    public static float ReelingTensionMultiplier(int mode)=>TensionMultiplier(mode);
    public static float DamageMultiplier(int mode)=>mode==Low?.5f:mode==High?2f:1f;
    public static float Charge(float current,int mode,float seconds)=>Mathf.Clamp01(current+(mode==High?Mathf.Max(0,seconds)/SkillChargeSeconds:0));
    public static bool IsLookGesture(float distance)=>distance>0f && !float.IsInfinity(distance);
    public const float SkillStunSeconds=3f;
    public static float SkillStunDuration(bool windgrove)=>SkillStunSeconds;
    public static int SkillDamage(int normalRodDamage)=>normalRodDamage*20;
}
