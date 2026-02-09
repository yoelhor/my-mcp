// Program.cs
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging.AzureAppServices;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// The following line enables Application Insights telemetry collection.
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddHttpContextAccessor();

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

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

EchoTool.Initialize(
    logger: loggerFactory.CreateLogger("EchoTool"), 
    telemetry: app.Services.GetRequiredService<TelemetryClient>(),
    httpContextAccessor: app.Services.GetRequiredService<IHttpContextAccessor>());

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