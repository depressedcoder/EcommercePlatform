using Serilog;
using System.Diagnostics;

namespace PaymentService.Logging;

public class ActivityLogMiddleware
{
    private readonly RequestDelegate _next;

    public ActivityLogMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var user = context.User.Identity?.IsAuthenticated == true ? context.User.Identity?.Name : "Anonymous";

        var request = context.Request;
        var method = request.Method;
        var path = request.Path;
        var ip = context.Connection.RemoteIpAddress?.ToString();

        string body = string.Empty;
        if (method == "POST" || method == "PUT")
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        await _next(context);
        stopwatch.Stop();

        Log.Information("AUDIT | {Timestamp} | {User} | {Method} {Path} | IP: {IP} | Status: {StatusCode} | {ElapsedMs}ms | Payload: {Payload}",
            DateTime.UtcNow,
            user,
            method,
            path,
            ip,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            body
        );
    }
}
