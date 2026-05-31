// Program.cs
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);
var scopesSupported = builder.Configuration.GetSection("Mcp:Scopes").Get<string[]>() ?? ["mcp:tools"];
var McpUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");

// If the McpUrl is not set, default to localhost with the current port
if (string.IsNullOrEmpty(McpUrl))
{
    var port = builder.Configuration["PORT"] ?? "5117";
    McpUrl = $"http://localhost:{port}";
}
else
{
    McpUrl = $"https://{McpUrl}/";
}


// Authentication configuration based on https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/ProtectedMcpServer/Program.cs
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Configure to validate tokens from our in-memory OAuth server
    options.Authority = builder.Configuration["Mcp:Authority"];
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidAudience = builder.Configuration["Mcp:Audience"], // Validate that the audience matches the resource metadata as suggested in RFC 8707
        ValidIssuer = builder.Configuration["Mcp:Authority"]
    };

})
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        AuthorizationServers = { builder.Configuration["Mcp:Authority"] ?? string.Empty },
        Resource = McpUrl,
        ScopesSupported = scopesSupported,
    };
});

builder.Services.AddAuthorization();


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
        Title = "Custom MCP Server demo",
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


app.MapMcp();

// Add request logging middleware
app.UseMiddleware<RequestLoggingMiddleware>();

// Provide information about the server
app.MapGet("/info", (TelemetryClient telemetryClient) =>
{
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "Unknown";
        
    string date = $"Date: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";
    
    return new { Name = "Anonymous MCP Server", Version = version, Date = date };
});

app.Run();