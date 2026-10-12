return Proto.Run(args, Console.Out, Console.Error);

internal static class Proto
{
    private const string Usage =
        "Usage: proto [--json] <command> [options]\n\nCommands:\n  greet <name> [--times <n>]\n  tag add <name>\n\nOptions:\n  --json       Machine-readable output\n  --version    Show version\n  --help       Show help";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var json = false;
        var rest = new List<string>();
        foreach (var a in args)
        {
            if (a == "--json")
            {
                json = true;
            }
            else
            {
                rest.Add(a);
            }
        }

        if (rest.Count == 0)
        {
            stderr.WriteLine(Usage);
            return 2;
        }

        switch (rest[0])
        {
            case "--version":
                stdout.WriteLine("proto 1.0.0");
                return 0;
            case "--help":
                stdout.WriteLine(Usage);
                return 0;
            case "greet":
                return Greet(rest, json, stdout, stderr);
            case "tag":
                if (rest.Count == 3 && rest[1] == "add")
                {
                    stdout.WriteLine($"added {rest[2]}");
                    return 0;
                }

                stderr.WriteLine("Usage: proto tag add <name>");
                return 2;
            default:
                stderr.WriteLine(rest[0].StartsWith('-')
                    ? $"Unknown option '{rest[0]}'"
                    : $"Unknown command '{rest[0]}'");
                return 2;
        }
    }

    private static int Greet(List<string> rest, bool json, TextWriter stdout, TextWriter stderr)
    {
        string? name = null;
        var times = 1;
        for (var i = 1; i < rest.Count; i++)
        {
            if (rest[i] == "--times")
            {
                if (i + 1 >= rest.Count || !int.TryParse(rest[i + 1], out times))
                {
                    stderr.WriteLine("Invalid value for --times");
                    return 2;
                }

                i++;
            }
            else if (name is null)
            {
                name = rest[i];
            }
            else
            {
                stderr.WriteLine($"Unexpected argument '{rest[i]}'");
                return 2;
            }
        }

        if (name is null)
        {
            stderr.WriteLine("Missing argument <name>");
            return 2;
        }

        for (var i = 0; i < times; i++)
        {
            stdout.WriteLine(json ? "{\"greeting\":\"Hello, " + name + "\"}" : $"Hello, {name}");
        }

        return 0;
    }
}
