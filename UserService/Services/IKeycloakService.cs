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

    // user registration and login
    Task<RegistrationResponseDto> RegisterUserAsync(UserRegistrationDto dto);
    Task<TokenResponseDto> LoginUserAsync(LoginDto dto);
    Task<TokenResponseDto> RefreshTokenAsync(string refreshToken);
    Task<bool> ResetPasswordAsync(string userId, string newPassword);
    Task<bool> SendPasswordResetEmailAsync(string email);
    Task<bool> VerifyEmailAsync(string userId, string token);
    Task<bool> LogoutAsync(string refreshToken, string? username = null);
} 