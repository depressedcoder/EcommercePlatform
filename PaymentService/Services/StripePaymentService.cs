using PaymentService.DTO;
using PaymentService.Models;
using Stripe;
using Stripe.Checkout;
using PaymentService.Clients;
using PaymentService.Config;
using PaymentService.Repositories;
using Microsoft.Extensions.Options;
using PaymentService.Enums;

namespace PaymentService.Services
{
    public class StripePaymentService : IStripePaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IOrderServiceClient _orderServiceClient;
        private readonly ILogger<StripePaymentService> _logger;
        private readonly StripeSettings _stripeSettings;

        public StripePaymentService(
            IPaymentRepository paymentRepository,
            IOrderServiceClient orderServiceClient,
            IOptions<StripeSettings> stripeSettings,
            ILogger<StripePaymentService> logger)
        {
            _paymentRepository = paymentRepository;
            _orderServiceClient = orderServiceClient;
            _logger = logger;
            _stripeSettings = stripeSettings.Value;
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }

        public async Task<string> CreateCheckoutSessionAsync(StripeCheckoutRequest request)
        {
            _logger.LogInformation("Creating Stripe checkout session for OrderId: {OrderId}", request.OrderId);

            // Check for existing completed payment
            var existingPayment = await _paymentRepository.GetByOrderIdAsync(request.OrderId);
            if (existingPayment != null && existingPayment.Status == PaymentStatus.Completed)
            {
                _logger.LogInformation("Payment already completed for OrderId: {OrderId}", request.OrderId);
                return $"Payment already completed for OrderId: {request.OrderId}.";
            }

            var payment = new Payment
            {
                OrderId = request.OrderId,
                Amount = request.Amount,
                PaymentMethod = Enums.PaymentMethod.Stripe,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            var createdPayment = await _paymentRepository.CreateAsync(payment);
            _logger.LogInformation("Created payment record with ID: {PaymentId}", createdPayment.Id);

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            UnitAmount = (long)(request.Amount * 100), // Convert to cents
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Order #{request.OrderId}",
                                Description = "Payment for your order"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = $"{_stripeSettings.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_stripeSettings.CancelUrl}?order_id={request.OrderId}",
                Metadata = new Dictionary<string, string>
                {
                    { "orderId", request.OrderId.ToString() },
                    { "paymentId", createdPayment.Id.ToString() }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            createdPayment.TransactionId = session.Id;
            await _paymentRepository.UpdateAsync(createdPayment);

            _logger.LogInformation("Created Stripe session {SessionId} for OrderId: {OrderId}", 
                session.Id, request.OrderId);

            return session.Url;
        }

        public async Task<PaymentConfirmationResult> ConfirmCheckoutByOrderAsync(int orderId)
        {
            _logger.LogInformation("Confirming Stripe payment for OrderId: {OrderId}", orderId);

            var payment = await _paymentRepository.GetByOrderIdAsync(orderId);
            if (payment == null)
            {
                _logger.LogWarning("No payment found for OrderId: {OrderId}", orderId);
                return new PaymentConfirmationResult { Status = "NotFound" };
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                _logger.LogInformation("Payment already completed for OrderId: {OrderId}", orderId);
                return new PaymentConfirmationResult { Status = "AlreadyCompleted" };
            }

            if (string.IsNullOrEmpty(payment.TransactionId))
            {
                _logger.LogWarning("No Stripe session ID found for OrderId: {OrderId}", orderId);
                return new PaymentConfirmationResult { Status = "NotPaid" };
            }

            var service = new SessionService();
            var session = await service.GetAsync(payment.TransactionId);

            if (session.PaymentStatus != "paid")
            {
                _logger.LogWarning("Payment not completed for OrderId: {OrderId}, SessionId: {SessionId}",
                    orderId, session.Id);
                return new PaymentConfirmationResult { Status = "NotPaid" };
            }

            payment.Status = PaymentStatus.Completed;
            payment.UpdatedAt = DateTime.UtcNow;
            await _paymentRepository.UpdateAsync(payment);

            _logger.LogInformation("Updated payment status to {Status} for OrderId: {OrderId}",
                payment.Status, orderId);

            // Notify OrderService about the payment status
            var success = await _orderServiceClient.UpdatePaymentStatusAsync(
                orderId,
                payment.Id.ToString(),
                payment.TransactionId,
                payment.Status.ToString(),
                "Payment completed via Stripe");

            if (!success)
            {
                _logger.LogWarning("Failed to update order status for OrderId: {OrderId}", orderId);
                return new PaymentConfirmationResult { Status = "OrderUpdateFailed" };
            }

            return new PaymentConfirmationResult { Status = "Success" };
        }

        public async Task<Payment?> GetPaymentByOrderIdAsync(int orderId)
        {
            return await _paymentRepository.GetByOrderIdAsync(orderId);
        }
    }
}
