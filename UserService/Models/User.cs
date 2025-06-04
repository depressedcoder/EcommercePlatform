namespace UserService.Models
{
    public class User
    {
        public Guid Id { get; set; }         // Should match Keycloak's UUID
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
