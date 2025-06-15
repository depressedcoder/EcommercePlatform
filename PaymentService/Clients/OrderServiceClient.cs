using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentService.Config;
using PaymentService.Services;

namespace PaymentService.Clients;

public class OrderServiceClient : IOrderServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IServiceTokenProvider _tokenProvider;

    public OrderServiceClient(
        HttpClient httpClient,
        IOptions<ServiceConfig> serviceConfig,
        ILogger<OrderServiceClient> logger,
    IServiceTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _tokenProvider = tokenProvider;
        _httpClient.BaseAddress = new Uri(serviceConfig.Value.OrderServiceUrl);
    }

    public async Task<bool> UpdatePaymentStatusAsync(int orderId, string paymentId, string transactionId, string paymentStatus, string? notes = null)
    {
        try
        {
            _logger.LogInformation("Updating payment status for order {OrderId} to {PaymentStatus}", 
                orderId, paymentStatus);

            var request = new
            {
                PaymentId = paymentId,
                TransactionId = transactionId,
                PaymentStatus = paymentStatus,
                Notes = notes
            };
            var token = await _tokenProvider.GetTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PatchAsJsonAsync(
                $"api/order/{orderId}/payment-status",
                request,
                _jsonOptions);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Unauthorized or forbidden when updating payment status for order {OrderId}. Status code: {StatusCode}", 
                    orderId, response.StatusCode);
                return false;
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated payment status for order {OrderId}", orderId);
                return true;
            }

            _logger.LogWarning("Failed to update payment status for order {OrderId}. Status code: {StatusCode}", 
                orderId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment status for order {OrderId}", orderId);
            return false;
        }
    }
} 