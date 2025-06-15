using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentService.Clients;
using PaymentService.Config;
using PaymentService.Data;
using PaymentService.DTO;
using PaymentService.Models;
using System.Collections.Concurrent;

namespace PaymentService.Services;

public class BkashPaymentService : IPaymentService
{
    private readonly BkashClient _bkashClient;
    private readonly PaymentDbContext _db;
    private readonly BkashSettings _settings;
    private readonly HttpClient _http;
    public BkashPaymentService(BkashClient bkashClient, IOptions<BkashSettings> options, HttpClient http, PaymentDbContext db)
    {
        _bkashClient = bkashClient;
        _db = db;
        _settings = options.Value;
        _http = http;
    }

    public async Task<InitiatePaymentResponse> InitiatePaymentAsync(InitiatePaymentRequest request)
    {
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

        var entity = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount.ToString("F2"),
            BkashPaymentId = paymentId!,
            Status = "Initiated",
            BkashToken = token,               
            TokenIssuedAt = DateTime.UtcNow  
        };

        _db.Payments.Add(entity);
        await _db.SaveChangesAsync();

        return new InitiatePaymentResponse
        {
            PaymentId = paymentId!,
            BkashUrl = bkashUrl!,
            Status = "Initiated"
        };
    }

    public async Task<string> ExecutePaymentAsync(string paymentId)
    {
        var payload = new { paymentID = paymentId };
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.BkashPaymentId == paymentId);
        if (payment == null) return "Payment not found";
        var token = payment.BkashToken!;

        var executeResponse = await _bkashClient.PostAsync(_settings.ExecutePaymentUrl, payload, token);

        var trxId = executeResponse["trxID"]?.ToString();
        var status = executeResponse["statusMessage"]?.ToString();

        payment.Status = status!;
        payment.TrxId = trxId;
        await _db.SaveChangesAsync();

        var updateRequest = new
        {
            OrderId = payment.OrderId,
            PaymentStatus = payment.Status
        };

        var orderUpdateResponse = await _http.PostAsJsonAsync(
            "https://localhost:7038/api/order/update-payment-status",
            updateRequest
        );

        orderUpdateResponse.EnsureSuccessStatusCode();

        return $"{status}: TRX={trxId}";
    }
}

