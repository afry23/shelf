using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

return Proto.Run(args, Console.Out, Console.Error);

internal class JsonSettings : CommandSettings
{
    [CommandOption("--json")]
    [Description("Machine-readable output")]
    public bool Json { get; init; }
}

internal sealed class GreetSettings : JsonSettings
{
    [CommandArgument(0, "<name>")]
    [Description("Who to greet")]
    public string Name { get; init; } = string.Empty;

    [CommandOption("--times")]
    [DefaultValue(1)]
    [Description("Repeat count")]
    public int Times { get; init; } = 1;
}

internal sealed class TagAddSettings : JsonSettings
{
    [CommandArgument(0, "<name>")]
    public string Name { get; init; } = string.Empty;
}

internal sealed class GreetCommand(TextWriter stdout) : Command<GreetSettings>
{
    public override int Execute(CommandContext context, GreetSettings settings, CancellationToken cancellationToken)
    {
        for (var i = 0; i < settings.Times; i++)
        {
            stdout.WriteLine(settings.Json ? "{\"greeting\":\"Hello, " + settings.Name + "\"}" : $"Hello, {settings.Name}");
        }

        return 0;
    }
}

internal sealed class TagAddCommand(TextWriter stdout) : Command<TagAddSettings>
{
    public override int Execute(CommandContext context, TagAddSettings settings, CancellationToken cancellationToken)
    {
        stdout.WriteLine($"added {settings.Name}");
        return 0;
    }
}

internal sealed class Registrar(TextWriter stdout) : ITypeRegistrar
{
    public ITypeResolver Build() => new Resolver(stdout);

    public void Register(Type service, Type implementation)
    {
    }

    public void RegisterInstance(Type service, object implementation)
    {
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
    }
}

internal sealed class Resolver(TextWriter stdout) : ITypeResolver
{
    public object? Resolve(Type? type)
    {
        if (type == typeof(GreetCommand))
        {
            return new GreetCommand(stdout);
        }

        if (type == typeof(TagAddCommand))
        {
            return new TagAddCommand(stdout);
        }

        return null;
    }
}

internal static class Proto
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(stdout),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
        });

        var app = new CommandApp(new Registrar(stdout));
        app.Configure(c =>
        {
            c.SetApplicationName("proto");
            c.SetApplicationVersion("1.0.0");
            c.ConfigureConsole(console);
            c.PropagateExceptions();
            c.AddCommand<GreetCommand>("greet");
            c.AddBranch("tag", t => t.AddCommand<TagAddCommand>("add"));
        });

        try
        {
            return app.Run(args);
        }
        catch (CommandParseException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }
        catch (CommandRuntimeException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }
}
