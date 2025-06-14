using OrderService.Models;

namespace OrderService.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task<IEnumerable<Order>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Order>> GetAllAsync();
    Task<Order> CreateAsync(Order order);
    Task<Order> UpdateAsync(Order order);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);
}
