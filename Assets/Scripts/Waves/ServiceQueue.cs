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
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || double.IsInfinity(elapsed + seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        double target = elapsed + seconds;
        while (elapsed < target)
        {
            double rate = multiplier / serviceSeconds;
            double step = Math.Min(target - elapsed, Math.Max(0, NextArrival - elapsed));
            for (int i = 0; i < seats.Length; i++)
                if (seats[i] >= 0) step = Math.Min(step, Math.Max(0, (1 - seats[i]) / rate));
            for (int i = 0; i < seats.Length; i++) if (seats[i] >= 0) seats[i] += step * rate;
            elapsed += step;
            for (int i = 0; i < seats.Length; i++)
                if (seats[i] >= 1 - Epsilon) { seats[i] = -1; inService--; completed++; }
            while (arrived < total && NextArrival <= elapsed + Epsilon)
            {
                waiting.Enqueue(NextArrival);
                arrived++;
            }
            for (int i = 0; i < seats.Length && waiting.Count > 0; i++)
                if (seats[i] < 0)
                {
                    double arrival = waiting.Dequeue();
                    if (TrackWaitingTime) waits.Add(Math.Max(0, elapsed - arrival));
                    seats[i] = 0; inService++;
                }
        }
    }
    public NeedSnapshot Snapshot() => new NeedSnapshot(type, total, arrived, waiting.Count,
        inService, completed, TrackWaitingTime && waiting.Count > 0 ? Math.Max(0, elapsed - waiting.Peek()) : 0,
        waits.ToArray(), (double[])seats.Clone(), TrackWaitingTime);
}

