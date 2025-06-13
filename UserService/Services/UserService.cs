using UserService.Models;
using UserService.Repositories;
using UserService.Services;

namespace UserService.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly ICacheService _cache;
    private const string UserCachePrefix = "user:";
    private const string UserListCacheKey = "users:all";

    public UserService(IUserRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var cacheKey = $"{UserCachePrefix}{username}";
        var cachedUser = await _cache.GetAsync<User>(cacheKey);
        
        if (cachedUser != null)
            return cachedUser;

        var user = await _repository.GetByUsernameAsync(username);
        if (user != null)
        {
            await _cache.SetAsync(cacheKey, user);
        }

        return user;
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        // Note: With Keycloak, this method is no longer needed as authentication is handled by Keycloak
        throw new NotImplementedException("Authentication is now handled by Keycloak");
    }

    public async Task CreateAsync(User user)
    {
        await _repository.AddAsync(user);
        await _cache.SetAsync($"{UserCachePrefix}{user.Username}", user);
        await _cache.RemoveAsync(UserListCacheKey); // Invalidate the list cache
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        var cachedUsers = await _cache.GetAsync<IEnumerable<User>>(UserListCacheKey);
        if (cachedUsers != null)
            return cachedUsers;

        var users = await _repository.GetAllAsync();
        await _cache.SetAsync(UserListCacheKey, users);
        return users;
    }

    public async Task UpdateUserAsync(User user)
    {
        await _repository.UpdateAsync(user); 
        await _cache.RemoveAsync($"{UserCachePrefix}{user.Username}");
        await _cache.RemoveAsync(UserListCacheKey);
    }

    public async Task DeleteUserAsync(string username)
    {
        var user = await _repository.GetByUsernameAsync(username);
        if (user != null)
        {
            await _repository.DeleteAsync(user);
            await _cache.RemoveAsync($"user:{username}");
            await _cache.RemoveAsync("users:all");
        }
    }
}
