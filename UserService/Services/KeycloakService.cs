using System.Net.Http.Headers;
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

    private class KeycloakUser
    {
        public string Id { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public bool Enabled { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
        public List<string>? RealmRoles { get; set; }
    }

    private class KeycloakRole
    {
        [JsonPropertyName("id")]
        public string id { get; set; } = null!;
        [JsonPropertyName("name")]
        public string name { get; set; } = null!;
        [JsonPropertyName("composite")]
        public bool composite { get; set; }
        [JsonPropertyName("clientRole")]
        public bool clientRole { get; set; }
        [JsonPropertyName("containerId")]
        public string containerId { get; set; } = null!;
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = null!;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_expires_in")]
        public int RefreshExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = null!;

        [JsonPropertyName("not-before-policy")]
        public int NotBeforePolicy { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = null!;
    }
} 