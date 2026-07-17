using System.Diagnostics;
using System.Net;
using System.Text;

namespace CopperHead;

/// <summary>
/// Adds/removes host (/32) routes using the built-in Windows route.exe.
/// </summary>
public sealed class RouteManager
{
    private readonly object _gate = new();
    private readonly HashSet<string> _managed = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ManagedDestinations
    {
        get { lock (_gate) return _managed.ToList(); }
    }

    public void EnsureHostRoute(IPAddress destination, IPAddress gateway, int interfaceIndex, int metric = 1)
    {
        if (destination.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Only IPv4 host routes are supported.", nameof(destination));

        var dest = destination.ToString();
        DeleteHostRoute(destination, forget: false);

        var args = $"add {dest} mask 255.255.255.255 {gateway} METRIC {metric} IF {interfaceIndex}";
        RunRoute(args, treatAlreadyExistsAsSuccess: true);

        lock (_gate)
            _managed.Add(dest);
    }

    public void DeleteHostRoute(IPAddress destination, bool forget = true)
    {
        var dest = destination.ToString();
        RunRoute($"delete {dest}", treatAlreadyExistsAsSuccess: true);
        if (forget)
        {
            lock (_gate)
                _managed.Remove(dest);
        }
    }

    public void ClearManagedRoutes()
    {
        List<string> snapshot;
        lock (_gate)
            snapshot = _managed.ToList();

        foreach (var dest in snapshot)
            RunRoute($"delete {dest}", treatAlreadyExistsAsSuccess: true);

        lock (_gate)
            _managed.Clear();
    }

    private static void RunRoute(string arguments, bool treatAlreadyExistsAsSuccess)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "route.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.Default,
            StandardErrorEncoding = Encoding.Default,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start route.exe");

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(15_000);

        if (proc.ExitCode == 0)
            return;

        var combined = $"{stdout}\n{stderr}".Trim();
        if (treatAlreadyExistsAsSuccess &&
            (combined.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
             combined.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
             combined.Contains("The route deletion failed", StringComparison.OrdinalIgnoreCase) ||
             combined.Contains("element not found", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"route.exe {arguments} failed (exit {proc.ExitCode}): {combined}");
    }
}
