using LibreHardwareMonitor.Hardware;
using System.Runtime.Versioning;

namespace PCMonitor.Core;

[SupportedOSPlatform("windows")]
internal sealed class LibreHardwareMetricsReader : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMotherboardEnabled = true
    };

    private bool _isOpen;
    private DateTimeOffset _retryAfter;

    public HardwareMetricsSnapshot Read()
    {
        if (!_isOpen && DateTimeOffset.UtcNow < _retryAfter)
        {
            return HardwareMetricsSnapshot.Empty;
        }

        try
        {
            if (!_isOpen)
            {
                _computer.Open();
                _isOpen = true;
            }

            var hardware = _computer.Hardware.SelectMany(EnumerateHardware).ToArray();
            foreach (var device in hardware)
            {
                device.Update();
            }

            var gpu = hardware.FirstOrDefault(device => device.HardwareType.ToString().StartsWith("Gpu", StringComparison.OrdinalIgnoreCase));
            var gpuSensors = gpu?.Sensors.Where(sensor => sensor.Value.HasValue).ToArray() ?? [];
            var cpuSensors = hardware
                .Where(device => string.Equals(device.HardwareType.ToString(), "Cpu", StringComparison.OrdinalIgnoreCase))
                .SelectMany(device => device.Sensors)
                .Where(sensor => sensor.Value.HasValue)
                .ToArray();

            var gpuLoad = ChooseByName(gpuSensors.Where(sensor => sensor.SensorType == SensorType.Load), "core", "gpu")?.Value;
            var gpuTemperature = ChooseByName(gpuSensors.Where(sensor => sensor.SensorType == SensorType.Temperature), "core", "gpu")?.Value;
            var cpuTemperature = ChooseByName(cpuSensors.Where(sensor => sensor.SensorType == SensorType.Temperature), "package", "core average")?.Value;
            var memoryUsedSensor = FindMemorySensor(gpuSensors, "used");
            var memoryTotalSensor = FindMemorySensor(gpuSensors, "total");
            var fanSpeed = hardware
                .Where(device => device.HardwareType.ToString().Contains("Motherboard", StringComparison.OrdinalIgnoreCase))
                .SelectMany(device => device.Sensors)
                .FirstOrDefault(sensor => sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)?.Value;

            return new HardwareMetricsSnapshot(
                gpu?.Name,
                NormalizePercent(gpuLoad),
                ToBytes(memoryUsedSensor),
                ToBytes(memoryTotalSensor),
                Positive(gpuTemperature),
                Positive(cpuTemperature),
                Positive(fanSpeed));
        }
        catch (Exception)
        {
            if (!_isOpen)
            {
                _retryAfter = DateTimeOffset.UtcNow.AddMinutes(2);
            }

            return HardwareMetricsSnapshot.Empty;
        }
    }

    public void Dispose()
    {
        if (_isOpen)
        {
            try
            {
                _computer.Close();
            }
            catch (Exception)
            {
                // Sensor cleanup is best effort during shutdown.
            }

            _isOpen = false;
        }
    }

    private static IEnumerable<IHardware> EnumerateHardware(IHardware hardware)
    {
        yield return hardware;
        foreach (var child in hardware.SubHardware)
        {
            foreach (var nested in EnumerateHardware(child))
            {
                yield return nested;
            }
        }
    }

    private static ISensor? ChooseByName(IEnumerable<ISensor> sensors, params string[] preferredWords)
    {
        var values = sensors.ToArray();
        foreach (var word in preferredWords)
        {
            var match = values.FirstOrDefault(sensor => sensor.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return values.FirstOrDefault();
    }

    private static ISensor? FindMemorySensor(IEnumerable<ISensor> sensors, string use)
    {
        return sensors.FirstOrDefault(sensor =>
            sensor.Name.Contains("memory", StringComparison.OrdinalIgnoreCase) &&
            sensor.Name.Contains(use, StringComparison.OrdinalIgnoreCase) &&
            sensor.SensorType.ToString() is "Data" or "SmallData");
    }

    private static ulong? ToBytes(ISensor? sensor)
    {
        if (sensor?.Value is not float value || !float.IsFinite(value) || value < 0)
        {
            return null;
        }

        var multiplier = sensor.SensorType.ToString() == "SmallData" ? 1024d * 1024d : 1024d * 1024d * 1024d;
        return (ulong)Math.Clamp(value * multiplier, 0, ulong.MaxValue);
    }

    private static double? NormalizePercent(float? value) =>
        value is float percent && float.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : null;

    private static double? Positive(float? value) =>
        value is float number && float.IsFinite(number) && number >= 0 ? number : null;
}
