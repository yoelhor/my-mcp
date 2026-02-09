using System.ComponentModel;
using Microsoft.ApplicationInsights;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class EchoTool
{
    private static ILogger _logger = null!;
    private static TelemetryClient _telemetry;

    public static void Initialize(ILogger logger, TelemetryClient telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
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

        //_logger.LogInformation("No Authorization header present");

        return $"Containers in Azure Blob Storage Account: {(string.IsNullOrEmpty(Environment.MachineName) ? "Unknown" : Environment.MachineName)} MCP Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} Date: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";
    }
}