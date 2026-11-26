using System;
using UnityEngine;

[AddComponentMenu("Waves/Wave Services")]
public sealed class WaveServicesComponent : MonoBehaviour
{
    private static readonly NeedType[] AllNeeds = (NeedType[])Enum.GetValues(typeof(NeedType));

    [SerializeField] private BuildingSystem buildings;
    [SerializeField, Min(0.1f)] private float serviceSeconds = 12;
    private WaveServices runtime;

    public bool IsConfigured => buildings != null;
    public bool IsComplete => runtime != null && runtime.IsComplete;
    public event Action<NeedType, double, double, double> ServiceSpeedChanged;

    private void OnEnable()
    {
        if (buildings != null)
            buildings.OnChanged += ApplyServiceMultipliers;
    }

    private void OnDisable()
    {
        if (buildings != null)
            buildings.OnChanged -= ApplyServiceMultipliers;
    }

    private int AvailableCapacity(NeedType need)
    {
        var building = buildings.Find(WaveModelComponent.BuildingId(need));
        return building != null && building.unlocked && !building.IsSpa ? Mathf.Max(0, buildings.Capacity(building)) : 0;
    }

    private WaveServices Create(WaveDefinition definition)
    {
        if (!IsConfigured) throw new InvalidOperationException("Assign Buildings on the Wave Services scene component.");
        return new WaveServices(definition, AvailableCapacity, serviceSeconds);
    }

    public void BeginWave(WaveDefinition definition)
    {
        runtime = Create(definition);
        ApplyServiceMultipliers();
    }

    public WaveSnapshot Forecast(WaveDefinition definition) => Create(definition).Snapshot();

    public void Tick(double seconds) { runtime?.Tick(seconds); }

    public WaveSnapshot Snapshot() => runtime?.Snapshot();

    // Sets the speed multiplier of a need directly. The normal owner is ApplyServiceMultipliers
    // (Worker utilization x Manager multiplier), so this is for debugging and tests.
    public void SetSpeedMultiplier(NeedType need, double multiplier)
    {
        if (runtime == null) return;
        bool exists = runtime.TryGetRates(need, out double previousRate, out _);
        runtime.SetSpeedMultiplier(need, multiplier);
        if (exists && runtime.TryGetRates(need, out double currentRate, out double arrivalRate) && currentRate != previousRate)
            ServiceSpeedChanged?.Invoke(need, previousRate, currentRate, arrivalRate);
    }

    public void ClearWave() { runtime = null; }

    // Pushes the combined multiplier (Worker utilization x Manager multiplier) into the running wave
    // for every wave need. Energy is not part of it yet.
    public void ApplyServiceMultipliers()
    {
        if (buildings == null || runtime == null) return;

        foreach (NeedType need in AllNeeds)
        {
            if (!need.IsWaveNeed()) continue;
            double multiplier = buildings.ServiceMultiplier(need);
            if (multiplier > 0)
                SetSpeedMultiplier(need, multiplier);
        }
    }

    // Kept so existing callers still compile.
    public void ApplyWorkerUtilization() => ApplyServiceMultipliers();
}