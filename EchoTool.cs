using System.ComponentModel;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Azure.Core;
using Azure.Storage.Blobs;
using System.Threading.Tasks;

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
public static class EchoTool
{
    private static ILogger _logger = null!;
    private static TelemetryClient _telemetry;
    private static IHttpContextAccessor _httpContextAccessor = null!;

    public static void Initialize(
        ILogger logger,
        TelemetryClient telemetry,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _telemetry = telemetry;
        _httpContextAccessor = httpContextAccessor;

    }

    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"hello {message}";

    [McpServerTool, Description("Returns the length of a message.")]
    public static string ContentLength(string message) => $"Your message is {message.Length} characters long.";

    [McpServerTool, Description("Returns the MCP version.")]
    public static string GetVersion() => $"MCP (anonymous) Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} Server: {(string.IsNullOrEmpty(Environment.MachineName) ? "Unknown" : Environment.MachineName)} Date: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";

    [McpServerTool, Description("Add iteme to the shoping cart.")]
    public static string AddToCart(string item) => $"Item '{item}' added to the shopping cart ({(string.IsNullOrEmpty(Environment.MachineName) ? "Unknown" : Environment.MachineName)}).";


    [McpServerTool, Description("List counteiners in an Azure Blob Storage account.")]
    public static string ListBlobContainers()
    {

        _logger.LogInformation("ListBlobContainers called...");

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

        var accountName = Environment.GetEnvironmentVariable("accountName");
        if (string.IsNullOrWhiteSpace(accountName))
        {
            _logger.LogWarning("ListBlobContainers: Missing environment variable 'accountName'.");
            return "Missing environment variable 'accountName'.";
        }

        // 3. Initialize the Client
        var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
        var blobServiceClient = new BlobServiceClient(serviceUri, credential);

        // Now you can interact with the storage
        var containerClient = blobServiceClient.GetBlobContainerClient("$root");
        
        // Contacinate container names into a single string
        var containerNames = new List<string>();    
        foreach (var container in blobServiceClient.GetBlobContainers())
        {
            containerNames.Add(container.Name);
        }

        // Return the list of container names as a comma-separated string
        var result = string.Join(", ", containerNames);   
        return $"Containers in Azure Blob Storage Account: {result}";
    }
}