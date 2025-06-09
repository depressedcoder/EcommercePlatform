using OrderService.Logging;

namespace OrderService.Extensions;

public static class LoggingExtensions
{
    public static void AddAppLogging(this WebApplicationBuilder builder)
    {
        SerilogConfiguration.ConfigureSerilog(builder);
    }
}
