using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.DTO;
using PaymentService.Enums;
using PaymentService.Models;
using Stripe;
using Stripe.Checkout;

namespace PaymentService.Services
{
    public class StripePaymentService : IStripePaymentService
    {
        private readonly IConfiguration _config;
        private readonly PaymentDbContext _context;
        private readonly ILogger<StripePaymentService> _logger;
        private readonly HttpClient _http;

        public StripePaymentService(IConfiguration config, PaymentDbContext context, HttpClient http, ILogger<StripePaymentService> logger)
        {
            _config = config;
            _context = context;
            _logger = logger;
            _http = http;

            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        }

        public async Task<string> CreateCheckoutSessionAsync(StripeCheckoutRequest request)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = request.Currency,
                            UnitAmount = (long)(request.Amount * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Order #{request.OrderId}"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                Metadata = new Dictionary<string, string>
                {
                    { "OrderId", request.OrderId.ToString() }
                },
                SuccessUrl = "https://localhost:4200/payment-success?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = "https://localhost:4200/payment-cancel"
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            // Save session ID temporarily
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                Amount = request.Amount.ToString(),
                PaymentProvider = PaymentProvider.Stripe,
                Status = "Pending",
                StripeSessionId = session.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return session.Url!;
        }

        public async Task<bool> ConfirmCheckoutByOrderAsync(int orderId)
        {
            var payment = await _context.Payments
                .OrderByDescending(p => p.CreatedAt) // in case of retries
                .FirstOrDefaultAsync(p => p.OrderId == orderId && p.PaymentProvider == PaymentProvider.Stripe);

            if (payment == null || string.IsNullOrWhiteSpace(payment.StripeSessionId))
                return false;

            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(payment.StripeSessionId);

            if (session.PaymentStatus == "paid")
            {
                payment.Status = "Succeeded";
                await _context.SaveChangesAsync();
                await NotifyOrderServiceAsync(orderId, payment.Status);
                return true;
            }

            payment.Status = "Failed";
            await _context.SaveChangesAsync();
            await NotifyOrderServiceAsync(orderId, payment.Status);
            return false;
        }

        private async Task NotifyOrderServiceAsync(int orderId, string status)
        {
            var updateRequest = new
            {
                OrderId = orderId,
                PaymentStatus = status
            };

            var response = await _http.PostAsJsonAsync("https://localhost:7038/api/order/update-payment-status", updateRequest);
            
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Failed to update order status for OrderId {OrderId}", orderId);
        }
    }
}
