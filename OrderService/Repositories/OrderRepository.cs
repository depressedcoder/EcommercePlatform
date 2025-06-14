using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly ICacheService _cache;
    private readonly ILogger<OrderRepository> _logger;
    private const string OrderCacheKeyPrefix = "order:";
    private const string UserOrdersCacheKeyPrefix = "user_orders:";
    private const string OrdersByStatusCacheKeyPrefix = "orders_by_status:";

    public OrderRepository(
        OrderDbContext context,
        ICacheService cache,
        ILogger<OrderRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        var cacheKey = $"{OrderCacheKeyPrefix}{id}";
        
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            _logger.LogDebug("Cache miss for order {OrderId}, fetching from database", id);
            return await _context.Orders.FindAsync(id);
        });
    }

    public async Task<IEnumerable<Order>> GetByUserIdAsync(Guid userId)
    {
        var cacheKey = $"{UserOrdersCacheKeyPrefix}{userId}";
        
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            _logger.LogDebug("Cache miss for user orders {UserId}, fetching from database", userId);
            return await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        });
    }

    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        return await _context.Orders
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order> CreateAsync(Order order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        
        // Invalidate user orders cache
        await _cache.RemoveAsync($"{UserOrdersCacheKeyPrefix}{order.UserId}");
        await _cache.RemoveAsync($"{OrdersByStatusCacheKeyPrefix}{order.Status}");
        
        _logger.LogInformation("Created new order {OrderId} for user {UserId}", order.Id, order.UserId);
        return order;
    }

    public async Task<Order> UpdateAsync(Order order)
    {
        var existingOrder = await _context.Orders.FindAsync(order.Id);
        if (existingOrder == null)
        {
            throw new KeyNotFoundException($"Order with ID {order.Id} not found");
        }

        // Update properties
        existingOrder.Status = order.Status;
        existingOrder.PaymentId = order.PaymentId;
        existingOrder.TransactionId = order.TransactionId;
        existingOrder.PaymentStatus = order.PaymentStatus;
        existingOrder.Notes = order.Notes;
        existingOrder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate caches
        await _cache.RemoveAsync($"{OrderCacheKeyPrefix}{order.Id}");
        await _cache.RemoveAsync($"{UserOrdersCacheKeyPrefix}{order.UserId}");
        await _cache.RemoveAsync($"{OrdersByStatusCacheKeyPrefix}{existingOrder.Status}");
        await _cache.RemoveAsync($"{OrdersByStatusCacheKeyPrefix}{order.Status}");

        _logger.LogInformation("Updated order {OrderId}", order.Id);
        return existingOrder;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return false;
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        // Invalidate caches
        await _cache.RemoveAsync($"{OrderCacheKeyPrefix}{id}");
        await _cache.RemoveAsync($"{UserOrdersCacheKeyPrefix}{order.UserId}");
        await _cache.RemoveAsync($"{OrdersByStatusCacheKeyPrefix}{order.Status}");

        _logger.LogInformation("Deleted order {OrderId}", id);
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Orders.AnyAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
    {
        var cacheKey = $"{OrdersByStatusCacheKeyPrefix}{status}";
        
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            _logger.LogDebug("Cache miss for orders with status {Status}, fetching from database", status);
            return await _context.Orders
                .Where(o => o.Status == status)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        });
    }
}
