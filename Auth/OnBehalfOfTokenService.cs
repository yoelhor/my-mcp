using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

namespace my_mcp_demo.Auth;

public interface IOnBehalfOfTokenService
{
    Task<(string AccessToken, DateTimeOffset ExpiresOn)> GetAccessTokenForUserAsync(string userAccessToken, CancellationToken cancellationToken = default);
}

public sealed class OnBehalfOfTokenService : IOnBehalfOfTokenService
{
    private readonly IConfidentialClientApplication _app;
    private readonly string[] _scopes;

    public OnBehalfOfTokenService(IConfiguration configuration)
    {
        // Read necessary configuration for OBO token acquisition
        string? clientId = configuration["Mcp:ClientId"];
        string? clientSecret = configuration["Mcp:ClientSecret"];
        string authority = configuration["Mcp:Authority"] ?? "";

        // Check that required configuration values are present
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Mcp configuration is incomplete. Set Mcp:ClientId, Mcp:ClientSecret, and Mcp:Authority.");
        }

        // Initialize the confidential client application for OBO flow
        _app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(authority)
            .WithClientSecret(clientSecret)
            .Build();

        // Read the scopes for the downstream API from configuration
        _scopes = configuration
            .GetSection("Mcp:DownstreamApis:AzureStorageAccount:Scopes")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;

        // Validate that at least one scope is configured for the downstream API
        if (_scopes.Length == 0)
        {
            throw new InvalidOperationException("No scopes configured for downstream API. Set Mcp:DownstreamApis:AzureStorageAccount:Scopes in configuration.");
        }
    }

    public async Task<(string AccessToken, DateTimeOffset ExpiresOn)> GetAccessTokenForUserAsync(
        string userAccessToken,
        CancellationToken cancellationToken = default)
    {
        // Validate that the inbound user access token is present
        if (string.IsNullOrWhiteSpace(userAccessToken))
        {
            throw new InvalidOperationException("Inbound user access token is missing.");
        }

        // Use the MSAL library to acquire an access token for the downstream API on behalf of the user
        var result = await _app
            .AcquireTokenOnBehalfOf(_scopes, new UserAssertion(userAccessToken))
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        // Return the acquired access token and its expiration time to the caller
        return (result.AccessToken, result.ExpiresOn);
    }
}
