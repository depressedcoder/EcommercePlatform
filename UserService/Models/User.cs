using System.ComponentModel.DataAnnotations;

namespace UserService.Models;

public class User
{
    [Key]
    public string Id { get; set; } = null!;  // Keycloak's subject (sub) claim
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new(); // For custom attributes
    public List<string> Roles { get; set; } = new(); // Store Keycloak roles
}
