namespace OrderService.Models
{
    public class Order
    {
        public int Id { get; set; }
        public Guid UserId { get; set; } // matching Keycloak user ID
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
