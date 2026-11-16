using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf convert &lt;work&gt;</c> (F-CNV-1): the PDF of a work becomes a reflowable EPUB, made by the person's own converter.</summary>
internal static class ConvertCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var file = new Option<string?>("--file") { Description = "The PDF to convert, if the work has more than one" };
        var replace = new Option<bool>("--replace") { Description = "Convert again if the work has an EPUB already; the old EPUB goes to the trash" };
        var library = LibraryContext.CreateOption();
        var command = new Command("convert", "Turn the PDF of a work into a reflowable EPUB, for example for a Kindle, with the converter of convert.command");
        command.Arguments.Add(reference);
        command.Options.Add(file);
        command.Options.Add(replace);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            ConvertResult result;
            try
            {
                result = LibraryConvert.Convert(
                    context.Root,
                    work,
                    new ConvertOptions(parseResult.GetValue(file), parseResult.GetValue(replace)),
                    context.Settings.Convert,
                    environment.Converter,
                    environment.WorkingDirectory,
                    environment.Cancellation);
            }
            catch (ConvertException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return e.Kind == ConvertFailureKind.Usage ? ExitCodes.Usage : ExitCodes.Error;
            }
            catch (LibraryEditException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", result.Work.ToString());
                    JsonOutput.Property(writer, "source", result.Source);
                    JsonOutput.Property(writer, "name", result.File);
                    if (result.Replaced is { } replaced)
                    {
                        JsonOutput.Property(writer, "replaced", replaced);
                    }

                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine($"Converted {result.Source} to {result.File}" + (result.Replaced is { } old ? $" (replaced {old}). Undo with shelf undo." : ". Undo with shelf undo."));
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
