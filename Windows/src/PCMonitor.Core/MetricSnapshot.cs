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

public sealed record MetricSnapshot(
    DateTimeOffset CapturedAt,
    double? CpuUsagePercent,
    ulong? TotalPhysicalBytes,
    ulong? AvailablePhysicalBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    IReadOnlyList<DiskVolumeSnapshot> Disks)
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
    double UploadBytesPerSecond);
