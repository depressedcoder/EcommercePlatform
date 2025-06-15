using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.DTO;
using OrderService.Models;
using OrderService.Services;
using System.Security.Claims;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _service;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService service, ILogger<OrderController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var username = User.Identity?.Name ?? "Unknown";
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        _logger.LogInformation("User {Username} with role {Role} requested all orders.", username, role);

        return Ok(await _service.GetOrdersAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var username = User.Identity?.Name ?? "Unknown";
        _logger.LogInformation("User {Username} is requesting order #{OrderId}", username, id);

        var order = await _service.GetOrderAsync(id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null) return Unauthorized();

        var order = new Order
        {
            UserId = Guid.Parse(userIdClaim),
            TotalAmount = request.TotalAmount,
            PaymentStatus = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _service.PlaceOrderAsync(order);

        var response = new OrderResponse
        {
            Id = order.Id,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount,
            Status = order.PaymentStatus,
            CreatedAt = order.CreatedAt
        };

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("update-payment-status")]
    public async Task<IActionResult> UpdatePaymentStatus([FromBody] UpdateOrderStatusRequest request)
    {
        var order = await _service.GetOrderAsync(request.OrderId);
        if (order == null) return NotFound();

        order.PaymentStatus = request.PaymentStatus;
        await _service.UpdateOrderAsync(order);
        return Ok("Payment status updated");
    }
}
