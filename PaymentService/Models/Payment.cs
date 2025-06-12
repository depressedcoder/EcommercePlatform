using PaymentService.Enums;

namespace PaymentService.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int OrderId { get; set; }
    public string Amount { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string? TrxId { get; set; }
    public string? BkashPaymentId { get; set; }
    public string? BkashToken { get; set; }
    public DateTime? TokenIssuedAt { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? StripeSessionId { get; set; }
    public string? StripeClientSecret { get; set; }
    public PaymentProvider PaymentProvider { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}