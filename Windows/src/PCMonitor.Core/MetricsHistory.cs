namespace PCMonitor.Core;

public sealed class MetricsHistory
{
    private readonly Queue<MetricSample> _samples = new();

    public int Count => _samples.Count;

    public void Add(MetricSnapshot snapshot, int maximumSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSamples);

        _samples.Enqueue(ToSample(snapshot));
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

    public IReadOnlyList<MetricSample> GetSince(DateTimeOffset timestamp) =>
        _samples.Where(sample => sample.CapturedAt >= timestamp).ToArray();

    public static MetricSample ToSample(MetricSnapshot snapshot) => new(
        snapshot.CapturedAt,
        snapshot.CpuUsagePercent,
        snapshot.MemoryUsagePercent,
        snapshot.DownloadBytesPerSecond,
        snapshot.UploadBytesPerSecond,
        snapshot.DiskReadBytesPerSecond,
        snapshot.DiskWriteBytesPerSecond,
        snapshot.Hardware.GpuLoadPercent,
        snapshot.Hardware.GpuMemoryUsedBytes,
        snapshot.Hardware.GpuMemoryTotalBytes,
        snapshot.Hardware.GpuTemperatureCelsius,
        snapshot.Hardware.CpuTemperatureCelsius,
        snapshot.NetworkInterfaces,
        snapshot.Hardware.FanSpeedRpm);
}
