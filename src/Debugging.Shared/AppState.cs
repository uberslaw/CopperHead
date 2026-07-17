using System.Text.Json;
using System.Text.Json.Serialization;

namespace Debugging.Shared;

public sealed class AppState
{
    public bool MonitoringEnabled { get; set; } = true;
    public DateTime? LastToggleUtc { get; set; }
    public string? ToggledBy { get; set; }

    public static AppState Load()
    {
        try
        {
            if (!File.Exists(Paths.StateFile))
            {
                return new AppState();
            }

            var json = File.ReadAllText(Paths.StateFile);
            return JsonSerializer.Deserialize<AppState>(json, JsonOptions) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Paths.DataRoot);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(Paths.StateFile, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
