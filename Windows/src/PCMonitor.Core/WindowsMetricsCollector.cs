using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PCMonitor.Core;

[SupportedOSPlatform("windows")]
public sealed class WindowsMetricsCollector : IMetricsCollector
{
    private ulong? _previousIdle;
    private ulong? _previousKernel;
    private ulong? _previousUser;
    private ulong? _previousReceived;
    private ulong? _previousSent;
    private DateTimeOffset? _previousNetworkTime;

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var capturedAt = DateTimeOffset.UtcNow;
        var cpu = ReadCpuUsage();
        var memory = ReadMemory();
        var network = ReadNetworkRates(capturedAt);
        var disks = ReadDisks();

        return Task.FromResult(new MetricSnapshot(
            capturedAt,
            cpu,
            memory?.TotalBytes,
            memory?.AvailableBytes,
            network.DownloadBytesPerSecond,
            network.UploadBytesPerSecond,
            disks));
    }

    private double? ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        var idleValue = idle.ToUInt64();
        var kernelValue = kernel.ToUInt64();
        var userValue = user.ToUInt64();
        double? usage = null;

        if (_previousIdle is ulong previousIdle &&
            _previousKernel is ulong previousKernel &&
            _previousUser is ulong previousUser)
        {
            var idleDelta = Delta(idleValue, previousIdle);
            var totalDelta = Delta(kernelValue, previousKernel) + Delta(userValue, previousUser);
            var busyDelta = Math.Max(0, totalDelta - idleDelta);
            usage = totalDelta == 0 ? 0 : Math.Clamp(busyDelta * 100d / totalDelta, 0, 100);
        }

        _previousIdle = idleValue;
        _previousKernel = kernelValue;
        _previousUser = userValue;
        return usage;
    }

    private static (ulong TotalBytes, ulong AvailableBytes)? ReadMemory()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        return GlobalMemoryStatusEx(ref status)
            ? (status.TotalPhysical, status.AvailablePhysical)
            : null;
    }

    private (double DownloadBytesPerSecond, double UploadBytesPerSecond) ReadNetworkRates(DateTimeOffset capturedAt)
    {
        ulong received = 0;
        ulong sent = 0;

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                var statistics = networkInterface.GetIPv4Statistics();
                received += unchecked((ulong)Math.Max(0, statistics.BytesReceived));
                sent += unchecked((ulong)Math.Max(0, statistics.BytesSent));
            }
            catch (NetworkInformationException)
            {
                // An interface can disappear while a snapshot is being collected.
            }
            catch (PlatformNotSupportedException)
            {
                // Keep the remaining interfaces available on unusual network stacks.
            }
        }

        var downloadRate = 0d;
        var uploadRate = 0d;

        if (_previousReceived is ulong previousReceived &&
            _previousSent is ulong previousSent &&
            _previousNetworkTime is DateTimeOffset previousTime)
        {
            var elapsedSeconds = (capturedAt - previousTime).TotalSeconds;
            if (elapsedSeconds > 0)
            {
                downloadRate = Delta(received, previousReceived) / elapsedSeconds;
                uploadRate = Delta(sent, previousSent) / elapsedSeconds;
            }
        }

        _previousReceived = received;
        _previousSent = sent;
        _previousNetworkTime = capturedAt;
        return (downloadRate, uploadRate);
    }

    private static IReadOnlyList<DiskVolumeSnapshot> ReadDisks()
    {
        var volumes = new List<DiskVolumeSnapshot>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                volumes.Add(new DiskVolumeSnapshot(
                    drive.Name.TrimEnd('\\'),
                    string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Disque local" : drive.VolumeLabel,
                    unchecked((ulong)Math.Max(0, drive.TotalSize)),
                    unchecked((ulong)Math.Max(0, drive.AvailableFreeSpace))));
            }
            catch (IOException)
            {
                // Ignore a drive that becomes unavailable during enumeration.
            }
            catch (UnauthorizedAccessException)
            {
                // Drive enumeration must not stop CPU, memory or network metrics.
            }
        }

        return volumes;
    }

    private static ulong Delta(ulong current, ulong previous) => current >= previous ? current - previous : 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public readonly ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
