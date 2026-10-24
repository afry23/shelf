using System.ComponentModel;
using System.Diagnostics;

namespace Shelf.Index;

/// <summary>The remote folder cannot be fetched; <see cref="ToolMissing"/> tells a missing tool from a failed run.</summary>
public sealed class RemoteFetchException(string message, bool toolMissing = false, Exception? innerException = null) : Exception(message, innerException)
{
    public bool ToolMissing { get; } = toolMissing;
}

/// <summary>Copies the book files of a remote folder into a local one (F-IMP-8).</summary>
public interface IRemoteFetcher
{
    /// <param name="remote">An rclone path such as <c>gdrive:Books</c>.</param>
    /// <param name="destination">An existing local folder.</param>
    /// <exception cref="RemoteFetchException">The tool is missing or the copy failed.</exception>
    void Fetch(string remote, string destination, CancellationToken cancellation);
}

/// <summary>Runs the user's own <c>rclone</c>; shelf opens no connection and holds no credentials.</summary>
public sealed class RcloneFetcher : IRemoteFetcher
{
    /// <summary>The arguments of <c>rclone copy</c>; separate arguments, never a command line to parse.</summary>
    public static IReadOnlyList<string> Arguments(string remote, string destination) =>
        ["copy", remote, destination, "--include", "*.epub", "--include", "*.pdf"];

    public void Fetch(string remote, string destination, CancellationToken cancellation)
    {
        var info = new ProcessStartInfo("rclone") { RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in Arguments(remote, destination))
        {
            info.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(info)!;
        }
        catch (Win32Exception e)
        {
            throw new RemoteFetchException("rclone was not found; install it from https://rclone.org and set up a remote with 'rclone config'", toolMissing: true, e);
        }

        using (process)
        {
            using var registration = cancellation.Register(() => process.Kill(entireProcessTree: true));
            _ = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            cancellation.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
            {
                var detail = error.Trim().ReplaceLineEndings(" ");
                throw new RemoteFetchException($"rclone failed (exit code {process.ExitCode}): {detail}");
            }
        }
    }
}
