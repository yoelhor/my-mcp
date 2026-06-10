using Microsoft.ApplicationInsights;

public static class InfoEndpoints
{
	public static void MapInfoEndpoints(this WebApplication app)
	{
		app.MapGet("/Info", (TelemetryClient telemetryClient) =>
		{
			var version = System.Reflection.Assembly.GetExecutingAssembly()
				.GetName().Version?.ToString() ?? "Unknown";

			string date = $"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

			return new { Name = "Anonymous MCP Server", Version = version, Date = date };
		});

		app.MapGet("/App", (TelemetryClient telemetryClient) =>
		{
			var version = System.Reflection.Assembly.GetExecutingAssembly()
				.GetName().Version?.ToString() ?? "Unknown";

			string date = $"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

			return new { Name = "MCP Server app only access", Version = version, Date = date };
		}).RequireAuthorization();

		app.MapGet("/obo", (TelemetryClient telemetryClient) =>
		{
			var version = System.Reflection.Assembly.GetExecutingAssembly()
				.GetName().Version?.ToString() ?? "Unknown";

			string date = $"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

			return new { Name = "MCP Server OBO access", Version = version, Date = date };
		}).RequireAuthorization("RequireWriteScope");
	}
}
