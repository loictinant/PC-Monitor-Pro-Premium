using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PCMonitor.Core;

[SupportedOSPlatform("windows")]
public sealed class WindowsMetricsCollector : IMetricsCollector, IDisposable
{
    private readonly Dictionary<string, NetworkCounterState> _previousNetworkById = new(StringComparer.OrdinalIgnoreCase);
    private readonly PdhDiskActivityReader _diskActivityReader = new();
    private readonly LibreHardwareMetricsReader _hardwareReader = new();
    private ulong? _previousIdle;
    private ulong? _previousKernel;
    private ulong? _previousUser;

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var capturedAt = DateTimeOffset.UtcNow;
        var cpu = ReadCpuUsage();
        var memory = ReadMemory();
        var network = ReadNetworkRates(capturedAt);
        var disks = ReadDisks();
        var diskActivity = _diskActivityReader.Read();
        var hardware = _hardwareReader.Read();

        return Task.FromResult(new MetricSnapshot(
            capturedAt,
            cpu,
            memory?.TotalBytes,
            memory?.AvailableBytes,
            network.DownloadBytesPerSecond,
            network.UploadBytesPerSecond,
            disks,
            network.Interfaces,
            diskActivity.ReadBytesPerSecond,
            diskActivity.WriteBytesPerSecond,
            hardware));
    }

    public void Dispose()
    {
        _diskActivityReader.Dispose();
        _hardwareReader.Dispose();
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

    private (double DownloadBytesPerSecond, double UploadBytesPerSecond, IReadOnlyList<NetworkInterfaceSnapshot> Interfaces)
        ReadNetworkRates(DateTimeOffset capturedAt)
    {
        var interfaces = new List<NetworkInterfaceSnapshot>();
        var currentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var downloadRate = 0d;
        var uploadRate = 0d;

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
                var received = unchecked((ulong)Math.Max(0, statistics.BytesReceived));
                var sent = unchecked((ulong)Math.Max(0, statistics.BytesSent));
                var id = networkInterface.Id;
                currentIds.Add(id);

                var interfaceDownload = 0d;
                var interfaceUpload = 0d;
                if (_previousNetworkById.TryGetValue(id, out var previous))
                {
                    var elapsedSeconds = (capturedAt - previous.CapturedAt).TotalSeconds;
                    if (elapsedSeconds > 0)
                    {
                        interfaceDownload = Delta(received, previous.ReceivedBytes) / elapsedSeconds;
                        interfaceUpload = Delta(sent, previous.SentBytes) / elapsedSeconds;
                    }
                }

                _previousNetworkById[id] = new NetworkCounterState(received, sent, capturedAt);
                downloadRate += interfaceDownload;
                uploadRate += interfaceUpload;
                interfaces.Add(new NetworkInterfaceSnapshot(
                    id,
                    string.IsNullOrWhiteSpace(networkInterface.Name) ? networkInterface.Description : networkInterface.Name,
                    interfaceDownload,
                    interfaceUpload));
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

        foreach (var staleId in _previousNetworkById.Keys.Where(id => !currentIds.Contains(id)).ToArray())
        {
            _previousNetworkById.Remove(staleId);
        }

        return (downloadRate, uploadRate, interfaces.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray());
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

    private sealed record NetworkCounterState(ulong ReceivedBytes, ulong SentBytes, DateTimeOffset CapturedAt);

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
