using PaymentService.DTO;

namespace PaymentService.Services;

public interface IStripePaymentService
{
    Task<string> CreateCheckoutSessionAsync(StripeCheckoutRequest request);
    Task<bool> ConfirmCheckoutByOrderAsync(int orderId);
}
