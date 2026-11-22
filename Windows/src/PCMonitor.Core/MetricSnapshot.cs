namespace PCMonitor.Core;

public sealed record DiskVolumeSnapshot(
    string Name,
    string Label,
    ulong TotalBytes,
    ulong FreeBytes)
{
    public ulong UsedBytes => TotalBytes >= FreeBytes ? TotalBytes - FreeBytes : 0;

    public double UsedPercent => TotalBytes == 0 ? 0 : UsedBytes * 100d / TotalBytes;
}

public sealed record NetworkInterfaceSnapshot(
    string Id,
    string Name,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond);

public sealed record HardwareMetricsSnapshot(
    string? GpuName,
    double? GpuLoadPercent,
    ulong? GpuMemoryUsedBytes,
    ulong? GpuMemoryTotalBytes,
    double? GpuTemperatureCelsius,
    double? CpuTemperatureCelsius,
    double? FanSpeedRpm)
{
    public static HardwareMetricsSnapshot Empty { get; } = new(null, null, null, null, null, null, null);
}

public sealed record MetricSnapshot(
    DateTimeOffset CapturedAt,
    double? CpuUsagePercent,
    ulong? TotalPhysicalBytes,
    ulong? AvailablePhysicalBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    IReadOnlyList<DiskVolumeSnapshot> Disks,
    IReadOnlyList<NetworkInterfaceSnapshot> NetworkInterfaces,
    double? DiskReadBytesPerSecond,
    double? DiskWriteBytesPerSecond,
    HardwareMetricsSnapshot Hardware)
{
    public ulong? UsedPhysicalBytes => TotalPhysicalBytes is ulong total && AvailablePhysicalBytes is ulong available
        ? total >= available ? total - available : 0
        : null;

    public double? MemoryUsagePercent => TotalPhysicalBytes is ulong total && total > 0 && UsedPhysicalBytes is ulong used
        ? used * 100d / total
        : null;
}

public sealed record MetricSample(
    DateTimeOffset CapturedAt,
    double? CpuUsagePercent,
    double? MemoryUsagePercent,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    double? DiskReadBytesPerSecond = null,
    double? DiskWriteBytesPerSecond = null,
    double? GpuLoadPercent = null,
    ulong? GpuMemoryUsedBytes = null,
    ulong? GpuMemoryTotalBytes = null,
    double? GpuTemperatureCelsius = null,
    double? CpuTemperatureCelsius = null,
    IReadOnlyList<NetworkInterfaceSnapshot>? NetworkInterfaces = null,
    double? FanSpeedRpm = null);
