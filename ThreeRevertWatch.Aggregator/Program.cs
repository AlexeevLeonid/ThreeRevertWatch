using Microsoft.Extensions.Options;
using Serilog;
using ThreeRevertWatch.Aggregator;
using ThreeRevertWatch.Aggregator.Configuration;
using ThreeRevertWatch.Aggregator.ReadModel;
using ThreeRevertWatch.Infrastructure.Logging;
using ThreeRevertWatch.Infrastructure.Persistence;

SerilogExtensions.ConfigureBootstrap("Aggregator");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((_, configuration) =>
        configuration.ConfigureThreeRevertWatchLogging(builder.Configuration, "Aggregator"));

    var redis = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redis))
    {
        builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redis);
    }

    builder.Services.AddAggregator(builder.Configuration);
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        await app.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);
    }

    app.MapHealthChecks("/health");

    app.MapGet("/api/conflicts/topics", async (
        IConflictReadModelStore store,
        IOptions<ConflictTopicsCatalogOptions> options,
        CancellationToken ct) =>
    {
        var topicIds = options.Value.Topics
            .Select(t => t.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var topics = await store.GetTopicsAsync(ct);
        return Results.Ok(topics
            .Where(topic => topicIds.Contains(topic.TopicId))
            .Select(topic => CompactTopic(topic, 4)));
    });

    app.MapGet("/api/conflicts/topics/{topicId}", async (
        string topicId,
        IConflictReadModelStore store,
        IOptions<ConflictTopicsCatalogOptions> options,
        CancellationToken ct) =>
    {
        if (!IsTrackedTopic(topicId, options.Value))
        {
            return Results.NotFound();
        }

        var topic = await store.GetTopicAsync(topicId, ct);
        return topic is null ? Results.NotFound() : Results.Ok(CompactTopic(topic, 0));
    });

    app.MapGet("/api/conflicts/topics/{topicId}/articles", async (
        string topicId,
        int? limit,
        IConflictReadModelStore store,
        IOptions<ConflictTopicsCatalogOptions> options,
        CancellationToken ct) =>
    {
        if (!IsTrackedTopic(topicId, options.Value))
        {
            return Results.NotFound();
        }

        var articles = await store.GetArticleSnapshotsAsync(topicId, ct, ClampLimit(limit));
        return Results.Ok(articles
            .OrderByDescending(a => a.ConflictScore)
            .Select(a => new ThreeRevertWatch.Contracts.ArticleListItemDto(
                a.TopicId,
                a.Wiki,
                a.PageId,
                a.Title,
                a.TopicRelevanceScore,
                a.ConflictScore,
                a.RecentEditCount,
                a.RecentRevertCount,
                a.RecentParticipantCount,
                a.Status,
                a.RecentEdits.FirstOrDefault()?.Timestamp ?? a.UpdatedAt)));
    });

    app.MapGet("/api/conflicts/topics/{topicId}/activity", async (
        string topicId,
        int? hours,
        IConflictReadModelStore store,
        IOptions<ConflictTopicsCatalogOptions> options,
        CancellationToken ct) =>
    {
        if (!IsTrackedTopic(topicId, options.Value))
        {
            return Results.NotFound();
        }

        return Results.Ok(await store.GetTopicActivityAsync(topicId, hours ?? 24, ct));
    });

    app.MapGet("/api/conflicts/topics/{topicId}/articles/{pageId:long}", async (
        string topicId,
        long pageId,
        IConflictReadModelStore store,
        IOptions<ConflictTopicsCatalogOptions> options,
        CancellationToken ct) =>
    {
        if (!IsTrackedTopic(topicId, options.Value))
        {
            return Results.NotFound();
        }

        var article = await store.GetArticleSnapshotAsync(topicId, pageId, ct);
        return article is null ? Results.NotFound() : Results.Ok(article);
    });

    app.MapGet("/api/conflicts/topics/{topicId}/articles/{pageId:long}/edits", async (
        string topicId,
        long pageId,
        IConflictReadModelStore store,
        IOptions<ConflictTopicsCatalogOptions> options,
        CancellationToken ct) =>
    {
        if (!IsTrackedTopic(topicId, options.Value))
        {
            return Results.NotFound();
        }

        var article = await store.GetArticleSnapshotAsync(topicId, pageId, ct);
        return article is null ? Results.NotFound() : Results.Ok(article.RecentEdits);
    });

    app.MapGet("/api/conflicts/topics/{topicId}/articles/{pageId:long}/participants", async (
        string topicId,
        long pageId,
        IConflictReadModelStore store,
        IOptions<ConflictTopicsCatalogOptions> options,
        CancellationToken ct) =>
    {
        if (!IsTrackedTopic(topicId, options.Value))
        {
            return Results.NotFound();
        }

        var article = await store.GetArticleSnapshotAsync(topicId, pageId, ct);
        return article is null
            ? Results.NotFound()
            : Results.Ok(new { article.Participants, article.ParticipantClusters });
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aggregator terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static bool IsTrackedTopic(string topicId, ConflictTopicsCatalogOptions options)
    => options.Topics.Any(topic => string.Equals(topic.Id, topicId, StringComparison.OrdinalIgnoreCase));

static ThreeRevertWatch.Contracts.TopicSnapshotDto CompactTopic(
    ThreeRevertWatch.Contracts.TopicSnapshotDto topic,
    int articleLimit)
    => topic with { Articles = topic.Articles.Take(articleLimit).ToList() };

static int? ClampLimit(int? limit)
    => limit is null ? null : Math.Clamp(limit.Value, 1, 500);
