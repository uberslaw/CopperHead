namespace Debugging.Shared;

public sealed class IntervalAnalyzer
{
    private readonly List<double> _intervalsSeconds = [];
    private const int MaxSamples = 12;
    private const double MinIntervalSeconds = 30;
    private const double MaxIntervalSeconds = 86_400;

    public double? EstimatedIntervalSeconds { get; private set; }
    public DateTime? NextExpectedStartUtc { get; private set; }
    public IReadOnlyList<double> ObservedIntervalsSeconds => _intervalsSeconds;

    public void RecordRestartGap(TimeSpan gap)
    {
        var seconds = gap.TotalSeconds;
        if (seconds < MinIntervalSeconds || seconds > MaxIntervalSeconds)
        {
            return;
        }

        _intervalsSeconds.Add(seconds);
        if (_intervalsSeconds.Count > MaxSamples)
        {
            _intervalsSeconds.RemoveAt(0);
        }

        if (_intervalsSeconds.Count >= 3)
        {
            EstimatedIntervalSeconds = Median(_intervalsSeconds);
            NextExpectedStartUtc = DateTime.UtcNow.AddSeconds(EstimatedIntervalSeconds.Value);
        }
    }

    public void ShiftNextExpectedStartFrom(DateTime lastStartDetectedUtc)
    {
        if (EstimatedIntervalSeconds is null)
        {
            return;
        }

        NextExpectedStartUtc = lastStartDetectedUtc.AddSeconds(EstimatedIntervalSeconds.Value);
    }

    public TimeSpan GetSleepDuration(bool inBurstWindow)
    {
        if (inBurstWindow)
        {
            return TimeSpan.FromSeconds(2);
        }

        if (EstimatedIntervalSeconds is null || NextExpectedStartUtc is null)
        {
            return TimeSpan.FromSeconds(15);
        }

        var leadTime = TimeSpan.FromSeconds(10);
        var untilBurst = NextExpectedStartUtc.Value - DateTime.UtcNow - leadTime;

        if (untilBurst <= TimeSpan.Zero)
        {
            return TimeSpan.FromSeconds(2);
        }

        var capped = untilBurst > TimeSpan.FromMinutes(5)
            ? TimeSpan.FromMinutes(5)
            : untilBurst;

        return capped < TimeSpan.FromSeconds(2)
            ? TimeSpan.FromSeconds(2)
            : capped;
    }

    public bool IsInBurstWindow()
    {
        if (EstimatedIntervalSeconds is null || NextExpectedStartUtc is null)
        {
            return false;
        }

        var windowStart = NextExpectedStartUtc.Value - TimeSpan.FromSeconds(15);
        var windowEnd = NextExpectedStartUtc.Value + TimeSpan.FromSeconds(45);
        var now = DateTime.UtcNow;
        return now >= windowStart && now <= windowEnd;
    }

    public string Summary()
    {
        if (EstimatedIntervalSeconds is null)
        {
            return _intervalsSeconds.Count == 0
                ? "Learning restart pattern (no intervals yet)."
                : $"Learning restart pattern ({_intervalsSeconds.Count} sample(s), need 3).";
        }

        var samples = string.Join(", ", _intervalsSeconds.Select(s => FormatDuration(s)));
        return $"Estimated restart interval: {FormatDuration(EstimatedIntervalSeconds.Value)}. Samples: {samples}";
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    public static string FormatDuration(double totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds:0}s";
        }

        if (totalSeconds < 3600)
        {
            return $"{totalSeconds / 60:0.#}m";
        }

        return $"{totalSeconds / 3600:0.#}h";
    }
}
