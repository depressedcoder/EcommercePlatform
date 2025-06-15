using Serilog;
using System.Diagnostics;

namespace PaymentService.Logging;

public class ActivityLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ActivityLogMiddleware> _logger;

    public ActivityLogMiddleware(RequestDelegate next, ILogger<ActivityLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Request started: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await _next(context);

            sw.Stop();
            _logger.LogInformation(
                "Request completed: {Method} {Path} - Status: {StatusCode} - Time: {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Request failed: {Method} {Path} - Time: {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
