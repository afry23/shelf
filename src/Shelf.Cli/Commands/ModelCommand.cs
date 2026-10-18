using System.CommandLine;
using Shelf.Semantic;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf model install [folder]</c> (F-SEM-1, PRD 9.2): puts the embedding model into <c>.shelf/models/</c>.</summary>
internal static class ModelCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("model", "Work with the embedding model");
        command.Subcommands.Add(Install(json, stdout, stderr, environment));
        return command;
    }

    private static Command Install(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var folder = new Argument<string?>("folder")
        {
            Description = "Folder with the model files, for an install without a network (default: download)",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var library = LibraryContext.CreateOption();
        var command = new Command("install", "Install the embedding model into the library, from a folder or by download");
        command.Arguments.Add(folder);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var name = context.Settings.Semantic.Model;
            var model = environment.ModelCatalog.Find(name)
                ?? throw new ModelInstallException(
                    $"unknown model '{name}'; known models: {string.Join(", ", environment.ModelCatalog.Models.Select(m => m.Name))}");
            void Log(string message) => stderr.WriteLine(message);

            var source = parseResult.GetValue(folder);
            var result = source is null
                ? ModelInstaller.Download(model, context.Root, environment.ModelDownloader, Log)
                : ModelInstaller.InstallFromFolder(model, context.Root, Path.GetFullPath(source, environment.WorkingDirectory), Log);

            var status = result.Status switch
            {
                InstallStatus.Installed => "installed",
                InstallStatus.Downloaded => "downloaded",
                _ => "already installed",
            };
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("model", model.Name);
                    writer.WriteString("status", status);
                    writer.WriteString("path", result.Folder.Directory);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine(result.Status switch
                {
                    InstallStatus.Installed => $"Installed {model.Name} in {result.Folder.Directory}.",
                    InstallStatus.Downloaded => $"Downloaded {model.Name} to {result.Folder.Directory}.",
                    _ => $"{model.Name} is already installed in {result.Folder.Directory}.",
                });
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
