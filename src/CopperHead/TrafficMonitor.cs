using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CopperHead;

public sealed class TrafficRow
{
    public required string Key { get; init; } // ip:port
    public required string Ip { get; init; }
    public required int Port { get; init; }
    public string? Hostname { get; set; }
    public bool Pinned { get; set; }
    public double TxPerSec { get; set; }
    public double RxPerSec { get; set; }
    public ulong SessionTx { get; set; }
    public ulong SessionRx { get; set; }
    public ulong AllTimeTx { get; set; }
    public ulong AllTimeRx { get; set; }
    public bool Active { get; set; }
}

/// <summary>
/// Samples per-connection TCP byte counters (Windows ESTATS) for named processes
/// and aggregates by remote IP:port.
/// </summary>
public sealed class TrafficMonitor
{
    private readonly object _gate = new();
    private readonly Dictionary<string, EndpointAccum> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly TrafficStatsStore _store;
    private HashSet<string> _pinned;
    private DateTime _lastSampleUtc = DateTime.MinValue;

    public TrafficMonitor(TrafficStatsStore store, IEnumerable<string>? pinnedKeys = null)
    {
        _store = store;
        _pinned = new HashSet<string>(pinnedKeys ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> PinnedKeys
    {
        get { lock (_gate) return _pinned.ToList(); }
    }

    public void SetPinned(IEnumerable<string> keys)
    {
        lock (_gate)
            _pinned = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
    }

    public bool TogglePin(string key)
    {
        lock (_gate)
        {
            if (!_pinned.Add(key))
            {
                _pinned.Remove(key);
                return false;
            }
            return true;
        }
    }

    public void ResetSession()
    {
        lock (_gate)
        {
            foreach (var e in _endpoints.Values)
            {
                e.SessionTx = 0;
                e.SessionRx = 0;
            }
        }
    }

    public IReadOnlyList<TrafficRow> Sample(IEnumerable<string> processNames)
    {
        var wanted = processNames
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p[..^4] : p)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pids = new HashSet<int>();
        if (wanted.Count > 0)
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (wanted.Contains(proc.ProcessName))
                        pids.Add(proc.Id);
                }
                catch { /* ignore */ }
                finally { proc.Dispose(); }
            }
        }

        var dns = DnsCacheReader.GetIpToHostMap();
        var now = DateTime.UtcNow;
        double dt;
        lock (_gate)
        {
            dt = _lastSampleUtc == DateTime.MinValue
                ? 1.0
                : Math.Max(0.2, (now - _lastSampleUtc).TotalSeconds);
            _lastSampleUtc = now;
        }

        // Sum bytes across all sockets for each remote IP:port in this sample.
        var sampleTotals = new Dictionary<string, (string Ip, int Port, ulong Tx, ulong Rx)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in TcpTableReader.GetIpv4Rows())
        {
            if (!pids.Contains(row.ProcessId))
                continue;
            if (row.State != 5)
                continue;
            if (ConnectionDiscovery.IsPrivateOrLocal(row.RemoteAddress))
                continue;

            var ip = row.RemoteAddress.ToString();
            var key = $"{ip}:{row.RemotePort}";

            TcpEstats.TryEnable(row);
            if (!TcpEstats.TryReadBytes(row, out var tx, out var rx))
                continue;

            if (sampleTotals.TryGetValue(key, out var cur))
                sampleTotals[key] = (ip, row.RemotePort, cur.Tx + tx, cur.Rx + rx);
            else
                sampleTotals[key] = (ip, row.RemotePort, tx, rx);
        }

        lock (_gate)
        {
            foreach (var kv in sampleTotals)
            {
                var key = kv.Key;
                var (ip, port, tx, rx) = kv.Value;

                if (!_endpoints.TryGetValue(key, out var acc))
                {
                    acc = new EndpointAccum
                    {
                        Ip = ip,
                        Port = port,
                        LastTx = tx,
                        LastRx = rx,
                    };
                    var lifetime = _store.Get(key);
                    acc.AllTimeTx = lifetime.Tx;
                    acc.AllTimeRx = lifetime.Rx;
                    _endpoints[key] = acc;
                    // First sighting: establish baseline, no rate/session spike.
                    if (dns.TryGetValue(ip, out var host0))
                        acc.Hostname = host0;
                    acc.Active = true;
                    continue;
                }

                var dTx = tx >= acc.LastTx ? tx - acc.LastTx : 0;
                var dRx = rx >= acc.LastRx ? rx - acc.LastRx : 0;
                acc.LastTx = tx;
                acc.LastRx = rx;
                acc.SessionTx += dTx;
                acc.SessionRx += dRx;
                acc.AllTimeTx += dTx;
                acc.AllTimeRx += dRx;
                acc.TxPerSec = dTx / dt;
                acc.RxPerSec = dRx / dt;
                acc.Active = true;
                if (dns.TryGetValue(ip, out var host))
                    acc.Hostname = host;
                _store.Set(key, acc.AllTimeTx, acc.AllTimeRx);
            }

            foreach (var acc in _endpoints.Values)
            {
                var key = $"{acc.Ip}:{acc.Port}";
                if (!sampleTotals.ContainsKey(key))
                {
                    acc.Active = false;
                    acc.TxPerSec = 0;
                    acc.RxPerSec = 0;
                }
            }

            _store.Save();

            return _endpoints.Select(kv => new TrafficRow
            {
                Key = kv.Key,
                Ip = kv.Value.Ip,
                Port = kv.Value.Port,
                Hostname = kv.Value.Hostname,
                Pinned = _pinned.Contains(kv.Key),
                TxPerSec = kv.Value.TxPerSec,
                RxPerSec = kv.Value.RxPerSec,
                SessionTx = kv.Value.SessionTx,
                SessionRx = kv.Value.SessionRx,
                AllTimeTx = kv.Value.AllTimeTx,
                AllTimeRx = kv.Value.AllTimeRx,
                Active = kv.Value.Active,
            }).ToList();
        }
    }

    private sealed class EndpointAccum
    {
        public required string Ip { get; init; }
        public required int Port { get; init; }
        public string? Hostname { get; set; }
        public ulong LastTx { get; set; }
        public ulong LastRx { get; set; }
        public ulong SessionTx { get; set; }
        public ulong SessionRx { get; set; }
        public ulong AllTimeTx { get; set; }
        public ulong AllTimeRx { get; set; }
        public double TxPerSec { get; set; }
        public double RxPerSec { get; set; }
        public bool Active { get; set; }
    }
}

public sealed class TrafficStatsStore
{
    private readonly string _path;
    private readonly Dictionary<string, Lifetime> _data = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;

    public TrafficStatsStore(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "traffic-stats.json");
        Load();
    }

    public (ulong Tx, ulong Rx) Get(string key) =>
        _data.TryGetValue(key, out var v) ? (v.Tx, v.Rx) : (0, 0);

    public void Set(string key, ulong tx, ulong rx)
    {
        _data[key] = new Lifetime { Tx = tx, Rx = rx };
        _dirty = true;
    }

    public void Save()
    {
        if (!_dirty) return;
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
        _dirty = false;
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<Dictionary<string, Lifetime>>(json);
            if (data is null) return;
            foreach (var kv in data)
                _data[kv.Key] = kv.Value;
        }
        catch
        {
            // ignore corrupt file
        }
    }

    private sealed class Lifetime
    {
        public ulong Tx { get; set; }
        public ulong Rx { get; set; }
    }
}

internal static class TcpEstats
{
    private const int TcpConnectionEstatsData = 1;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint SetPerTcpConnectionEStats(
        ref MibTcpRow row,
        int estatsType,
        IntPtr rw,
        uint rwVersion,
        uint rwSize,
        uint offset);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetPerTcpConnectionEStats(
        ref MibTcpRow row,
        int estatsType,
        IntPtr rw,
        uint rwVersion,
        uint rwSize,
        IntPtr ros,
        uint rosVersion,
        uint rosSize,
        IntPtr rod,
        uint rodVersion,
        uint rodSize);

    public static void TryEnable(TcpTableReader.Row row)
    {
        var tcpRow = ToMib(row);
        var rw = new TcpEstatsDataRw { EnableCollection = 1 };
        var rwPtr = Marshal.AllocHGlobal(Marshal.SizeOf<TcpEstatsDataRw>());
        try
        {
            Marshal.StructureToPtr(rw, rwPtr, false);
            SetPerTcpConnectionEStats(ref tcpRow, TcpConnectionEstatsData, rwPtr, 0,
                (uint)Marshal.SizeOf<TcpEstatsDataRw>(), 0);
        }
        catch
        {
            // ignore
        }
        finally
        {
            Marshal.FreeHGlobal(rwPtr);
        }
    }

    public static bool TryReadBytes(TcpTableReader.Row row, out ulong tx, out ulong rx)
    {
        tx = 0;
        rx = 0;
        var tcpRow = ToMib(row);
        var rodSize = Marshal.SizeOf<TcpEstatsDataRod>();
        var rodPtr = Marshal.AllocHGlobal(rodSize);
        try
        {
            var ret = GetPerTcpConnectionEStats(
                ref tcpRow,
                TcpConnectionEstatsData,
                IntPtr.Zero, 0, 0,
                IntPtr.Zero, 0, 0,
                rodPtr, 0, (uint)rodSize);
            if (ret != 0)
                return false;

            var rod = Marshal.PtrToStructure<TcpEstatsDataRod>(rodPtr);
            tx = rod.DataBytesOut;
            rx = rod.DataBytesIn;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(rodPtr);
        }
    }

    private static MibTcpRow ToMib(TcpTableReader.Row row) => new()
    {
        state = (uint)row.State,
        localAddr = row.LocalAddrRaw,
        localPort = row.LocalPortRaw,
        remoteAddr = row.RemoteAddrRaw,
        remotePort = row.RemotePortRaw,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRow
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpEstatsDataRw
    {
        public byte EnableCollection;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpEstatsDataRod
    {
        public ulong DataBytesOut;
        public ulong DataBytesIn;
        public ulong DataBytesOutTotal;
        public ulong DataBytesInTotal;
        public ulong SegsOut;
        public ulong SegsIn;
        public uint SoftErrors;
        public uint SoftErrorReason;
        public uint SndUna;
        public uint SndNxt;
        public uint SndMax;
        public ulong ThruBytesAcked;
        public uint RcvNxt;
        public ulong ThruBytesReceived;
    }
}
