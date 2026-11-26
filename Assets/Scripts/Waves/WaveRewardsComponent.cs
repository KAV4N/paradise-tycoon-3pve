using UnityEngine;

[AddComponentMenu("Waves/Wave Rewards")]
public sealed class WaveRewardsComponent : MonoBehaviour
{
    [Header("Build document 4.4 - satisfaction tuning")]
    [SerializeField] private WaveRatingRules rating = new WaveRatingRules();
    // Energy is granted immediately by GameStateManager; no claim UI is required.
    [SerializeField] private WaveRewardRules rewards = new WaveRewardRules();

    public WaveResult Last { get; private set; }
    public bool IsConfigured => rating != null && rewards != null;

    public void ValidateConfiguration()
    {
        if (!IsConfigured) throw new System.InvalidOperationException("Wave Rewards is not configured.");
        rating.Validate(); rewards.Validate();
    }

    // Live satisfaction (0-100) for the HUD while the wave is running.
    // Call it from the progress event with the current snapshot.
    public double EvaluateLive(WaveSnapshot snapshot)
    {
        ValidateConfiguration();
        return rating.EvaluateLive(snapshot);
    }

    // Average satisfaction (0-100) of one need of a wave. Used by the S5 investment recommendation.
    public double EvaluateNeed(NeedSnapshot need)
    {
        ValidateConfiguration();
        return rating.NeedSatisfaction(need);
    }

    // Final evaluation after the wave ends. Computed and logged once per wave;
    // calling it again for the same wave returns the cached result without logging.
    public WaveResult Prepare(WaveSnapshot completed)
    {
        ValidateConfiguration();
        if (completed == null || !completed.IsComplete) throw new System.ArgumentException("The wave has not completed.");
        if (Last != null && Last.WaveNumber == completed.Number) return Last;
        Last = rewards.Create(completed, rating);
        LogResult(Last);
        return Last;
    }

    private static void LogResult(WaveResult result)
    {
        Debug.Log($"[Wave {result.WaveNumber}] RESULT\n" +
                  $"  Customers:    {result.Customers}\n" +
                  $"  Satisfaction: {result.Satisfaction:F1} / 100{(result.Perfect ? " (PERFECT)" : "")}\n" +
                  $"  Stars:        {result.Stars}");
    }
}
