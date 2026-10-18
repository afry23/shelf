using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Reqnroll;
using Shelf.Cli;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Steps for the MCP server. Most scenarios run the server in the test process and talk to it with the SDK's own client
/// over two pipes; the process scenarios start <c>shelf mcp</c> for real and read its raw standard output.
/// </summary>
[Binding]
public sealed class McpSteps(ScenarioEnvironment environment, ConfigSteps workspace) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private AnonymousPipeServerStream? _toServer;
    private AnonymousPipeServerStream? _fromServer;
    private AnonymousPipeClientStream? _serverInput;
    private AnonymousPipeClientStream? _serverOutput;
    private Task<int>? _server;
    private McpClient? _client;
    private IList<McpClientTool> _tools = [];
    private CallToolResult? _result;
    private JsonNode? _document;
    private McpException? _failure;
    private McpProtocolException? _protocolFailure;
    private string? _resourceType;
    private Process? _process;
    private readonly List<string> _processOut = [];
    private string _processErr = string.Empty;
    private int _processExit = int.MinValue;

    private string Library(string name) => Path.Combine(workspace.WorkingDirectory, name);

    // ---- in-process server ----

    [Given("the MCP server is running on the library {string}")]
    public void GivenTheServerIsRunning(string library)
    {
        Stop();
        _toServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        _fromServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        _serverInput = new AnonymousPipeClientStream(PipeDirection.In, _toServer.ClientSafePipeHandle);
        _serverOutput = new AnonymousPipeClientStream(PipeDirection.Out, _fromServer.ClientSafePipeHandle);

        var cli = CliEnvironment
            .FromVariables(name => environment.Variables.GetValueOrDefault(name), stdoutIsTerminal: false)
            .WithDirectories(workspace.WorkingDirectory, Path.Combine(workspace.WorkingDirectory, CliSteps.HomeFolder))
            .WithMcpStreams(_serverInput, _serverOutput);
        if (environment.Models is { } models)
        {
            cli = cli.WithModels(models.Catalog, models);
        }

        environment.EmbedderOpens = 0;
        if (environment.Embedder is not null)
        {
            cli = cli.WithEmbedder(folder =>
            {
                environment.EmbedderOpens++;
                return environment.EmbedderFor(Path.GetFileName(folder.Directory));
            });
        }

        _server = Task.Run(() => ShelfApp.Run(["mcp", "--library", Library(library)], new StringWriter(), new StringWriter(), cli));

        _client = McpClient.CreateAsync(new StreamClientTransport(_toServer, _fromServer)).WaitAsync(Timeout).GetAwaiter().GetResult();
        _tools = _client.ListToolsAsync().AsTask().WaitAsync(Timeout).GetAwaiter().GetResult();
    }

    [When("I call the tool {string} with the arguments:")]
    public void WhenICallTheToolWith(string tool, string arguments) => Call(tool, JsonNode.Parse(arguments)!.AsObject());

    [Given("I call the tool {string} with the arguments:")]
    public void GivenICallTheToolWith(string tool, string arguments) => Call(tool, JsonNode.Parse(arguments)!.AsObject());

    [When("I call the unknown tool {string}")]
    public void WhenICallTheUnknownTool(string tool) => Call(tool, []);

    [When("I read the passage at the location {string} of that book")]
    public void WhenIReadThePassageAt(string path)
    {
        var book = _document ?? throw new InvalidOperationException("No book was fetched before.");
        var location = Navigate(book, path).GetValue<string>();
        Call("read_passage", new JsonObject { ["id"] = book["id"]!.GetValue<string>(), ["location"] = location });
    }

    [When("I read the passage at the location {string} of the work {string}")]
    public void WhenIReadThePassageAtOfTheWork(string locationPath, string idPath)
    {
        var result = _document ?? throw new InvalidOperationException("No result was fetched before.");
        Call("read_passage", new JsonObject { ["id"] = Navigate(result, idPath).GetValue<string>(), ["location"] = Navigate(result, locationPath).GetValue<string>() });
    }

    [When("I read the resource {string}")]
    public void WhenIReadTheResource(string uri)
    {
        _failure = null;
        _protocolFailure = null;
        try
        {
            var result = _client!.ReadResourceAsync(uri).AsTask().WaitAsync(Timeout).GetAwaiter().GetResult();
            var content = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
            _resourceType = content.MimeType;
            _document = JsonNode.Parse(content.Text);
        }
        catch (McpProtocolException e)
        {
            _protocolFailure = e;
        }
    }

    // ---- what the server announces ----

    [Then("the server is called {string}")]
    public void ThenTheServerIsCalled(string name) => Assert.Equal(name, _client!.ServerInfo.Name);

    [Then("the server instructions mention {string}")]
    public void ThenTheInstructionsMention(string text) => Assert.Contains(text, _client!.ServerInstructions, StringComparison.Ordinal);

    [Then("the server offers exactly the tools:")]
    public void ThenTheServerOffersExactly(DataTable expected) =>
        Assert.Equal(expected.Rows.Select(r => r["tool"]).Order(StringComparer.Ordinal), Names());

    [Then("the tool names sorted are {string}")]
    public void ThenTheToolNamesSortedAre(string names) => Assert.Equal(names.Split(", "), Names());

    [Then("every tool except {string} and {string} is marked read-only")]
    public void ThenEveryToolExceptIsReadOnly(string a, string b) =>
        Assert.All(_tools.Where(t => t.Name != a && t.Name != b), t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint == true, $"{t.Name} is not marked read-only"));

    [Then("no tool is marked destructive")]
    public void ThenNoToolIsDestructive() =>
        Assert.All(_tools, t => Assert.True(t.ProtocolTool.Annotations?.DestructiveHint != true, $"{t.Name} is marked destructive"));

    [Then("the tool {string} is not read-only and not destructive")]
    public void ThenTheToolIsNotReadOnlyAndNotDestructive(string name)
    {
        var annotations = Tool(name).ProtocolTool.Annotations;
        Assert.True(annotations?.ReadOnlyHint != true, $"{name} is marked read-only");
        Assert.True(annotations?.DestructiveHint == false, $"{name} is not explicitly marked as not destructive");
    }

    [Then("the server offers the tools {string} and {string}")]
    public void ThenTheServerOffersTheTools(string a, string b)
    {
        Assert.Contains(a, Names());
        Assert.Contains(b, Names());
    }

    [Then(@"no tool name starts with {string}, {string}, {string} or {string}")]
    public void ThenNoToolNameStartsWith(string a, string b, string c, string d) =>
        Assert.DoesNotContain(Names(), n => new[] { a, b, c, d }.Any(w => n.StartsWith(w, StringComparison.Ordinal)));

    [Then("the embedder was opened {int} time(s)")]
    public void ThenTheEmbedderWasOpened(int count) => Assert.Equal(count, environment.EmbedderOpens);

    [Then("every tool is marked read-only")]
    public void ThenEveryToolIsReadOnly() =>
        Assert.All(_tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint == true, $"{t.Name} is not marked read-only"));

    [Then("every tool has a description")]
    public void ThenEveryToolHasADescription() =>
        Assert.All(_tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description), $"{t.Name} has no description"));

    [Then("the tool {string} requires the arguments {string}")]
    public void ThenTheToolRequires(string tool, string arguments) =>
        Assert.Equal(arguments.Split(", ").Order(StringComparer.Ordinal), Required(tool).Order(StringComparer.Ordinal));

    [Then("the tool {string} requires no arguments")]
    public void ThenTheToolRequiresNoArguments(string tool) => Assert.Empty(Required(tool));

    [Then("every argument of the tool {string} has a description")]
    public void ThenEveryArgumentHasADescription(string tool)
    {
        var properties = Tool(tool).ProtocolTool.InputSchema.GetProperty("properties");
        Assert.NotEmpty(properties.EnumerateObject());
        foreach (var property in properties.EnumerateObject())
        {
            Assert.True(property.Value.TryGetProperty("description", out var d) && !string.IsNullOrWhiteSpace(d.GetString()), $"{tool}.{property.Name} has no description");
        }
    }

    [Then("the server offers the resource templates:")]
    public void ThenTheServerOffersTheResourceTemplates(DataTable expected)
    {
        var templates = _client!.ListResourceTemplatesAsync().AsTask().WaitAsync(Timeout).GetAwaiter().GetResult();
        Assert.Equal(expected.Rows.Select(r => r["uri"]).Order(StringComparer.Ordinal), templates.Select(t => t.UriTemplate).Order(StringComparer.Ordinal));
    }

    // ---- results ----

    [Then("the tool call succeeds")]
    public void ThenTheToolCallSucceeds() => Assert.True(_failure is null && _result is { IsError: not true }, Describe());

    [Then("the tool call fails")]
    public void ThenTheToolCallFails() => Assert.True(_failure is not null || _result is { IsError: true }, "The call succeeded.");

    [Then("the tool call fails with the message containing {string}")]
    public void ThenTheToolCallFailsWithTheMessage(string text)
    {
        Assert.True(_failure is not null || _result is { IsError: true }, "The call succeeded.");
        Assert.Contains(text, Message(), StringComparison.Ordinal);
    }

    [Then("the call fails with the protocol code {int}")]
    public void ThenTheCallFailsWithTheCode(int code) =>
        Assert.Equal(code, (int)Assert.IsType<McpProtocolException>(_protocolFailure ?? _failure).ErrorCode);

    [Then("the result has the string {string} equal to {string}")]
    public void ThenTheResultHasTheString(string path, string expected) => Assert.Equal(expected, Navigate(Document(), path).GetValue<string>());

    [Then("the result has the string {string} containing {string}")]
    public void ThenTheResultHasTheStringContaining(string path, string text) =>
        Assert.Contains(text, Navigate(Document(), path).GetValue<string>(), StringComparison.Ordinal);

    [Then("the result has the number {string} equal to {int}")]
    public void ThenTheResultHasTheNumber(string path, int expected) => Assert.Equal(expected, Navigate(Document(), path).GetValue<int>());

    [Then("the result has the boolean {string} equal to {word}")]
    public void ThenTheResultHasTheBoolean(string path, string expected) =>
        Assert.Equal(expected == "true", Navigate(Document(), path).GetValue<bool>());

    [Then("the result has the array {string} with {int} items")]
    public void ThenTheResultHasTheArray(string path, int count) => Assert.Equal(count, Navigate(Document(), path).AsArray().Count);

    [Then("the result has the property {string} set to null")]
    public void ThenTheResultHasThePropertySetToNull(string path)
    {
        var (parent, name) = Parent(Document(), path);
        Assert.True(parent.AsObject().ContainsKey(name), $"{path} is missing");
        Assert.Null(parent[name]);
    }

    [Then("the result has every property that the output schema of {string} requires")]
    public void ThenTheResultHasEveryRequiredProperty(string tool)
    {
        var schema = Tool(tool).ProtocolTool.OutputSchema ?? throw new InvalidOperationException($"{tool} has no output schema.");
        var required = schema.TryGetProperty("required", out var r) ? r.EnumerateArray().Select(e => e.GetString()!).ToList() : [];
        Assert.NotEmpty(required);
        var document = Document().AsObject();
        Assert.All(required, name => Assert.True(document.ContainsKey(name), $"{tool}: required property '{name}' is missing from the result"));
    }

    [Then("the resource has the type {string}")]
    public void ThenTheResourceHasTheType(string type) => Assert.Equal(type, _resourceType);

    [Then("the resource is a list with {int} items")]
    public void ThenTheResourceIsAListWith(int count) => Assert.Equal(count, Document().AsArray().Count);

    // ---- the server as a process ----

    [When("I start {string} as a process on the library {string} and send these requests:")]
    public void WhenIStartTheProcess(string command, string library, DataTable requests)
    {
        Assert.Equal("shelf mcp", command);
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workspace.WorkingDirectory,
        };
        info.ArgumentList.Add(typeof(ShelfApp).Assembly.Location);
        info.ArgumentList.Add("mcp");
        info.ArgumentList.Add("--library");
        info.ArgumentList.Add(Library(library));
        var home = Path.Combine(workspace.WorkingDirectory, CliSteps.HomeFolder);
        Directory.CreateDirectory(home);
        info.Environment["HOME"] = home;
        foreach (var (name, value) in environment.Variables)
        {
            info.Environment[name] = value;
        }

        _process = Process.Start(info)!;
        var errors = _process.StandardError.ReadToEndAsync();
        var lines = Task.Run(() =>
        {
            while (_process.StandardOutput.ReadLine() is { } line)
            {
                lock (_processOut)
                {
                    _processOut.Add(line);
                }
            }
        });
        foreach (var row in requests.Rows)
        {
            _process.StandardInput.WriteLine(row["request"]);
            _requestsWithId += JsonNode.Parse(row["request"])!["id"] is not null ? 1 : 0;
        }

        _process.StandardInput.Flush();
        _errors = errors;
        _lines = lines;
    }

    private Task<string>? _errors;
    private Task? _lines;

    [When("I close its input")]
    public void WhenICloseItsInput()
    {
        // The server answers requests as they come; give it the time to do so, then end its input.
        var deadline = DateTime.UtcNow + Timeout;
        var expected = ExpectedResponses();
        while (DateTime.UtcNow < deadline && Responses() < expected && !_process!.HasExited)
        {
            Thread.Sleep(50);
        }

        _process!.StandardInput.Close();
        Assert.True(_process.WaitForExit((int)Timeout.TotalMilliseconds), "The process did not end after its input was closed.");
        _lines!.Wait(Timeout);
        _processErr = _errors!.GetAwaiter().GetResult();
        _processExit = _process.ExitCode;
    }

    private int _requestsWithId;

    [Then("every line on its standard output is a JSON-RPC message")]
    public void ThenEveryLineIsJsonRpc()
    {
        Assert.NotEmpty(_processOut);
        foreach (var line in _processOut)
        {
            var node = JsonNode.Parse(line);
            Assert.Equal("2.0", node!["jsonrpc"]!.GetValue<string>());
        }
    }

    [Then("its standard output has {int} responses")]
    public void ThenItsStandardOutputHasResponses(int count) => Assert.Equal(count, Responses());

    [Then("the response {int} of the process is a success")]
    public void ThenTheResponseIsASuccess(int id)
    {
        var response = Response(id);
        Assert.NotNull(response["result"]);
        Assert.True(response["result"]!["isError"]?.GetValue<bool>() != true, response.ToJsonString());
    }

    [Then("the response {int} of the process is a tool error")]
    public void ThenTheResponseIsAToolError(int id) =>
        Assert.True(Response(id)["result"]?["isError"]?.GetValue<bool>() == true, Response(id).ToJsonString());

    [Then("the response {int} of the process contains {string}")]
    public void ThenTheResponseContains(int id, string text) => Assert.Contains(text, Response(id).ToJsonString(), StringComparison.Ordinal);

    [Then("the response {int} of the process does not contain {string}")]
    public void ThenTheResponseDoesNotContain(int id, string text) => Assert.DoesNotContain(text, Response(id).ToJsonString(), StringComparison.Ordinal);

    [Then("its standard error is empty")]
    public void ThenItsStandardErrorIsEmpty() => Assert.Equal(string.Empty, _processErr.Trim());

    [Then("its standard error is not empty")]
    public void ThenItsStandardErrorIsNotEmpty() => Assert.NotEmpty(_processErr.Trim());

    [Then("its standard error contains {string}")]
    public void ThenItsStandardErrorContains(string text) => Assert.Contains(text, _processErr, StringComparison.Ordinal);

    [Then("its standard output is empty")]
    public void ThenItsStandardOutputIsEmpty() => Assert.Empty(_processOut);

    [Then("the process ends with the exit code {int}")]
    public void ThenTheProcessEndsWith(int code) => Assert.Equal(code, _processExit);

    public void Dispose()
    {
        Stop();
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
        }

        _process?.Dispose();
    }

    // ---- helpers ----

    private void Call(string tool, JsonObject arguments)
    {
        _result = null;
        _document = null;
        _failure = null;
        _protocolFailure = null;
        try
        {
            var values = arguments.ToDictionary(p => p.Key, p => (object?)p.Value);
            _result = _client!.CallToolAsync(tool, values).AsTask().WaitAsync(Timeout).GetAwaiter().GetResult();
            _document = _result.StructuredContent is { } structured ? JsonNode.Parse(structured.GetRawText()) : null;
        }
        catch (McpProtocolException e)
        {
            _protocolFailure = e;
        }
        catch (McpException e)
        {
            _failure = e;
        }
    }

    private JsonNode Document() => _document ?? throw new InvalidOperationException($"The call returned no structured result. {Describe()}");

    private string Describe() => _failure?.Message ?? _protocolFailure?.Message ?? (_result is { IsError: true } ? Message() : "The call succeeded.");

    private string Message() =>
        _failure?.Message ?? _protocolFailure?.Message ?? string.Join(' ', _result!.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private List<string> Names() => [.. _tools.Select(t => t.Name).Order(StringComparer.Ordinal)];

    private McpClientTool Tool(string name) => _tools.Single(t => t.Name == name);

    private List<string> Required(string tool) =>
        Tool(tool).ProtocolTool.InputSchema.TryGetProperty("required", out var required)
            ? [.. required.EnumerateArray().Select(e => e.GetString()!)]
            : [];

    private int ExpectedResponses() => _requestsWithId;

    private int Responses()
    {
        lock (_processOut)
        {
            return _processOut.Count(l => JsonNode.Parse(l)?["id"] is not null);
        }
    }

    private JsonNode Response(int id)
    {
        lock (_processOut)
        {
            return _processOut.Select(l => JsonNode.Parse(l)!).Single(n => n["id"]?.GetValue<int>() == id);
        }
    }

    /// <summary>A path such as <c>results[0].passages[1].title</c> or <c>[0].title</c> into a JSON document.</summary>
    private static JsonNode Navigate(JsonNode root, string path)
    {
        var node = root;
        foreach (var part in path.Split('.'))
        {
            var bracket = part.IndexOf('[', StringComparison.Ordinal);
            var name = bracket < 0 ? part : part[..bracket];
            if (name.Length > 0)
            {
                node = node[name] ?? throw new InvalidOperationException($"'{name}' is missing in {node.ToJsonString()}");
            }

            while (bracket >= 0)
            {
                var close = part.IndexOf(']', bracket);
                node = node[int.Parse(part[(bracket + 1)..close], CultureInfo.InvariantCulture)]!;
                bracket = part.IndexOf('[', close);
            }
        }

        return node;
    }

    private static (JsonNode Parent, string Name) Parent(JsonNode root, string path)
    {
        var cut = path.LastIndexOf('.');
        return cut < 0 ? (root, path) : (Navigate(root, path[..cut]), path[(cut + 1)..]);
    }

    private void Stop()
    {
        try
        {
            _client?.DisposeAsync().AsTask().Wait(Timeout);
        }
        catch (Exception e) when (e is AggregateException or ObjectDisposedException or IOException)
        {
            // The server has gone already.
        }

        _toServer?.Dispose();
        _server?.Wait(Timeout);
        _fromServer?.Dispose();
        _serverInput?.Dispose();
        _serverOutput?.Dispose();
        _client = null;
        _server = null;
    }
}
