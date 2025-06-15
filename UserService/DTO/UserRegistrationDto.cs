using System.ComponentModel.DataAnnotations;

namespace UserService.DTO;

public class UserRegistrationDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = null!;

    [Phone]
    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public Dictionary<string, string>? Attributes { get; set; }
} 