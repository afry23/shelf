using System.CommandLine;
using System.CommandLine.Invocation;
using Proto;

// Exit codes as in PRD 7.1: 0 success, 1 error, 2 usage.
return await ShelfApp.RunAsync(args, Console.Out, Console.Error);

internal static class ShelfApp
{
    public const string Version = "proto-shelf 0.0.1";

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var root = new RootCommand("proto-shelf: one Native AOT binary with all libraries of the M0 spikes (ADR 0008)");

        var file = new Argument<string>("file") { Description = "TOML file" };
        var key = new Argument<string>("key") { Description = "Dotted key" };
        var configGet = new Command("get", "Print the value of a key") { file, key };
        configGet.SetAction(pr => ConfigCommand.Get(pr.GetRequiredValue(file), pr.GetRequiredValue(key), stdout, stderr));
        root.Subcommands.Add(new Command("config", "Configuration") { configGet });

        var vec = new Option<string?>("--vec") { Description = "Path of the sqlite-vec extension (default: next to the binary)" };
        var dbCheck = new Command("check", "Run the SQLite FTS5 and sqlite-vec checks in memory") { vec };
        dbCheck.SetAction(pr => DbCommand.Check(pr.GetValue(vec), stdout, stderr));
        root.Subcommands.Add(new Command("db", "Database") { dbCheck });

        var path = new Argument<string>("path") { Description = "EPUB or PDF file" };
        var extract = new Command("extract", "Print the metadata of an EPUB or PDF file") { path };
        extract.SetAction(async (pr, ct) => await ExtractCommand.RunAsync(pr.GetRequiredValue(path), stdout, stderr));
        root.Subcommands.Add(extract);

        var coverPdf = new Argument<string>("pdf") { Description = "PDF file" };
        var coverOut = new Argument<string>("out") { Description = "Output directory for cover.png, cover.jpg and cover.webp" };
        var cover = new Command("cover", "Render page 1 of a PDF (longest edge 600 px)") { coverPdf, coverOut };
        cover.SetAction(pr => Proto.Cover.CoverCommand.Run(pr.GetRequiredValue(coverPdf), pr.GetRequiredValue(coverOut), stdout, stderr));
        root.Subcommands.Add(cover);

        var mcp = new Command("mcp", "Run the MCP server on stdio");
        mcp.SetAction(async (_, _) => await Proto.Mcp.McpHost.RunAsync());
        root.Subcommands.Add(mcp);

        var bind = new Option<string>("--bind") { Description = "IP address to bind", DefaultValueFactory = _ => "127.0.0.1" };
        var port = new Option<int>("--port") { Description = "Port (0 = any)", DefaultValueFactory = _ => 0 };
        var token = new Option<string?>("--token") { Description = "Access token for /api/v1 (required for non-loopback binds)" };
        var served = new Option<string?>("--file") { Description = "File served at /api/v1/files/F1" };
        var serve = new Command("serve", "Run the web server with the embedded UI") { bind, port, token, served };
        serve.SetAction(async (pr, _) => await Proto.Web.WebHost.RunAsync(pr.GetRequiredValue(bind), pr.GetValue(port), pr.GetValue(token), pr.GetValue(served)));
        root.Subcommands.Add(serve);

        var version = root.Options.OfType<VersionOption>().Single();
        version.Action = new VersionAction(stdout);

        var parse = root.Parse(args);
        if (parse.Errors.Count > 0)
        {
            foreach (var e in parse.Errors)
                stderr.WriteLine(e.Message);
            stderr.WriteLine("Usage: proto-shelf [options] [command]");
            return 2;
        }

        try
        {
            return await parse.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr, EnableDefaultExceptionHandler = false });
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
            output.WriteLine(Version);
            return 0;
        }
    }
}
