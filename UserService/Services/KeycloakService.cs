using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UserService.Config;
using UserService.DTO;
using UserService.Models;

namespace UserService.Services;

public class KeycloakService : IKeycloakService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakSettings _settings;
    private readonly IUserService _userService;
    private readonly ICacheService _cacheService;
    private const string TokenCachePrefix = "keycloak_token:";
    private const string UserCachePrefix = "keycloak_user:";
    private const string Realm = "EcommercePlatform";

    public KeycloakService(
        HttpClient httpClient,
        IOptions<KeycloakSettings> settings,
        IUserService userService,
        ICacheService cacheService)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _userService = userService;
        _cacheService = cacheService;

        _httpClient.BaseAddress = new Uri(_settings.Authority.Replace($"/realms/{Realm}", ""));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<User?> GetUserFromKeycloakAsync(string userId)
    {
        var cacheKey = $"{UserCachePrefix}{userId}";
        var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
        if (cachedUser != null)
            return cachedUser;

        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Get user basic info
        var response = await _httpClient.GetAsync($"/admin/realms/{Realm}/users/{userId}");
        if (!response.IsSuccessStatusCode)
            return null;

        var keycloakUser = await response.Content.ReadFromJsonAsync<KeycloakUser>();
        if (keycloakUser == null)
            return null;

        // Get user realm roles
        var rolesResponse = await _httpClient.GetAsync($"/admin/realms/{Realm}/users/{userId}/role-mappings/realm");
        List<KeycloakRole>? roles = null;
        if (rolesResponse.IsSuccessStatusCode)
        {
            roles = await rolesResponse.Content.ReadFromJsonAsync<List<KeycloakRole>>();
        }
        keycloakUser.RealmRoles = roles?.Select(r => r.name).ToList() ?? new List<string>();

        var user = MapToUser(keycloakUser);
        await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromMinutes(5));
        return user;
    }

    public async Task SyncUserFromKeycloakAsync(string userId)
    {
        var keycloakUser = await GetUserFromKeycloakAsync(userId);
        if (keycloakUser == null)
            return;

        var existingUser = await _userService.GetByUsernameAsync(keycloakUser.Username);
        if (existingUser == null)
        {
            await _userService.CreateAsync(keycloakUser);
        }
        else
        {
            // Update existing user with Keycloak data
            existingUser.PhoneNumber = keycloakUser.PhoneNumber;
            existingUser.Address = keycloakUser.Address;
            existingUser.IsActive = keycloakUser.IsActive;
            existingUser.Attributes = keycloakUser.Attributes;
            existingUser.Email = keycloakUser.Email;
            existingUser.FullName = keycloakUser.FullName;
            existingUser.Roles = keycloakUser.Roles;
            await _userService.UpdateUserAsync(existingUser);
        }
    }

    public async Task<string> CreateUserInKeycloakAsync(CreateUserDto dto)
    {
        var keycloakUser = new
        {
            username = dto.Username,
            email = dto.Email,
            enabled = dto.IsActive,
            firstName = dto.FullName.Split(' ').FirstOrDefault(),
            lastName = dto.FullName.Split(' ').Skip(1).FirstOrDefault() ?? "",
            attributes = dto.Attributes,
        };

        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.PostAsJsonAsync($"/admin/realms/{Realm}/users", keycloakUser);
        response.EnsureSuccessStatusCode();

        // Get the location header (contains the new user ID)
        var location = response.Headers.Location?.ToString();
        var userId = location?.Split('/').Last();

        // Assign roles if any
        if (dto.Roles != null && dto.Roles.Any())
        {
            var tokenForRoleAssign = await GetAdminTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenForRoleAssign);

            var rolesResponse = await _httpClient.GetAsync($"/admin/realms/{Realm}/roles");
            rolesResponse.EnsureSuccessStatusCode();
            var allRoles = await rolesResponse.Content.ReadFromJsonAsync<List<KeycloakRole>>();

            var rolesToAssign = allRoles!
                .Where(r => dto.Roles.Contains(r.name))
                .ToList();

            if (rolesToAssign.Any())
            {
                var assignResponse = await _httpClient.PostAsJsonAsync(
                    $"/admin/realms/{Realm}/users/{userId}/role-mappings/realm",
                    rolesToAssign
                );
                assignResponse.EnsureSuccessStatusCode();
            }
        }

        return userId!;
    }

    public async Task UpdateUserInKeycloakAsync(string userId, UpdateUserDto dto)
    {
        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var keycloakUser = new
        {
            username = dto.Username,
            email = dto.Email,
            enabled = dto.IsActive,
            firstName = dto.FullName.Split(' ').FirstOrDefault(),
            lastName = dto.FullName.Split(' ').Skip(1).FirstOrDefault() ?? "",
            attributes = dto.Attributes,
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"/admin/realms/{Realm}/users/{userId}",
            keycloakUser
        );
        response.EnsureSuccessStatusCode();

        if (dto.Roles != null && dto.Roles.Any())
        {
            // Remove all current realm roles
            var currentRolesResponse = await _httpClient.GetAsync($"/admin/realms/{Realm}/users/{userId}/role-mappings/realm");
            currentRolesResponse.EnsureSuccessStatusCode();
            var currentRoles = await currentRolesResponse.Content.ReadFromJsonAsync<List<KeycloakRole>>();
            if (currentRoles != null && currentRoles.Any())
            {
                var json = System.Text.Json.JsonSerializer.Serialize(currentRoles);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"/admin/realms/{Realm}/users/{userId}/role-mappings/realm", UriKind.Relative),
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
                var removeResponse = await _httpClient.SendAsync(request);
                if (!removeResponse.IsSuccessStatusCode)
                {
                    var error = await removeResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to remove roles from user. Status: {removeResponse.StatusCode}, Error: {error}");
                }
            }

            var rolesResponse = await _httpClient.GetAsync($"/admin/realms/{Realm}/roles");
            rolesResponse.EnsureSuccessStatusCode();
            var allRoles = await rolesResponse.Content.ReadFromJsonAsync<List<KeycloakRole>>();
            var rolesToAssign = allRoles!
                .Where(r => dto.Roles.Contains(r.name))
                .ToList();

            if (rolesToAssign.Any())
            {
                var assignResponse = await _httpClient.PostAsJsonAsync(
                    $"/admin/realms/{Realm}/users/{userId}/role-mappings/realm",
                    rolesToAssign
                );
                assignResponse.EnsureSuccessStatusCode();
            }
        }
    }

    public async Task DeleteUserFromKeycloakAsync(string userId)
    {
        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.DeleteAsync($"/admin/realms/{Realm}/users/{userId}");
        response.EnsureSuccessStatusCode();

        await _cacheService.RemoveAsync($"{UserCachePrefix}{userId}");
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/realms/{Realm}/protocol/openid-connect/userinfo");
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(string userId)
    {
        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.GetAsync($"/admin/realms/{Realm}/users/{userId}/role-mappings/realm");
        if (!response.IsSuccessStatusCode)
            return Array.Empty<string>();

        var roles = await response.Content.ReadFromJsonAsync<List<KeycloakRole>>();
        return roles?.Select(r => r.name) ?? Array.Empty<string>();
    }

    public async Task<RegistrationResponseDto> RegisterUserAsync(UserRegistrationDto dto)
    {
        // First create the user in Keycloak
        var keycloakUser = new
        {
            username = dto.Username,
            email = dto.Email,
            enabled = true,
            emailVerified = false,
            firstName = dto.FullName.Split(' ').FirstOrDefault(),
            lastName = dto.FullName.Split(' ').Skip(1).FirstOrDefault() ?? "",
            attributes = dto.Attributes ?? new Dictionary<string, string>(),
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = dto.Password,
                    temporary = false
                }
            }
        };

        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.PostAsJsonAsync($"/admin/realms/{Realm}/users", keycloakUser);
        response.EnsureSuccessStatusCode();

        // Get the location header (contains the new user ID)
        var location = response.Headers.Location?.ToString();
        var userId = location?.Split('/').Last();

        if (string.IsNullOrEmpty(userId))
            throw new Exception("Failed to get user ID after registration");

        // Assign default role ("user")
        var rolesResponse = await _httpClient.GetAsync($"/admin/realms/{Realm}/roles");
        rolesResponse.EnsureSuccessStatusCode();
        var allRoles = await rolesResponse.Content.ReadFromJsonAsync<List<KeycloakRole>>();
        var userRole = allRoles!.FirstOrDefault(r => r.name == "user");

        if (userRole != null)
        {
            var assignResponse = await _httpClient.PostAsJsonAsync(
                $"/admin/realms/{Realm}/users/{userId}/role-mappings/realm",
                new[] { userRole }
            );
            assignResponse.EnsureSuccessStatusCode();
        }
        // Sync user data to our database
        await SyncUserFromKeycloakAsync(userId);
        // Trigger email verification 
        await VerifyEmailAsync(userId, "");

        return new RegistrationResponseDto
        {
            Message = "Registration successful. Please check your email to verify your account before logging in."
        };
    }

    public async Task<TokenResponseDto> LoginUserAsync(LoginDto dto)
    {
        // Fetch user from Keycloak to check email verification
        var user = await GetUserFromKeycloakByUsernameAsync(dto.Username);
        if (user == null || !(user.EmailVerified ?? false))
        {
            throw new Exception("Please verify your email before logging in.");
        }

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["username"] = dto.Username,
            ["password"] = dto.Password
        };

        var response = await _httpClient.PostAsync(
            $"/realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(tokenRequest));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Login failed. Status: {response.StatusCode}, Error: {error}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (tokenResponse?.AccessToken == null)
            throw new Exception("Failed to get access token");

        return new TokenResponseDto
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken ?? throw new Exception("No refresh token received"),
            ExpiresIn = tokenResponse.ExpiresIn,
            TokenType = tokenResponse.TokenType
        };
    }

    public async Task<bool> LogoutAsync(string refreshToken, string? username = null)
    {
        // Revoke the refresh token in Keycloak
        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["refresh_token"] = refreshToken
        };

        var response = await _httpClient.PostAsync(
            $"/realms/{Realm}/protocol/openid-connect/logout",
            new FormUrlEncodedContent(tokenRequest));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return false;
        }

        if (!string.IsNullOrEmpty(username))
        {
            // Remove app user cache
            await _cacheService.RemoveAsync($"user:{username}");

            // Remove Keycloak user cache by userId
            var keycloakUser = await GetUserFromKeycloakByUsernameAsync(username);
            if (keycloakUser != null)
                await _cacheService.RemoveAsync($"keycloak_user:{keycloakUser.Id}");
        }

        return true;
    }

    public async Task<TokenResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["refresh_token"] = refreshToken
        };

        var response = await _httpClient.PostAsync(
            $"/realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(tokenRequest));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Token refresh failed. Status: {response.StatusCode}, Error: {error}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (tokenResponse?.AccessToken == null)
            throw new Exception("Failed to refresh token");

        return new TokenResponseDto
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken ?? throw new Exception("No refresh token received"),
            ExpiresIn = tokenResponse.ExpiresIn,
            TokenType = tokenResponse.TokenType
        };
    }

    public async Task<bool> ResetPasswordAsync(string userId, string newPassword)
    {
        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var passwordReset = new
        {
            type = "password",
            value = newPassword,
            temporary = false
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"/admin/realms/{Realm}/users/{userId}/reset-password",
            passwordReset);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        var adminToken = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // First find the user by email
        var response = await _httpClient.GetAsync($"/admin/realms/{Realm}/users?email={Uri.EscapeDataString(email)}");
        if (!response.IsSuccessStatusCode)
            return false;

        var users = await response.Content.ReadFromJsonAsync<List<KeycloakUser>>();
        var user = users?.FirstOrDefault();
        if (user == null)
            return false;

        // Send password reset email
        var resetResponse = await _httpClient.PutAsync(
            $"/admin/realms/{Realm}/users/{user.Id}/execute-actions-email",
            new StringContent(
                JsonSerializer.Serialize(new[] { "UPDATE_PASSWORD" }), 
                System.Text.Encoding.UTF8, 
                "application/json"));

        return resetResponse.IsSuccessStatusCode;
    }

    public async Task<bool> VerifyEmailAsync(string userId, string verificationToken)
    {
        var adminToken = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.PutAsync(
            $"/admin/realms/{Realm}/users/{userId}/execute-actions-email",
            new StringContent(
                JsonSerializer.Serialize(new[] { "VERIFY_EMAIL" }), 
                System.Text.Encoding.UTF8, 
                "application/json"));

        return response.IsSuccessStatusCode;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var cacheKey = $"{TokenCachePrefix}admin";
        var cachedToken = await _cacheService.GetAsync<string>(cacheKey);
        if (!string.IsNullOrEmpty(cachedToken))
            return cachedToken;

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret
        };

        var response = await _httpClient.PostAsync(
            $"/realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(tokenRequest));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to get admin token from Keycloak. Status: {response.StatusCode}, Error: {error}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        
        if (tokenResponse?.AccessToken == null)
            throw new Exception("Failed to get admin token from Keycloak");

        await _cacheService.SetAsync(cacheKey, tokenResponse.AccessToken, 
            TimeSpan.FromSeconds(tokenResponse.ExpiresIn - 30)); // Cache for slightly less than expiry

        return tokenResponse.AccessToken;
    }

    private async Task<KeycloakUser?> GetUserFromKeycloakByUsernameAsync(string username)
    {
        var token = await GetAdminTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _httpClient.GetAsync($"/admin/realms/{Realm}/users?username={Uri.EscapeDataString(username)}");
        if (!response.IsSuccessStatusCode)
            return null;
        var users = await response.Content.ReadFromJsonAsync<List<KeycloakUser>>();
        return users?.FirstOrDefault();
    }

    private User MapToUser(KeycloakUser keycloakUser)
    {
        return new User
        {
            Id = keycloakUser.Id,
            Username = keycloakUser.Username,
            Email = keycloakUser.Email,
            FullName = $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim(),
            IsActive = keycloakUser.Enabled,
            Attributes = keycloakUser.Attributes ?? new Dictionary<string, string>(),
            Roles = keycloakUser.RealmRoles ?? new List<string>()
        };
    }
} 