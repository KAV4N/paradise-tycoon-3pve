using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WaveCurrencyRewardsChecks
{
    [MenuItem("Tools/Waves/Verify Coins and Reputation Claim")]
    private static void Verify()
    {
        var rules = new WaveRewardRules();
        int[] expected = { 141, 211, 282, 352, 422 };
        for (int stars = 1; stars <= 5; stars++)
            Require(rules.CalculateCoins(22, stars, false) == expected[stars - 1], "Coins at " + stars + " stars");
        Require(rules.CalculateCoins(22, 5, true) == 458, "Perfect multiplier and rounding");
        Require(rules.CalculateCoins(22, 0, false) == 141, "Lowest rating still earns Coins");
        Require(rules.CalculateCoins(int.MaxValue, 5, true) == int.MaxValue, "Coins overflow protection");
        var active = new WaveServices(new WaveDefinition(1, 12, 90, 8, 8, 6, 0, 0), need => 3);
        bool rejected = false;
        try { rules.Create(active.Snapshot(), new WaveRatingRules()); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, "Incomplete waves cannot produce rewards");
        active.Tick(5000);
        var result = rules.Create(active.Snapshot(), new WaveRatingRules());
        Require(result.Reputation == Math.Max(1, result.Stars), "Reputation follows stars with a minimum of one");

        if (Application.isPlaying)
        {
            var manager = UnityEngine.Object.FindAnyObjectByType<GameStateManager>();
            Require(manager != null && manager.State == GameState.WAVE_COMPLETE, "Finish a wave and leave S4 open before testing the claim");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var completed = (WaveResult)typeof(GameStateManager).GetField("completedResult", flags).GetValue(manager);
            var managerData = new SerializedObject(manager);
            var wallet = (BuildingSystem)managerData.FindProperty("buildings").objectReferenceValue;
            var view = (WaveCompleteUI)managerData.FindProperty("waveCompleteUI").objectReferenceValue;
            var button = (Button)new SerializedObject(view).FindProperty("continueButton").objectReferenceValue;
            var continueMethod = typeof(GameStateManager).GetMethod("ContinueFromWaveComplete", flags);
            int coinsBefore = wallet.coins, reputationBefore = wallet.reputation;
            int reentries = 0;
            Action reenter = () => { reentries++; continueMethod.Invoke(manager, null); };
            wallet.OnChanged += reenter;
            try
            {
                button.onClick.Invoke();
                button.onClick.Invoke();
                continueMethod.Invoke(manager, null);
            }
            finally { wallet.OnChanged -= reenter; }
            Require(reentries == 1, "Wallet callback runs once, including reentrant claim attempts");
            Require(wallet.coins == (int)Math.Min(int.MaxValue, (long)coinsBefore + completed.Coins), "Coins credited exactly once");
            Require(wallet.reputation == (int)Math.Min(int.MaxValue, (long)reputationBefore + completed.Reputation), "Reputation credited exactly once");
            Debug.Log("PASS: coin tiers/Perfect/rounding/overflow, incomplete wave rejection, Reputation by stars, double-click and reentrant claim paid once.");
        }
        else Debug.Log("PASS: coin tiers/Perfect/rounding/overflow, incomplete wave rejection and Reputation by stars. Run again in Play Mode with S4 open to test the claim.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Wave currency check failed: " + message);
    }
}
