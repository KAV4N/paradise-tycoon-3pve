using System;
using TMPro;
using UnityEngine;

[Serializable]
public sealed class ResortValueSystem
{
    [Header("Resort Value from staff")]
    [Min(0)] public int workerHireGain;
    [Min(0)] public int managerHireGain;

    [Header("Optional Resort Value UI")]
    public TMP_Text workerHireGainLabel;
    public TMP_Text managerHireGainLabel;
    public TMP_Text reviewGainLabel;

    public int UnlockGain(BuildingSystem.Building building)
    {
        return building.unlockResortValueGain;
    }

    public int UpgradeGain(int[] gainsByLevel, int currentLevel)
    {
        // The table is zero-based. The current level indexes the next level's reward.
        return gainsByLevel != null && currentLevel >= 0 && currentLevel < gainsByLevel.Length
            ? gainsByLevel[currentLevel]
            : -1;
    }

    public void Add(ref int total, int gain)
    {
        total = (int)Math.Min(int.MaxValue, (long)total + Mathf.Max(0, gain));
    }

    public void AddReview(ref int total, int gain)
    {
        Add(ref total, gain);
        SetText(reviewGainLabel, GainLine("Resort Value gain", gain));
    }

    public void RefreshHud(TMP_Text totalLabel, int total)
    {
        SetText(totalLabel, total.ToString());
        SetText(workerHireGainLabel, GainLine("Resort Value gain", workerHireGain));
        SetText(managerHireGainLabel, GainLine("Resort Value gain", managerHireGain));
    }

    public void ClearReviewLabel()
    {
        SetText(reviewGainLabel, "");
    }

    public string BalanceText(int total)
    {
        return $"Resort Value {total}";
    }

    public string UnlockPreview(BuildingSystem.Building building)
    {
        return GainLine("Resort Value", UnlockGain(building), "\n");
    }

    public string UpgradePreview(int gain)
    {
        return GainLine("Resort Value gain", gain, "\n");
    }

    private static string GainLine(string label, int gain, string prefix = "")
    {
        return gain > 0 ? $"{prefix}{label}: +{gain}" : "";
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
            target.text = value;
    }
}
