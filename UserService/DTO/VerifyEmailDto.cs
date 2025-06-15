using System.ComponentModel.DataAnnotations;

namespace UserService.DTO;

public class VerifyEmailDto
{
    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    public string Token { get; set; } = null!;
} 