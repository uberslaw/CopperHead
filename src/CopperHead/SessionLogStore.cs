using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CopperHead;

public sealed record LogProcessInfo(
    string ProcessKey,
    string DisplayName,
    string Directory,
    DateTime LastWriteUtc,
    int EventCount,
    int KnownEndpointCount);

/// <summary>
/// Per-process JSONL event logs + known-endpoint memory under logs/{ProcessKey}/.
/// </summary>
public sealed class SessionLogStore
{
    private readonly object _gate = new();
    private string _processKey = "unknown";
    private string _displayName = "unknown";
    private StreamWriter? _writer;

    public string ProcessKey
    {
        get { lock (_gate) return _processKey; }
    }

    public string DisplayName
    {
        get { lock (_gate) return _displayName; }
    }

    public static string LogsRoot => Path.Combine(AppContext.BaseDirectory, "logs");

    public static string SanitizeKey(string processList)
    {
        var parts = processList
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p[..^4] : p)
            .Where(p => p.Length > 0)
            .Select(p => Regex.Replace(p, @"[^\w\-.]+", "_"))
            .ToArray();
        if (parts.Length == 0) return "unknown";
        var key = string.Join("+", parts);
        return key.Length > 80 ? key[..80] : key;
    }

    public void SetProcess(string processList)
    {
        var key = SanitizeKey(processList);
        var display = string.Join(", ", processList
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(display))
            display = key;

        lock (_gate)
        {
            if (string.Equals(_processKey, key, StringComparison.OrdinalIgnoreCase) && _writer is not null)
            {
                _displayName = display;
                return;
            }

            CloseWriter_NoLock();
            _processKey = key;
            _displayName = display;
            Directory.CreateDirectory(ProcessDir_NoLock());
            var path = Path.Combine(ProcessDir_NoLock(), "events.jsonl");
            _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = true,
            };
            Write_NoLock("session", $"Logging switched to process set: {display}", null);
        }
    }

    public void Write(string category, string message, object? data = null)
    {
        lock (_gate)
        {
            if (_writer is null)
                SetProcess(_displayName);
            Write_NoLock(category, message, data);
        }
    }

    public HashSet<string> LoadKnownEndpoints()
    {
        lock (_gate)
        {
            var path = Path.Combine(ProcessDir_NoLock(), "known.json");
            if (!File.Exists(path))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [];
                return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public void SaveKnownEndpoints(IEnumerable<string> keys)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(ProcessDir_NoLock());
            var path = Path.Combine(ProcessDir_NoLock(), "known.json");
            var list = keys.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
            File.WriteAllText(path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public void Close()
    {
        lock (_gate)
            CloseWriter_NoLock();
    }

    public static IReadOnlyList<LogProcessInfo> ListProcessLogs()
    {
        if (!Directory.Exists(LogsRoot))
            return [];

        var results = new List<LogProcessInfo>();
        foreach (var dir in Directory.GetDirectories(LogsRoot))
        {
            var key = Path.GetFileName(dir);
            var eventsPath = Path.Combine(dir, "events.jsonl");
            var knownPath = Path.Combine(dir, "known.json");
            var lastWrite = Directory.GetLastWriteTimeUtc(dir);
            var eventCount = 0;
            if (File.Exists(eventsPath))
            {
                lastWrite = File.GetLastWriteTimeUtc(eventsPath);
                eventCount = CountLines(eventsPath);
            }

            var knownCount = 0;
            string display = key;
            if (File.Exists(knownPath))
            {
                try
                {
                    knownCount = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(knownPath))?.Count ?? 0;
                }
                catch { /* ignore */ }
            }

            // Prefer last session message display name if present
            display = TryReadDisplayName(eventsPath) ?? key.Replace('+', ',');

            results.Add(new LogProcessInfo(key, display, dir, lastWrite, eventCount, knownCount));
        }

        return results.OrderByDescending(r => r.LastWriteUtc).ToList();
    }

    public static string BuildHtmlReport(string processKey)
    {
        var dir = Path.Combine(LogsRoot, processKey);
        var eventsPath = Path.Combine(dir, "events.jsonl");
        var knownPath = Path.Combine(dir, "known.json");
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine($"<title>CopperHead — {WebUtility.HtmlEncode(processKey)}</title>");
        sb.AppendLine("""
            <style>
              body{font-family:Segoe UI,system-ui,sans-serif;margin:24px;background:#f6f7f9;color:#1a1a1a;}
              h1{margin:0 0 4px;font-size:1.4rem;}
              .meta{color:#666;margin-bottom:20px;}
              .card{background:#fff;border:1px solid #e2e4e8;border-radius:8px;padding:16px;margin-bottom:16px;}
              table{width:100%;border-collapse:collapse;font-size:0.92rem;}
              th,td{text-align:left;padding:8px 10px;border-bottom:1px solid #eee;vertical-align:top;}
              th{background:#f0f2f5;position:sticky;top:0;}
              .cat{display:inline-block;padding:2px 8px;border-radius:999px;background:#eef2ff;color:#3730a3;font-size:0.8rem;}
              .ts{color:#666;white-space:nowrap;}
              code{font-family:Consolas,monospace;font-size:0.85rem;}
              ul{margin:0;padding-left:18px;}
            </style>
            """);
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>CopperHead log — {WebUtility.HtmlEncode(processKey.Replace("+", ", "))}</h1>");
        sb.AppendLine($"<div class='meta'>Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");

        sb.AppendLine("<div class='card'><h2>Known endpoints</h2>");
        if (File.Exists(knownPath))
        {
            try
            {
                var known = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(knownPath)) ?? [];
                if (known.Count == 0) sb.AppendLine("<p>None yet.</p>");
                else
                {
                    sb.AppendLine("<ul>");
                    foreach (var k in known.OrderBy(x => x))
                        sb.AppendLine($"<li><code>{WebUtility.HtmlEncode(k)}</code></li>");
                    sb.AppendLine("</ul>");
                }
            }
            catch
            {
                sb.AppendLine("<p>Could not read known.json</p>");
            }
        }
        else sb.AppendLine("<p>None yet.</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='card'><h2>Event history</h2><table><thead><tr><th>Time</th><th>Category</th><th>Message</th></tr></thead><tbody>");
        if (File.Exists(eventsPath))
        {
            foreach (var line in File.ReadLines(eventsPath).Reverse().Take(2000).Reverse())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var ts = root.TryGetProperty("ts", out var t) ? t.GetString() : "";
                    var cat = root.TryGetProperty("category", out var c) ? c.GetString() : "";
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "";
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td class='ts'>{WebUtility.HtmlEncode(ts)}</td>");
                    sb.AppendLine($"<td><span class='cat'>{WebUtility.HtmlEncode(cat)}</span></td>");
                    sb.AppendLine($"<td>{WebUtility.HtmlEncode(msg)}</td>");
                    sb.AppendLine("</tr>");
                }
                catch
                {
                    sb.AppendLine($"<tr><td colspan='3'><code>{WebUtility.HtmlEncode(line)}</code></td></tr>");
                }
            }
        }
        else
        {
            sb.AppendLine("<tr><td colspan='3'>No events logged yet.</td></tr>");
        }

        sb.AppendLine("</tbody></table></div></body></html>");
        return sb.ToString();
    }

    public static string WriteHtmlReport(string processKey)
    {
        var dir = Path.Combine(LogsRoot, processKey);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "report.html");
        File.WriteAllText(path, BuildHtmlReport(processKey), Encoding.UTF8);
        return path;
    }

    private string ProcessDir_NoLock() => Path.Combine(LogsRoot, _processKey);

    private void Write_NoLock(string category, string message, object? data)
    {
        if (_writer is null) return;
        var payload = new Dictionary<string, object?>
        {
            ["ts"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["process"] = _displayName,
            ["processKey"] = _processKey,
            ["category"] = category,
            ["message"] = message,
        };
        if (data is not null)
            payload["data"] = data;
        _writer.WriteLine(JsonSerializer.Serialize(payload));
    }

    private void CloseWriter_NoLock()
    {
        try { _writer?.Dispose(); }
        catch { /* ignore */ }
        _writer = null;
    }

    private static int CountLines(string path)
    {
        var n = 0;
        using var reader = new StreamReader(path);
        while (reader.ReadLine() is not null) n++;
        return n;
    }

    private static string? TryReadDisplayName(string eventsPath)
    {
        if (!File.Exists(eventsPath)) return null;
        try
        {
            // Read last few lines for a session switch message
            var lines = File.ReadLines(eventsPath).TakeLast(20);
            foreach (var line in lines.Reverse())
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("process", out var p))
                    return p.GetString();
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
