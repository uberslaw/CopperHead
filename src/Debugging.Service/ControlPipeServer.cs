using System.IO.Pipes;
using System.Text;
using Debugging.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Debugging.Service;

public sealed class ControlPipeServer : BackgroundService
{
    private readonly ILogger<ControlPipeServer> _logger;

    public ControlPipeServer(ILogger<ControlPipeServer> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(Paths.DataRoot);

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                Paths.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
                await HandleClientAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pipe server error.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private static async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        var command = await reader.ReadLineAsync(stoppingToken);
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        switch (command.Trim().ToUpperInvariant())
        {
            case PipeMessages.Enable:
                UpdateState(enabled: true, toggledBy: "tray");
                writer.WriteLine("OK|enabled");
                break;

            case PipeMessages.Disable:
                UpdateState(enabled: false, toggledBy: "tray");
                writer.WriteLine("OK|disabled");
                break;

            case PipeMessages.GetState:
                var state = AppState.Load();
                writer.WriteLine(
                    $"STATE|{(state.MonitoringEnabled ? "enabled" : "disabled")}|{state.LastToggleUtc:O}|{state.ToggledBy}");
                break;

            case PipeMessages.RefreshLog:
                writer.Write(EventLogWriter.ReadAll());
                break;

            default:
                writer.WriteLine("ERR|unknown command");
                break;
        }
    }

    private static void UpdateState(bool enabled, string toggledBy)
    {
        var state = AppState.Load();
        state.MonitoringEnabled = enabled;
        state.LastToggleUtc = DateTime.UtcNow;
        state.ToggledBy = toggledBy;
        state.Save();

        EventLogWriter.Append(enabled
            ? "Monitoring enabled from tray."
            : "Monitoring disabled from tray.");
    }
}
