using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Waves/Wave Services")]
public sealed class WaveServicesComponent : MonoBehaviour
{
    private static readonly NeedType[] AllNeeds = (NeedType[])Enum.GetValues(typeof(NeedType));

    [SerializeField] private BuildingSystem buildings;
    [SerializeField, Min(0.1f)] private float serviceSeconds = 12;
    private WaveServices runtime;

    [Header("Energy Boost")]
    [SerializeField, Min(1)] private int boostEnergyCost = 1;           // Energy spent per activation
    [SerializeField, Min(1f)] private float boostDurationSeconds = 15f; // how long the boost lasts (wave time)
    [SerializeField, Min(1.01f)] private float boostMultiplier = 1.5f;  // service speed x1.5 while boosted
    private readonly Dictionary<NeedType, float> boostRemaining = new Dictionary<NeedType, float>();
    private readonly List<NeedType> expiredBoosts = new List<NeedType>();

    public int BoostEnergyCost => boostEnergyCost;
    public float BoostDuration => boostDurationSeconds;
    public float BoostMultiplier => boostMultiplier;
    public bool HasService(NeedType need) => runtime != null && runtime.TryGetRates(need, out _, out _);
    public bool IsBoosted(NeedType need) => boostRemaining.ContainsKey(need);
    public float BoostTimeLeft(NeedType need) => boostRemaining.TryGetValue(need, out float left) ? left : 0f;
    public event Action<NeedType, bool> BoostChanged; // need, isActive

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
        boostRemaining.Clear();
        ApplyServiceMultipliers();
    }

    public WaveSnapshot Forecast(WaveDefinition definition) => Create(definition).Snapshot();

    public void Tick(double seconds)
    {
        runtime?.Tick(seconds);
        TickBoosts((float)seconds);
    }

    // Spends Energy and starts the boost for one service. Returns false when it cannot be used.
    public bool TryActivateBoost(NeedType need)
    {
        if (buildings == null || runtime == null || !need.IsWaveNeed()) return false;
        if (boostRemaining.ContainsKey(need)) return false;                  // already boosted
        if (!runtime.TryGetRates(need, out _, out _)) return false;          // service not in this wave
        if (buildings.energy < boostEnergyCost) return false;                // not enough Energy

        // Register first: TrySpendCurrencies fires OnChanged -> ApplyServiceMultipliers, which must see the boost.
        boostRemaining[need] = boostDurationSeconds;
        if (!buildings.TrySpendCurrencies(0, 0, boostEnergyCost, 0))
        {
            boostRemaining.Remove(need);
            return false;
        }
        ApplyServiceMultipliers();
        BoostChanged?.Invoke(need, true);
        return true;
    }

    private void TickBoosts(float seconds)
    {
        if (boostRemaining.Count == 0 || seconds <= 0f) return;

        expiredBoosts.Clear();
        foreach (var need in new List<NeedType>(boostRemaining.Keys))
        {
            float left = boostRemaining[need] - seconds;
            if (left <= 0f) expiredBoosts.Add(need);
            else boostRemaining[need] = left;
        }
        if (expiredBoosts.Count == 0) return;

        foreach (var need in expiredBoosts) boostRemaining.Remove(need);
        ApplyServiceMultipliers();
        foreach (var need in expiredBoosts) BoostChanged?.Invoke(need, false);
    }

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

    public void ClearWave() { runtime = null; boostRemaining.Clear(); }

    // Pushes the combined multiplier (Worker utilization x Manager multiplier) into the running wave
    // for every wave need, times the Energy boost multiplier while a boost is active.
    public void ApplyServiceMultipliers()
    {
        if (buildings == null || runtime == null) return;

        foreach (NeedType need in AllNeeds)
        {
            if (!need.IsWaveNeed()) continue;
            double multiplier = buildings.ServiceMultiplier(need);
            if (multiplier > 0)
            {
                if (boostRemaining.ContainsKey(need)) multiplier *= boostMultiplier;
                SetSpeedMultiplier(need, multiplier);
            }
        }
    }

    // Kept so existing callers still compile.
    public void ApplyWorkerUtilization() => ApplyServiceMultipliers();
}