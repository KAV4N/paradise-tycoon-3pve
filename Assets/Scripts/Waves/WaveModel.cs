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