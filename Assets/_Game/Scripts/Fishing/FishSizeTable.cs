using System;

// Model-derived total-length tables, not individual measured specimens.
// Provenance and species assumptions: Tools/ShopWorldChecks/FishSizeData.md.
public static class FishSizeTable
{
    private static readonly float[][] Weights = {
        new float[] {0.0012267f, 0.0102300f, 0.0353767f, 0.0853158f, 0.1688784f, 0.2950317f, 0.4728528f, 0.7115104f, 1.0202533f, 1.4083997f, 1.8853307f}, // Scomber australasicus
        new float[] {0.0017335f, 0.0134886f, 0.0447918f, 0.1049583f, 0.2031752f, 0.3485356f, 0.5500594f, 0.8167060f, 1.1573833f, 1.5809546f, 2.0962435f, 2.7120387f, 3.4370964f, 4.2801439f, 5.2498817f, 6.3549851f, 7.6041066f, 9.0058768f, 10.5689063f, 12.3017862f}, // Lutjanus campechanus
        new float[] {0.0012044f, 0.0097697f, 0.0332412f, 0.0792487f, 0.1554750f, 0.2696422f, 0.4295039f, 0.6428399f, 0.9174522f, 1.2611625f, 1.6818101f, 2.1872500f, 2.7853513f, 3.4839966f, 4.2910805f, 5.2145088f, 6.2621981f, 7.4420747f, 8.7620743f, 10.2301416f}, // Dicentrarchus labrax
        new float[] {0.0020795f, 0.0158482f, 0.0519909f, 0.1207807f, 0.2322437f, 0.3962278f, 0.6224422f, 0.9204823f, 1.2998473f, 1.7699532f, 2.3401428f, 3.0196933f, 3.8178232f, 4.7436969f, 5.8064298f, 7.0150915f, 8.3787094f, 9.9062716f, 11.6067289f, 13.4889976f, 15.5619610f, 17.8344715f, 20.3153519f, 23.0133969f, 25.9373746f, 29.0960275f, 32.4980736f, 36.1522075f, 40.0671012f, 44.2514051f, 48.7137488f, 53.4627416f, 58.5069735f, 63.8550157f, 69.5154213f, 75.4967257f, 81.8074472f, 88.4560878f, 95.4511330f, 102.8010532f}, // Seriola lalandi
        new float[] {0.0018536f, 0.0151406f, 0.0517248f, 0.1236696f, 0.2431646f, 0.4224930f, 0.6740131f, 1.0101455f, 1.4433647f, 1.9861922f, 2.6511916f, 3.4509642f, 4.3981459f, 5.5054044f, 6.7854365f, 8.2509665f, 9.9147440f, 11.7895429f, 13.8881592f, 16.2234107f, 18.8081352f, 21.6551900f, 24.7774506f, 28.1878101f, 31.8991787f, 35.9244824f, 40.2766629f, 44.9686769f, 50.0134955f, 55.4241035f, 61.2134994f, 67.3946945f, 73.9807130f, 80.9845911f, 88.4193768f, 96.2981297f, 104.6339207f, 113.4398315f, 122.7289544f, 132.5143921f}, // retired ID 4 / Yellowfin compatibility
        new float[] {0.0018536f, 0.0151406f, 0.0517248f, 0.1236696f, 0.2431646f, 0.4224930f, 0.6740131f, 1.0101455f, 1.4433647f, 1.9861922f, 2.6511916f, 3.4509642f, 4.3981459f, 5.5054044f, 6.7854365f, 8.2509665f, 9.9147440f, 11.7895429f, 13.8881592f, 16.2234107f, 18.8081352f, 21.6551900f, 24.7774506f, 28.1878101f, 31.8991787f, 35.9244824f, 40.2766629f, 44.9686769f, 50.0134955f, 55.4241035f, 61.2134994f, 67.3946945f, 73.9807130f, 80.9845911f, 88.4193768f, 96.2981297f, 104.6339207f, 113.4398315f, 122.7289544f, 132.5143921f}, // Thunnus albacares
        new float[] {0.0018190f, 0.0154891f, 0.0542185f, 0.1318890f, 0.2628212f, 0.4616687f, 0.7433545f, 1.1230293f, 1.6160411f, 2.2379122f}, // Parupeneus cyclostomus
        new float[] {0.0016784f, 0.0138047f, 0.0473525f, 0.1135421f, 0.2237502f, 0.3894703f, 0.6222897f, 0.9338736f, 1.3359548f, 1.8403251f}, // Parupeneus spilurus
        new float[] {0.0018536f, 0.0151406f, 0.0517248f, 0.1236696f, 0.2431646f, 0.4224930f, 0.6740131f, 1.0101455f, 1.4433647f, 1.9861922f, 2.6511916f, 3.4509642f, 4.3981459f, 5.5054044f, 6.7854365f, 8.2509665f, 9.9147440f, 11.7895429f, 13.8881592f, 16.2234107f, 18.8081352f, 21.6551900f, 24.7774506f, 28.1878101f, 31.8991787f, 35.9244824f, 40.2766629f, 44.9686769f, 50.0134955f, 55.4241035f, 61.2134994f, 67.3946945f, 73.9807130f, 80.9845911f, 88.4193768f, 96.2981297f, 104.6339207f, 113.4398315f, 122.7289544f, 132.5143921f}, // Bigeye gameplay size curve
        new float[] {0.0011716f,0.0097708f,0.0337885f,0.0814857f,0.1612969f,0.2817868f,0.4516248f,0.6795683f,0.9744507f,1.3451719f,1.8006918f,2.3500245f,3.0022333f,3.7664271f,4.6517571f,5.6674138f,6.8226246f,8.1266516f,9.5887897f,11.2183648f}, // Sarda sarda (Bonito)
        new float[] {0.0015304f,0.0125876f,0.0431777f,0.1035316f,0.2040232f,0.3551326f,0.5674254f,0.8515384f,1.2181701f,1.6780724f,2.2420457f,2.9209336f,3.7256195f,4.6670229f}, // Centropristis striata (Black Sea Bass)
        new float[] {0.0012638f,0.0104739f,0.0360643f,0.0867113f,0.1713634f,0.2993969f,0.4803911f,0.7241117f,1.0403645f,1.4388422f,1.9291215f,2.5207841f,3.2224173f,4.0426136f,4.9909739f,6.0771070f,7.3114208f,8.7049088f,10.2677553f,12.0100439f,13.9427033f,16.0766441f,18.4227125f,20.9915967f,23.7950314f,26.8456014f,30.1558458f,33.7372705f,37.6033315f,41.7674472f,46.2430008f,51.0433423f,56.1827902f,61.6756332f,67.5361320f,73.7785204f,80.4170063f,87.4667726f,94.9429787f,102.8607615f}, // Morone saxatilis (Striped Bass)
        new float[] {0.0015652f,0.0128817f,0.0441847f,0.1058547f,0.2086845f,0.3638955f,0.5824606f,0.8754019f,1.2537384f,1.7286444f,2.3114468f,3.0136274f}, // Paralabrax maculatofasciatus (Spotted Sand Bass)
        GameplayCurve(18f,32), // Albacore: gameplay approximation kg = 18 * metres^3
        GameplayCurve(14f,40), // Greater Amberjack: gameplay approximation kg = 14 * metres^3
        GameplayCurve(5f,40), // Blacktip shark gameplay approximation: kg = 5 * metres^3
        GameplayCurve(10f,16), // Yellowtail snapper gameplay approximation: kg = 10 * metres^3
        GameplayCurve(16f,22), // Mutton snapper gameplay approximation: kg = 16 * metres^3 (~13.3 kg at 0.94 m)
        GameplayCurve(5f,40), // Blacktip reef shark gameplay size curve
        GameplayCurve(8f,70), // Mako gameplay size curve
        GameplayCurve(8f,70), // Scarred variant delegates to the normal curve below
        GameplayCurve(15f,22), // Mangrove snapper gameplay approximation kg = 15 * metres^3
        GameplayCurve(9f,36), // Blackfin barracuda slender-body approximation kg = 9 * metres^3
        GameplayCurve(9f,40), // Great barracuda slender-body approximation kg = 9 * metres^3
        GameplayCurve(17f,36), // Bluefin trevally gameplay approximation kg = 17 * metres^3
        GameplayCurve(18f,36), // Golden trevally gameplay approximation kg = 18 * metres^3
        GameplayCurve(17f,36), // Giant trevally uses the 1.8x Bluefin formula below
        GameplayCurve(10f,14), // Winter flounder gameplay approximation: kg = 10 * metres^3
        GameplayCurve(10f,14), // Roughtail stingray: same gameplay size curve as flounder
        GameplayCurve(10f,14), // Leopard stingray: identical size curve to Roughtail
        GameplayCurve(10f,16), // Mahogany snapper: same length curve as Yellowtail Snapper
        RhinecanthusAculeatusCurve(), // Picasso triggerfish: measured species-specific W(g)=0.1559*TL(cm)^2.4059
        BalistoidesViridescensCurve(), // Titan triggerfish: FishBase Bayesian W(g)=0.03631*TL(cm)^2.93
        EmperorAngelfishCurve(), // Pomacanthus imperator, FishBase Bayesian estimate
        EmperorAngelfishCurve(), // Juvenile: same physical curve
        PantherGrouperCurve(), // Chromileptes altivelis: FishBase Bayesian estimate
        CoralGrouperCurve() // Cephalopholis miniata, Coral Hind / Coral Grouper
    };

    // Edwards et al. (2011), Rodrigues lagoon measurements, n=17 (14-27 cm TL).
    // Five-centimetre samples extrapolate to the FishBase 30 cm maximum; metres in game.
    private static float[] RhinecanthusAculeatusCurve()
    {
        var row=new float[6];
        for(int i=0;i<row.Length;i++)
        {
            double totalLengthCm=(i+1)*5.0;
            row[i]=(float)(0.1559*Math.Pow(totalLengthCm,2.4059)/1000.0);
        }
        return row;
    }

    // FishBase (Balistoides viridescens), Bayesian species length–weight model.
    // Total length is centimetres; normal fish range is about 15–75 cm.
    private static float[] BalistoidesViridescensCurve()
    {
        var row=new float[15];
        for(int i=0;i<row.Length;i++)
        {
            double lengthCm=(i+1)*5.0;
            row[i]=(float)(0.03631*Math.Pow(lengthCm,2.93)/1000.0);
        }
        return row;
    }

    private static float[] EmperorAngelfishCurve()
    {
        var row=new float[10];
        for(int i=0;i<row.Length;i++)row[i]=(float)(0.01738*Math.Pow((i+1)*5.0,2.88)/1000.0);
        return row;
    }

    // W(g)=0.01479*TL(cm)^3.08; reported maximum 70 cm total length.
    private static float[] PantherGrouperCurve()
    {
        var row=new float[14];
        for(int i=0;i<row.Length;i++)row[i]=(float)(0.01479*Math.Pow((i+1)*5.0,3.08)/1000.0);
        return row;
    }

    // FishBase Bayesian W(g)=0.01148*TL(cm)^3.04; max 50 cm total length.
    private static float[] CoralGrouperCurve()
    {
        var row=new float[10];
        for(int i=0;i<row.Length;i++)row[i]=(float)(0.01148*Math.Pow((i+1)*5.0,3.04)/1000.0);
        return row;
    }

    private static float[] GameplayCurve(float kgAtOneMetre,int samples)
    { var row=new float[samples];for(int i=0;i<samples;i++){float m=(i+1)*.05f;row[i]=kgAtOneMetre*m*m*m;}return row; }

    // Inverse of LengthMetres, so tiny pond catches retain their size in every view/save.
    public static float WeightForLength(int species,float metres)
    {
        if(species==FishCatalog.BattleScarredMakoSharkId)return 1.5f*WeightForLength(FishCatalog.MakoSharkId,metres/1.5f);
        // Giant Trevally scales both caught weight and rendered length 1.8x at
        // the same Bluewater Cay size percentile, matching the shark variant pattern.
        if(species==FishCatalog.GiantTrevallyId)return 1.8f*WeightForLength(FishCatalog.BluefinTrevallyId,metres/1.8f);
        var row=Weights[Math.Max(0,Math.Min(Weights.Length-1,FishCatalog.CanonicalId(species)))];
        float index=Math.Max(0,Math.Min(row.Length-1,metres/.05f-1));
        int low=(int)Math.Floor(index),high=Math.Min(low+1,row.Length-1);
        return row[low]+(row[high]-row[low])*(index-low);
    }

    public static float LengthMetres(int species,float kg)
    {
        if(species==FishCatalog.BattleScarredMakoSharkId)return 1.5f*LengthMetres(FishCatalog.MakoSharkId,kg/1.5f);
        if(species==FishCatalog.GiantTrevallyId)return 1.8f*LengthMetres(FishCatalog.BluefinTrevallyId,kg/1.8f);
        var weights=Weights[Math.Max(0,Math.Min(Weights.Length-1,FishCatalog.CanonicalId(species)))];
        if(float.IsNaN(kg)||kg<=0)return .05f;
        if(kg>=weights[weights.Length-1])
        {
            int last=weights.Length-1;
            float lastLength=weights.Length*.05f;
            float previousLength=(weights.Length-1)*.05f;
            double weightRatio=Math.Max(1.000001,weights[last]/weights[last-1]);
            double lengthRatio=Math.Max(1.000001,lastLength/previousLength);
            double exponent=Math.Log(weightRatio)/Math.Log(lengthRatio);
            if(double.IsNaN(exponent)||double.IsInfinity(exponent)||exponent<1.5)exponent=3.0;
            double extrapolated=lastLength*Math.Pow(kg/weights[last],1.0/exponent);
            return (float)Math.Min(lastLength*1.8,Math.Max(lastLength,extrapolated));
        }
        int hi=1;while(hi<weights.Length-1&&weights[hi]<kg)hi++;
        float t=(kg-weights[hi-1])/(weights[hi]-weights[hi-1]);
        return Math.Max(.05f,(hi+t)*.05f);
    }
}

