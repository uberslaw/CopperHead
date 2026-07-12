using Debugging.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Debugging.Service;

public sealed class MonitorWorker : BackgroundService
{
    private readonly ILogger<MonitorWorker> _logger;
    private readonly CiscoServiceMonitor _monitor = new();

    public MonitorWorker(ILogger<MonitorWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(Paths.DataRoot);
        _logger.LogInformation("Debugging monitor worker started.");

        await _monitor.RunAsync(() => AppState.Load().MonitoringEnabled, stoppingToken);
    }
}
