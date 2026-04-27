using Serilog;
using ThreeRevertWatch.ConflictDetector;
using ThreeRevertWatch.Infrastructure.Logging;
using ThreeRevertWatch.Infrastructure.Persistence;

SerilogExtensions.ConfigureBootstrap("ConflictDetector");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((_, configuration) =>
        configuration.ConfigureThreeRevertWatchLogging(builder.Configuration, "ConflictDetector"));
    builder.Services.AddConflictDetector(builder.Configuration);
    builder.Services.AddHealthChecks();

    var app = builder.Build();
    if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        await app.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);
    }

    app.MapHealthChecks("/health");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ConflictDetector terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
