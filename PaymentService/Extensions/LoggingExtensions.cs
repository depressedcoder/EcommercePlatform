using PaymentService.Logging;

namespace PaymentService.Extensions;

public static class LoggingExtensions
{
    public static void AddAppLogging(this WebApplicationBuilder builder)
    {
        SerilogConfiguration.ConfigureSerilog(builder);
    }
}
