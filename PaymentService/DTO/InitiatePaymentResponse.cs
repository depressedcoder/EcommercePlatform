namespace PaymentService.DTO;

public class InitiatePaymentResponse
{
    public string BkashUrl { get; set; } = null!;
    public string PaymentId { get; set; } = null!;
    public string Status { get; set; } = "Initiated";
}
