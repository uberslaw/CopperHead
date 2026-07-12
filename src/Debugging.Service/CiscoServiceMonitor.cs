using System.ServiceProcess;
using Debugging.Shared;

namespace Debugging.Service;

public sealed class CiscoServiceMonitor
{
    private readonly Dictionary<string, WatchedServiceState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IntervalAnalyzer> _analyzers = new(StringComparer.OrdinalIgnoreCase);

    public CiscoServiceMonitor()
    {
        foreach (var target in ServiceTarget.CiscoTargets)
        {
            _states[target.ServiceName] = new WatchedServiceState(target);
            _analyzers[target.ServiceName] = new IntervalAnalyzer();
        }
    }

    public async Task RunAsync(Func<bool> isEnabled, CancellationToken stoppingToken)
    {
        EventLogWriter.Append("Debugging monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!isEnabled())
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            var burstWindow = _analyzers.Values.Any(analyzer => analyzer.IsInBurstWindow());
            var delay = _analyzers.Values
                .Select(analyzer => analyzer.GetSleepDuration(burstWindow))
                .Min();

            foreach (var state in _states.Values)
            {
                CheckService(state, stoppingToken);
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private void CheckService(WatchedServiceState state, CancellationToken stoppingToken)
    {
        ServiceController? controller = null;

        try
        {
            controller = ServiceResolver.Resolve(state.Target);
            if (controller is null)
            {
                if (!state.MissingLogged)
                {
                    EventLogWriter.Append($"{state.Label}: service not installed on this machine.");
                    state.MissingLogged = true;
                }

                return;
            }

            state.MissingLogged = false;
            controller.Refresh();
            var status = controller.Status;

            if (status == ServiceControllerStatus.Running)
            {
                HandleRunningService(state, controller);
            }
            else if (status == ServiceControllerStatus.Stopped)
            {
                state.WasRunning = false;
            }
        }
        catch (Exception ex)
        {
            EventLogWriter.Append($"{state.Label}: check failed - {ex.Message}");
        }
        finally
        {
            controller?.Dispose();
        }
    }

    private void HandleRunningService(WatchedServiceState state, ServiceController controller)
    {
        var now = DateTime.UtcNow;
        var analyzer = _analyzers[state.Target.ServiceName];

        if (!state.WasRunning)
        {
            state.WasRunning = true;
            state.LastStartDetectedUtc = now;

            if (state.LastStoppedUtc is DateTime lastStopped)
            {
                var gap = now - lastStopped;
                analyzer.RecordRestartGap(gap);
                EventLogWriter.Append(
                    $"{state.Label}: startup detected. Gap since last stop: {IntervalAnalyzer.FormatDuration(gap.TotalSeconds)}. {analyzer.Summary()}");
            }
            else
            {
                EventLogWriter.Append($"{state.Label}: startup detected (first observation).");
            }

            analyzer.ShiftNextExpectedStartFrom(now);
        }

        try
        {
            if (controller.CanStop)
            {
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                state.LastStoppedUtc = DateTime.UtcNow;
                EventLogWriter.Append($"{state.Label}: stopped.");
            }
            else
            {
                EventLogWriter.Append($"{state.Label}: running but service does not allow stop requests.");
            }
        }
        catch (Exception ex)
        {
            EventLogWriter.Append($"{state.Label}: stop failed - {ex.Message}");
        }
    }

    private sealed class WatchedServiceState(ServiceTarget target)
    {
        public ServiceTarget Target { get; } = target;
        public string Label => Target.DisplayNameContains;
        public bool WasRunning { get; set; }
        public bool MissingLogged { get; set; }
        public DateTime? LastStoppedUtc { get; set; }
        public DateTime? LastStartDetectedUtc { get; set; }
    }
}
