namespace Debugging.Shared;

public static class EventLogWriter
{
    private static readonly object Gate = new();

    public static void Append(string message)
    {
        Directory.CreateDirectory(Paths.DataRoot);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}";

        lock (Gate)
        {
            File.AppendAllText(Paths.EventLogFile, line);
        }
    }

    public static string ReadAll()
    {
        if (!File.Exists(Paths.EventLogFile))
        {
            return string.Empty;
        }

        lock (Gate)
        {
            return File.ReadAllText(Paths.EventLogFile);
        }
    }
}
