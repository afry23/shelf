using System.Runtime.InteropServices;
using Shelf.Cli;

using var interrupt = new CancellationTokenSource();

// A service manager or a container stops a process with SIGTERM: the same orderly stop as Ctrl-C.
using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    interrupt.Cancel();
});

Console.CancelKeyPress += (_, e) =>
{
    // Long commands stop at a safe point and say how to continue; a second Ctrl-C ends the process at once.
    if (!interrupt.IsCancellationRequested)
    {
        e.Cancel = true;
        interrupt.Cancel();
    }
};

return ShelfApp.Run(
    args,
    Console.Out,
    Console.Error,
    CliEnvironment
        .FromVariables(Environment.GetEnvironmentVariable, !Console.IsOutputRedirected)
        .WithDirectories(Environment.CurrentDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        .WithCancellation(interrupt.Token));
