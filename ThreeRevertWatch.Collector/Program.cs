using Serilog;
using ThreeRevertWatch.Collector;
using ThreeRevertWatch.Infrastructure.Logging;

SerilogExtensions.ConfigureBootstrap("Collector");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((_, configuration) =>
        configuration.ConfigureThreeRevertWatchLogging(builder.Configuration, "Collector"));
    builder.Services.AddCollector(builder.Configuration);
    builder.Services.AddHealthChecks();

    var app = builder.Build();
    app.MapHealthChecks("/health");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Collector terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
