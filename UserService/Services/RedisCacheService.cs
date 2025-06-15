using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using UserService.Config;

namespace UserService.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly RedisSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(IDistributedCache cache, IOptions<RedisSettings> options)
    {
        _cache = cache;
        _settings = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var cachedValue = await _cache.GetStringAsync(GetKey(key));
        if (string.IsNullOrEmpty(cachedValue))
            return default;

        return JsonSerializer.Deserialize<T>(cachedValue, _jsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes)
        };

        var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
        await _cache.SetStringAsync(GetKey(key), serializedValue, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(GetKey(key));
    }

    public async Task<bool> ExistsAsync(string key)
    {
        var value = await _cache.GetAsync(GetKey(key));
        return value != null;
    }

    private string GetKey(string key) => $"{_settings.InstanceName}{key}";
} 