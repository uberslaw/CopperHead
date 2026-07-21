using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace CopperHead;

public sealed class AppConfig
{
    public List<string> Hostnames { get; set; } = new();
    public string? AdapterName { get; set; }
    public string? Gateway { get; set; }
    public int RefreshSeconds { get; set; } = 30;
    public string? LastTraceTarget { get; set; }
    public string WatchProcesses { get; set; } = "Cursor";
    public string? HostListUrl { get; set; }
    public bool AutoAddDiscoveries { get; set; }
    /// <summary>Null = default on (needed for corp proxies). Explicit true/false from UI.</summary>
    public bool? IncludePrivateRemotes { get; set; }
    public int DiscoverSeconds { get; set; } = 15;
    public List<string> PinnedTrafficKeys { get; set; } = new();
    public int TrafficSortColumn { get; set; } = 10; // All time TX default (desc)
    public bool TrafficSortAsc { get; set; }
    /// <summary>0 = pre Country/ASN columns; 1 = current Traffic column layout.</summary>
    public int TrafficColumnSchema { get; set; }

    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig LoadOrDefault(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
        {
            return new AppConfig
            {
                Hostnames =
                [
                    "api2.cursor.sh",
                    "api5.cursor.sh",
                ],
                RefreshSeconds = 30,
                LastTraceTarget = "api2.cursor.sh",
                WatchProcesses = "Cursor",
                HostListUrl = "https://raw.githubusercontent.com/uberslaw/CopperHead/master/docs/hosts-cursor.txt",
                DiscoverSeconds = 15,
            };
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}

public sealed class HostRouteService
{
    private readonly RouteManager _routes = new();
    private readonly Dictionary<string, HashSet<string>> _hostToIps = new(StringComparer.OrdinalIgnoreCase);

    public RouteManager Routes => _routes;

    public event Action<string>? Log;

    public void WriteLog(string message) =>
        Log?.Invoke($"{DateTime.Now:HH:mm:ss}  {message}");

    public IReadOnlyCollection<string> ManagedDestinations => _routes.ManagedDestinations;

    /// <summary>Last successful host → IPv4 set from <see cref="RefreshAsync"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> HostToIps
    {
        get
        {
            return _hostToIps.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyCollection<string>)kv.Value.ToList(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task RefreshAsync(
        IEnumerable<string> hostnames,
        NetworkAdapterChoice adapter,
        CancellationToken cancellationToken)
    {
        var desiredHosts = hostnames
            .Select(h => h.Trim())
            .Where(h => h.Length > 0 && !h.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var desiredIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in desiredHosts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ips = await ResolveIpv4Async(host, cancellationToken).ConfigureAwait(false);
            _hostToIps[host] = ips.Select(i => i.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (ips.Count == 0)
            {
                Write($"WARN  {host}: no A records");
                continue;
            }

            Write($"DNS   {host} -> {string.Join(", ", ips)}");
            foreach (var ip in ips)
            {
                desiredIps.Add(ip.ToString());
                _routes.EnsureHostRoute(ip, adapter.Gateway, adapter.InterfaceIndex);
                Write($"ROUTE {ip}/32 via {adapter.Gateway} IF {adapter.InterfaceIndex} ({adapter.Name})");
            }
        }

        foreach (var existing in _routes.ManagedDestinations.ToList())
        {
            if (desiredIps.Contains(existing))
                continue;

            _routes.DeleteHostRoute(IPAddress.Parse(existing));
            Write($"DEL   {existing}/32 (stale)");
        }

        foreach (var key in _hostToIps.Keys.Except(desiredHosts, StringComparer.OrdinalIgnoreCase).ToList())
            _hostToIps.Remove(key);
    }

    public void StopAndClear()
    {
        _routes.ClearManagedRoutes();
        _hostToIps.Clear();
        Write("Cleared managed routes.");
    }

    public static async Task<List<IPAddress>> ResolveIpv4Async(string hostname, CancellationToken ct)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostname, ct).ConfigureAwait(false);
            return addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Distinct()
                .ToList();
        }
        catch (SocketException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    private void Write(string message) => WriteLog(message);
}
