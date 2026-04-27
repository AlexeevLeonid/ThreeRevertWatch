using Serilog;
using ThreeRevertWatch.Infrastructure.Logging;
using ThreeRevertWatch.Infrastructure.Persistence;
using ThreeRevertWatch.TopicMatcher;
using ThreeRevertWatch.TopicMatcher.Persistence;

SerilogExtensions.ConfigureBootstrap("TopicMatcher");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((_, configuration) =>
        configuration.ConfigureThreeRevertWatchLogging(builder.Configuration, "TopicMatcher"));
    builder.Services.AddTopicMatcher(builder.Configuration);
    builder.Services.AddHealthChecks();

    var app = builder.Build();
    if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        await app.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);
        await app.Services.GetRequiredService<IConflictTopicSeeder>().SeedAsync(app.Lifetime.ApplicationStopping);
    }

    app.MapHealthChecks("/health");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TopicMatcher terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
