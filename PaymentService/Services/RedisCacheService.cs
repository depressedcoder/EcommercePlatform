using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using PaymentService.Config;
using System.Text.Json;

namespace PaymentService.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
}

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly RedisSettings _settings;

    public RedisCacheService(IDistributedCache cache, IOptions<RedisSettings> settings)
    {
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _cache.GetStringAsync(key);
        if (value == null)
            return default;

        return JsonSerializer.Deserialize<T>(value);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes)
        };

        var jsonValue = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, jsonValue, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
} 