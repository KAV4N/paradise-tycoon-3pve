using UnityEngine;

[CreateAssetMenu(fileName = "WorkerTuning", menuName = "Paradise Tycoon/Worker Tuning")]
public sealed class WorkerTuningConfig : ScriptableObject
{
    [Header("Hiring economy")]
    [Min(0)] public int workerBasePrice = 400;
    [Min(0)] public int workerPriceIncrement = 200;

    [Header("Roster limits")]
    [Range(0f, 1f)] public float workerCapRatio = 0.8f;

    [Header("Building utilization")]
    [Min(0.01f)] public float utilizationWithWorker = 1f;
    [Min(0.01f)] public float utilizationEmpty = 0.33f;

    [Tooltip("Contribution of the 1st, 2nd and 3rd Worker. Later entries are ignored by the current building slot model.")]
    [Min(0f)] public float[] workerDiminishingReturns = { 1f, 0.5f, 0.25f };

    public int GetHirePrice(int successfulPaidHires)
    {
        long price = (long)workerBasePrice + (long)Mathf.Max(0, successfulPaidHires) * workerPriceIncrement;
        return (int)Mathf.Clamp(price, 0L, int.MaxValue);
    }

    public float GetUtilization(int workerCount)
    {
        if (workerCount <= 0)
            return Mathf.Max(0.01f, utilizationEmpty);

        float contribution = 0f;
        int count = Mathf.Min(workerCount, workerDiminishingReturns == null ? 0 : workerDiminishingReturns.Length);
        for (int i = 0; i < count; i++)
            contribution += Mathf.Max(0f, workerDiminishingReturns[i]);

        return Mathf.Max(0.01f, utilizationEmpty) +
               (Mathf.Max(0.01f, utilizationWithWorker) - Mathf.Max(0.01f, utilizationEmpty)) * contribution;
    }

    private void OnValidate()
    {
        workerBasePrice = Mathf.Max(0, workerBasePrice);
        workerPriceIncrement = Mathf.Max(0, workerPriceIncrement);
        workerCapRatio = Mathf.Clamp01(workerCapRatio);
        utilizationWithWorker = Mathf.Max(0.01f, utilizationWithWorker);
        utilizationEmpty = Mathf.Max(0.01f, utilizationEmpty);
        if (workerDiminishingReturns == null || workerDiminishingReturns.Length == 0)
            workerDiminishingReturns = new[] { 1f, 0.5f, 0.25f };
        for (int i = 0; i < workerDiminishingReturns.Length; i++)
            workerDiminishingReturns[i] = Mathf.Max(0f, workerDiminishingReturns[i]);
    }
}
