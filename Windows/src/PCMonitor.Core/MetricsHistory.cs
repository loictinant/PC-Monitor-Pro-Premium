namespace PCMonitor.Core;

public sealed class MetricsHistory
{
    private readonly Queue<MetricSample> _samples = new();

    public int Count => _samples.Count;

    public void Add(MetricSnapshot snapshot, int maximumSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSamples);

        _samples.Enqueue(new MetricSample(
            snapshot.CapturedAt,
            snapshot.CpuUsagePercent,
            snapshot.MemoryUsagePercent,
            snapshot.DownloadBytesPerSecond,
            snapshot.UploadBytesPerSecond));

        while (_samples.Count > maximumSamples)
        {
            _samples.Dequeue();
        }
    }

    public IReadOnlyList<MetricSample> GetLatest(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return _samples.Skip(Math.Max(0, _samples.Count - count)).ToArray();
    }
}
