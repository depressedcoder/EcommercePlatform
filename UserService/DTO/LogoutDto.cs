namespace UserService.DTO
{
    public class LogoutDto
    {
        public string RefreshToken { get; set; } = null!;
        public string? Username { get; set; } // Optional, for cache clearing
    }
}
