using System.ComponentModel;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Azure.Core;
using Azure.Storage.Blobs;
using System.Threading.Tasks;

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
        }

        // // 1. Wrap your string token
        // // The 'ExpiresOn' is required; set it to the token's actual expiry or a future offset
        // var token = new AccessToken(accessToken, DateTimeOffset.UtcNow.AddHours(1));

        // // 2. Create a Static Token Credential
        // var credential = TokenCredential.Create((tokenRequestContext, cancellationToken) => token);

        // // 3. Initialize the Client
        // var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
        // var blobServiceClient = new BlobServiceClient(serviceUri, credential);

        // // Now you can interact with the storage
        // var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        // Console.WriteLine($"Connected to container: {containerClient.Name}");

        return $"Containers in Azure Blob Storage Account: {(string.IsNullOrEmpty(Environment.MachineName) ? "Unknown" : Environment.MachineName)} MCP Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} Date: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";
    }
}