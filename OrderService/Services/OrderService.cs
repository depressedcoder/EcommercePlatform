using OrderService.Models;
using OrderService.Repositories;

namespace OrderService.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repo;

    public OrderService(IOrderRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<Order>> GetOrdersAsync() => await _repo.GetAllAsync();
    public async Task<Order?> GetOrderAsync(int id) => await _repo.GetByIdAsync(id);
    public async Task PlaceOrderAsync(Order order) => await _repo.CreateAsync(order);
    public async Task UpdateOrderAsync(Order order) => await _repo.UpdateOrderAsync(order);
}
