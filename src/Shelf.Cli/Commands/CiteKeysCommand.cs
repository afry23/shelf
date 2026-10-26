using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf citekeys [work]</c>: builds cite keys from author, year and title. Without a work, every work that has no key gets
/// one; a named work gets a new key even if it has one.
/// </summary>
internal static class CiteKeysCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var work = new Argument<string?>("work")
        {
            Description = "Give this work a new cite key (even if it has one); without it, every work without a key gets one. " + WorkSupport.Argument("work").Description,
            Arity = ArgumentArity.ZeroOrOne,
        };
        var library = LibraryContext.CreateOption();
        var command = new Command("citekeys", "Build cite keys from author, year and title (surname + year + first word of the title)");
        command.Arguments.Add(work);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var reference = parseResult.GetValue(work);
            var only = reference is null ? null : WorkSupport.Resolve(context, environment, reference);
            var assigned = LibraryCiteKeys.Assign(context.Root, only);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("generated", assigned.Count);
                    writer.WriteStartArray("works");
                    foreach (var item in assigned)
                    {
                        writer.WriteStartObject();
                        JsonOutput.Property(writer, "id", item.Work.Id.ToString());
                        JsonOutput.Property(writer, "citeKey", item.Key);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }));
            }
            else if (only is not null)
            {
                stdout.WriteLine(assigned.Count == 0
                    ? $"No cite key for {only.Id}: the author is not known."
                    : $"Cite key of {only.Id}: {assigned[0].Key}");
            }
            else if (assigned.Count == 0)
            {
                stdout.WriteLine("No works without a cite key.");
            }
            else
            {
                stdout.WriteLine($"Generated {TextFormat.Plural(assigned.Count, "cite key", "cite keys")}.");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
