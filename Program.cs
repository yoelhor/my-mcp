// Program.cs
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.IdentityModel.Tokens;
using my_mcp_demo.Auth;
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

// Get required write role (fallback to legacy scope key, then default).
var requiredWriteRole =
    builder.Configuration.GetSection("Mcp:Roles:WriteRole")?.Value ??
    builder.Configuration.GetSection("Mcp:Scopes:WriteScope")?.Value ??
    "mymcp.write";

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
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = builder.Configuration["Mcp:Audience"], // Validate that the audience matches the resource metadata as suggested in RFC 8707
        ValidIssuer = builder.Configuration["Mcp:Authority"],
        NameClaimType = "name",
        RoleClaimType = "roles"
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
    options.AddPolicy("RequireReadRole", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {

            string requiredReadRole = builder.Configuration["Mcp:AppRoles:ReadRole"] ?? "mymcp.readonly";

            // Inspect the "roles" claim directly.
            bool hasRequiredRole = context.User.Claims.Any(claim =>
                (claim.Type == "roles" ||
                 claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") &&
                string.Equals(claim.Value, requiredReadRole, StringComparison.OrdinalIgnoreCase));

            if (!hasRequiredRole)
            {
                Console.WriteLine($"Authorization failed. Required role '{requiredReadRole}' not found in token.");
            }

            return hasRequiredRole;
        });
    });

    options.AddPolicy("RequireWriteRole", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireAssertion(context =>
    {

        string requiredWriteRole = builder.Configuration["Mcp:AppRoles:WriteRole"] ?? "mymcp.readWrite";

        // Inspect the "roles" claim directly.
        bool hasRequiredRole = context.User.Claims.Any(claim =>
            (claim.Type == "roles" ||
             claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") &&
            string.Equals(claim.Value, requiredWriteRole, StringComparison.OrdinalIgnoreCase));

        if (!hasRequiredRole)
        {
            Console.WriteLine($"Authorization failed. Required role '{requiredWriteRole}' not found in token.");
        }

        return hasRequiredRole;
    });
});


// The following line enables Application Insights telemetry collection.
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IOnBehalfOfTokenService, OnBehalfOfTokenService>();

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
    configuration: app.Configuration,
    logger: loggerFactory.CreateLogger("McpTools"),
    telemetry: app.Services.GetRequiredService<TelemetryClient>(),
    httpContextAccessor: app.Services.GetRequiredService<IHttpContextAccessor>(),
    oboTokenService: app.Services.GetRequiredService<IOnBehalfOfTokenService>()
);


app.MapMcp().RequireAuthorization();

// Add request logging middleware
app.UseMiddleware<RequestLoggingMiddleware>();

// Add custom endpoints for info and app access
app.MapInfoEndpoints();


app.Run();