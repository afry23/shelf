using System.Diagnostics;
using System.Text;

namespace ProtoClient;

/// <summary>A server process started from the command given on the client's command line plus scenario arguments.</summary>
internal sealed class ServerProc : IDisposable
{
    private readonly Process _proc;
    private readonly StringBuilder _stderr = new();
    private readonly TaskCompletionSource<string> _listening = new();

    private ServerProc(Process proc)
    {
        _proc = proc;
    }

    public string Stderr
    {
        get { lock (_stderr) return _stderr.ToString(); }
    }

    public string Stdout { get; private set; } = "";

    public int? ExitCode => _proc.HasExited ? _proc.ExitCode : null;

    public Task<string> Listening => _listening.Task;

    public static ServerProc Start(string[] command, IEnumerable<string> extraArgs)
    {
        var psi = new ProcessStartInfo(command[0])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            // An empty working directory: nothing the server serves may come from the file system next to it.
            WorkingDirectory = Directory.CreateTempSubdirectory("proto-web-cwd-").FullName,
        };
        foreach (var a in command.Skip(1).Concat(extraArgs))
            psi.ArgumentList.Add(a);
        var proc = Process.Start(psi)!;
        var sp = new ServerProc(proc);
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            lock (sp._stderr)
                sp._stderr.AppendLine(e.Data);
            if (e.Data.StartsWith("listening on ", StringComparison.Ordinal))
                sp._listening.TrySetResult(e.Data["listening on ".Length..].Split(',')[0]);
        };
        proc.BeginErrorReadLine();
        _ = Task.Run(async () => sp.Stdout = await proc.StandardOutput.ReadToEndAsync());
        return sp;
    }

    public async Task<string> WaitForUrl()
    {
        var done = await Task.WhenAny(_listening.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (done != _listening.Task)
            throw new TimeoutException("server did not report a listening address. stderr: " + Stderr);
        return await _listening.Task;
    }

    public async Task<bool> WaitForExit(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _proc.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_proc.HasExited)
        {
            _proc.Kill(entireProcessTree: true);
            _proc.WaitForExit(5000);
        }

        _proc.Dispose();
    }
}
