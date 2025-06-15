using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.DTO;
using OrderService.Models;
using OrderService.Services;
using OrderService.Extensions;
using System.Security.Claims;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        IOrderService orderService,
        ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(CreateOrderDto orderDto)
    {
        try
        {
            var userId = User.GetUserId();
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
            var userName = User.FindFirst("preferred_username")?.Value ?? string.Empty;

            var order = await _orderService.CreateOrderAsync(orderDto, userId, userEmail, userName);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for user {UserId}", User.GetUserId());
            return StatusCode(500, "An error occurred while creating the order");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(int id)
    {
        try
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            // Check if user is authorized to view this order
            if (order.UserId != User.GetUserId() && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the order");
        }
    }

    [HttpGet("my-orders")]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetMyOrders()
    {
        try
        {
            var orders = await _orderService.GetUserOrdersAsync(User.GetUserId());
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for user {UserId}", User.GetUserId());
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetAllOrders()
    {
        try
        {
            var orders = await _orderService.GetAllOrdersAsync();
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all orders");
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    [HttpGet("status/{status}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrdersByStatus(OrderStatus status)
    {
        try
        {
            var orders = await _orderService.GetOrdersByStatusAsync(status);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders with status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<OrderResponseDto>> UpdateOrderStatus(int id, UpdateOrderStatusDto statusDto)
    {
        try
        {
            var order = await _orderService.UpdateOrderStatusAsync(id, statusDto);
            return Ok(order);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for order {OrderId}", id);
            return StatusCode(500, "An error occurred while updating the order status");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        try
        {
            var result = await _orderService.DeleteOrderAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order {OrderId}", id);
            return StatusCode(500, "An error occurred while deleting the order");
        }
    }

    [HttpPatch("{id}/payment-status")]
    public async Task<ActionResult<OrderResponseDto>> UpdatePaymentStatus(int id, UpdatePaymentStatusDto paymentStatusDto)
    {
        try
        {
            var order = await _orderService.UpdatePaymentStatusAsync(id, paymentStatusDto);
            return Ok(order);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment status for order {OrderId}", id);
            return StatusCode(500, "An error occurred while updating the payment status");
        }
    }
}
