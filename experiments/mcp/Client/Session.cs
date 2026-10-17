using System.Diagnostics;
using System.Text.Json.Nodes;

namespace ProtoClient;

internal sealed class Session(Process proc)
{
    private int _nextId;

    public int StdoutLines { get; private set; }

    public List<string> NonJsonRpcStdoutLines { get; } = [];

    public async Task<JsonNode> Call(string method, JsonNode parameters)
    {
        var id = ++_nextId;
        var request = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters };
        await proc.StandardInput.WriteLineAsync(request.ToJsonString());
        await proc.StandardInput.FlushAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var line = await proc.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new InvalidOperationException("server closed stdout");
            StdoutLines++;
            JsonNode? node = null;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (System.Text.Json.JsonException)
            {
            }

            if (node is not JsonObject obj || obj["jsonrpc"]?.GetValue<string>() != "2.0")
            {
                NonJsonRpcStdoutLines.Add(line);
                continue;
            }

            // Notifications and server requests carry no matching id; skip them.
            if (obj["method"] is not null || obj["id"]?.GetValue<int>() != id)
                continue;
            return obj;
        }
    }

    public void Notify(string method)
    {
        var n = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
        proc.StandardInput.WriteLine(n.ToJsonString());
        proc.StandardInput.Flush();
    }
}

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
