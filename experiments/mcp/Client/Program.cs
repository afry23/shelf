using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProtoClient;

// Raw newline-delimited JSON-RPC client over stdio (independent of the SDK, so it also checks the wire format).
// Usage: proto-mcp-client <dump-file> <server-command> [server-args...]
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: proto-mcp-client <dump-file> <server-command> [args...]");
    return 2;
}

var dumpPath = args[0];
var psi = new ProcessStartInfo(args[1])
{
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    StandardOutputEncoding = new UTF8Encoding(false),
    StandardInputEncoding = new UTF8Encoding(false),
};
foreach (var a in args.Skip(2))
    psi.ArgumentList.Add(a);

using var proc = Process.Start(psi)!;
var session = new Session(proc);
var results = new Results();
var dump = new JsonArray();

var init = await session.Call("initialize", new JsonObject
{
    ["protocolVersion"] = "2025-06-18",
    ["capabilities"] = new JsonObject(),
    ["clientInfo"] = new JsonObject { ["name"] = "proto-mcp-client", ["version"] = "0.0.1" },
});
session.Notify("notifications/initialized");
dump.Add(Normalize("initialize", init));

// M2
var r = init["result"];
results.Row("M2", r is not null
    && r["serverInfo"]?["name"]?.GetValue<string>() == "proto-mcp"
    && r["serverInfo"]?["version"]?.GetValue<string>() == "0.0.1"
    && r["capabilities"]?["tools"] is not null
    && r["capabilities"]?["resources"] is not null,
    $"serverInfo={r?["serverInfo"]} protocolVersion={r?["protocolVersion"]} capabilities={r?["capabilities"]}");

// M3
var list = await session.Call("tools/list", new JsonObject());
dump.Add(Normalize("tools/list", list));
var tools = list["result"]?["tools"]?.AsArray() ?? [];
var byName = tools.ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
var names = string.Join(",", byName.Keys.OrderBy(x => x, StringComparer.Ordinal));
var rp = byName.GetValueOrDefault("read_passage")?["inputSchema"];
var rpProps = rp?["properties"]?.AsObject();
var rpRequired = rp?["required"]?.AsArray().Select(x => x!.GetValue<string>()).ToArray() ?? [];
var maxChars = rpProps?["maxChars"];
var at = byName.GetValueOrDefault("add_tags")?["inputSchema"]?["properties"]?["tags"];
results.Row("M3",
    names == "add_tags,get_book,list_tags,read_passage"
    && rpProps is not null && rpProps.ContainsKey("id") && rpProps.ContainsKey("location") && rpProps.ContainsKey("maxChars")
    && rpRequired.Contains("id") && rpRequired.Contains("location") && !rpRequired.Contains("maxChars")
    && maxChars?["type"]?.GetValue<string>() == "integer"
    && maxChars?["default"]?.GetValue<int>() == 4000
    && at?["type"]?.GetValue<string>() == "array" && at?["items"]?["type"]?.GetValue<string>() == "string",
    $"tools={names} read_passage.required=[{string.Join(",", rpRequired)}] maxChars={maxChars?.ToJsonString()} add_tags.tags={at?.ToJsonString()}");
Console.WriteLine("observed M3-schemas: " + string.Join(" | ", byName.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}: in={k.Value["inputSchema"]?.ToJsonString()} out={k.Value["outputSchema"]?.ToJsonString() ?? "none"}")));

// M4
var gb = await session.Call("tools/call", Call("get_book", new JsonObject { ["id"] = "B1" }));
dump.Add(Normalize("get_book", gb));
var expectedBook = JsonNode.Parse("""
{"id":"B1","title":"Event Streams in Practice","authors":["Jördis Ålander"],
 "formats":[{"kind":"pdf","sizeBytes":1048576,"pages":212},{"kind":"epub","sizeBytes":524288,"pages":null}],
 "tags":["tech/streams","reference"]}
""");
// The SDK's default serializer options omit null properties, so `pages` is absent for the EPUB in structuredContent
// (the output schema still lists it as required); the resource text below comes from the generated context and keeps the null.
var expectedBookStructured = JsonNode.Parse("""
{"id":"B1","title":"Event Streams in Practice","authors":["Jördis Ålander"],
 "formats":[{"kind":"pdf","sizeBytes":1048576,"pages":212},{"kind":"epub","sizeBytes":524288}],
 "tags":["tech/streams","reference"]}
""");
var gbStructured = gb["result"]?["structuredContent"];
var gbRequired = byName.GetValueOrDefault("get_book")?["outputSchema"]?["properties"]?["formats"]?["items"]?["required"]?.AsArray().Select(x => x!.GetValue<string>()).ToArray() ?? [];
results.Row("M4", gb["result"]?["isError"]?.GetValue<bool>() != true && JsonNode.DeepEquals(gbStructured, expectedBookStructured),
    $"structuredContent={gbStructured?.ToJsonString()}");
Console.WriteLine($"observed M4-null-vs-schema: structuredContent omits null 'pages' for the EPUB; outputSchema formats.items.required=[{string.Join(",", gbRequired)}]");

// M5
var p1 = await session.Call("tools/call", Call("read_passage", new JsonObject { ["id"] = "B1", ["location"] = "p17" }));
var p2 = await session.Call("tools/call", Call("read_passage", new JsonObject { ["id"] = "B1", ["location"] = "p17", ["maxChars"] = 100 }));
var p3 = await session.Call("tools/call", Call("read_passage", new JsonObject { ["id"] = "B1", ["location"] = "p17", ["maxChars"] = 30000 }));
dump.Add(Normalize("read_passage default", p1));
dump.Add(Normalize("read_passage 100", p2));
dump.Add(Normalize("read_passage 30000", p3));
string Text(JsonNode n) => n["result"]?["content"]?[0]?["text"]?.GetValue<string>() ?? "";
const string prefix = "[B1@p17]";
results.Row("M5",
    Text(p1).Length == prefix.Length + 4000 && Text(p1).StartsWith(prefix, StringComparison.Ordinal)
    && Text(p2).Length == prefix.Length + 100
    && p3["result"]?["isError"]?.GetValue<bool>() == true && p3["error"] is null && Text(p3).Contains("20000"),
    $"default.len={Text(p1).Length} 100.len={Text(p2).Length} 30000.isError={p3["result"]?["isError"]} text='{Text(p3)}'");

// M6
var lt = await session.Call("tools/call", Call("list_tags", new JsonObject()));
dump.Add(Normalize("list_tags", lt));
var expectedTags = JsonNode.Parse("""
{"name":"root","count":9,"children":[
 {"name":"tech","count":7,"children":[{"name":"streams","count":4,"children":[]},{"name":"databases","count":3,"children":[]}]},
 {"name":"reference","count":2,"children":[]}]}
""");
var at2 = await session.Call("tools/call", Call("add_tags", new JsonObject { ["id"] = "B1", ["tags"] = new JsonArray("reference", "new") }));
dump.Add(Normalize("add_tags", at2));
var expectedAdd = JsonNode.Parse("""{"id":"B1","tags":["tech/streams","reference","new"],"added":1}""");
results.Row("M6",
    JsonNode.DeepEquals(lt["result"]?["structuredContent"], expectedTags) && JsonNode.DeepEquals(at2["result"]?["structuredContent"], expectedAdd),
    $"list_tags={lt["result"]?["structuredContent"]?.ToJsonString()} add_tags={at2["result"]?["structuredContent"]?.ToJsonString()}");

// M7
var unknown = await session.Call("tools/call", Call("no_such_tool", new JsonObject()));
var badType = await session.Call("tools/call", Call("read_passage", new JsonObject { ["id"] = "B1", ["location"] = "p1", ["maxChars"] = "abc" }));
var after = await session.Call("tools/list", new JsonObject());
dump.Add(Normalize("unknown tool", unknown));
dump.Add(Normalize("bad argument type", badType));
bool IsErr(JsonNode n) => n["error"] is not null || n["result"]?["isError"]?.GetValue<bool>() == true;
results.Row("M7", IsErr(unknown) && IsErr(badType) && after["result"]?["tools"]?.AsArray().Count == 4 && !proc.HasExited,
    $"unknown={Summary(unknown)} badType={Summary(badType)} nextRequestTools={after["result"]?["tools"]?.AsArray().Count}");

// M8
var tpl = await session.Call("resources/templates/list", new JsonObject());
dump.Add(Normalize("resources/templates/list", tpl));
var uris = (tpl["result"]?["resourceTemplates"]?.AsArray() ?? []).Select(t => t!["uriTemplate"]!.GetValue<string>()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
var rb = await session.Call("resources/read", new JsonObject { ["uri"] = "shelf://book/B1" });
var rt = await session.Call("resources/read", new JsonObject { ["uri"] = "shelf://book/B1/toc" });
var rx = await session.Call("resources/read", new JsonObject { ["uri"] = "shelf://book/NOPE" });
dump.Add(Normalize("resources/read book", rb));
dump.Add(Normalize("resources/read toc", rt));
dump.Add(Normalize("resources/read unknown", rx));
var rbText = JsonNode.Parse(rb["result"]?["contents"]?[0]?["text"]?.GetValue<string>() ?? "null");
var rtText = JsonNode.Parse(rt["result"]?["contents"]?[0]?["text"]?.GetValue<string>() ?? "null");
var expectedToc = JsonNode.Parse("""[{"title":"Introduction","page":1},{"title":"Logs and Streams","page":17},{"title":"Backpressure","page":88}]""");
results.Row("M8",
    string.Join(",", uris) == "shelf://book/{id},shelf://book/{id}/toc"
    && JsonNode.DeepEquals(rbText, expectedBook) && JsonNode.DeepEquals(rtText, expectedToc)
    && rx["error"] is not null,
    $"templates=[{string.Join(",", uris)}] unknown={Summary(rx)}");

// M9
proc.StandardInput.Close();
await proc.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
var stderr = await proc.StandardError.ReadToEndAsync();
results.Row("M9",
    session.StdoutLines > 0 && session.NonJsonRpcStdoutLines.Count == 0 && stderr.Contains("read_passage id=B1 location=p17 maxChars=100", StringComparison.Ordinal),
    $"stdoutLines={session.StdoutLines} nonJsonRpc={session.NonJsonRpcStdoutLines.Count} stderrBytes={stderr.Length} exit={proc.ExitCode}");
foreach (var bad in session.NonJsonRpcStdoutLines.Take(3))
    Console.WriteLine($"observed M9-bad-stdout: {bad}");

File.WriteAllText(dumpPath, dump.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"passed {results.Passed}/{results.Total}");
return results.Passed == results.Total ? 0 : 1;

static JsonObject Call(string name, JsonObject arguments) => new() { ["name"] = name, ["arguments"] = arguments };

static string Summary(JsonNode n) =>
    n["error"] is { } e ? $"error({e["code"]}:{e["message"]})" : $"result.isError={n["result"]?["isError"]}";

// The response without the client-chosen id; the dump is compared between the AOT and the JIT server.
// The order of `tools` and `resourceTemplates` is not stable between runs of the same build (observed, ADR 0006),
// so both lists are sorted by name here; everything else is compared as sent.
static JsonNode Normalize(string label, JsonNode response)
{
    var copy = response.DeepClone().AsObject();
    copy.Remove("id");
    foreach (var key in new[] { "tools", "resourceTemplates" })
    {
        if (copy["result"]?[key] is JsonArray list)
        {
            var sorted = list.Select(x => x!.DeepClone()).OrderBy(x => x["name"]!.GetValue<string>(), StringComparer.Ordinal).ToArray();
            copy["result"]![key] = new JsonArray(sorted);
        }
    }

    return new JsonObject { ["step"] = label, ["response"] = copy };
}
