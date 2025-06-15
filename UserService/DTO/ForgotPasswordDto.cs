using System.ComponentModel.DataAnnotations;

namespace UserService.DTO;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
} 