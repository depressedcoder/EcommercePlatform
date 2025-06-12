namespace PaymentService.DTO;

public class StripeCheckoutRequest
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
}
