using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;


public sealed class WaveDefinition
{
    public int Number { get; }
    public int Customers { get; }
    public double ArrivalSeconds { get; }
    public IReadOnlyDictionary<NeedType, int> Demand { get; }

    public WaveDefinition(int number, int customers, double arrivalSeconds, int rooms, int drinks, int food, int relax, int oceanView)
    {
        if (number < 1 || customers < 0 || arrivalSeconds <= 0 || double.IsNaN(arrivalSeconds) || double.IsInfinity(arrivalSeconds))
            throw new ArgumentOutOfRangeException(nameof(number));
        var demand = new Dictionary<NeedType, int> {
            { NeedType.Rooms, rooms }, { NeedType.Drinks, drinks }, { NeedType.Food, food },
            { NeedType.Relax, relax }, { NeedType.OceanView, oceanView }
        };
        foreach (int value in demand.Values) if (value < 0) throw new ArgumentOutOfRangeException(nameof(rooms));
        Number = number; Customers = customers; ArrivalSeconds = arrivalSeconds;
        Demand = new ReadOnlyDictionary<NeedType, int>(demand);
    }
}

public sealed class NeedSnapshot
{
    public NeedType Type { get; }
    public int Total { get; }
    public int Arrived { get; }
    public int Waiting { get; }
    public int InService { get; }
    public int Completed { get; }
    public double LongestCurrentWait { get; }
    public bool TracksWaitingTime { get; }
    public IReadOnlyList<double> WaitsBeforeService { get; }
    // -1 = empty seat; 0..1 = normalized service progress.
    public IReadOnlyList<double> SeatProgress { get; }
    internal NeedSnapshot(NeedType type, int total, int arrived, int waiting, int inService,
        int completed, double longestWait, double[] waits, double[] seats, bool tracksWaitingTime = true)
    {
        Type = type; Total = total; Arrived = arrived; Waiting = waiting; InService = inService;
        Completed = completed; LongestCurrentWait = longestWait;
        TracksWaitingTime = tracksWaitingTime;
        WaitsBeforeService = Array.AsReadOnly(waits); SeatProgress = Array.AsReadOnly(seats);
    }
}

public sealed class WaveSnapshot
{
    public int Number { get; }
    public int Customers { get; }
    public double Elapsed { get; }
    public bool ArrivalsClosed { get; }
    public IReadOnlyList<NeedSnapshot> Needs { get; }
    public int Total { get; }
    public int Completed { get; }
    public bool IsComplete => ArrivalsClosed && Completed == Total;
    internal WaveSnapshot(WaveDefinition definition, double elapsed, List<NeedSnapshot> needs)
    {
        Number = definition.Number; Customers = definition.Customers; Elapsed = elapsed;
        ArrivalsClosed = elapsed >= definition.ArrivalSeconds;
        Needs = needs.AsReadOnly();
        foreach (var need in needs) { Total += need.Total; Completed += need.Completed; }
    }
}
