namespace Common.Auth.Models;

public class AuthResponse
{
    public string Token { get; set; } = null!;
    public string Role { get; set; } = null!;
}
