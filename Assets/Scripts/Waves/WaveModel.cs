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

public static class WaveCatalog
{
    public const int Count = 8;
    // Columns: visual customers, Rooms, Drinks, Food, Relax, Ocean View.
    private static readonly int[,] Values = {
        {12,8,8,6,0,0}, {18,12,16,10,0,0}, {24,16,18,14,0,0}, {26,16,18,14,8,0},
        {34,20,22,18,16,0}, {40,25,28,22,20,0}, {42,25,28,22,20,10}, {52,31,35,28,25,16}
    };
    public static WaveDefinition Get(int number, double arrivalSeconds = 90)
    {
        if (number < 1 || number > Count) throw new ArgumentOutOfRangeException(nameof(number));
        int i = number - 1;
        return new WaveDefinition(number, Values[i, 0], arrivalSeconds, Values[i, 1], Values[i, 2], Values[i, 3], Values[i, 4], Values[i, 5]);
    }
    public static string BuildingId(NeedType need)
    {
        switch (need)
        {
            case NeedType.Rooms: return "Reception";
            case NeedType.Food: return "Restaurant";
            case NeedType.Drinks: return "Bar";
            case NeedType.Relax: return "Pool";
            case NeedType.OceanView: return "Bungalow";
            default: throw new ArgumentOutOfRangeException(nameof(need));
        }
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
