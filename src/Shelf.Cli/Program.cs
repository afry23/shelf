using Shelf.Cli;

return ShelfApp.Run(
    args,
    Console.Out,
    Console.Error,
    CliEnvironment
        .FromVariables(Environment.GetEnvironmentVariable, !Console.IsOutputRedirected)
        .WithDirectories(Environment.CurrentDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
