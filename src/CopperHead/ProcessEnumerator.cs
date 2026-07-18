using System.Diagnostics;

namespace CopperHead;

public sealed record RunningProcessInfo(
    int Pid,
    string Name,
    string Path,
    string Company)
{
    public string DisplayKey => Name;
}

/// <summary>
/// Enumerates running processes with best-effort executable paths.
/// </summary>
public static class ProcessEnumerator
{
    public static IReadOnlyList<RunningProcessInfo> Snapshot()
    {
        var results = new List<RunningProcessInfo>();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var name = proc.ProcessName;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string path = "";
                string company = "";
                try
                {
                    // Throws UnauthorizedAccessException / Win32Exception for many system processes
                    path = proc.MainModule?.FileName ?? "";
                }
                catch
                {
                    path = "";
                }

                if (path.Length > 0)
                {
                    try
                    {
                        var version = FileVersionInfo.GetVersionInfo(path);
                        company = version.CompanyName?.Trim() ?? "";
                    }
                    catch { /* ignore */ }
                }

                results.Add(new RunningProcessInfo(proc.Id, name, path, company));
            }
            catch
            {
                // process exited mid-enumeration
            }
            finally
            {
                proc.Dispose();
            }
        }

        return results
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Pid)
            .ToList();
    }
}
