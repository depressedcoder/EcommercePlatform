namespace OrderService.Models;

public class Order
{
    public int Id { get; set; }
    public Guid UserId { get; set; } // matching Keycloak user ID
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? PaymentId { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentStatus { get; set; }
}
