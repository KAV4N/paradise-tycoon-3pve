// Unused backup: keeps FIFO waiting and service timing, but records no waiting-time statistics.
// WaveServices always uses ServiceQueue; this variant is not connected to gameplay.
public sealed class ServiceQueueWithoutWaitTracking : ServiceQueue
{
    protected override bool TrackWaitingTime => false;

    public ServiceQueueWithoutWaitTracking(NeedType type, int demand, int capacity,
        double serviceSeconds, double arrivalSeconds)
        : base(type, demand, capacity, serviceSeconds, arrivalSeconds)
    {
    }
}
