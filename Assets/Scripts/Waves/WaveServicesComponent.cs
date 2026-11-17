using System;
using UnityEngine;

[AddComponentMenu("Waves/Wave Services")]
public sealed class WaveServicesComponent : MonoBehaviour
{
    [SerializeField] private MvpBuildingSystem buildings;
    [SerializeField] private WorkerRosterComponent workerRoster;
    [SerializeField, Min(0.1f)] private float serviceSeconds = 12;
    private WaveServices runtime;
    public bool IsConfigured => buildings != null;
    public bool IsComplete => runtime != null && runtime.IsComplete;
    public event Action<NeedType, double, double, double> ServiceSpeedChanged;

    private int AvailableCapacity(NeedType need)
    {
        var building = buildings.Find(WaveCatalog.BuildingId(need));
        return building != null && building.unlocked && !building.IsSpa ? Mathf.Max(0, buildings.Capacity(building)) : 0;
    }
    private WaveServices Create(WaveDefinition definition)
    {
        if (!IsConfigured) throw new ArgumentException("Assign Buildings on the Wave Services scene component.");
        var created = new WaveServices(definition, AvailableCapacity, serviceSeconds);
        if (workerRoster != null)
        {
            foreach (var pair in definition.Demand)
            {
                string buildingId = WaveCatalog.BuildingId(pair.Key);
                created.SetSpeedMultiplier(pair.Key, workerRoster.GetUtilization(buildingId));
            }
        }
        return created;
    }
    public void BeginWave(WaveDefinition definition) { runtime = Create(definition); }
    public WaveSnapshot Forecast(WaveDefinition definition) => Create(definition).Snapshot();
    public void Tick(double seconds) { runtime?.Tick(seconds); }
    public WaveSnapshot Snapshot() => runtime?.Snapshot();
    public void SetSpeedMultiplier(NeedType need, double multiplier)
    {
        if (runtime == null) return;
        bool exists = runtime.TryGetRates(need, out double previousRate, out _);
        runtime.SetSpeedMultiplier(need, multiplier);
        if (exists && runtime.TryGetRates(need, out double currentRate, out double arrivalRate) && currentRate != previousRate)
            ServiceSpeedChanged?.Invoke(need, previousRate, currentRate, arrivalRate);
    }
    public void ClearWave() { runtime = null; }
}
