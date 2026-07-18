using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
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
/// No geo database ship; best-effort (CDN/anycast may show PoP country).
/// </summary>
public static class IpGeoLookup
{
    private static readonly ConcurrentDictionary<string, IpGeoInfo> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<int, string> AsnNameCache = new();
    private static readonly object PersistGate = new();
    private static bool _loaded;
    private static readonly Regex TxtQuote = new("\"([^\"]+)\"", RegexOptions.Compiled);

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
        if (Cache.TryGetValue(key, out var hit))
            return hit;

        var info = QueryOrigin(ip);
        Cache[key] = info;
        SchedulePersist();
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
        var missing = unique.Where(k => !Cache.ContainsKey(k)).ToList();
        if (missing.Count == 0)
            return;

        Parallel.ForEach(
            missing,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            key =>
            {
                if (!IPAddress.TryParse(key, out var ip))
                    return;
                Cache[key] = QueryOrigin(ip);
            });
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
            var parts = txt.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
                return IpGeoInfo.Empty;

            if (!int.TryParse(parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0], out var asn))
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
                    // Prefer short org before " - " when present
                    var dash = name.IndexOf(" - ", StringComparison.Ordinal);
                    if (dash > 0 && dash < 24)
                        name = name[(dash + 3)..].Trim();
                    // Drop trailing ", US"
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
        // Prefer native DnsQuery; fall back to nslookup (same style as ipconfig DNS cache).
        var native = NativeDnsTxt.Query(dnsName);
        if (native is { Count: > 0 })
            return native[0];

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
            proc.WaitForExit(8_000);
            foreach (Match m in TxtQuote.Matches(output))
            {
                var v = m.Groups[1].Value.Trim();
                if (v.Contains('|'))
                    return v;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

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
                            Cache[ip] = new IpGeoInfo(row.Cc ?? "", row.Asn, row.Name ?? "");
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
        // Fire-and-forget; coalesce via lock inside Persist.
        ThreadPool.QueueUserWorkItem(_ => Persist());
    }

    private static void Persist()
    {
        lock (PersistGate)
        {
            try
            {
                var rows = Cache.ToDictionary(
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
                    // DNS_RECORD (x64): pNext, pName, wType, wDataLength, Flags, dwTtl, dwReserved, Data
                    var wType = Marshal.ReadInt16(ptr, IntPtr.Size * 2);
                    if (wType != DnsTypeText)
                        continue;

                    // Data starts after fixed header (32 bytes on x64, 28 on x86 with padding quirks)
                    var dataOffset = IntPtr.Size * 2 + 16; // wType+wDataLength+Flags+dwTtl+dwReserved = 16
                    var stringCount = Marshal.ReadInt32(ptr, dataOffset);
                    // PWSTR array follows DWORD with pointer alignment
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
