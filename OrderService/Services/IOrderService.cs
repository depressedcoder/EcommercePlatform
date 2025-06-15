using OrderService.DTO;
using OrderService.Models;

namespace OrderService.Services;

public interface IOrderService
{
    Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto orderDto, Guid userId, string userEmail, string userName);
    Task<OrderResponseDto?> GetOrderByIdAsync(int id);
    Task<IEnumerable<OrderResponseDto>> GetUserOrdersAsync(Guid userId);
    Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync();
    Task<OrderResponseDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto statusDto);
    Task<bool> DeleteOrderAsync(int id);
    Task<IEnumerable<OrderResponseDto>> GetOrdersByStatusAsync(OrderStatus status);
    Task<OrderResponseDto> UpdatePaymentStatusAsync(int orderId, UpdatePaymentStatusDto paymentStatusDto);
}
