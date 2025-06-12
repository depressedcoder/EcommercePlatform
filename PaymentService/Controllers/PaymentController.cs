using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.DTO;
using PaymentService.Services;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IStripePaymentService _stripePaymentService;

    public PaymentController(IPaymentService paymentService, IStripePaymentService stripePaymentService)
    {
        _paymentService = paymentService;
        _stripePaymentService = stripePaymentService;
    }

    [HttpPost("initiate")]
    [Authorize]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest request)
    {
        return Ok(await _paymentService.InitiatePaymentAsync(request));
    }

    [HttpGet("confirm")]
    public async Task<IActionResult> Confirm([FromQuery] string paymentId)
    {
        var result = await _paymentService.ExecutePaymentAsync(paymentId);
        return Ok(new { Message = result });
    }

    [HttpPost("stripe/checkout")]
    public async Task<IActionResult> Checkout([FromBody] StripeCheckoutRequest request)
    {
        var url = await _stripePaymentService.CreateCheckoutSessionAsync(request);
        return Ok(new { url });
    }

    [HttpPost("stripe/confirm-by-order")]
    public async Task<IActionResult> ConfirmByOrder([FromBody] int orderId)
    {
        var success = await _stripePaymentService.ConfirmCheckoutByOrderAsync(orderId);
        if (success)
            return Ok(new { message = "Payment confirmed via OrderId." });

        return BadRequest(new { message = "Payment failed or not found for this order." });
    }
}
