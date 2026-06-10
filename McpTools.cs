using System.ComponentModel;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Azure.Core;
using Azure.Storage.Blobs;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

// Custom TokenCredential implementation for static token
public class StaticTokenCredential : TokenCredential
{
    private readonly AccessToken _token;

    public StaticTokenCredential(AccessToken token)
    {
        _token = token;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return _token;
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new ValueTask<AccessToken>(_token);
    }
}

[McpServerToolType]
public static class McpTools
{
    private static ILogger _logger = null!;
    private static TelemetryClient _telemetry;
    private static IHttpContextAccessor _httpContextAccessor = null!;
    private static string _writeScope = "mymcp.write";
    private static string _username => _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "unknown";

    public static void Initialize(
        ILogger logger,
        TelemetryClient telemetry,
        IHttpContextAccessor httpContextAccessor,
        string writeScope)
    {
        _logger = logger;
        _telemetry = telemetry;
        _httpContextAccessor = httpContextAccessor;
        _writeScope = writeScope;
    }

    private static void Log(string toolName)
    {
        string authHeaderValue = "No Authorization header present";

        if (_httpContextAccessor.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            authHeaderValue = authHeader.ToString();
        }

        _logger.LogInformation($"*** The MCP tool '{toolName}' was called. Authorization header: {authHeaderValue}");
    }

    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(
        [Description("The message to echo back.")] string message)
    {
        Log("Echo");
        return $"hello {message}";
    }

    [McpServerTool, Description("Returns the length of a message.")]
    public static string ContentLength(
        [Description("The message to calculate the length of.")] string message)
    {
        Log("ContentLength");
        return $"Your message is {message.Length} characters long.";
    }

    [McpServerTool, Description("Returns the MCP version.")]
    public static string GetVersion()
    {
        Log("GetVersion");

        return $"""
            My MCP:
            Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}
            Server: {(string.IsNullOrEmpty(Environment.MachineName) ? "Unknown" : Environment.MachineName)}
            Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
            User: {_username}
            """;
    }

    [McpServerTool, Description("Add item to the shopping cart.")]
    // [Authorize(Policy = "RequireWriteScope")]
    public static string AddToCart(
        [Description("The item name to add to the shopping cart.")] string item)
    {
        Log("AddToCart");

        // Check for required scope in the token claims and return an appropriate message
        string message = HasWriteScope()
            ? $"Item '{item}' has been added to the shopping cart"
            : $"User does NOT have required scope '{_writeScope}'. Authorization will fail.";

        // Return the message along with additional context information
        return $"""
        {message}
        Item '{item}' has been added to the shopping cart.
        Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
        User: {_username}
        """;
    }

    [McpServerTool, Description("List containers in an Azure Blob Storage account.")]
    public static string ListBlobContainers()
    {
        Log("ListBlobContainers");
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            _logger.LogWarning("ListBlobContainers: No active HttpContext is available.");
            return "No active HttpContext is available.";
        }

        // Get Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            _logger.LogInformation("ListBlobContainers: Authorization Header found: {AuthHeader}", authHeader.ToString());
        }
        else
        {
            _logger.LogInformation("ListBlobContainers: No Authorization header present");
            return "No Authorization header present.";
        }

        // 1. Wrap your string token
        // The 'ExpiresOn' is required; set it to the token's actual expiry or a future offset
        var tokenValue = authHeader.ToString();
        if (tokenValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            tokenValue = tokenValue["Bearer ".Length..].Trim();
        }

        var token = new AccessToken(tokenValue, DateTimeOffset.UtcNow.AddHours(1));

        // 2. Create a Static Token Credential
        var credential = new StaticTokenCredential(token);

        var accountName = Environment.GetEnvironmentVariable("BlobStorageAccount");
        if (string.IsNullOrWhiteSpace(accountName))
        {
            _logger.LogWarning("ListBlobContainers: Missing environment variable 'BlobStorageAccount'.");
            return "Missing environment variable 'BlobStorageAccount'.";
        }

        try
        {
            // 3. Initialize the client
            var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
            var blobServiceClient = new BlobServiceClient(serviceUri, credential);

            // Concatenate container names into a single string
            var containerNames = new List<string>();
            foreach (var container in blobServiceClient.GetBlobContainers())
            {
                containerNames.Add(container.Name);
            }

            // Return the list of container names as a comma-separated string
            var result = string.Join(", ", containerNames);
            return $"Containers in Azure Blob Storage Account: {result}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListBlobContainers: Error while listing blob containers.");
            return ex.Message;
        }
    }

    private static bool HasWriteScope()
    {
        // Entra ID issues scopes as a space-delimited string in "scp".
        // Depending on claim mapping, it may appear under a schema URI.
        var tokenScopes = _httpContextAccessor.HttpContext?.User?.Claims
            .Where(claim =>
                string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase) ||
                claim.Type.EndsWith("/scope", StringComparison.OrdinalIgnoreCase) ||
                claim.Type.EndsWith("/scopes", StringComparison.OrdinalIgnoreCase))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return tokenScopes?.Contains(_writeScope, StringComparer.OrdinalIgnoreCase) == true;
    }
}