using System.CommandLine;
using System.CommandLine.Invocation;

return Proto.Run(args, Console.Out, Console.Error);

internal static class Proto
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var json = new Option<bool>("--json") { Description = "Machine-readable output", Recursive = true };

        var name = new Argument<string>("name") { Description = "Who to greet" };
        var times = new Option<int>("--times") { Description = "Repeat count", DefaultValueFactory = _ => 1 };
        var greet = new Command("greet", "Greet someone") { name, times };
        greet.SetAction(pr =>
        {
            var n = pr.GetRequiredValue(name);
            var useJson = pr.GetValue(json);
            for (var i = 0; i < pr.GetValue(times); i++)
            {
                stdout.WriteLine(useJson ? "{\"greeting\":\"Hello, " + n + "\"}" : $"Hello, {n}");
            }

            return 0;
        });

        var tagName = new Argument<string>("name") { Description = "Tag name" };
        var add = new Command("add", "Add a tag") { tagName };
        add.SetAction(pr =>
        {
            stdout.WriteLine($"added {pr.GetRequiredValue(tagName)}");
            return 0;
        });
        var tag = new Command("tag", "Manage tags") { add };

        var root = new RootCommand("proto") { json, greet, tag };
        var version = root.Options.OfType<VersionOption>().Single();
        version.Action = new VersionAction(stdout);

        var parse = root.Parse(args);
        if (parse.Errors.Count > 0)
        {
            foreach (var e in parse.Errors)
            {
                stderr.WriteLine(e.Message);
            }

            stderr.WriteLine("Usage: proto [options] [command]");
            return 2;
        }

        try
        {
            return parse.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr, EnableDefaultExceptionHandler = false });
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private sealed class VersionAction(TextWriter output) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            output.WriteLine("proto 1.0.0");
            return 0;
        }
    }
}
