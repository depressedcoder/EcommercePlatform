using System.ComponentModel.DataAnnotations;
using OrderService.Models;

namespace OrderService.DTO;

public class CreateOrderDto
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than 0")]
    public decimal TotalAmount { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? PaymentId { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentStatus { get; set; }
    public string? Notes { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus Status { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdatePaymentStatusDto
{
    [Required]
    public string PaymentId { get; set; } = string.Empty;

    [Required]
    public string TransactionId { get; set; } = string.Empty;

    [Required]
    public string PaymentStatus { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }
} 