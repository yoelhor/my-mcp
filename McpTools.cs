using System.ComponentModel;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Azure.Core;
using Azure.Storage.Blobs;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using my_mcp_demo.Auth;
using System.Text.Json;

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
    private static IConfiguration _configuration = null!;
    private static ILogger _logger = null!;
    private static TelemetryClient _telemetry;
    private static IHttpContextAccessor _httpContextAccessor = null!;
    private static IOnBehalfOfTokenService _oboTokenService = null!;
    private static string _writeScope = string.Empty;
    private static string _username => _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "unknown";

    public static void Initialize(
        IConfiguration configuration,
        ILogger logger,
        TelemetryClient telemetry,
        IHttpContextAccessor httpContextAccessor,
        IOnBehalfOfTokenService oboTokenService)
    {
        _configuration = configuration;
        _logger = logger;
        _telemetry = telemetry;
        _httpContextAccessor = httpContextAccessor;
        _oboTokenService = oboTokenService;
        _writeScope = configuration.GetSection("Mcp:Scopes:WriteScope")?.Value ?? "mymcp.write";
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
    public static async Task<string> ListBlobContainers()
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

        var inboundToken = authHeader.ToString();
        if (inboundToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            inboundToken = inboundToken["Bearer ".Length..].Trim();
        }

        var accountName = _configuration["Mcp:DownstreamApis:AzureStorageAccount:AccountName"];
        if (string.IsNullOrWhiteSpace(accountName))
        {
            _logger.LogWarning("Missing configuration for Azure Storage account name. Check Mcp:DownstreamApis:AzureStorageAccount:AccountName.");
            return "Missing configuration for Azure Storage account name.";
        }

        // Log the Storage account name being accessed.
        _logger.LogInformation("Azure Storage account '{AccountName}'", accountName);

        try
        {
            // Exchange incoming token (aud = this API) for downstream token (aud = storage).
            var (accessToken, expiresOn) = await _oboTokenService.GetAccessTokenForUserAsync(inboundToken);
            var credential = new StaticTokenCredential(new AccessToken(accessToken, expiresOn));

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

    [McpServerTool, Description("Get finance report of Contoso company.")]
    public static async Task<string> GetFinanceReport()
    {
        Log("GetFinanceReport");

        // Obtain an access token from the the sidecar API (this API) to call the downstream Azure Storage API.
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            _logger.LogWarning("GetFinanceReport: No active HttpContext is available.");
            return "No active HttpContext is available.";
        }

        // Read the sidecar API URL from configuration
        var sideCarApiUrl = _configuration["Mcp:DownstreamApis:AzureStorageAccount:AppOnlyAccessSidecarApi"];
        if (string.IsNullOrWhiteSpace(sideCarApiUrl))
        {
            _logger.LogWarning("Missing configuration for Sidecar API URL. Check Mcp:DownstreamApis:AzureStorageAccount:AppOnlyAccessSidecarApi.");
            return "Missing configuration for Sidecar API URL.";
        }

        // Make a  HTTP GET call and read the response body as a string. No, authorization header is sent to the sidecar API, as it is unauthenticated.
        using var httpClient = new HttpClient();
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(sideCarApiUrl);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFinanceReport: Error while calling Sidecar API.");
            return $"Error while calling Sidecar API: {ex.Message}";
        }

        // Read the response body as a string
        var accessToken = await response.Content.ReadAsStringAsync();

        // Get the access token value from the JSON string. 
        // The format may vary based on your sidecar API implementation.
        // {"authorizationHeader":"Bearer eyJ0eXAi"}
        try
        {
            var jsonDoc = JsonDocument.Parse(accessToken);
            if (jsonDoc.RootElement.TryGetProperty("authorizationHeader", out var authHeaderElement))
            {
                accessToken = authHeaderElement.GetString() ?? string.Empty;
            }
            else
            {
                _logger.LogWarning("GetFinanceReport: 'authorizationHeader' property not found in Sidecar API response.");
                return "Invalid response from Sidecar API: 'authorizationHeader' property not found.";
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "GetFinanceReport: Error parsing JSON response from Sidecar API.");
            return $"Error parsing JSON response from Sidecar API: {ex.Message}";
        }

        // Remove the "Bearer " prefix if it exists
        if (accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            accessToken = accessToken["Bearer ".Length..].Trim();
        }

        // Log the access token
        _logger.LogInformation("GetFinanceReport: Access token obtained from Sidecar API: {AccessToken}", accessToken);

        // Get the Azure Storage account name from configuration
        var accountName = _configuration["Mcp:DownstreamApis:AzureStorageAccount:AccountName"];
        if (string.IsNullOrWhiteSpace(accountName))
        {
            _logger.LogWarning("Missing configuration for Azure Storage account name. Check Mcp:DownstreamApis:AzureStorageAccount:AccountName.");
            return "Missing configuration for Azure Storage account name.";
        }

        try
        {
            // Convert the access token string to an AccessToken object with an expiration time.
            var credential = new StaticTokenCredential(new AccessToken(accessToken, DateTimeOffset.UtcNow.AddHours(1)));

            var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
            var blobServiceClient = new BlobServiceClient(serviceUri, credential);

            // Get the finance report blob from a specific container and blob name
            var containerClient = blobServiceClient.GetBlobContainerClient("finance-reports");
            var blobClient = containerClient.GetBlobClient("Contoso-Finance-Report-2024-2026.md");
            var downloadInfo = await blobClient.DownloadAsync();
            using (var reader = new StreamReader(downloadInfo.Value.Content))
            {
                var content = await reader.ReadToEndAsync();
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFinanceReport: Error while retrieving finance report.");
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