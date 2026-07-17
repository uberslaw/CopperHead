namespace Debugging.Shared;

public sealed record ServiceTarget(string ServiceName, string DisplayNameContains)
{
    public static IReadOnlyList<ServiceTarget> CiscoTargets { get; } =
    [
        new("csc_te_agent", "ThousandEyes Endpoint Agent"),
        new("csc_umbrellaagent", "Umbrella Agent"),
        new("csc_swgagent", "Umbrella SWG Agent"),
        new("csc_zta_agent", "Zero Trust Access Agent"),
    ];
}
