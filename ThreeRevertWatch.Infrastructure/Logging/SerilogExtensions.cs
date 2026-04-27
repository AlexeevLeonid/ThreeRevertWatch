using Microsoft.Extensions.Configuration;
using Serilog;

namespace ThreeRevertWatch.Infrastructure.Logging;

public static class SerilogExtensions
{
    public static void ConfigureBootstrap(string serviceName)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    public static LoggerConfiguration ConfigureThreeRevertWatchLogging(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string serviceName)
    {
        return loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName);
    }
}

