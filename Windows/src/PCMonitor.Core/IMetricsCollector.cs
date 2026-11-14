namespace PCMonitor.Core;

public interface IMetricsCollector
{
    Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default);
}
