using System.Text.Json.Serialization;

namespace UserService.Models;

public class KeycloakUser
{
    public string Id { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public bool Enabled { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public List<string>? RealmRoles { get; set; }
    public bool? EmailVerified { get; set; }
}

public class KeycloakRole
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

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

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