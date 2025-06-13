using UserService.DTO;
using UserService.Models;

namespace UserService.Services;

public interface IKeycloakService
{
    Task<User?> GetUserFromKeycloakAsync(string userId);
    Task SyncUserFromKeycloakAsync(string userId);
    Task<string> CreateUserInKeycloakAsync(CreateUserDto dto);
    Task UpdateUserInKeycloakAsync(string userId, UpdateUserDto dto);
    Task DeleteUserFromKeycloakAsync(string userId);
    Task<bool> ValidateTokenAsync(string token);
    Task<IEnumerable<string>> GetUserRolesAsync(string userId);
} 