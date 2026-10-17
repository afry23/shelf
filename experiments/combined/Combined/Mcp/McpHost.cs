using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Proto.Mcp;

/// <summary>The stdio MCP server of experiments/mcp (ADR 0006), unchanged apart from being a method.</summary>
internal static class McpHost
{
    public static async Task<int> RunAsync()
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(new StderrLoggerProvider()));

        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in Tools.Create())
            toolCollection.Add(tool);

        var resourceCollection = new McpServerResourceCollection();
        foreach (var resource in Resources.Create())
            resourceCollection.Add(resource);

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "proto-mcp", Version = "0.0.1" },
            ToolCollection = toolCollection,
            ResourceCollection = resourceCollection,
        };

        var transport = new StdioServerTransport("proto-mcp", loggerFactory);
        await using var server = McpServer.Create(transport, options, loggerFactory);
        await server.RunAsync();
        return 0;
    }
}
