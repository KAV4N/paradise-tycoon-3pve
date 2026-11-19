using System;

// Build document 4.4 / 4.6 / 5.6. Pure data and calculations, reusable without a scene.
[Serializable]
public sealed class WaveRatingRules
{
    public double patienceGrace = 10;
    public double patienceFloorAfter = 30;
    public double unitFloor = 25;
    public double oceanViewWeight = 1.5;
    public double[] starThresholds = { 40, 55, 70, 80, 90 };

    public void Validate()
    {
        if (!Finite(patienceGrace) || !Finite(patienceFloorAfter) || patienceGrace < 0 || patienceFloorAfter <= patienceGrace ||
            !Finite(unitFloor) || unitFloor < 0 || unitFloor > 100 ||
            !Finite(oceanViewWeight) || oceanViewWeight < 1 || starThresholds == null || starThresholds.Length != 5)
            throw new ArgumentException("Invalid wave satisfaction tuning.");
        for (int i = 0; i < starThresholds.Length; i++)
            if (!Finite(starThresholds[i]) || starThresholds[i] < 0 || starThresholds[i] > 100 ||
                (i > 0 && starThresholds[i] <= starThresholds[i - 1]))
                throw new ArgumentException("Configure five increasing star thresholds between 0 and 100.");
    }

    // Satisfaction of one unit: 100 within the grace period, then a linear fall to unitFloor.
    public double Contribution(double wait)
    {
        if (!Finite(wait) || wait < 0) throw new ArgumentOutOfRangeException(nameof(wait));
        if (wait <= patienceGrace) return 100;
        double t = Math.Min(1, (wait - patienceGrace) / (patienceFloorAfter - patienceGrace));
        return 100 + (unitFloor - 100) * t;
    }

    // Final rating of a completed wave: weighted average over every unit's wait before service.
    public double Evaluate(WaveSnapshot snapshot)
    {
        Validate();
        if (snapshot == null || !snapshot.IsComplete || snapshot.Total == 0)
            throw new ArgumentException("Only a completed, nonempty wave can receive a rating.");
        foreach (var need in snapshot.Needs)
            if (!need.TracksWaitingTime || need.WaitsBeforeService.Count != need.Total)
                throw new ArgumentException("A completed wave must include every unit's waiting history.");

        Accumulate(snapshot, out double weighted, out double weights);
        return weighted / weights;
    }

    // Running satisfaction during a wave: served units (final wait) + units still waiting (wait so far).
    // Units that have not arrived yet are not counted. No customers yet = 100.
    public double EvaluateLive(WaveSnapshot partial)
    {
        Validate();
        if (partial == null) throw new ArgumentNullException(nameof(partial));
        Accumulate(partial, out double weighted, out double weights);
        return weights == 0 ? 100 : weighted / weights;
    }

    // Plain threshold lookup (0..5). TODO (other task): clamp the worst result to 1 star.
    public int Stars(double satisfaction)
    {
        Validate();
        if (!Finite(satisfaction) || satisfaction < 0 || satisfaction > 100)
            throw new ArgumentOutOfRangeException(nameof(satisfaction));
        int stars = 0;
        for (int i = 0; i < starThresholds.Length; i++)
            if (satisfaction >= starThresholds[i]) stars = i + 1;
        return stars;
    }

    private void Accumulate(WaveSnapshot snapshot, out double weighted, out double weights)
    {
        weighted = 0; weights = 0;
        foreach (var need in snapshot.Needs)
        {
            if (!need.TracksWaitingTime) continue;
            double weight = need.Type == NeedType.OceanView ? oceanViewWeight : 1;
            foreach (double wait in need.WaitsBeforeService)
            {
                weighted += Contribution(wait) * weight;
                weights += weight;
            }
            foreach (double wait in need.CurrentWaits)
            {
                weighted += Contribution(wait) * weight;
                weights += weight;
            }
        }
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

[Serializable]
public sealed class WaveRewardRules
{
    // TODO (Energy task): energyByStars table and the Energy amount.
    // TODO (Materials task): materials roll for 4+ stars.
    public void Validate() { }

    public WaveResult Create(WaveSnapshot snapshot, WaveRatingRules rating)
    {
        Validate();
        if (rating == null) throw new ArgumentNullException(nameof(rating));
        double satisfaction = rating.Evaluate(snapshot);
        int stars = rating.Stars(satisfaction);
        return new WaveResult(snapshot.Number, snapshot.Customers, satisfaction, stars, 0, 0);
    }
}

[Serializable]
public sealed class WaveResult
{
    public int WaveNumber { get; }
    public int Customers { get; }
    public double Satisfaction { get; }
    public int Stars { get; }
    public int Energy { get; }     // filled by the Energy task
    public int Materials { get; }  // filled by the Materials task
    public bool Perfect => Satisfaction >= 100;
    internal WaveResult(int number, int customers, double satisfaction, int stars, int energy, int materials)
    {
        WaveNumber = number; Customers = customers; Satisfaction = satisfaction;
        Stars = stars; Energy = energy; Materials = materials;
    }
}