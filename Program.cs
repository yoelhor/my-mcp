// Program.cs
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging.AzureAppServices;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// The following line enables Application Insights telemetry collection.
builder.Services.AddApplicationInsightsTelemetry();

// Add Azure stream log service
builder.Logging.AddAzureWebAppDiagnostics();
builder.Services.Configure<AzureFileLoggerOptions>(options =>
{
    options.FileName = "azure-diagnostics-";
    options.FileSizeLimit = 50 * 1024;
    options.RetainedFileCountLimit = 5;
});

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "Welcome to my test MCP server. This MCP server is your comprehensive platform designed for efficient travel management. Gain access to specialized tools for seamless interaction with our customer service, including features for booking your next flight and securing exclusive hotel deals.",
        Title = "Yoel's MCP Server",
        Version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "1.0.0"
    };
})
    // Use HTTP transport
    .WithHttpTransport()

    // Register tools from the current assembly using the McpServerTool attribute
    .WithToolsFromAssembly();
var app = builder.Build();

// Add request logging middleware
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapMcp();

// Provide information about the server
app.MapGet("/info", (TelemetryClient telemetryClient) =>
{
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "Unknown";

    return new { Name = "Anonymous MCP Server", Version = version };
});

app.Run();

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"hello {message}";

    [McpServerTool, Description("Returns the length of a message.")]
    public static string ContentLength(string message) => $"Your message is {message.Length} characters long.";

    [McpServerTool, Description("Returns the MCP version.")]
    public static string GetVersion() => $"MCP (anonymous) Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} Server: {(string.IsNullOrEmpty(Environment.MachineName) ? "Unknown" : Environment.MachineName)}";
}