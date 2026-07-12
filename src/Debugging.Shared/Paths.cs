namespace Debugging.Shared;

public static class Paths
{
    public const string AppFolderName = "Debugging";
    public const string PipeName = "DebuggingControl";

    public static string DataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderName);

    public static string StateFile => Path.Combine(DataRoot, "state.json");
    public static string EventLogFile => Path.Combine(DataRoot, "events.log");
    public static string ServiceLogFile => Path.Combine(DataRoot, "service.log");
}
