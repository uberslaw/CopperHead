using System.IO.Pipes;
using System.Text;
using Debugging.Shared;

namespace Debugging.Tray;

public static class PipeClient
{
    public static async Task<string?> SendAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                Paths.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(1500, cancellationToken);

            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

            await writer.WriteLineAsync(command);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
