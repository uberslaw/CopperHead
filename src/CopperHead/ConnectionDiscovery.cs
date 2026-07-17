using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace CopperHead;

public sealed record DiscoveredEndpoint(
    string ProcessName,
    int ProcessId,
    IPAddress RemoteAddress,
    int RemotePort,
    string? Hostname)
{
    public string DisplayKey => Hostname ?? RemoteAddress.ToString();

    public override string ToString()
    {
        if (ProcessId == 0 && RemotePort == 0)
            return DisplayKey; // persisted / not currently live
        return Hostname is null
            ? $"{RemoteAddress}:{RemotePort}  ←  {ProcessName} ({ProcessId})"
            : $"{Hostname}  ({RemoteAddress}:{RemotePort})  ←  {ProcessName} ({ProcessId})";
    }
}

/// <summary>
/// Discovers remote IPv4 endpoints used by named processes via the Windows TCP table.
/// No injection — read-only IP Helper API. Hostnames come from the DNS client cache when possible.
/// </summary>
public static class ConnectionDiscovery
{
    public static IReadOnlyList<DiscoveredEndpoint> Scan(IEnumerable<string> processNames)
    {
        var wanted = processNames
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p[..^4] : p)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (wanted.Count == 0)
            return [];

        var pids = new Dictionary<int, string>();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (wanted.Contains(proc.ProcessName))
                    pids[proc.Id] = proc.ProcessName;
            }
            catch
            {
                // process may exit while enumerating
            }
            finally
            {
                proc.Dispose();
            }
        }

        if (pids.Count == 0)
            return [];

        var dns = DnsCacheReader.GetIpToHostMap();
        var rows = TcpTableReader.GetIpv4Rows();
        var results = new List<DiscoveredEndpoint>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!pids.TryGetValue(row.ProcessId, out var name))
                continue;
            if (row.State != 5) // MIB_TCP_STATE_ESTAB
                continue;
            if (IsPrivateOrLocal(row.RemoteAddress))
                continue;

            dns.TryGetValue(row.RemoteAddress.ToString(), out var host);
            var key = $"{name}|{row.RemoteAddress}|{host}";
            if (!seen.Add(key))
                continue;

            results.Add(new DiscoveredEndpoint(
                name,
                row.ProcessId,
                row.RemoteAddress,
                row.RemotePort,
                host));
        }

        return results
            .OrderBy(r => r.Hostname ?? r.RemoteAddress.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsPrivateOrLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        var b = ip.GetAddressBytes();
        if (b[0] == 10)
            return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            return true;
        if (b[0] == 192 && b[1] == 168)
            return true;
        if (b[0] == 169 && b[1] == 254)
            return true;
        if (b[0] == 127)
            return true;
        return false;
    }
}

internal static class TcpTableReader
{
    internal readonly record struct Row(
        int ProcessId,
        IPAddress RemoteAddress,
        int RemotePort,
        int State,
        uint LocalAddrRaw,
        uint LocalPortRaw,
        uint RemoteAddrRaw,
        uint RemotePortRaw);

    private const int AfInet = 2;
    private const int TcpTableOwnerPidAll = 5;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        int tblClass,
        uint reserved);

    public static List<Row> GetIpv4Rows()
    {
        var size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var ret = GetExtendedTcpTable(buffer, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
            if (ret != 0)
                throw new InvalidOperationException($"GetExtendedTcpTable failed: {ret}");

            var count = Marshal.ReadInt32(buffer);
            var rowPtr = buffer + 4;
            var rows = new List<Row>(count);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

            for (var i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr + i * rowSize);
                var addr = new IPAddress(row.remoteAddr);
                var port = (int)SwapUInt16((ushort)row.remotePort);
                rows.Add(new Row(
                    (int)row.owningPid,
                    addr,
                    port,
                    (int)row.state,
                    row.localAddr,
                    row.localPort,
                    row.remoteAddr,
                    row.remotePort));
            }

            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static ushort SwapUInt16(ushort value) =>
        (ushort)((value >> 8) | (value << 8));

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }
}

/// <summary>
/// Best-effort parse of <c>ipconfig /displaydns</c> to map IPs → hostnames.
/// </summary>
public static class DnsCacheReader
{
    public static Dictionary<string, string> GetIpToHostMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig.exe",
                Arguments = "/displaydns",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return map;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10_000);

            string? currentName = null;
            foreach (var raw in output.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("Record Name", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf(':');
                    currentName = idx >= 0 ? line[(idx + 1)..].Trim() : null;
                    continue;
                }

                if (currentName is null)
                    continue;

                // "A (Host) Record . . . : 1.2.3.4"  or  "Data . . . : 1.2.3.4"
                if (line.StartsWith("A (Host) Record", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Data", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    var data = line[(idx + 1)..].Trim();
                    if (IPAddress.TryParse(data, out var ip) &&
                        ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        map.TryAdd(ip.ToString(), currentName);
                    }
                }
            }
        }
        catch
        {
            // cache unavailable — still return IP-only discoveries
        }

        return map;
    }
}
