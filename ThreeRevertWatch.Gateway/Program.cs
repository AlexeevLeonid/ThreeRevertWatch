using Serilog;
using ThreeRevertWatch.Gateway;
using ThreeRevertWatch.Gateway.Hubs;
using ThreeRevertWatch.Gateway.Services;
using ThreeRevertWatch.Infrastructure.Logging;

SerilogExtensions.ConfigureBootstrap("Gateway");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((_, configuration) =>
        configuration.ConfigureThreeRevertWatchLogging(builder.Configuration, "Gateway"));

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LanCors", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
            else
            {
                policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true);
            }
        });
    });

    builder.Services.AddGatewayServices(builder.Configuration);
    builder.Services.AddHealthChecks();

    var app = builder.Build();
    app.UseCors("LanCors");
    app.MapHealthChecks("/health");
    app.MapHub<ConflictsHub>("/hubs/conflicts");

    app.MapGet("/api/conflicts/topics", async (AggregatorProxyClient client, CancellationToken ct) =>
        Results.Ok(await client.GetTopicsAsync(ct)));

    app.MapGet("/api/conflicts/topics/{topicId}", async (string topicId, AggregatorProxyClient client, CancellationToken ct) =>
    {
        var topic = await client.GetTopicAsync(topicId, ct);
        return topic is null ? Results.NotFound() : Results.Ok(topic);
    });

    app.MapGet("/api/conflicts/topics/{topicId}/articles", async (string topicId, AggregatorProxyClient client, CancellationToken ct) =>
        Results.Ok(await client.GetArticlesAsync(topicId, ct)));

    app.MapGet("/api/conflicts/topics/{topicId}/articles/{pageId:long}", async (
        string topicId,
        long pageId,
        AggregatorProxyClient client,
        CancellationToken ct) =>
    {
        var article = await client.GetArticleAsync(topicId, pageId, ct);
        return article is null ? Results.NotFound() : Results.Ok(article);
    });

    app.MapGet("/api/conflicts/topics/{topicId}/articles/{pageId:long}/edits", async (
        string topicId,
        long pageId,
        AggregatorProxyClient client,
        CancellationToken ct) =>
        Results.Ok(await client.GetEditsAsync(topicId, pageId, ct)));

    app.MapGet("/api/conflicts/topics/{topicId}/articles/{pageId:long}/participants", async (
        string topicId,
        long pageId,
        AggregatorProxyClient client,
        CancellationToken ct) =>
    {
        var participants = await client.GetParticipantsAsync(topicId, pageId, ct);
        return participants is null ? Results.NotFound() : Results.Ok(participants);
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
