using Serilog.Events;
using Serilog;

namespace UserService.Logging;

public static class SerilogConfiguration
{
    public static void ConfigureSerilog(WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.File(
                path: "Logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90, // Keep last 3 months of daily logs
                restrictedToMinimumLevel: LogEventLevel.Information
            )
            //.WriteTo.Elasticsearch(...)  // Future: Elasticsearch config here
            .CreateLogger();

        builder.Host.UseSerilog();
    }
}
