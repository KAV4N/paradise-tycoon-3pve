using System;
using System.Collections.Generic;

// uniform arrivals, FIFO waiting and independent occupied seats.
public class ServiceQueue
{
    protected virtual bool TrackWaitingTime => true;
    private const double Epsilon = 1e-9;
    private readonly NeedType type;
    private readonly int total;
    private readonly double arrivalSeconds;
    private readonly double serviceSeconds;
    private readonly double[] seats;
    private readonly Queue<double> waiting = new Queue<double>();
    private readonly List<double> waits = new List<double>();
    private double elapsed;
    private int arrived, inService, completed;
    private double multiplier = 1;
    public bool IsComplete => elapsed >= arrivalSeconds && completed == total;
    public double SpeedMultiplier
    {
        get => multiplier;
        set { Positive(value); multiplier = value; }
    }
    internal static void Positive(double value)
    {
        if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
    }
    public ServiceQueue(NeedType type, int demand, int capacity, double serviceSeconds, double arrivalSeconds)
    {
        if (!type.IsWaveNeed() || demand < 0 || capacity < 1) throw new ArgumentOutOfRangeException(nameof(demand));
        Positive(serviceSeconds); Positive(arrivalSeconds);
        this.type = type; total = demand; this.serviceSeconds = serviceSeconds; this.arrivalSeconds = arrivalSeconds;
        seats = new double[capacity];
        for (int i = 0; i < capacity; i++) seats[i] = -1;
    }
    private double NextArrival => arrived < total ? arrivalSeconds * ((arrived + 1.0) / total) : double.PositiveInfinity;
    public void Tick(double seconds)
    {
        //TODO: ticking
    }
}

