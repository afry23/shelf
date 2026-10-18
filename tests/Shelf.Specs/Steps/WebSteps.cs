using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Shelf.Cli;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Steps for the web server. The server runs in the test process on a free port (<c>--port 0</c>); the port is read from the
/// line that it announces on standard error. Requests go out with a plain <see cref="HttpClient"/> that follows no redirect
/// and keeps no cookies of its own, so that a scenario sees exactly what the server answered.
/// </summary>
[Binding]
public sealed class WebSteps(ScenarioEnvironment environment, ConfigSteps workspace, CliSteps cli) : IDisposable
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
    private string? _cookie;
    private JsonNode? _json;

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

    [When("I request {string} with the cookie")]
    public void WhenIRequestWithTheCookie(string request) => Send(request, header: null, withCookie: true);

    // ---- answers ----

    [Then("the response status is {int}")]
    public void ThenTheStatusIs(int status) =>
        Assert.True(status == (int)_response!.StatusCode, $"Expected status {status} but it was {(int)_response.StatusCode}. Body: {_body}");

    [Then("the response header {string} is {string}")]
    public void ThenTheHeaderIs(string name, string expected) => Assert.Equal(expected, Header(name));

    [Then("the response header {string} is none")]
    public void ThenTheHeaderIsNone(string name) => Assert.Null(Header(name));

    [Then("the response header {string} starts with {string}")]
    public void ThenTheHeaderStartsWith(string name, string prefix) => Assert.StartsWith(prefix, Header(name), StringComparison.Ordinal);

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

    [Then("the JSON field {string} of the response is {int}")]
    public void ThenTheFieldIsInt(string field, int expected) => Assert.Equal(expected, Json()[field]!.GetValue<int>());

    [Then("the JSON field {string} of the response is {string}")]
    public void ThenTheFieldIsString(string field, string expected) => Assert.Equal(expected, Json()[field]!.GetValue<string>());

    [Then("the JSON field {string} of the response is not empty")]
    public void ThenTheFieldIsNotEmpty(string field) => Assert.False(string.IsNullOrEmpty(Json()[field]!.GetValue<string>()));

    [Then("the JSON field {string} of the response starts with {string}")]
    public void ThenTheFieldStartsWith(string field, string prefix) => Assert.StartsWith(prefix, Json()[field]!.GetValue<string>(), StringComparison.Ordinal);

    [Then("the JSON document has the path {string} with the method {string}")]
    public void ThenTheDocumentHasThePath(string path, string method) => Assert.NotNull(Json()["paths"]![path]![method]);

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

    private void Send(string request, string? header, bool withCookie)
    {
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

        if (withCookie)
        {
            message.Headers.TryAddWithoutValidation("Cookie", _cookie ?? throw new InvalidOperationException("No cookie was set before."));
        }

        _response?.Dispose();
        _response = _http.SendAsync(message).WaitAsync(Timeout).GetAwaiter().GetResult();
        _body = _response.Content.ReadAsStringAsync().WaitAsync(Timeout).GetAwaiter().GetResult();
        _json = null;
        if (SetCookies().FirstOrDefault() is { } set)
        {
            _cookie = set.Split(';')[0];
        }
    }

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
