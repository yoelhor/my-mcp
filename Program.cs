// Program.cs
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// Get the list of scopes from configuration at Mcp:Scopes 
var scopesSupported = builder.Configuration.GetSection("Mcp:Scopes")
    .GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();

// Get write scope from flattened scopes list (fallback to default if not configured).
var writeScope = builder.Configuration.GetSection("Mcp:Scopes:WriteScope")?.Value ?? "mymcp.write";

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

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            // var name = context.Principal?.Identity?.Name ?? "unknown";
            // var email = context.Principal?.FindFirstValue("preferred_username") ?? "unknown";
            // Console.WriteLine($"Token validated for: {name} ({email})");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"Challenging client to authenticate with Entra ID");
            return Task.CompletedTask;
        }
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireWriteScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            // Entra ID issues scopes as a space-delimited string in "scp".
            // Depending on claim mapping, it may appear under a schema URI.
            var tokenScopes = context.User.Claims
                .Where(claim =>
                    string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase) ||
                    claim.Type.EndsWith("/scope", StringComparison.OrdinalIgnoreCase) ||
                    claim.Type.EndsWith("/scopes", StringComparison.OrdinalIgnoreCase))
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            bool hasWriteScope = tokenScopes.Contains(writeScope, StringComparer.OrdinalIgnoreCase);

            if (!hasWriteScope)
            {
                Console.WriteLine($"Authorization failed. Required scope '{writeScope}' not found in token.");
            }

            return hasWriteScope;
        });
    });
});


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

    // Required when tools have authorization metadata (for example [Authorize]).
    .AddAuthorizationFilters()

    // Register tools from the current assembly using the McpServerTool attribute
    .WithToolsFromAssembly();
var app = builder.Build();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

McpTools.Initialize(
    logger: loggerFactory.CreateLogger("McpTools"),
    telemetry: app.Services.GetRequiredService<TelemetryClient>(),
    httpContextAccessor: app.Services.GetRequiredService<IHttpContextAccessor>());


app.MapMcp().RequireAuthorization();

// Add request logging middleware
app.UseMiddleware<RequestLoggingMiddleware>();

// Demostrate public endpoint that returns server information, without authentication and authorization
app.MapGet("/Info", (TelemetryClient telemetryClient) =>
{
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "Unknown";

    string date = $"Date: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";

    return new { Name = "Anonymous MCP Server", Version = version, Date = date };
});

// Demostrate app only access endpoint that returns server information, protected by authentication and authorization
app.MapGet("/App", (TelemetryClient telemetryClient) =>
{
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "Unknown";

    string date = $"Date: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";

    return new { Name = "MCP Server app only access", Version = version, Date = date };
}).RequireAuthorization();

// Demostrate obo access endpoint that returns server information, protected by authentication and authorization
app.MapGet("/obo", (TelemetryClient telemetryClient) =>
{
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "Unknown";

    string date = $"Date: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";

    return new { Name = "MCP Server OBO access", Version = version, Date = date };
}).RequireAuthorization("RequireWriteScope");


app.Run();