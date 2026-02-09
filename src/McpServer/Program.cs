using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MigrationToolkit.McpServer;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// R-MCP-02: All logging to stderr, never stdout (MCP protocol uses stdout)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<SpyClient>();
builder.Services.AddSingleton<ScopeRegistry>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
