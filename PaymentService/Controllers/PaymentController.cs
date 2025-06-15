using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.DTO;
using PaymentService.Extensions;
using PaymentService.Services;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _bkashService;
    private readonly IStripePaymentService _stripeService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService bkashService,
        IStripePaymentService stripeService,
        ILogger<PaymentController> logger)
    {
        _bkashService = bkashService;
        _stripeService = stripeService;
        _logger = logger;
    }

    [HttpPost("bkash/initiate")]
    public async Task<ActionResult<InitiatePaymentResponse>> InitiateBkashPayment(InitiatePaymentRequest request)
    {
        try
        {
            _logger.LogInformation("User {UserId} initiating Bkash payment for OrderId: {OrderId}", 
                User.GetUserId(), request.OrderId);

            var response = await _bkashService.InitiatePaymentAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Bkash payment for OrderId: {OrderId}", request.OrderId);
            return StatusCode(500, "An error occurred while initiating the payment");
        }
    }

    [HttpPost("bkash/execute/{paymentId}")]
    public async Task<ActionResult<string>> ExecuteBkashPayment(string paymentId)
    {
        try
        {
            _logger.LogInformation("User {UserId} executing Bkash payment: {PaymentId}", 
                User.GetUserId(), paymentId);

            var result = await _bkashService.ExecutePaymentAsync(paymentId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Bkash payment: {PaymentId}", paymentId);
            return StatusCode(500, "An error occurred while executing the payment");
        }
    }

    [HttpPost("stripe/create-session")]
    public async Task<ActionResult<string>> CreateStripeSession(StripeCheckoutRequest request)
    {
        try
        {
            _logger.LogInformation("User {UserId} creating Stripe session for OrderId: {OrderId}", 
                User.GetUserId(), request.OrderId);

            var sessionUrl = await _stripeService.CreateCheckoutSessionAsync(request);
            return Ok(new { url = sessionUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stripe session for OrderId: {OrderId}", request.OrderId);
            return StatusCode(500, "An error occurred while creating the payment session");
        }
    }

    [HttpPost("stripe/confirm/{orderId}")]
    public async Task<IActionResult> ConfirmStripePayment(int orderId)
    {
        try
        {
            _logger.LogInformation("User {UserId} confirming Stripe payment for OrderId: {OrderId}",
                User.GetUserId(), orderId);

            var result = await _stripeService.ConfirmCheckoutByOrderAsync(orderId);

            if (result.Status == "NotFound")
            {
                return NotFound(new
                {
                    Success = false,
                    Message = $"No payment found for OrderId: {orderId}."
                });
            }
            if (result.Status == "AlreadyCompleted")
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Payment for OrderId: {orderId} is already completed."
                });
            }
            if (result.Status == "NotPaid")
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Payment for OrderId: {orderId} is not completed yet."
                });
            }
            if (result.Status == "OrderUpdateFailed")
            {
                return StatusCode(502, new
                {
                    Success = false,
                    Message = $"Payment succeeded but failed to update order status in OrderService."
                });
            }

            // Success
            return Ok(new
            {
                Success = true,
                Message = $"Payment confirmed and order updated for OrderId: {orderId}."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming Stripe payment for OrderId: {OrderId}", orderId);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while confirming the payment"
            });
        }
    }

    [HttpGet("status/{orderId}")]
    public async Task<ActionResult<PaymentStatusResponse>> GetPaymentStatus(int orderId)
    {
        try
        {
            _logger.LogInformation("User {UserId} checking payment status for OrderId: {OrderId}", 
                User.GetUserId(), orderId);

            var payment = await _bkashService.GetPaymentByOrderIdAsync(orderId) ?? await _stripeService.GetPaymentByOrderIdAsync(orderId);
            if (payment == null)
            {
                return NotFound(new PaymentStatusResponse { Status = "NotFound", Message = "No payment found for this order." });
            }

            return Ok(new PaymentStatusResponse
            {
                Status = payment.Status.ToString(),
                Message = $"Payment status for order {orderId} is {payment.Status}."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking payment status for OrderId: {OrderId}", orderId);
            return StatusCode(500, "An error occurred while checking the payment status");
        }
    }
}
