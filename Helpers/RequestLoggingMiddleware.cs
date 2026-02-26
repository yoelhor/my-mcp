using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly TelemetryClient _telemetry;
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, TelemetryClient telemetry)
    {
        _next = next;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log request information
        string page = "", method = "", authorizationHeader = "", requestBody = "";

        // Get Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            authorizationHeader = authHeader.ToString();
        }
        else
        {
            authorizationHeader = "No Authorization header present";
        }

        // Get the page name
        if (context.Request.Method == "POST")
        {

            // Get the request body as a string
            context.Request.EnableBuffering();
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            // If the page is "/" and the type is POST, then set page to "MCP tool"
            page = "MCP tool";

            // Get the MCP method name from the body
            try
            {
                var bodyJson = System.Text.Json.JsonDocument.Parse(requestBody);
                if (bodyJson.RootElement.TryGetProperty("method", out var methodElement))
                {
                    method = methodElement.GetString() ?? "Unknown method";
                }
            }
            catch (System.Text.Json.JsonException)
            {
                page = "Unknown page";
            }
        }
        else
        {
            page = "Unknown page";
        }

        _logger.LogInformation("*** Request Path: {RequestPath}, Method: {RequestMethod}", context.Request.Path, context.Request.Method);
        _logger.LogInformation("*** Request Body: {RequestBody}", requestBody);
        _logger.LogInformation("*** Request Authorization Header: {AuthHeader}", authorizationHeader);
        _logger.LogInformation("*** Request MCP Method: {McpMethod}", method);

        await _next(context);
    }
}
