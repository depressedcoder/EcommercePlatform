using System.ComponentModel.DataAnnotations;

namespace UserService.DTO;

public class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; set; } = null!;
} 