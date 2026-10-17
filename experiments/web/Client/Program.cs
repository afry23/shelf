using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProtoClient;

// Usage: proto-web-client <dump-file> <ui-dist-dir> <test-file> <server-command> [server-args...]
// Runs rows W3-W10 against servers started from <server-command>; writes a normalized dump for the AOT/JIT diff (W11).
if (args.Length < 4)
{
    Console.Error.WriteLine("usage: proto-web-client <dump-file> <ui-dist-dir> <test-file> <server-command> [args...]");
    return 2;
}

var dumpPath = args[0];
var distDir = args[1];
var testFile = args[2];
var command = args.Skip(3).ToArray();
var testBytes = File.ReadAllBytes(testFile);
var results = new Results();
var dump = new JsonArray();
const string Token = "s3cret-token";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

async Task<(HttpResponseMessage Resp, byte[] Body)> Get(string url, string? range = null, string? bearer = null, HttpMethod? method = null, HttpContent? content = null)
{
    using var req = new HttpRequestMessage(method ?? HttpMethod.Get, url) { Content = content };
    if (range is not null)
        req.Headers.TryAddWithoutValidation("Range", range);
    if (bearer is not null)
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
    var resp = await http.SendAsync(req);
    var body = await resp.Content.ReadAsByteArrayAsync();
    return (resp, body);
}

// One entry per response in the dump; volatile parts (traceId, Date, Server, Last-Modified) are left out.
void Record(string row, string label, HttpResponseMessage resp, byte[] body)
{
    var ct = resp.Content.Headers.ContentType?.ToString();
    var entry = new JsonObject
    {
        ["row"] = row,
        ["label"] = label,
        ["status"] = (int)resp.StatusCode,
        ["contentType"] = ct,
        ["acceptRanges"] = resp.Headers.TryGetValues("Accept-Ranges", out var ar) ? string.Join(",", ar) : null,
        ["contentRange"] = resp.Content.Headers.ContentRange?.ToString(),
        ["contentLength"] = body.Length,
    };
    if (ct is not null && (ct.Contains("json", StringComparison.Ordinal)))
    {
        var node = JsonNode.Parse(body);
        if (node is JsonObject o)
            o.Remove("traceId");
        entry["body"] = node;
    }
    else
    {
        entry["bodySha256"] = Convert.ToHexString(SHA256.HashData(body));
    }

    dump.Add(entry);
}

string Text(byte[] b) => Encoding.UTF8.GetString(b);

// ---------------------------------------------------------------- default server: W3, W4, W5, W6, W9, W10
using (var server = ServerProc.Start(command, ["--port", "0", "--file", testFile]))
{
    var baseUrl = await server.WaitForUrl();

    // W3
    var distFiles = Directory.GetFiles(distDir, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var w3Ok = distFiles.Length >= 3;
    var w3Detail = new List<string>();
    foreach (var f in distFiles)
    {
        var rel = Path.GetRelativePath(distDir, f).Replace('\\', '/');
        var url = rel == "index.html" ? baseUrl + "/" : baseUrl + "/" + rel;
        var (resp, body) = await Get(url);
        var ct = resp.Content.Headers.ContentType?.MediaType;
        var expectedCt = rel.EndsWith(".html", StringComparison.Ordinal) ? "text/html" : rel.EndsWith(".js", StringComparison.Ordinal) ? "text/javascript" : rel.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : null;
        var ok = resp.StatusCode == HttpStatusCode.OK && body.AsSpan().SequenceEqual(File.ReadAllBytes(f)) && (expectedCt is null || ct == expectedCt);
        w3Ok &= ok;
        w3Detail.Add($"{rel}:{(int)resp.StatusCode}:{ct}:{body.Length}B:{(ok ? "ok" : "MISMATCH")}");
        Record("W3", rel, resp, body);
    }

    results.Row("W3", w3Ok, string.Join(" ", w3Detail) + $" cwd=empty-temp-dir distDir={Path.GetFileName(distDir)}");

    // W4
    var (fb, fbBody) = await Get(baseUrl + "/library/abc");
    var (miss, missBody) = await Get(baseUrl + "/missing.js");
    var (apiMiss, apiMissBody) = await Get(baseUrl + "/api/v1/unknown");
    Record("W4", "spa-fallback", fb, fbBody);
    Record("W4", "missing-js", miss, missBody);
    Record("W4", "api-unknown", apiMiss, apiMissBody);
    var indexBytes = File.ReadAllBytes(Path.Combine(distDir, "index.html"));
    results.Row("W4",
        fb.StatusCode == HttpStatusCode.OK && fbBody.AsSpan().SequenceEqual(indexBytes)
        && miss.StatusCode == HttpStatusCode.NotFound
        && apiMiss.StatusCode == HttpStatusCode.NotFound && (apiMiss.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.Ordinal) ?? false),
        $"/library/abc={(int)fb.StatusCode} /missing.js={(int)miss.StatusCode}:{miss.Content.Headers.ContentType?.MediaType} /api/v1/unknown={(int)apiMiss.StatusCode}:{apiMiss.Content.Headers.ContentType?.MediaType}");

    // W5
    var (b1, b1Body) = await Get(baseUrl + "/api/v1/books/B1");
    Record("W5", "get-book", b1, b1Body);
    var expectedBook = JsonNode.Parse("""
    {"id":"B1","title":"Event Streams in Practice","authors":["Jördis Ålander"],
     "formats":[{"kind":"pdf","sizeBytes":1048576,"pages":212},{"kind":"epub","sizeBytes":524288,"pages":null}],
     "tags":["tech/streams","reference"]}
    """);
    var gotBook = JsonNode.Parse(b1Body);
    var (nf, nfBody) = await Get(baseUrl + "/api/v1/books/NOPE");
    Record("W5", "get-book-unknown", nf, nfBody);
    var (ok1, ok1Body) = await Get(baseUrl + "/api/v1/books/B1/tags", method: HttpMethod.Put, content: new StringContent("""{"tags":["x","y"]}""", Encoding.UTF8, "application/json"));
    Record("W5", "put-tags", ok1, ok1Body);
    var (bad1, bad1Body) = await Get(baseUrl + "/api/v1/books/B1/tags", method: HttpMethod.Put, content: new StringContent("{bad", Encoding.UTF8, "application/json"));
    Record("W5", "put-tags-invalid-json", bad1, bad1Body);
    var (bad2, bad2Body) = await Get(baseUrl + "/api/v1/books/B1/tags", method: HttpMethod.Put, content: new StringContent("""{"tags":["ok",""]}""", Encoding.UTF8, "application/json"));
    Record("W5", "put-tags-empty-tag", bad2, bad2Body);
    bool IsJson(HttpResponseMessage r) => r.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.Ordinal) ?? false;
    results.Row("W5",
        b1.StatusCode == HttpStatusCode.OK && JsonNode.DeepEquals(gotBook, expectedBook)
        && nf.StatusCode == HttpStatusCode.NotFound && IsJson(nf) && JsonNode.Parse(nfBody)?["status"]?.GetValue<int>() == 404
        && ok1.StatusCode == HttpStatusCode.OK && JsonNode.Parse(ok1Body)?["tags"]?.ToJsonString() == """["x","y"]"""
        && bad1.StatusCode == HttpStatusCode.BadRequest && IsJson(bad1)
        && bad2.StatusCode == HttpStatusCode.BadRequest && IsJson(bad2),
        $"get={Text(b1Body)} unknown={(int)nf.StatusCode} put={Text(ok1Body)} invalidJson={(int)bad1.StatusCode}:{bad1.Content.Headers.ContentType?.MediaType} emptyTag={(int)bad2.StatusCode}:{bad2.Content.Headers.ContentType?.MediaType}");

    // W6
    var (full, fullBody) = await Get(baseUrl + "/api/v1/files/F1");
    var (r1, r1Body) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=0-99");
    var (r2, r2Body) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=500000-500999");
    var (r3, r3Body) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=-100");
    var (r4, r4Body) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=2000000-");
    var (r5, r5Body) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=1048000-");
    Record("W6", "full", full, fullBody);
    Record("W6", "range-0-99", r1, r1Body);
    Record("W6", "range-500000-500999", r2, r2Body);
    Record("W6", "range-suffix-100", r3, r3Body);
    Record("W6", "range-beyond-end", r4, r4Body);
    Record("W6", "range-open-end", r5, r5Body);
    var len = testBytes.Length;
    results.Row("W6",
        full.StatusCode == HttpStatusCode.OK && full.Headers.AcceptRanges.Contains("bytes") && fullBody.AsSpan().SequenceEqual(testBytes)
        && r1.StatusCode == HttpStatusCode.PartialContent && r1.Content.Headers.ContentRange?.ToString() == $"bytes 0-99/{len}" && r1Body.AsSpan().SequenceEqual(testBytes.AsSpan(0, 100))
        && r2.StatusCode == HttpStatusCode.PartialContent && r2.Content.Headers.ContentRange?.ToString() == $"bytes 500000-500999/{len}" && r2Body.AsSpan().SequenceEqual(testBytes.AsSpan(500000, 1000))
        && r3.StatusCode == HttpStatusCode.PartialContent && r3.Content.Headers.ContentRange?.ToString() == $"bytes {len - 100}-{len - 1}/{len}" && r3Body.AsSpan().SequenceEqual(testBytes.AsSpan(len - 100))
        && r4.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable
        && r5.StatusCode == HttpStatusCode.PartialContent && r5Body.AsSpan().SequenceEqual(testBytes.AsSpan(1048000)),
        $"full={(int)full.StatusCode}:{fullBody.Length}B 0-99={(int)r1.StatusCode}:{r1.Content.Headers.ContentRange} middle={(int)r2.StatusCode}:{r2.Content.Headers.ContentRange} suffix={(int)r3.StatusCode}:{r3.Content.Headers.ContentRange} beyond={(int)r4.StatusCode} openEnd={(int)r5.StatusCode}:{r5.Content.Headers.ContentRange}");
    // Observation only: multiple ranges.
    var (rm, rmBody) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=0-9,20-29");
    Console.WriteLine($"observed W6-multirange: status={(int)rm.StatusCode} contentType={rm.Content.Headers.ContentType} bodyBytes={rmBody.Length}");

    // W9
    var (oa, oaBody) = await Get(baseUrl + "/openapi/v1.json");
    JsonNode? doc = null;
    try
    {
        doc = JsonNode.Parse(oaBody);
    }
    catch (JsonException)
    {
    }

    var paths = doc?["paths"]?.AsObject();
    var bookPath = paths?["/api/v1/books/{id}"];
    var tagsPath = paths?["/api/v1/books/{id}/tags"];
    var filePath = paths?["/api/v1/files/{id}"];
    var schemas = doc?["components"]?["schemas"]?.AsObject();
    results.Row("W9",
        oa.StatusCode == HttpStatusCode.OK && doc?["openapi"] is not null
        && bookPath?["get"] is not null && bookPath["get"]!["responses"]?["404"] is not null
        && tagsPath?["put"] is not null && filePath?["get"] is not null
        && schemas is not null && schemas.ContainsKey("Book"),
        $"status={(int)oa.StatusCode} openapi={doc?["openapi"]} paths=[{string.Join(",", paths?.Select(p => p.Key) ?? [])}] schemas=[{string.Join(",", schemas?.Select(p => p.Key) ?? [])}] bookGetResponses=[{string.Join(",", bookPath?["get"]?["responses"]?.AsObject().Select(p => p.Key) ?? [])}]");
    // The document's `servers` entry holds the request's own base URL with the random port; it is left out of the dump.
    var docForDump = doc?.DeepClone();
    docForDump?.AsObject().Remove("servers");
    dump.Add(new JsonObject { ["row"] = "W9", ["label"] = "openapi", ["status"] = (int)oa.StatusCode, ["body"] = docForDump });

    // W10: stdout empty, stderr carries the startup line (and nothing alarming).
    await Task.Delay(300);
    var stderr = server.Stderr;
    results.Row("W10", server.Stdout.Length == 0 && stderr.Contains("listening on http://127.0.0.1:", StringComparison.Ordinal),
        $"stdoutBytes={server.Stdout.Length} stderrLines={stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length}");
    Console.WriteLine("observed W10-stderr: " + stderr.Replace("\n", " | ").Trim());
}

// ---------------------------------------------------------------- W7: bind rules
using (var server = ServerProc.Start(command, ["--port", "0", "--file", testFile]))
{
    var url = await server.WaitForUrl();
    var uri = new Uri(url);
    var loopbackOk = (await Get(url + "/api/v1/books/B1")).Resp.StatusCode == HttpStatusCode.OK;
    var other = NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Select(a => a.Address)
        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
    string otherDetail;
    bool otherRefused = true;
    if (other is null)
    {
        otherDetail = "no non-loopback IPv4 interface to probe";
    }
    else
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await tcp.ConnectAsync(other, uri.Port, cts.Token);
            otherRefused = false;
            otherDetail = $"connection to {other}:{uri.Port} SUCCEEDED";
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException)
        {
            otherDetail = $"connection to {other}:{uri.Port} refused";
        }
    }

    var bindNoToken = ServerProc.Start(command, ["--bind", "0.0.0.0", "--port", "0", "--file", testFile]);
    var exited = await bindNoToken.WaitForExit(TimeSpan.FromSeconds(15));
    var noTokenOk = exited && bindNoToken.ExitCode is not (0 or null) && bindNoToken.Stderr.Contains("--token", StringComparison.Ordinal) && !bindNoToken.Stderr.Contains("listening on", StringComparison.Ordinal);
    var noTokenDetail = $"exited={exited} exitCode={bindNoToken.ExitCode} stderr='{bindNoToken.Stderr.Trim()}'";
    bindNoToken.Dispose();

    var badBind = ServerProc.Start(command, ["--bind", "localhost.example", "--port", "0"]);
    var badExited = await badBind.WaitForExit(TimeSpan.FromSeconds(15));
    var badOk = badExited && badBind.ExitCode is not (0 or null) && !badBind.Stderr.Contains("listening on", StringComparison.Ordinal);
    var badDetail = $"exited={badExited} exitCode={badBind.ExitCode} stderr='{badBind.Stderr.Trim()}'";
    badBind.Dispose();

    using var wide = ServerProc.Start(command, ["--bind", "0.0.0.0", "--token", Token, "--port", "0", "--file", testFile]);
    var wideUrl = await wide.WaitForUrl();
    var wideOk = wide.ExitCode is null && wideUrl.StartsWith("http://0.0.0.0:", StringComparison.Ordinal);

    results.Row("W7", uri.Host == "127.0.0.1" && loopbackOk && otherRefused && noTokenOk && badOk && wideOk,
        $"default={url} loopbackOk={loopbackOk} {otherDetail}; 0.0.0.0 without token: {noTokenDetail}; non-IP bind: {badDetail}; 0.0.0.0 with token: {wideUrl}");
}

// ---------------------------------------------------------------- W8: token
using (var server = ServerProc.Start(command, ["--token", Token, "--port", "0", "--file", testFile]))
{
    var baseUrl = await server.WaitForUrl();
    var (none, noneBody) = await Get(baseUrl + "/api/v1/books/B1");
    var (wrong, wrongBody) = await Get(baseUrl + "/api/v1/books/B1", bearer: "nope");
    var (good, goodBody) = await Get(baseUrl + "/api/v1/books/B1", bearer: Token);
    var (file, _) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=0-9");
    var (fileAuth, _) = await Get(baseUrl + "/api/v1/files/F1", range: "bytes=0-9", bearer: Token);
    var (ui, uiBody) = await Get(baseUrl + "/");
    var (apiMissNoToken, _) = await Get(baseUrl + "/api/v1/unknown");
    Record("W8", "no-header", none, noneBody);
    Record("W8", "wrong-token", wrong, wrongBody);
    Record("W8", "good-token", good, goodBody);
    Record("W8", "ui-public", ui, uiBody);
    results.Row("W8",
        none.StatusCode == HttpStatusCode.Unauthorized && (none.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.Ordinal) ?? false)
        && wrong.StatusCode == HttpStatusCode.Unauthorized
        && good.StatusCode == HttpStatusCode.OK
        && file.StatusCode == HttpStatusCode.Unauthorized && fileAuth.StatusCode == HttpStatusCode.PartialContent
        && ui.StatusCode == HttpStatusCode.OK && apiMissNoToken.StatusCode == HttpStatusCode.Unauthorized,
        $"noHeader={(int)none.StatusCode} wrong={(int)wrong.StatusCode} good={(int)good.StatusCode} fileNoToken={(int)file.StatusCode} fileToken={(int)fileAuth.StatusCode} ui={(int)ui.StatusCode} unknownApiNoToken={(int)apiMissNoToken.StatusCode} wwwAuthenticate={none.Headers.WwwAuthenticate}");
}

File.WriteAllText(dumpPath, dump.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"passed {results.Passed}/{results.Total}");
return results.Passed == results.Total ? 0 : 1;

internal sealed class Results
{
    public int Passed { get; private set; }

    public int Total { get; private set; }

    public void Row(string id, bool ok, string detail)
    {
        Total++;
        if (ok)
            Passed++;
        Console.WriteLine($"{(ok ? "pass" : "FAIL")} {id}: {detail}");
    }
}
