namespace PaymentService.DTO;

public class UpdateOrderStatusRequest
{
    public int OrderId { get; set; }
    public string PaymentStatus { get; set; } = "Paid";
}
