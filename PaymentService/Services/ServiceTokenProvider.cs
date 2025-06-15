using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using PaymentService.Config;
using System.Text.Json;

namespace PaymentService.Services;

public interface IServiceTokenProvider
{
    Task<string> GetTokenAsync();
}

public class ServiceTokenProvider : IServiceTokenProvider
{
    private readonly ICacheService _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakSettings _keycloakSettings;

    public ServiceTokenProvider(
        ICacheService cache,
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakSettings> keycloakOptions)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _keycloakSettings = keycloakOptions.Value;
    }

    public async Task<string> GetTokenAsync()
    {
        var token = await _cache.GetAsync<string>("ServiceAccessToken");
        if (!string.IsNullOrEmpty(token))
            return token;

        var client = _httpClientFactory.CreateClient();
        var tokenEndpoint = $"{_keycloakSettings.Authority}/protocol/openid-connect/token";
        var parameters = new Dictionary<string, string>
        {
            {"grant_type", "client_credentials"},
            {"client_id", _keycloakSettings.ClientId},
            {"client_secret", _keycloakSettings.ClientSecret}
        };

        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(parameters));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(payload);
        token = json.RootElement.GetProperty("access_token").GetString()!;

        var expiresIn = json.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 300;
        await _cache.SetAsync("ServiceAccessToken", token, TimeSpan.FromSeconds(expiresIn - 60));

        return token;
    }
}
