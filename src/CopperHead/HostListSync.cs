using System.Text.Json;

namespace CopperHead;

/// <summary>
/// Fetches a hostname list from a URL (e.g. raw GitHub).
/// Supports plain text (one host per line) or JSON: { "hostnames": ["a","b"] } / ["a","b"].
/// </summary>
public static class HostListSync
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    static HostListSync()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("CopperHead/1.2");
    }

    public static async Task<IReadOnlyList<string>> FetchAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Host list URL is empty.", nameof(url));

        using var response = await Http.GetAsync(url.Trim(), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var media = response.Content.Headers.ContentType?.MediaType ?? "";
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (media.Contains("json", StringComparison.OrdinalIgnoreCase) ||
            body.TrimStart().StartsWith('{') ||
            body.TrimStart().StartsWith('['))
        {
            return ParseJson(body);
        }

        return ParseText(body);
    }

    public static IReadOnlyList<string> Merge(IEnumerable<string> existing, IEnumerable<string> incoming)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var h in existing.Concat(incoming))
        {
            var t = h.Trim();
            if (t.Length == 0 || t.StartsWith('#'))
                continue;
            if (set.Add(t))
                ordered.Add(t);
        }
        return ordered;
    }

    private static List<string> ParseText(string body) =>
        body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> ParseJson(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            return ReadStringArray(doc.RootElement);

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "hostnames", "hosts", "domains" })
            {
                if (doc.RootElement.TryGetProperty(name, out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    return ReadStringArray(arr);
                }
            }
        }

        throw new InvalidOperationException("JSON host list must be an array or { \"hostnames\": [...] }.");
    }

    private static List<string> ReadStringArray(JsonElement arr) =>
        arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
