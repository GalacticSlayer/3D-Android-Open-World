using System;

// Standalone checks: compile with AbyssalHardenedScales.cs. No Unity dependencies.
public static class HardenedScalesChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    public static void Main()
    {
        var scales = new AbyssalHardenedScales();
        scales.ObserveStatus(0, 1f, 0f, 5f);
        Check(!scales.IsActive(1f), "Initial status must not activate protection");
        scales.ObserveStatus(0, 2f, 0f, 5f);
        Check(!scales.IsActive(2f), "Repeated frames must not reroll");
        scales.ObserveStatus(1, 3f, .5f, 5f);
        Check(!scales.IsActive(3f), "50% boundary must fail");
        scales.ObserveStatus(2, 4f, .49999f, 6f);
        Check(scales.IsActive(4f), "Roll below 50% must activate");
        Check(scales.Opacity(4f) == .4f, "Initial opacity must be 40%");
        Check(scales.ReduceDamage(1000, 4f) == 200, "Lightning must lose 80% damage");
        Check(scales.ReduceDamage(4000, 4f) == 800, "Hookset must lose 80% damage");
        Check(scales.ReduceDamage(150, 4f) == 30, "Critical must lose 80% damage");
        Check(scales.Opacity(4.19f) == .9f, "Hit must flash at 90%");
        Check(scales.Opacity(4.21f) == .5f, "Flash must settle at 50% after 0.2s");
        int total = 0;
        for (int i = 0; i < 100; i++) total += scales.ReduceDamage(1, 5f);
        Check(total == 20, "Small integer hits must still reduce damage by exactly 80%");
        scales.ObserveStatus(6, 6f, 0f, 8f);
        Check(scales.IsActive(9.999f), "Changing to stunned must not cancel protection");
        Check(!scales.IsActive(10f), "Active status changes must not extend protection");
        Check(scales.ReduceDamage(100, 10f) == 100, "Expired protection must not reduce damage");
        scales.ObserveStatus(5, 11f, 0f, 8f);
        Check(scales.IsActive(18.999f) && !scales.IsActive(19f), "Controlled transition can activate for 8 seconds");
        Check(scales.Opacity(11f) == .4f, "New activation must reset opacity");
        scales.Reset(0);
        Check(!scales.IsActive(11f) && scales.ReduceDamage(100, 11f) == 100,
            "Catch, loss, subdued and next hook reset must clear defence");
        scales.ObserveStatus(1, 12f, 0f, 2f);
        Check(scales.IsActive(16.999f) && !scales.IsActive(17f), "Minimum duration is 5 seconds");
        scales.Reset(0);
        scales.ObserveStatus(1, 20f, 0f, 100f);
        Check(scales.IsActive(27.999f) && !scales.IsActive(28f), "Maximum duration is 8 seconds");
        int activations = 0;
        for (int i = 0; i < 1000; i++)
        {
            scales.Reset(0);
            scales.ObserveStatus(1, 1f, i / 1000f, 5f);
            if (scales.IsActive(1f)) activations++;
        }
        Check(activations == 500, "Exactly 50% of uniform rolls must activate");
        Console.WriteLine("PASS: " + checks + " Hardened Scales checks");
    }
}
