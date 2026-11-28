using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Aggregator.Security;

public sealed class IdentityServerTokenProvider(
    HttpClient httpClient,
    IConfiguration configuration)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsTokenValid())
            return _accessToken!;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (IsTokenValid())
                return _accessToken!;

            var clientId = configuration["IdentityServer:ClientId"]
                ?? throw new InvalidOperationException(
                    "IdentityServer:ClientId is missing.");

            var clientSecret = configuration["IdentityServer:ClientSecret"]
                ?? throw new InvalidOperationException(
                    "IdentityServer:ClientSecret is missing.");

            var scopes = configuration["IdentityServer:Scopes"]
                ?? throw new InvalidOperationException(
                    "IdentityServer:Scopes is missing.");

            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = scopes
                });

            using var response = await httpClient.PostAsync(
                "connect/token",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content
                .ReadFromJsonAsync<TokenResponse>(
                    cancellationToken: cancellationToken);

            if (tokenResponse is null ||
                string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException(
                    "IdentityServer returned an empty access token.");
            }

            _accessToken = tokenResponse.AccessToken;

            // Оновлюємо токен трохи раніше за його фактичне завершення.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(
                Math.Max(tokenResponse.ExpiresIn - 60, 1));

            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsTokenValid()
        => !string.IsNullOrWhiteSpace(_accessToken)
           && DateTimeOffset.UtcNow < _expiresAt;

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
