using System;
using UnityEngine;

[AddComponentMenu("Waves/Wave Services")]
public sealed class WaveServicesComponent : MonoBehaviour
{
    [SerializeField] private MvpBuildingSystem buildings;
    [SerializeField, Min(0.1f)] private float serviceSeconds = 12;
    private WaveServices runtime;
    public bool IsConfigured => buildings != null;
    public bool IsComplete => runtime != null && runtime.IsComplete;

    private int AvailableCapacity(NeedType need)
    {
        var building = buildings.Find(WaveCatalog.BuildingId(need));
        return building != null && building.unlocked && !building.IsSpa ? Mathf.Max(0, buildings.Capacity(building)) : 0;
    }
    private WaveServices Create(WaveDefinition definition)
    {
        if (!IsConfigured) throw new ArgumentException("Assign Buildings on the Wave Services scene component.");
        return new WaveServices(definition, AvailableCapacity, serviceSeconds);
    }
    public void BeginWave(WaveDefinition definition) { runtime = Create(definition); }
    public WaveSnapshot Forecast(WaveDefinition definition) => Create(definition).Snapshot();
    public void Tick(double seconds) { runtime?.Tick(seconds); }
    public WaveSnapshot Snapshot() => runtime?.Snapshot();
    public void SetSpeedMultiplier(NeedType need, double multiplier) { runtime?.SetSpeedMultiplier(need, multiplier); }
    public void ClearWave() { runtime = null; }
}
