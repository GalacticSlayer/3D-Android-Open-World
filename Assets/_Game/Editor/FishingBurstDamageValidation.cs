using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic editor validation for the core fishing damage contract. It exercises
/// the same FishingBurstDamageRuntime formula used in play mode, including every
/// edge of the random roll, so a future change cannot silently reintroduce -1/-2/-3
/// Level 2 hits or sub-8 Level 2 criticals.
/// </summary>
public static class FishingBurstDamageValidation
{
    [InitializeOnLoadMethod]
    private static void ValidateAfterCompile()
    {
        EditorApplication.delayCall += () => Validate(false);
    }

    [MenuItem("Tools/Open World/Validate Fishing Damage Ranges")]
    private static void ValidateFromMenu()
    {
        Validate(true);
    }

    private static void Validate(bool showSuccess)
    {
        try
        {
            CheckTier(0,2,4,0f,0,0);
            CheckTier(1,4,8,.05f,8,16);
            CheckTier(2,6,12,.08f,12,24);
            CheckTier(3,10,20,.10f,20,40);
            CheckTier(4,25,35,.13f,50,70);
            CheckTier(5,35,50,.13f,70,100);
            CheckTier(6,55,75,.15f,110,150);

            // Sweep the complete [0,1) damage-roll interval densely. This is not a
            // statistical test; it verifies the deterministic mapping bounds.
            for(int tier=0;tier<=ShopCatalog.MaxRodTier;tier++)
            {
                int min=FishingBurstDamageRuntime.NormalMinimumForTier(tier);
                int max=FishingBurstDamageRuntime.NormalMaximumForTier(tier);
                for(int i=0;i<10000;i++)
                {
                    bool critical;
                    float roll=(i+.5f)/10000f;
                    int normal=FishingBurstDamageRuntime.RollBurstForTier(tier,roll,1f,out critical);
                    Require(!critical,"Normal validation roll unexpectedly crit at tier "+tier);
                    Require(normal>=min && normal<=max,"Normal burst outside advertised range at tier "+tier+": "+normal);

                    if(tier>0)
                    {
                        int crit=FishingBurstDamageRuntime.RollBurstForTier(tier,roll,0f,out critical);
                        Require(critical,"Forced critical did not crit at tier "+tier);
                        Require(crit>=min*2 && crit<=max*2,"Critical burst outside doubled range at tier "+tier+": "+crit);
                        Require(crit==normal*2,"Critical is not exactly 2x its normal burst at tier "+tier);
                    }
                }
            }

            if(showSuccess)
                EditorUtility.DisplayDialog(
                    "Fishing Damage Validated",
                    "All seven rod tiers validated. Level 7: 55-75 normal, 110-150 critical, 15% critical chance. 10,000 deterministic rolls per tier passed.",
                    "OK");
        }
        catch(Exception exception)
        {
            Debug.LogError("[FISHING DAMAGE VALIDATION FAILED] "+exception.Message);
            if(showSuccess)EditorUtility.DisplayDialog("Fishing Damage Validation Failed",exception.Message,"OK");
        }
    }

    private static void CheckTier(int tier,int min,int max,float critChance,int critMin,int critMax)
    {
        Require(FishingBurstDamageRuntime.NormalMinimumForTier(tier)==min,"Wrong minimum for rod tier "+tier);
        Require(FishingBurstDamageRuntime.NormalMaximumForTier(tier)==max,"Wrong maximum for rod tier "+tier);
        Require(Mathf.Abs(FishingBurstDamageRuntime.CriticalChanceForTier(tier)-critChance)<.0001f,"Wrong critical chance for rod tier "+tier);

        bool critical;
        int low=FishingBurstDamageRuntime.RollBurstForTier(tier,0f,1f,out critical);
        Require(!critical && low==min,"Low-end normal roll failed for tier "+tier);
        int high=FishingBurstDamageRuntime.RollBurstForTier(tier,.999999f,1f,out critical);
        Require(!critical && high==max,"High-end normal roll failed for tier "+tier);

        if(tier>0)
        {
            int lowCrit=FishingBurstDamageRuntime.RollBurstForTier(tier,0f,0f,out critical);
            Require(critical && lowCrit==critMin,"Low-end critical roll failed for tier "+tier);
            int highCrit=FishingBurstDamageRuntime.RollBurstForTier(tier,.999999f,0f,out critical);
            Require(critical && highCrit==critMax,"High-end critical roll failed for tier "+tier);
        }
    }

    private static void Require(bool condition,string message)
    {
        if(!condition)throw new InvalidOperationException(message);
    }
}
