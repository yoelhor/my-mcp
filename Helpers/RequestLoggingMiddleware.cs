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
        string page = "", method = "", requestBody = "";
        bool isAuthorizationHeaderProvided = false;


        // Get Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            _logger.LogInformation("Authorization Header found: {AuthHeader}", authHeader.ToString());
            isAuthorizationHeaderProvided = true;
        }
        else
        {
            _logger.LogInformation("No Authorization header present");
        }

        // Get the x-custom-thread-id header
        string sessionId = null;
        if (context.Request.Headers.TryGetValue("x-custom-thread-id", out var sessionIdHeader))
        {
            sessionId = sessionIdHeader.ToString();
            _logger.LogInformation("x-custom-thread-id Header found: {SessionId}", sessionId);
        }
        else
        {
            _logger.LogInformation("No x-custom-thread-id header present");
        }

        // Get the request body
        context.Request.EnableBuffering(); // Allow reading body multiple times

        if (context.Request.ContentLength > 0)
        {
            using var reader = new StreamReader(
                context.Request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reset stream position

            // Track the full request using the logger
            _logger.LogInformation("Request Body: {RequestBody}", requestBody);
        }

        // Get the page name
        if (context.Request.Path.HasValue && context.Request.Path.Value != "/")
        {
            page = context.Request.Path;
        }
        else if (context.Request.Path.HasValue && context.Request.Path.Value == "/" && context.Request.Method == "POST")
        {
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

        // Track the request using Application Insights
        // We only track page views for the root path and /info
        if (context.Request.Path.Value == "/" || context.Request.Path.Value == "/info")
        {
            PageViewTelemetry pageView = new PageViewTelemetry(page);

            // The MCP method
            if (!string.IsNullOrEmpty(method))
            {
                pageView.Properties.Add("McpMethod", method);
            }

            // The agent session ID
            if (!string.IsNullOrEmpty(sessionId))
            {
                pageView.Properties.Add("AgentThreadId", sessionId);
            }
            
            // Whether the Authorization header was provided
            pageView.Properties.Add("BearerTokenPresent", isAuthorizationHeaderProvided.ToString());

            _telemetry.TrackPageView(pageView);
        }

        await _next(context);
    }
}
