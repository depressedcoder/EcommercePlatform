using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using PaymentService.Config;
using PaymentService.DTO;

namespace PaymentService.Clients;

public class BkashClient
{
    private readonly HttpClient _http;
    private readonly BkashSettings _settings;
    private string? _cachedToken;
    private DateTime _tokenIssuedAt;

    public BkashClient(HttpClient http, IOptions<BkashSettings> options)
    {
        _http = http;
        _settings = options.Value;
    }

    public async Task<string> GetTokenAsync()
    {
        if (_cachedToken != null && (DateTime.UtcNow - _tokenIssuedAt).TotalMinutes < 55)
            return _cachedToken;

        var payload = new
        {
            app_key = _settings.AppKey,
            app_secret = _settings.AppSecret
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}{_settings.GrantTokenUrl}")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Add("username", _settings.Username);
        request.Headers.Add("password", _settings.Password);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BkashTokenResponse>();
        _cachedToken = result!.IdToken;
        _tokenIssuedAt = DateTime.UtcNow;

        return _cachedToken;
    }

    public async Task<JObject> PostAsync(string path, object data, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl + path)
        {
            Content = JsonContent.Create(data)
        };

        request.Headers.Add("authorization", token);
        request.Headers.Add("x-app-key", _settings.AppKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JObject.Parse(json);
    }
}