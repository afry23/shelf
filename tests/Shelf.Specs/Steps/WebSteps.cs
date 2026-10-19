using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Shelf.Cli;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Steps for the web server. The server runs in the test process on a free port (<c>--port 0</c>); the port is read from the
/// line that it announces on standard error. Requests go out with a plain <see cref="HttpClient"/> that follows no redirect
/// and keeps no cookies of its own, so that a scenario sees exactly what the server answered.
/// </summary>
[Binding]
public sealed class WebSteps(ScenarioEnvironment environment, ConfigSteps workspace, CliSteps cli, CoverSteps covers) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
    private readonly LockedWriter _stdout = new();
    private readonly LockedWriter _stderr = new();
    private CancellationTokenSource? _stop;
    private Task<int>? _server;
    private int _port;
    private HttpResponseMessage? _response;
    private string _body = string.Empty;
    private byte[] _bytes = [];
    private string? _cookie;
    private JsonNode? _json;

    /// <summary>The address of the running server, as a browser reaches it.</summary>
    public Uri BaseAddress => new($"http://127.0.0.1:{_port}/");

    /// <summary>The standard error of the running server.</summary>
    public string ServerErrors => _stderr.ToString();
    private string _workId = string.Empty;
    private string _script = string.Empty;
    private string _style = string.Empty;

    // ---- the server ----

    [Given("the web server is running on the library {string}")]
    public void GivenTheWebServerIsRunning(string library) => Start(library, string.Empty);

    [Given("the web server is running on the library {string} with the arguments {string}")]
    [When("the web server is running on the library {string} with the arguments {string}")]
    public void GivenTheWebServerIsRunningWith(string library, string arguments) => Start(library, arguments);

    [When("the web server is running on the library {string}")]
    public void WhenTheWebServerIsRunning(string library) => Start(library, string.Empty);

    [When("the server is interrupted")]
    public void WhenTheServerIsInterrupted() => _stop!.Cancel();

    [When("I run shelf with the arguments {string} while the web server is running")]
    public void WhenIRunShelfWhileTheServerRuns(string arguments) =>
        cli.WhenIRunShelfWithArguments(arguments.Replace("{port}", _port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));

    [Then("the server announced an address on loopback")]
    public void ThenTheServerAnnouncedLoopback() => Assert.Contains($"Listening on http://127.0.0.1:{_port}", _stderr.ToString());

    [Then("the server announced an address on all interfaces")]
    public void ThenTheServerAnnouncedAllInterfaces() => Assert.Contains($"Listening on http://0.0.0.0:{_port}", _stderr.ToString());

    [Then("the server wrote nothing to standard output")]
    public void ThenTheServerWroteNothing() => Assert.Equal(string.Empty, _stdout.ToString());

    [Then("the standard error of the server does not contain {string}")]
    public void ThenTheServerErrorDoesNotContain(string text) => Assert.DoesNotContain(text, _stderr.ToString(), StringComparison.Ordinal);

    [Then("the server ends with the exit code {int}")]
    public void ThenTheServerEnds(int expected)
    {
        Assert.True(_server!.Wait(Timeout), "The server did not stop.");
        Assert.True(expected == _server.Result, $"Expected exit code {expected} but it was {_server.Result}. Standard error: {_stderr}");
    }

    // ---- requests ----

    [Given("I request {string}")]
    [When("I request {string}")]
    public void WhenIRequest(string request) => Send(request, header: null, withCookie: false);

    [Given("I request {string} with the header {string}")]
    [When("I request {string} with the header {string}")]
    public void WhenIRequestWithTheHeader(string request, string header) => Send(request, header, withCookie: false);

    [Given("I send {string} with the JSON:")]
    [When("I send {string} with the JSON:")]
    public void WhenISendWithTheJson(string request, string body) => Send(request, header: null, withCookie: false, body);

    [When("I request {string} with the cookie")]
    public void WhenIRequestWithTheCookie(string request) => Send(request, header: null, withCookie: true);

    // ---- answers ----

    [When("I request {string} again with the ETag")]
    public void WhenIRequestAgainWithTheETag(string request) =>
        Send(request, "If-None-Match: " + (Header("ETag") ?? throw new InvalidOperationException("The last response had no ETag.")), withCookie: false);

    [Then("the response body is empty")]
    public void ThenTheBodyIsEmpty() => Assert.Empty(_bytes);

    [Then("the response body is the file {string}")]
    public void ThenTheBodyIsTheFile(string path) => Assert.True(File(path).AsSpan().SequenceEqual(_bytes), $"The body ({_bytes.Length} bytes) is not the file {path}.");

    [Then("the response body is the file {string} from byte {int}")]
    public void ThenTheBodyIsTheFileFrom(string path, int first) =>
        Assert.True(File(path).AsSpan(first).SequenceEqual(_bytes), $"The body ({_bytes.Length} bytes) is not the file {path} from byte {first}.");

    [Then("the response body is bytes {int} to {int} of the file {string}")]
    public void ThenTheBodyIsBytesOfTheFile(int first, int last, string path) =>
        Assert.True(File(path).AsSpan(first, last - first + 1).SequenceEqual(_bytes), $"The body ({_bytes.Length} bytes) is not bytes {first} to {last} of the file {path}.");

    [Then("the response body is the last {int} bytes of the file {string}")]
    public void ThenTheBodyIsTheEndOfTheFile(int count, string path) =>
        Assert.True(File(path).AsSpan(^count..).SequenceEqual(_bytes), $"The body ({_bytes.Length} bytes) is not the last {count} bytes of the file {path}.");

    [Then("the response header {string} is the size of the file {string}")]
    public void ThenTheHeaderIsTheSizeOfTheFile(string name, string path) =>
        Assert.Equal(File(path).Length.ToString(CultureInfo.InvariantCulture), Header(name));

    [Given("I look up the ID of the work {string}")]
    public void GivenILookUpTheIdOfTheWork(string title)
    {
        using var document = System.Text.Json.JsonDocument.Parse(global::System.IO.File.ReadAllText(Path.Combine(covers.FindWorkFolder(title), "book.json")));
        _workId = document.RootElement.GetProperty("id").GetString()!;
    }

    [Then("the response body is the cover {string} of the work {string}")]
    public void ThenTheBodyIsTheCover(string name, string title) =>
        Assert.True(System.IO.File.ReadAllBytes(Path.Combine(covers.FindWorkFolder(title), name)).AsSpan().SequenceEqual(_bytes), "The body is not the cover file.");

    [Then("the JSON field {string} of the response is true")]
    public void ThenTheFieldIsTrue(string path) => Assert.True(Field(path)!.GetValue<bool>());

    [Then("the JSON field {string} of the response is false")]
    public void ThenTheFieldIsFalse(string path) => Assert.False(Field(path)!.GetValue<bool>());

    [Given("I note the script and the style of the page")]
    public void GivenINoteTheScriptAndTheStyle()
    {
        Send("GET /", header: null, withCookie: false);
        _script = System.Text.RegularExpressions.Regex.Match(_body, "src=\"(/assets/[^\"]+\\.js)\"").Groups[1].Value;
        _style = System.Text.RegularExpressions.Regex.Match(_body, "href=\"(/assets/[^\"]+\\.css)\"").Groups[1].Value;
        Assert.NotEmpty(_script);
        Assert.NotEmpty(_style);
    }

    [Then("the response body is the built UI file {string}")]
    public void ThenTheBodyIsTheBuiltUiFile(string path) => AssertBodyIsBuilt(path);

    [Then("the response body is the built UI file of the noted script")]
    public void ThenTheBodyIsTheBuiltScript() => AssertBodyIsBuilt(_script.TrimStart('/'));

    [Then("the response body is the built UI file of the noted style")]
    public void ThenTheBodyIsTheBuiltStyle() => AssertBodyIsBuilt(_style.TrimStart('/'));

    [Then("the response body contains {string}")]
    public void ThenTheBodyContains(string text) => Assert.Contains(text, _body, StringComparison.Ordinal);

    [Then("the response status is {int}")]
    public void ThenTheStatusIs(int status) =>
        Assert.True(status == (int)_response!.StatusCode, $"Expected status {status} but it was {(int)_response.StatusCode}. Body: {_body}");

    [Then("the response header {string} is {string}")]
    public void ThenTheHeaderIs(string name, string expected) => Assert.Equal(expected, Header(name));

    [Then("the response header {string} is none")]
    public void ThenTheHeaderIsNone(string name) => Assert.Null(Header(name));

    [Then("the response header {string} starts with {string}")]
    public void ThenTheHeaderStartsWith(string name, string prefix) => Assert.StartsWith(prefix, Header(name), StringComparison.Ordinal);

    [Then("the response header {string} does not contain {string}")]
    public void ThenTheHeaderDoesNotContain(string name, string text) => Assert.DoesNotContain(text, Header(name) ?? string.Empty, StringComparison.Ordinal);

    [Then("the response header {string} contains {string}")]
    public void ThenTheHeaderContains(string name, string text) => Assert.Contains(text, Header(name), StringComparison.Ordinal);

    [Then("the response sets the cookie {string} with the attributes {string}")]
    public void ThenTheResponseSetsTheCookie(string name, string attributes)
    {
        var cookie = Assert.Single(SetCookies(), c => c.StartsWith(name + "=", StringComparison.Ordinal));
        var parts = cookie.Split(';', StringSplitOptions.TrimEntries);
        foreach (var attribute in attributes.Split(',', StringSplitOptions.TrimEntries))
        {
            Assert.Contains(parts, p => p.Equals(attribute, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Then("the response sets no cookie")]
    public void ThenTheResponseSetsNoCookie() => Assert.Empty(SetCookies());

    [Then("the response is a JSON document")]
    public void ThenTheResponseIsJson() => Assert.NotNull(Json());

    [Then("the JSON field {string} of the response is {float}")]
    public void ThenTheFieldIsNumber(string path, double expected) => Assert.Equal(expected, Field(path)!.GetValue<double>());

    [Then("the JSON field {string} of the response is {string}")]
    public void ThenTheFieldIsString(string path, string expected) => Assert.Equal(expected, Field(path)!.GetValue<string>());

    [Then("the JSON field {string} of the response is not zero")]
    public void ThenTheFieldIsNotZero(string path) => Assert.NotEqual(0, Field(path)!.GetValue<double>());

    [Then("the JSON field {string} of the response is null")]
    public void ThenTheFieldIsNull(string path) => Assert.Null(Field(path));

    [Then("the JSON field {string} of the response is not empty")]
    public void ThenTheFieldIsNotEmpty(string path) => Assert.False(string.IsNullOrEmpty(Field(path)!.GetValue<string>()));

    [Then("the JSON field {string} of the response starts with {string}")]
    public void ThenTheFieldStartsWith(string path, string prefix) => Assert.StartsWith(prefix, Field(path)!.GetValue<string>(), StringComparison.Ordinal);

    [Then("the JSON field {string} of the response contains {string}")]
    public void ThenTheFieldContains(string path, string text) => Assert.Contains(text, Field(path)!.GetValue<string>(), StringComparison.Ordinal);

    [Then("the JSON array {string} of the response has {int} entries")]
    public void ThenTheArrayHasEntries(string path, int count) => Assert.Equal(count, Field(path)!.AsArray().Count);

    [Then("the JSON array {string} of the response includes {string}")]
    public void ThenTheArrayIncludes(string path, string text) => Assert.Contains(text, Field(path)!.AsArray().Select(n => n!.GetValue<string>()));

    [Then("the JSON array {string} of the response does not include {string}")]
    public void ThenTheArrayDoesNotInclude(string path, string text) => Assert.DoesNotContain(text, Field(path)!.AsArray().Select(n => n!.GetValue<string>()));

    [Then("the JSON document has the path {string} with the method {string}")]
    public void ThenTheDocumentHasThePath(string path, string method) => Assert.NotNull(Json()["paths"]![path]![method]);

    // Before the working directory is deleted (the hook of ConfigSteps has the default order): a server that still runs keeps writing
    // into the index folder while it is being removed.
    [AfterScenario(Order = 200)]
    public void StopTheServer() => Stop();

    public void Dispose()
    {
        Stop();
        _http.Dispose();
        _response?.Dispose();
    }

    private void Start(string library, string arguments)
    {
        Stop();
        _stdout.Clear();
        _stderr.Clear();
        _stop = new CancellationTokenSource();
        var args = new List<string> { "serve", "--library", Path.Combine(workspace.WorkingDirectory, library) };
        var extra = ArgumentSplitter.Split(arguments);
        if (!extra.Contains("--port"))
        {
            args.AddRange(["--port", "0"]);
        }

        args.AddRange(extra);

        var shelf = CliEnvironment
            .FromVariables(name => environment.Variables.GetValueOrDefault(name), stdoutIsTerminal: false)
            .WithDirectories(workspace.WorkingDirectory, Path.Combine(workspace.WorkingDirectory, CliSteps.HomeFolder))
            .WithCancellation(_stop.Token);
        if (cli.MinimumLogLevel is { } level)
        {
            shelf = shelf.WithMinimumLogLevel(level);
        }

        if (environment.Models is { } models)
        {
            shelf = shelf.WithModels(models.Catalog, models);
        }

        if (environment.Embedder is not null)
        {
            shelf = shelf.WithEmbedder(folder => environment.EmbedderFor(Path.GetFileName(folder.Directory)));
        }

        _server = Task.Run(() => ShelfApp.Run([.. args], _stdout, _stderr, shelf));

        var deadline = DateTime.UtcNow + Timeout;
        while (!_stderr.ToString().Contains("Listening on ", StringComparison.Ordinal))
        {
            if (_server.IsCompleted)
            {
                Assert.Fail($"The server stopped with exit code {_server.Result}. Standard error: {_stderr}");
            }

            Assert.True(DateTime.UtcNow < deadline, $"The server did not start. Standard error: {_stderr}");
            Thread.Sleep(20);
        }

        var line = _stderr.ToString().Split('\n').First(l => l.StartsWith("Listening on ", StringComparison.Ordinal));
        _port = new Uri(line["Listening on ".Length..].Trim().Replace("0.0.0.0", "127.0.0.1", StringComparison.Ordinal)).Port;
    }

    private void Stop()
    {
        if (_server is null)
        {
            return;
        }

        _stop!.Cancel();
        _server.Wait(Timeout);
        _stop.Dispose();
        _server = null;
    }

    private void Send(string request, string? header, bool withCookie, string? body = null)
    {
        request = request.Replace("{id}", _workId, StringComparison.Ordinal)
            .Replace("{script}", _script, StringComparison.Ordinal)
            .Replace("{style}", _style, StringComparison.Ordinal);
        var space = request.IndexOf(' ', StringComparison.Ordinal);
        var message = new HttpRequestMessage(new HttpMethod(request[..space]), $"http://127.0.0.1:{_port}{request[(space + 1)..]}");
        if (header is not null)
        {
            var colon = header.IndexOf(':', StringComparison.Ordinal);
            var name = header[..colon];
            var value = header[(colon + 1)..].Trim().Replace("{port}", _port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                message.Headers.Host = value;
            }
            else
            {
                message.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (body is not null)
        {
            var type = message.Method == HttpMethod.Patch ? "application/merge-patch+json" : "application/json";
            message.Content = new StringContent(body, Encoding.UTF8, type);
        }

        if (withCookie)
        {
            message.Headers.TryAddWithoutValidation("Cookie", _cookie ?? throw new InvalidOperationException("No cookie was set before."));
        }

        _response?.Dispose();
        _response = _http.SendAsync(message).WaitAsync(Timeout).GetAwaiter().GetResult();
        _bytes = _response.Content.ReadAsByteArrayAsync().WaitAsync(Timeout).GetAwaiter().GetResult();
        _body = Encoding.UTF8.GetString(_bytes);
        _json = null;
        if (SetCookies().FirstOrDefault() is { } set)
        {
            _cookie = set.Split(';')[0];
        }
    }

    private void AssertBodyIsBuilt(string path)
    {
        var built = global::System.IO.File.ReadAllBytes(Path.Combine(RepositoryPaths.Root(), "src", "Shelf.Web.Ui", "dist", path));
        Assert.True(built.AsSpan().SequenceEqual(_bytes), $"The body ({_bytes.Length} bytes) is not the built file {path} ({built.Length} bytes).");
    }

    private byte[] File(string path) => System.IO.File.ReadAllBytes(Path.Combine(workspace.WorkingDirectory, path));

    private string? Header(string name)
    {
        var response = _response!;
        if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
        {
            return string.Join(", ", values);
        }

        return null;
    }

    private List<string> SetCookies() =>
        _response!.Headers.TryGetValues("Set-Cookie", out var values) ? [.. values] : [];

    /// <summary>
    /// The value at a path such as <c>items[0].title</c>. A property that is missing fails the step (a value that is not there
    /// must be written as null); a property that is null gives null.
    /// </summary>
    private JsonNode? Field(string path)
    {
        JsonNode? node = Json();
        foreach (var part in path.Split('.'))
        {
            var bracket = part.IndexOf('[', StringComparison.Ordinal);
            var name = bracket < 0 ? part : part[..bracket];
            if (name.Length > 0)
            {
                var obj = node as JsonObject ?? throw new InvalidOperationException($"'{path}': no object before '{name}' in {_body}");
                Assert.True(obj.ContainsKey(name), $"'{path}': the property '{name}' is missing in {_body}");
                node = obj[name];
            }

            while (bracket >= 0)
            {
                var close = part.IndexOf(']', bracket);
                var index = int.Parse(part[(bracket + 1)..close], CultureInfo.InvariantCulture);
                var array = node as JsonArray ?? throw new InvalidOperationException($"'{path}': no array before [{index}] in {_body}");
                Assert.True(index < array.Count, $"'{path}': index {index} is beyond the {array.Count} entries in {_body}");
                node = array[index];
                bracket = part.IndexOf('[', close);
            }
        }

        return node;
    }

    private JsonNode Json() => _json ??= JsonNode.Parse(_body) ?? throw new InvalidOperationException($"The body is no JSON: {_body}");

    /// <summary>A writer that the server thread and the step thread can use at the same time.</summary>
    private sealed class LockedWriter : TextWriter
    {
        private readonly StringBuilder _text = new();
        private readonly Lock _gate = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_gate)
            {
                _text.Append(value);
            }
        }

        public override void Write(string? value)
        {
            lock (_gate)
            {
                _text.Append(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (_gate)
            {
                _text.Append(value).Append('\n');
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _text.Clear();
            }
        }

        public override string ToString()
        {
            lock (_gate)
            {
                return _text.ToString();
            }
        }
    }
}
