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

    public static int AddReputation(int score, int gain)
    {
        return (int)Math.Min(int.MaxValue, (long)Math.Max(0, score) + Math.Max(0, gain));
    }

    public static int RestoreReputation(int savedScore)
    {
        return Math.Max(0, savedScore);
    }

    public static bool HasRequiredReputation(int score, int required)
    {
        return required >= 0 && score >= required;
    }

    public static string ReputationGateText(int score, int required)
    {
        return required < 0
            ? "Reputation threshold not configured"
            : $"Reputation {score} / {required}";
    }

    public static string ReputationBalanceText(int score)
    {
        return $"Reputation {score}";
    }

    public static void RefreshReputationHud(TMP_Text hud, int score)
    {
        SetText(hud, score.ToString());
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

public partial class BuildingSystem
{
    [Header("Currency Balances")]
    [Min(0)] public int coins;
    [Min(0)] public int materials;
    [Min(0)] public int energy;
    [Min(0)] public int gems;

    [Header("Existing HUD text references")]
    public TMP_Text coinsHud;
    public TMP_Text materialsHud;
    public TMP_Text energyHud;
    public TMP_Text gemsHud;

    public void AddCurrencies(int coinGain = 0, int materialGain = 0, int energyGain = 0, int gemGain = 0)
    {
        CreditCurrencies(coinGain, materialGain, energyGain, gemGain);

        OnChanged?.Invoke();
        RefreshHud();
        Refresh();
    }

    public bool TrySpendCurrencies(int coinCost = 0, int materialCost = 0, int energyCost = 0, int gemCost = 0)
    {
        if (!SpendCurrencies(coinCost, materialCost, energyCost, gemCost))
            return false;

        OnChanged?.Invoke();
        RefreshHud();
        Refresh();
        return true;
    }

    private void CreditCurrencies(int coinGain, int materialGain, int energyGain, int gemGain)
    {
        coins = AddCurrency(coins, coinGain);
        materials = AddCurrency(materials, materialGain);
        energy = AddCurrency(energy, energyGain);
        gems = AddCurrency(gems, gemGain);
    }

    private static int AddCurrency(int balance, int gain)
    {
        return (int)Math.Min(int.MaxValue, (long)balance + Mathf.Max(0, gain));
    }

    private bool SpendCurrencies(int coinCost, int materialCost, int energyCost, int gemCost)
    {
        if (coinCost < 0 || materialCost < 0 || energyCost < 0 || gemCost < 0 ||
            (coinCost > 0 && coins < coinCost) ||
            (materialCost > 0 && materials < materialCost) ||
            (energyCost > 0 && energy < energyCost) ||
            (gemCost > 0 && gems < gemCost))
            return false;

        coins -= coinCost;
        materials -= materialCost;
        energy -= energyCost;
        gems -= gemCost;
        return true;
    }

    private EconomyData CaptureEconomy()
    {
        return new EconomyData
        {
            coins = coins,
            materials = materials,
            energy = energy,
            gems = gems,
            reputation = reputation,
            resortValue = resortValue
        };
    }

    private void RestoreCurrencies(EconomyData economy)
    {
        coins = Mathf.Max(0, economy.coins);
        materials = Mathf.Max(0, economy.materials);
        energy = Mathf.Max(0, economy.energy);
        gems = Mathf.Max(0, economy.gems);
    }

    private void RefreshCurrencyHud()
    {
        SetHud(coinsHud, coins);
        SetHud(materialsHud, materials);
        SetHud(energyHud, energy);
        SetHud(gemsHud, gems);
    }

    private void SetHud(TMP_Text target, int value)
    {
        if (target == null)
            return;

        string label = value.ToString();

        if (target.text != label)
            target.text = label;
    }

#if UNITY_EDITOR
    private void BindCurrencyHud(TMP_Text text)
    {
        switch (text.transform.parent.name)
        {
            case "Coins":
                coinsHud = text;
                break;

            case "Materials":
                materialsHud = text;
                break;

            case "Energy":
                energyHud = text;
                break;

            case "Gems":
                gemsHud = text;
                break;
        }
    }

    private void ConfigureCurrencyHud()
    {
        ConfigureHudNumber(coinsHud, true);
        ConfigureHudNumber(materialsHud, true);
        ConfigureHudNumber(energyHud, true);
        ConfigureHudNumber(gemsHud, true);
    }
#endif
}
