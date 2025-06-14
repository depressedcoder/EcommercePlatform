using OrderService.DTO;
using OrderService.Models;
using OrderService.Repositories;

namespace OrderService.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto orderDto, Guid userId, string userEmail, string userName)
    {
        _logger.LogInformation("Creating new order for user {UserId}", userId);

        var order = new Order
        {
            UserId = userId,
            UserEmail = userEmail,
            UserName = userName,
            TotalAmount = orderDto.TotalAmount,
            Status = OrderStatus.Created,
            Notes = orderDto.Notes
        };

        var createdOrder = await _orderRepository.CreateAsync(order);
        return MapToOrderResponseDto(createdOrder);
    }

    public async Task<OrderResponseDto?> GetOrderByIdAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        return order != null ? MapToOrderResponseDto(order) : null;
    }

    public async Task<IEnumerable<OrderResponseDto>> GetUserOrdersAsync(Guid userId)
    {
        var orders = await _orderRepository.GetByUserIdAsync(userId);
        return orders.Select(MapToOrderResponseDto);
    }

    public async Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync()
    {
        var orders = await _orderRepository.GetAllAsync();
        return orders.Select(MapToOrderResponseDto);
    }

    public async Task<OrderResponseDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto statusDto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {id} not found");
        }

        order.Status = statusDto.Status;
        order.Notes = statusDto.Notes ?? order.Notes;
        order.UpdatedAt = DateTime.UtcNow;

        var updatedOrder = await _orderRepository.UpdateAsync(order);
        return MapToOrderResponseDto(updatedOrder);
    }

    public async Task<bool> DeleteOrderAsync(int id)
    {
        return await _orderRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<OrderResponseDto>> GetOrdersByStatusAsync(OrderStatus status)
    {
        var orders = await _orderRepository.GetByStatusAsync(status);
        return orders.Select(MapToOrderResponseDto);
    }

    private static OrderResponseDto MapToOrderResponseDto(Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            UserId = order.UserId,
            UserEmail = order.UserEmail,
            UserName = order.UserName,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            PaymentId = order.PaymentId,
            TransactionId = order.TransactionId,
            PaymentStatus = order.PaymentStatus,
            Notes = order.Notes
        };
    }
}
