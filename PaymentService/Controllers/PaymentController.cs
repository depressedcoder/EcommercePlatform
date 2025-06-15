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

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
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
}
