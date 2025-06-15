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
    private readonly BkashClient _bkashClient;
    private readonly IPaymentRepository _paymentRepository;
    private readonly BkashSettings _settings;
    private readonly IOrderServiceClient _orderServiceClient;
    private readonly ILogger<BkashPaymentService> _logger;

    public BkashPaymentService(
        BkashClient bkashClient,
        IPaymentRepository paymentRepository,
        IOrderServiceClient orderServiceClient,
        IOptions<BkashSettings> options,
        ILogger<BkashPaymentService> logger)
    {
        _bkashClient = bkashClient;
        _paymentRepository = paymentRepository;
        _orderServiceClient = orderServiceClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<InitiatePaymentResponse> InitiatePaymentAsync(InitiatePaymentRequest request)
    {
        _logger.LogInformation("Initiating Bkash payment for OrderId: {OrderId}", request.OrderId);

        var token = await _bkashClient.GetTokenAsync();

        var payload = new
        {
            mode = "0011",
            payerReference = "01619777282",
            callbackURL = "https://localhost:7266/swagger/index.html",
            amount = request.Amount.ToString("F2"),
            currency = "BDT",
            intent = "sale",
            merchantInvoiceNumber = $"Inv{DateTime.Now.Ticks}"
        };

        var response = await _bkashClient.PostAsync(_settings.CreatePaymentUrl, payload, token);
        var paymentId = response["paymentID"]?.ToString();
        var bkashUrl = response["bkashURL"]?.ToString();

        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            PaymentMethod = Enums.PaymentMethod.Bkash,
            Status = Enums.PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            TransactionId = paymentId
        };

        await _paymentRepository.CreateAsync(payment);

        _logger.LogInformation("Bkash payment initiated. PaymentId: {PaymentId}, OrderId: {OrderId}", paymentId, request.OrderId);

        return new InitiatePaymentResponse
        {
            PaymentId = paymentId!,
            BkashUrl = bkashUrl!,
            Status = "Initiated"
        };
    }

    public async Task<string> ExecutePaymentAsync(string paymentId)
    {
        _logger.LogInformation("Executing Bkash payment. PaymentId: {PaymentId}", paymentId);

        var payment = await _paymentRepository.GetByTransactionIdAsync(paymentId);
        if (payment == null)
        {
            _logger.LogWarning("Payment not found. PaymentId: {PaymentId}", paymentId);
            return "Payment not found";
        }

        var token = await _bkashClient.GetTokenAsync();
        var payload = new { paymentID = paymentId };
        var executeResponse = await _bkashClient.PostAsync(_settings.ExecutePaymentUrl, payload, token);

        var trxId = executeResponse["trxID"]?.ToString();
        var status = executeResponse["statusMessage"]?.ToString();

        payment.Status = status?.ToLower() == "completed" ? Enums.PaymentStatus.Completed : Enums.PaymentStatus.Failed;
        payment.TransactionId = trxId;
        payment.UpdatedAt = DateTime.UtcNow;
        await _paymentRepository.UpdateAsync(payment);

        _logger.LogInformation("Bkash payment executed. PaymentId: {PaymentId}, Status: {Status}, TrxId: {TrxId}", paymentId, status, trxId);

        // Update Order via OrderServiceClient
        var orderUpdateSuccess = await _orderServiceClient.UpdatePaymentStatusAsync(
            payment.OrderId,
            payment.Id.ToString(),
            payment.TransactionId ?? "",
            payment.Status.ToString(),
            "Payment completed via Bkash"
        );

        if (!orderUpdateSuccess)
        {
            _logger.LogWarning("Failed to update order status for OrderId: {OrderId}", payment.OrderId);
        }

        return $"{status}: TRX={trxId}";
    }

    public async Task<Payment?> GetPaymentByOrderIdAsync(int orderId)
    {
        return await _paymentRepository.GetByOrderIdAsync(orderId);
    }
}

