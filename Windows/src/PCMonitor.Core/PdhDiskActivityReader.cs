using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PCMonitor.Core;

[SupportedOSPlatform("windows")]
internal sealed class PdhDiskActivityReader : IDisposable
{
    private const uint PdhFormatDouble = 0x00000200;
    private readonly IntPtr _query;
    private readonly IntPtr _readCounter;
    private readonly IntPtr _writeCounter;

    public PdhDiskActivityReader()
    {
        if (PdhOpenQueryW(IntPtr.Zero, UIntPtr.Zero, out var query) != 0)
        {
            _query = IntPtr.Zero;
            _readCounter = IntPtr.Zero;
            _writeCounter = IntPtr.Zero;
            return;
        }

        _query = query;
        if (PdhAddEnglishCounterW(query, "\\PhysicalDisk(_Total)\\Disk Read Bytes/sec", IntPtr.Zero, out var readCounter) != 0)
        {
            readCounter = IntPtr.Zero;
        }
        _readCounter = readCounter;

        if (PdhAddEnglishCounterW(query, "\\PhysicalDisk(_Total)\\Disk Write Bytes/sec", IntPtr.Zero, out var writeCounter) != 0)
        {
            writeCounter = IntPtr.Zero;
        }
        _writeCounter = writeCounter;
    }

    public (double? ReadBytesPerSecond, double? WriteBytesPerSecond) Read()
    {
        if (_query == IntPtr.Zero || PdhCollectQueryData(_query) != 0)
        {
            return (null, null);
        }

        return (ReadCounter(_readCounter), ReadCounter(_writeCounter));
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
        }
    }

    private static double? ReadCounter(IntPtr counter)
    {
        if (counter == IntPtr.Zero ||
            PdhGetFormattedCounterValue(counter, PdhFormatDouble, out _, out var value) != 0 ||
            value.Status is not (0 or 1))
        {
            return null;
        }

        return double.IsFinite(value.Value.DoubleValue)
            ? Math.Max(0, value.Value.DoubleValue)
            : null;
    }

    [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(IntPtr dataSource, UIntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll", EntryPoint = "PdhCollectQueryData")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterValue")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint counterType, out PdhCounterValue value);

    [DllImport("pdh.dll", EntryPoint = "PdhCloseQuery")]
    private static extern uint PdhCloseQuery(IntPtr query);

    [StructLayout(LayoutKind.Explicit)]
    private struct CounterValueUnion
    {
        [FieldOffset(0)] public long LongValue;
        [FieldOffset(0)] public double DoubleValue;
        [FieldOffset(0)] public IntPtr StringValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhCounterValue
    {
        public uint Status;
        public CounterValueUnion Value;
    }
}
