using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CopperHead;

public sealed record IpGeoInfo(string CountryCode, int Asn, string AsnName)
{
    public static IpGeoInfo Empty { get; } = new("", 0, "");

    public bool HasData => CountryCode.Length > 0 || Asn > 0;

    public string CountryDisplay => CountryCode.Length == 0 ? "" : CountryCode;

    public string AsnDisplay
    {
        get
        {
            if (Asn <= 0)
                return "";
            return AsnName.Length == 0 ? $"AS{Asn}" : $"AS{Asn} {AsnName}";
        }
    }
}

/// <summary>
/// Country + ASN via Team Cymru DNS (origin.asn.cymru.com). Cached in memory and on disk.
/// Misses are not cached permanently so a later scan can succeed.
/// </summary>
public static class IpGeoLookup
{
    private static readonly ConcurrentDictionary<string, IpGeoInfo> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, long> MissUntilTicks =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<int, string> AsnNameCache = new();
    private static readonly object PersistGate = new();
    private static bool _loaded;
    private static readonly Regex TxtQuote = new("\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly TimeSpan MissBackoff = TimeSpan.FromSeconds(45);

    private static string CachePath =>
        Path.Combine(AppContext.BaseDirectory, "ip-geo-cache.json");

    public static IpGeoInfo Lookup(IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return IpGeoInfo.Empty;
        if (ConnectionDiscovery.IsPrivateOrLocal(ip))
            return IpGeoInfo.Empty;

        EnsureLoaded();
        var key = ip.ToString();
        if (Cache.TryGetValue(key, out var hit) && hit.HasData)
            return hit;

        if (MissUntilTicks.TryGetValue(key, out var until) && until > DateTime.UtcNow.Ticks)
            return IpGeoInfo.Empty;

        var info = QueryOrigin(ip);
        if (info.HasData)
        {
            Cache[key] = info;
            MissUntilTicks.TryRemove(key, out _);
            SchedulePersist();
        }
        else
        {
            // Do not persist blanks; back off briefly before retrying.
            Cache.TryRemove(key, out _);
            MissUntilTicks[key] = DateTime.UtcNow.Add(MissBackoff).Ticks;
        }

        return info;
    }

    public static IpGeoInfo Lookup(string ipText)
    {
        if (!IPAddress.TryParse(ipText, out var ip))
            return IpGeoInfo.Empty;
        return Lookup(ip);
    }

    /// <summary>Warm cache for many IPs (parallel, capped). Safe to call from background.</summary>
    public static void LookupMany(IEnumerable<IPAddress> ips)
    {
        var unique = ips
            .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Where(ip => !ConnectionDiscovery.IsPrivateOrLocal(ip))
            .Select(ip => ip.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        EnsureLoaded();
        var now = DateTime.UtcNow.Ticks;
        var missing = unique
            .Where(k => !Cache.TryGetValue(k, out var info) || !info.HasData)
            .Where(k => !MissUntilTicks.TryGetValue(k, out var until) || until <= now)
            .ToList();
        if (missing.Count == 0)
            return;

        var anyNew = 0;
        Parallel.ForEach(
            missing,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            key =>
            {
                if (!IPAddress.TryParse(key, out var ip))
                    return;
                var info = QueryOrigin(ip);
                if (!info.HasData)
                {
                    MissUntilTicks[key] = DateTime.UtcNow.Add(MissBackoff).Ticks;
                    return;
                }
                Cache[key] = info;
                MissUntilTicks.TryRemove(key, out _);
                Interlocked.Exchange(ref anyNew, 1);
            });

        if (anyNew != 0)
            SchedulePersist();
    }

    private static IpGeoInfo QueryOrigin(IPAddress ip)
    {
        try
        {
            var octets = ip.GetAddressBytes();
            var name = $"{octets[3]}.{octets[2]}.{octets[1]}.{octets[0]}.origin.asn.cymru.com";
            var txt = QueryTxt(name);
            if (txt is null)
                return IpGeoInfo.Empty;

            // "15169 | 8.8.8.0/24 | US | arin | 2023-12-28"
            // Sometimes multiple ASNs: "15169 36040 | ..."
            var parts = txt.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
                return IpGeoInfo.Empty;

            var asnToken = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!int.TryParse(asnToken, out var asn))
                asn = 0;
            var cc = parts[2].Trim().ToUpperInvariant();
            if (cc.Length is < 2 or > 3)
                cc = "";

            var asnName = asn > 0 ? LookupAsnName(asn) : "";
            return new IpGeoInfo(cc, asn, asnName);
        }
        catch
        {
            return IpGeoInfo.Empty;
        }
    }

    private static string LookupAsnName(int asn)
    {
        if (AsnNameCache.TryGetValue(asn, out var cached))
            return cached;

        try
        {
            var txt = QueryTxt($"AS{asn}.asn.cymru.com");
            // "15169 | US | arin | 2000-03-30 | GOOGLE - Google LLC, US"
            var name = "";
            if (txt is not null)
            {
                var parts = txt.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length >= 5)
                {
                    name = parts[4].Trim();
                    var dash = name.IndexOf(" - ", StringComparison.Ordinal);
                    if (dash > 0 && dash < 24)
                        name = name[(dash + 3)..].Trim();
                    var comma = name.LastIndexOf(',');
                    if (comma > 0 && name.Length - comma <= 4)
                        name = name[..comma].Trim();
                    if (name.Length > 40)
                        name = name[..40].Trim();
                }
            }

            AsnNameCache[asn] = name;
            return name;
        }
        catch
        {
            AsnNameCache[asn] = "";
            return "";
        }
    }

    private static string? QueryTxt(string dnsName)
    {
        // 1) UDP DNS to public resolvers (bypasses corp resolvers that drop TXT)
        foreach (var resolver in DnsTxtUdp.DefaultResolvers)
        {
            var viaUdp = DnsTxtUdp.Query(dnsName, resolver);
            var picked = PickCymruTxt(viaUdp);
            if (picked is not null)
                return picked;
        }

        // 2) Windows DnsQuery against the system resolver
        var native = PickCymruTxt(NativeDnsTxt.Query(dnsName));
        if (native is not null)
            return native;

        // 3) nslookup fallback
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nslookup.exe",
                Arguments = $"-type=TXT {dnsName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(8_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return null;
            }

            foreach (Match m in TxtQuote.Matches(output))
            {
                var v = m.Groups[1].Value.Trim();
                if (IsCymruTxt(v))
                    return v;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? PickCymruTxt(IReadOnlyList<string> records)
    {
        foreach (var r in records)
        {
            if (IsCymruTxt(r))
                return r.Trim();
        }
        return null;
    }

    private static bool IsCymruTxt(string v) =>
        v.Contains('|') && v.Split('|').Length >= 3;

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        lock (PersistGate)
        {
            if (_loaded)
                return;
            try
            {
                if (File.Exists(CachePath))
                {
                    var json = File.ReadAllText(CachePath);
                    var rows = JsonSerializer.Deserialize<Dictionary<string, CacheRow>>(json);
                    if (rows is not null)
                    {
                        foreach (var (ip, row) in rows)
                        {
                            var info = new IpGeoInfo(row.Cc ?? "", row.Asn, row.Name ?? "");
                            if (!info.HasData)
                                continue;
                            Cache[ip] = info;
                            if (row.Asn > 0 && !string.IsNullOrEmpty(row.Name))
                                AsnNameCache.TryAdd(row.Asn, row.Name);
                        }
                    }
                }
            }
            catch
            {
                // corrupt cache — start fresh
            }

            _loaded = true;
        }
    }

    private static void SchedulePersist()
    {
        ThreadPool.QueueUserWorkItem(_ => Persist());
    }

    private static void Persist()
    {
        lock (PersistGate)
        {
            try
            {
                var rows = Cache
                    .Where(kv => kv.Value.HasData)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => new CacheRow
                        {
                            Cc = kv.Value.CountryCode,
                            Asn = kv.Value.Asn,
                            Name = kv.Value.AsnName,
                        },
                        StringComparer.OrdinalIgnoreCase);
                var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CachePath, json);
            }
            catch
            {
                // ignore disk errors
            }
        }
    }

    private sealed class CacheRow
    {
        public string? Cc { get; set; }
        public int Asn { get; set; }
        public string? Name { get; set; }
    }
}

/// <summary>Minimal DNS TXT query over UDP (no NuGet).</summary>
internal static class DnsTxtUdp
{
    public static readonly IPAddress[] DefaultResolvers =
    [
        IPAddress.Parse("1.1.1.1"),
        IPAddress.Parse("8.8.8.8"),
    ];

    public static IReadOnlyList<string> Query(string name, IPAddress resolver, int timeoutMs = 2500)
    {
        try
        {
            var id = (ushort)Random.Shared.Next(1, 65535);
            var request = BuildTxtQuery(name, id);
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = timeoutMs;
            udp.Client.SendTimeout = timeoutMs;
            udp.Connect(resolver, 53);
            udp.Send(request, request.Length);

            var remote = new IPEndPoint(IPAddress.Any, 0);
            var response = udp.Receive(ref remote);
            return ParseTxtAnswers(response, id);
        }
        catch
        {
            return [];
        }
    }

    private static byte[] BuildTxtQuery(string name, ushort id)
    {
        using var ms = new MemoryStream(128);
        ms.WriteByte((byte)(id >> 8));
        ms.WriteByte((byte)(id & 0xFF));
        ms.WriteByte(0x01); // RD
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x01); // QDCOUNT = 1
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            if (bytes.Length > 63)
                continue;
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
        }
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x10); // TXT
        ms.WriteByte(0x00);
        ms.WriteByte(0x01); // IN
        return ms.ToArray();
    }

    private static IReadOnlyList<string> ParseTxtAnswers(byte[] msg, ushort expectedId)
    {
        if (msg.Length < 12)
            return [];

        var id = (ushort)((msg[0] << 8) | msg[1]);
        if (id != expectedId)
            return [];

        var flags = (ushort)((msg[2] << 8) | msg[3]);
        if ((flags & 0x000F) != 0) // RCODE
            return [];

        var qd = (msg[4] << 8) | msg[5];
        var an = (msg[6] << 8) | msg[7];
        var offset = 12;

        for (var i = 0; i < qd; i++)
        {
            if (!SkipName(msg, ref offset))
                return [];
            offset += 4; // qtype + qclass
            if (offset > msg.Length)
                return [];
        }

        var list = new List<string>();
        for (var i = 0; i < an; i++)
        {
            if (!SkipName(msg, ref offset))
                break;
            if (offset + 10 > msg.Length)
                break;

            var type = (msg[offset] << 8) | msg[offset + 1];
            var rdlen = (msg[offset + 8] << 8) | msg[offset + 9];
            offset += 10;
            if (offset + rdlen > msg.Length)
                break;

            if (type == 16) // TXT
            {
                var end = offset + rdlen;
                var sb = new StringBuilder();
                var p = offset;
                while (p < end)
                {
                    var len = msg[p++];
                    if (p + len > end)
                        break;
                    sb.Append(Encoding.ASCII.GetString(msg, p, len));
                    p += len;
                }
                var s = sb.ToString().Trim();
                if (s.Length > 0)
                    list.Add(s);
            }

            offset += rdlen;
        }

        return list;
    }

    private static bool SkipName(byte[] msg, ref int offset)
    {
        var jumps = 0;
        while (offset < msg.Length)
        {
            var len = msg[offset];
            if (len == 0)
            {
                offset++;
                return true;
            }

            if ((len & 0xC0) == 0xC0)
            {
                offset += 2;
                return true;
            }

            offset += 1 + len;
            if (++jumps > 64)
                return false;
        }

        return false;
    }
}

/// <summary>TXT lookup via dnsapi DnsQuery_W (Windows).</summary>
internal static class NativeDnsTxt
{
    private const short DnsTypeText = 0x0010;
    private const int DnsFreeRecordList = 1;

    [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode)]
    private static extern int DnsQuery(
        string pszName,
        short wType,
        int options,
        IntPtr pExtra,
        out IntPtr ppQueryResults,
        IntPtr pReserved);

    [DllImport("dnsapi.dll", CharSet = CharSet.Unicode)]
    private static extern void DnsRecordListFree(IntPtr pRecordList, int freeType);

    public static IReadOnlyList<string> Query(string name)
    {
        try
        {
            var status = DnsQuery(name, DnsTypeText, 0, IntPtr.Zero, out var results, IntPtr.Zero);
            if (status != 0 || results == IntPtr.Zero)
                return [];

            var list = new List<string>();
            try
            {
                for (var ptr = results; ptr != IntPtr.Zero; ptr = Marshal.ReadIntPtr(ptr))
                {
                    var wType = Marshal.ReadInt16(ptr, IntPtr.Size * 2);
                    if (wType != DnsTypeText)
                        continue;

                    var dataOffset = IntPtr.Size * 2 + 16;
                    var stringCount = Marshal.ReadInt32(ptr, dataOffset);
                    var arrayOffset = dataOffset + IntPtr.Size;
                    for (var i = 0; i < stringCount && i < 16; i++)
                    {
                        var sPtr = Marshal.ReadIntPtr(ptr, arrayOffset + i * IntPtr.Size);
                        if (sPtr == IntPtr.Zero)
                            continue;
                        var s = Marshal.PtrToStringUni(sPtr);
                        if (!string.IsNullOrWhiteSpace(s))
                            list.Add(s.Trim());
                    }
                }
            }
            finally
            {
                DnsRecordListFree(results, DnsFreeRecordList);
            }

            return list;
        }
        catch
        {
            return [];
        }
    }
}
