using System.ComponentModel;
using System.Diagnostics;

namespace Shelf.Cli;

/// <summary>Hands a file to the application that the system uses for it.</summary>
public interface IFileOpener
{
    /// <exception cref="IOException">The system could not open the file.</exception>
    void Open(string path);
}

/// <summary><c>open</c> on macOS, <c>xdg-open</c> on Linux, the shell on Windows.</summary>
internal sealed class SystemFileOpener : IFileOpener
{
    public static SystemFileOpener Instance { get; } = new();

    public void Open(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var shell = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }

            var program = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            var start = new ProcessStartInfo(program) { UseShellExecute = false, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new IOException($"{program} did not start");
            // The launcher returns as soon as it has handed the file over; a viewer it started may keep running.
            if (process.WaitForExit(TimeSpan.FromSeconds(5)) && process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd().Trim();
                throw new IOException(error.Length > 0 ? error : $"{program} exited with code {process.ExitCode}");
            }
        }
        catch (Win32Exception e)
        {
            throw new IOException(e.Message, e);
        }
    }
}
