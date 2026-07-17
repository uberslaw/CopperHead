using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CopperHead;

/// <summary>
/// Runs Windows tracert with live line callbacks.
/// Forces the selected NIC by ensuring a /32 route for the target first,
/// then verifies the path with tracert (which follows the routing table).
/// </summary>
public sealed class TraceRouteRunner
{
    private readonly RouteManager _routes;

    public TraceRouteRunner(RouteManager routes)
    {
        _routes = routes;
    }

    public async Task RunAsync(
        string target,
        NetworkAdapterChoice adapter,
        Action<string> write,
        CancellationToken cancellationToken)
    {
        target = target.Trim();
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Enter a hostname or IP to trace.");

        write($"TRACE start  target={target}");
        write($"TRACE nic    {adapter.Name}  local={adapter.LocalAddress}  gateway={adapter.Gateway}  IF={adapter.InterfaceIndex}");

        IPAddress destIp;
        if (IPAddress.TryParse(target, out var parsed) &&
            parsed.AddressFamily == AddressFamily.InterNetwork)
        {
            destIp = parsed;
        }
        else
        {
            var addrs = await Dns.GetHostAddressesAsync(target, cancellationToken).ConfigureAwait(false);
            destIp = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                     ?? throw new InvalidOperationException($"No IPv4 address for {target}");
            write($"TRACE dns    {target} -> {destIp}");
        }

        // Pin this destination to the chosen NIC so tracert follows it.
        _routes.EnsureHostRoute(destIp, adapter.Gateway, adapter.InterfaceIndex);
        write($"TRACE route  {destIp}/32 via {adapter.Gateway} IF {adapter.InterfaceIndex}");
        write($"TRACE note   First hop should be ~{adapter.Gateway} if tether is used.");
        write("----- tracert -----");

        var psi = new ProcessStartInfo
        {
            FileName = "tracert.exe",
            Arguments = $"-d -w 2000 -h 30 {destIp}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.Default,
            StandardErrorEncoding = Encoding.Default,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start tracert.exe");

        // Kill immediately when Stop is clicked (don't wait for WaitForExit).
        await using var killReg = cancellationToken.Register(() =>
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
        });

        var stdoutTask = Task.Run(() => DrainLines(proc.StandardOutput, write), CancellationToken.None);
        var stderrTask = Task.Run(() => DrainLines(proc.StandardError, line => write("ERR  " + line)), CancellationToken.None);

        try
        {
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }

            write("TRACE stopped.");
            throw;
        }
        finally
        {
            // Ensure readers finish after process death.
            try { await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false); }
            catch { /* ignore */ }
        }

        write($"----- done (exit {proc.ExitCode}) -----");
    }

    private static void DrainLines(StreamReader reader, Action<string> write)
    {
        try
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
                write(line);
        }
        catch
        {
            // process killed / stream closed
        }
    }
}
