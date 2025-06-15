namespace UserService.DTO;

public class CreateUserDto
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public List<string>? Roles { get; set; }
}
