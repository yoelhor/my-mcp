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
        string method = "", authorizationHeader = "", requestBody = "";

        // Log all HTTP request headers
        foreach (var header in context.Request.Headers)
        {
            _logger.LogInformation("*** Request Header: {HeaderName} = {HeaderValue}", header.Key, header.Value.ToString());
        }


        // Get Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            authorizationHeader = authHeader.ToString();
        }
        else
        {
            authorizationHeader = "No Authorization header present";
        }

        // Get the type of request (e.g., POST) and handle accordingly
        if (context.Request.Method == "POST")
        {

            // Get the request body as a string
            context.Request.EnableBuffering();
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }


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
                method = "Unknown method";
            }
        }
        else
        {
            method = "Unknown method";
        }

        _logger.LogInformation("*** Request Path: {RequestPath}, Method: {RequestMethod}", context.Request.Path, context.Request.Method);
        _logger.LogInformation("*** Request Body: {RequestBody}", requestBody);
        _logger.LogInformation("*** Request Authorization Header: {AuthHeader}", authorizationHeader);
        _logger.LogInformation("*** Request MCP Method: {McpMethod}", method);

        await _next(context);
    }
}
