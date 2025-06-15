using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderService.Models;

public enum OrderStatus
{
    Created,
    Processing,
    Paid,
    Cancelled,
    Failed
}

public class Order
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; } // Keycloak user ID

    [Required]
    [MaxLength(100)]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Created;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(50)]
    public string? PaymentId { get; set; }

    [MaxLength(100)]
    public string? TransactionId { get; set; }

    [MaxLength(50)]
    public string? PaymentStatus { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
