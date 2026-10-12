using Shelf.Cli;

return ShelfApp.Run(
    args,
    Console.Out,
    Console.Error,
    CliEnvironment.FromVariables(Environment.GetEnvironmentVariable, !Console.IsOutputRedirected));
