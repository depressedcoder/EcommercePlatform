using System.Threading.Tasks;

namespace PaymentService.Clients;
 
public interface IOrderServiceClient
{
    Task<bool> UpdatePaymentStatusAsync(int orderId, string paymentId, string transactionId, string paymentStatus, string? notes = null);
} 