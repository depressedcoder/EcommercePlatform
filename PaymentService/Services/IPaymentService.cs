using PaymentService.DTO;

namespace PaymentService.Services;

public interface IPaymentService
{
    Task<InitiatePaymentResponse> InitiatePaymentAsync(InitiatePaymentRequest request);
    Task<string> ExecutePaymentAsync(string paymentId);
}
