using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WaveTaskChecks
{
    [MenuItem("Tools/Waves/Verify Forecast")]
    private static void Verify()
    {
        bool pool = false, bungalow = false;
        Func<NeedType, int> capacity = need =>
            need == NeedType.Relax ? (pool ? 3 : 0) : need == NeedType.OceanView ? (bungalow ? 3 : 0) : 3;
        var definition = WaveCatalog.Get(8);
        var forecast = new WaveServices(definition, capacity).Snapshot();
        Require(forecast.Needs.Count == 3 && forecast.Elapsed == 0 && forecast.Completed == 0, "Locked services excluded from forecast");
        pool = true;
        Require(new WaveServices(definition, capacity).Snapshot().Needs.Count == 4, "Pool adds fourth forecast need");
        bungalow = true;
        var full = new WaveServices(definition, capacity).Snapshot();
        Require(full.Needs.Count == 5 && full.Total == 135, "Bungalows adds fifth forecast need and exact totals");
        var active = new WaveServices(definition, capacity); active.Tick(15);
        var before = active.Snapshot();
        new WaveServices(WaveCatalog.Get(1), capacity).Snapshot();
        Require(active.Snapshot().Elapsed == before.Elapsed && active.Snapshot().Completed == before.Completed, "Forecast cannot reset active service progress");
        var view = UnityEngine.Object.FindAnyObjectByType<WavePreparationUI>(FindObjectsInactive.Include);
        var manager = UnityEngine.Object.FindAnyObjectByType<GameStateManager>();
        Require(view != null && view.IsConfigured && manager != null, "Scene forecast UI exists");
        var managerData = new SerializedObject(manager);
        Require(managerData.FindProperty("preparationUI").objectReferenceValue == view, "Inspector lifecycle references wired");
        Debug.Log("PASS: forecast 3/4/5 needs, exact totals, no active service mutation, scene references.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Wave task check failed: " + message);
    }
}
