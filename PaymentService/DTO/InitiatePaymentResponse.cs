namespace PaymentService.DTO;

public class InitiatePaymentResponse
{
    public string BkashUrl { get; set; } = null!;
    public string PaymentId { get; set; } = null!;
    public string Status { get; set; } = "Initiated";
    public string? CheckoutUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
