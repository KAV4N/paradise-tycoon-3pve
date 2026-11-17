using System;
using System.Collections.Generic;
using UnityEngine;

public enum NeedStatus
{
    OK,
    BOTTLENECK,
    RECOVERING,
    SATISFIED
}

// Standalone observer. Nothing subscribes to gameplay or updates UI automatically.
// A future adapter must call Evaluate with active-wave snapshots and ResetForWave
// when restarting a wave with the same number and elapsed time.
[AddComponentMenu("Waves/Need Status Monitor")]
public sealed class NeedStatusMonitor : MonoBehaviour
{
    [SerializeField, Min(0)] private int bottleneckThreshold = 4;
    [SerializeField, Min(0.1f)] private float bottleneckHysteresisSeconds = 3f;

    private sealed class Observation
    {
        public NeedStatus Status;
        public int Waiting;
        public bool Measuring;
        public double WindowStart;
        public int WindowWaiting;
    }

    private readonly Dictionary<NeedType, Observation> observations = new Dictionary<NeedType, Observation>();
    private int waveNumber = -1;
    private double lastElapsed = -1;

    // Raised only on a transition. Initial OK states and resets are silent.
    public event Action<NeedType, NeedStatus> StatusChanged;

    public bool TryGetStatus(NeedType need, out NeedStatus status)
    {
        if (observations.TryGetValue(need, out var observation))
        {
            status = observation.Status;
            return true;
        }
        status = NeedStatus.OK;
        return false; // Absent/locked needs have no monitored state.
    }

    public void ResetForWave()
    {
        observations.Clear();
        waveNumber = -1;
        lastElapsed = -1;
    }

    public void Evaluate(WaveSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Number != waveNumber || snapshot.Elapsed < lastElapsed)
        {
            ResetForWave();
            waveNumber = snapshot.Number;
        }
        // No wall-clock timers: repeated snapshots during pause cannot advance a trend.
        if (snapshot.Elapsed == lastElapsed) return;
        lastElapsed = snapshot.Elapsed;

        foreach (var need in snapshot.Needs)
        {
            if (!observations.TryGetValue(need.Type, out var observation))
            {
                observation = new Observation();
                observations.Add(need.Type, observation);
            }
            observation.Waiting = need.Waiting;

            if (snapshot.ArrivalsClosed && need.Waiting == 0 && need.InService == 0 && need.Completed == need.Total)
            {
                observation.Measuring = false;
                Change(need.Type, observation, NeedStatus.SATISFIED);
                continue;
            }
            if (need.Waiting <= Math.Max(0, bottleneckThreshold))
            {
                observation.Measuring = false;
                Change(need.Type, observation, NeedStatus.OK);
                continue;
            }

            if (!observation.Measuring)
            {
                StartWindow(observation, snapshot.Elapsed);
                continue;
            }
            if (snapshot.Elapsed - observation.WindowStart < Math.Max(0.1, bottleneckHysteresisSeconds))
                continue;

            // Compare net queue movement over a full window, not individual frames.
            // Flat frames and single seat completions must not make the warning flicker.
            int change = need.Waiting - observation.WindowWaiting;
            StartWindow(observation, snapshot.Elapsed);
            if (change > 0)
                Change(need.Type, observation, NeedStatus.BOTTLENECK);
            else if (change < 0 && observation.Status == NeedStatus.BOTTLENECK)
                Change(need.Type, observation, NeedStatus.RECOVERING);
        }
    }

    // Optional future hook AFTER a successful player intervention. Rates are units/s,
    // using the current service capacity and speed; arrivalRate is zero during drain.
    // A speed increase alone is insufficient: the new throughput must exceed arrivals.
    public bool NotifyServiceImproved(NeedType need, double previousFillRate,
        double currentFillRate, double arrivalRate)
    {
        ValidateRate(previousFillRate);
        ValidateRate(currentFillRate);
        ValidateRate(arrivalRate);
        if (!observations.TryGetValue(need, out var observation) ||
            observation.Status != NeedStatus.BOTTLENECK ||
            observation.Waiting <= Math.Max(0, bottleneckThreshold) ||
            currentFillRate <= previousFillRate || currentFillRate <= arrivalRate)
            return false;

        StartWindow(observation, lastElapsed);
        Change(need, observation, NeedStatus.RECOVERING);
        return true;
    }

    private static void ValidateRate(double rate)
    {
        if (rate < 0 || double.IsNaN(rate) || double.IsInfinity(rate))
            throw new ArgumentOutOfRangeException(nameof(rate));
    }

    private static void StartWindow(Observation observation, double elapsed)
    {
        observation.Measuring = true;
        observation.WindowStart = elapsed;
        observation.WindowWaiting = observation.Waiting;
    }

    private void Change(NeedType need, Observation observation, NeedStatus next)
    {
        if (observation.Status == next) return;
        observation.Status = next;
        StatusChanged?.Invoke(need, next);
    }
}
