using PaymentService.DTO;
using PaymentService.Models;

namespace PaymentService.Services;

public interface IStripePaymentService
{
    Task<string> CreateCheckoutSessionAsync(StripeCheckoutRequest request);
    Task<PaymentConfirmationResult> ConfirmCheckoutByOrderAsync(int orderId);
    Task<Payment?> GetPaymentByOrderIdAsync(int orderId);
}
