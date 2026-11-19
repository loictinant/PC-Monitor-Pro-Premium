using PCMonitor.Core;
using System.Text.Json;

namespace PCMonitor.App;

internal sealed class PersistentMetricsHistory
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly TimeSpan MinimumSampleInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _historyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCMonitorPro",
        "history.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<MetricSample> _samples = [];
    private DateTimeOffset? _lastPersistedAt;
    private bool _isLoaded;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<MetricSample> GetLatest(TimeSpan range)
    {
        var cutoff = DateTimeOffset.UtcNow - range;
        return _samples.Where(sample => sample.CapturedAt >= cutoff).ToArray();
    }

    public async Task RecordAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadCoreAsync(cancellationToken);
            var sample = MetricsHistory.ToSample(snapshot);
            var latest = _samples.LastOrDefault();
            if (latest is not null && snapshot.CapturedAt - latest.CapturedAt < MinimumSampleInterval)
            {
                return;
            }

            _samples.Add(sample);
            Trim(snapshot.CapturedAt - Retention);

            if (_lastPersistedAt is null || snapshot.CapturedAt - _lastPersistedAt >= SaveInterval)
            {
                await SaveCoreAsync(cancellationToken);
                _lastPersistedAt = snapshot.CapturedAt;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadCoreAsync(cancellationToken);
            await SaveCoreAsync(cancellationToken);
            _lastPersistedAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = await File.ReadAllTextAsync(_historyPath, cancellationToken);
                var saved = JsonSerializer.Deserialize<List<MetricSample>>(json, JsonOptions);
                if (saved is not null)
                {
                    _samples.AddRange(saved.OrderBy(sample => sample.CapturedAt));
                }
            }
        }
        catch (IOException)
        {
            // Continue with an empty local history if the saved file is unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // History is optional; live monitoring continues without saved data.
        }
        catch (JsonException)
        {
            // Ignore damaged local history instead of preventing startup.
        }

        Trim(DateTimeOffset.UtcNow - Retention);
    }

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_historyPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(_samples, JsonOptions);
                await File.WriteAllTextAsync(_historyPath, json, cancellationToken);
            }
        }
        catch (IOException)
        {
            // A failed save must not interrupt live monitoring.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed save must not interrupt live monitoring.
        }
    }

    private void Trim(DateTimeOffset cutoff) => _samples.RemoveAll(sample => sample.CapturedAt < cutoff);
}
