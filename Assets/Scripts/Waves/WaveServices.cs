using System;
using System.Collections.Generic;

public sealed class WaveServices
{
    public WaveDefinition Definition { get; }
    public double Elapsed { get; private set; }
    private readonly Dictionary<NeedType, ServiceQueue> services = new Dictionary<NeedType, ServiceQueue>();
    public bool IsComplete
    {
        get
        {
            if (Elapsed < Definition.ArrivalSeconds) return false;
            foreach (var service in services.Values) if (!service.IsComplete) return false;
            return true;
        }
    }
    public WaveServices(WaveDefinition definition, Func<NeedType, int> availableCapacity, double serviceSeconds = 12)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ServiceQueue.Positive(serviceSeconds);
        foreach (var pair in definition.Demand)
        {
            int capacity = availableCapacity(pair.Key);
            // WR9: unavailable services produce no demand, rather than blocking the wave.
            if (pair.Value == 0 || capacity <= 0) continue;
            services.Add(pair.Key, new ServiceQueue(pair.Key, pair.Value, capacity, serviceSeconds, definition.ArrivalSeconds));
        }
    }
    public void Tick(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || double.IsInfinity(Elapsed + seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        foreach (var service in services.Values) service.Tick(seconds);
        Elapsed += seconds;
    }
    public void SetSpeedMultiplier(NeedType need, double multiplier)
    {
        ServiceQueue.Positive(multiplier);
        if (services.TryGetValue(need, out var service)) service.SpeedMultiplier = multiplier;
    }
    public WaveSnapshot Snapshot()
    {
        var needs = new List<NeedSnapshot>();
        foreach (var service in services.Values) needs.Add(service.Snapshot());
        return new WaveSnapshot(Definition, Elapsed, needs);
    }
}
