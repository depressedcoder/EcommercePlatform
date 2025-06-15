namespace PaymentService.DTO;

public class InitiatePaymentRequest
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}
