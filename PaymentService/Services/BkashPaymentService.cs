using Microsoft.Extensions.Options;
using PaymentService.Clients;
using PaymentService.Config;
using PaymentService.DTO;
using PaymentService.Enums;
using PaymentService.Models;
using PaymentService.Repositories;

namespace PaymentService.Services;

public class BkashPaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderServiceClient _orderServiceClient;
    private readonly ILogger<BkashPaymentService> _logger;
    private readonly BkashSettings _bkashSettings;

    public BkashPaymentService(
        IPaymentRepository paymentRepository,
        IOrderServiceClient orderServiceClient,
        IOptions<BkashSettings> bkashSettings,
        ILogger<BkashPaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
        _bkashSettings = bkashSettings.Value;
    }

    public async Task<InitiatePaymentResponse> InitiatePaymentAsync(InitiatePaymentRequest request)
    {
        _logger.LogInformation("Initiating Bkash payment for OrderId: {OrderId}", request.OrderId);

        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            PaymentMethod = PaymentMethod.Bkash,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var createdPayment = await _paymentRepository.CreateAsync(payment);
        _logger.LogInformation("Created payment record with ID: {PaymentId}", createdPayment.Id);

        // In a real implementation, you would call the Bkash API here
        // For now, we'll just return a mock response
        return new InitiatePaymentResponse
        {
            PaymentId = createdPayment.Id.ToString(),
            CheckoutUrl = $"{_bkashSettings.BaseUrl}/checkout/{createdPayment.Id}",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
    }

    public async Task<string> ExecutePaymentAsync(string paymentId)
    {
        _logger.LogInformation("Executing Bkash payment: {PaymentId}", paymentId);

        var payment = await _paymentRepository.GetByIdAsync(int.Parse(paymentId));
        if (payment == null)
        {
            _logger.LogWarning("Payment not found: {PaymentId}", paymentId);
            throw new KeyNotFoundException($"Payment with ID {paymentId} not found");
        }

        // In a real implementation, you would verify the payment with Bkash API
        // For now, we'll just simulate a successful payment
        payment.Status = PaymentStatus.Completed;
        payment.TransactionId = $"BKASH_{Guid.NewGuid():N}";
        payment.UpdatedAt = DateTime.UtcNow;

        var updatedPayment = await _paymentRepository.UpdateAsync(payment);
        _logger.LogInformation("Updated payment status to {Status} for PaymentId: {PaymentId}", 
            updatedPayment.Status, paymentId);

        // Notify OrderService about the payment status
        var success = await _orderServiceClient.UpdatePaymentStatusAsync(
            payment.OrderId,
            payment.Id.ToString(),
            payment.TransactionId,
            payment.Status.ToString(),
            "Payment completed via Bkash");

        if (!success)
        {
            _logger.LogWarning("Failed to update order status for OrderId: {OrderId}", payment.OrderId);
        }

        return "Payment completed successfully";
    }

    public async Task<Payment?> GetPaymentByOrderIdAsync(int orderId)
    {
        return await _paymentRepository.GetByOrderIdAsync(orderId);
    }
}

