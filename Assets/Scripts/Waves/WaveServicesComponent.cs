using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Waves/Wave Services")]
public sealed class WaveServicesComponent : MonoBehaviour
{
    [SerializeField] private BuildingSystem buildings;
    [SerializeField, Min(0.1f)] private float serviceSeconds = 12;
    private WaveServices runtime;

    public bool IsConfigured => buildings != null;
    public bool IsComplete => runtime != null && runtime.IsComplete;
    public event Action<NeedType, double, double, double> ServiceSpeedChanged;

    private void OnEnable()
    {
        if (buildings != null)
            buildings.OnChanged += ApplyWorkerUtilization;
    }

    private void OnDisable()
    {
        if (buildings != null)
            buildings.OnChanged -= ApplyWorkerUtilization;
    }

    private int AvailableCapacity(NeedType need)
    {
        var building = buildings.Find(WaveModelComponent.BuildingId(need));
        return building != null && building.unlocked && !building.IsSpa ? Mathf.Max(0, buildings.Capacity(building)) : 0;
    }

    private WaveServices Create(WaveDefinition definition)
    {
        if (!IsConfigured) throw new ArgumentException("Assign Buildings on the Wave Services scene component.");
        return new WaveServices(definition, AvailableCapacity, serviceSeconds);
    }

    public void BeginWave(WaveDefinition definition)
    {
        runtime = Create(definition);
        ApplyWorkerUtilization();
    }

    public WaveSnapshot Forecast(WaveDefinition definition) => Create(definition).Snapshot();

    public void Tick(double seconds) { runtime?.Tick(seconds); }

    public WaveSnapshot Snapshot() => runtime?.Snapshot();

    // Debug hook. Sets the multiplier directly; Worker utilization is the normal owner.
    public void SetSpeedMultiplier(NeedType need, double multiplier)
    {
        if (runtime == null) return;
        bool exists = runtime.TryGetRates(need, out double previousRate, out _);
        runtime.SetSpeedMultiplier(need, multiplier);
        if (exists && runtime.TryGetRates(need, out double currentRate, out double arrivalRate) && currentRate != previousRate)
            ServiceSpeedChanged?.Invoke(need, previousRate, currentRate, arrivalRate);
    }

    public void ClearWave() { runtime = null; }

    // Pushes Worker utilization into the running wave for every known wave need.
    public void ApplyWorkerUtilization()
    {
        if (buildings == null || runtime == null) return;

        foreach (NeedType need in Enum.GetValues(typeof(NeedType)))
        {
            if (!need.IsWaveNeed()) continue;
            double u = buildings.WorkerUtilization(need);
            if (u > 0)
                SetSpeedMultiplier(need, u);
        }
    }
}