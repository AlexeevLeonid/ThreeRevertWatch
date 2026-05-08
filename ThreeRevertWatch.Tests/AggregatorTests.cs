using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Aggregator.Configuration;
using ThreeRevertWatch.Aggregator.ReadModel;
using ThreeRevertWatch.Aggregator.Services;
using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Tests;

public sealed class AggregatorTests
{
    [Fact]
    public async Task Article_update_updates_topic_snapshot()
    {
        var service = CreateService(new InMemoryConflictReadModelStore());
        var update = new ArticleConflictUpdateEvent(
            Guid.NewGuid().ToString("n"),
            "topic",
            1,
            Edit(1),
            Snapshot(1, "A", 65),
            DateTimeOffset.UtcNow);

        var topic = await service.ApplyArticleUpdateAsync(update, CancellationToken.None);

        Assert.Equal("Conflict Topic", topic.DisplayName);
        Assert.Single(topic.Articles);
        Assert.True(topic.ConflictScore > 0);
    }

    [Fact]
    public async Task Topic_articles_are_sorted_by_conflict_score_descending()
    {
        var store = new InMemoryConflictReadModelStore();
        var service = CreateService(store);

        await service.ApplyArticleUpdateAsync(Update(Snapshot(1, "Low", 20)), CancellationToken.None);
        var topic = await service.ApplyArticleUpdateAsync(Update(Snapshot(2, "High", 80)), CancellationToken.None);

        Assert.Equal("High", topic.Articles[0].Title);
        Assert.Equal("Low", topic.Articles[1].Title);
    }

    [Fact]
    public async Task Topic_activity_groups_recent_edits_by_hour()
    {
        var store = new InMemoryConflictReadModelStore();
        await store.UpsertArticleAsync(Snapshot(1, "Article", 70), CancellationToken.None);

        var activity = await store.GetTopicActivityAsync("topic", 24, CancellationToken.None);

        Assert.Equal(24, activity.Hours.Count);
        Assert.Contains(activity.Hours, hour => hour.EditCount == 1 && hour.ParticipantCount == 1);
    }

    [Fact]
    public async Task Topic_list_keeps_only_preview_articles()
    {
        var store = new InMemoryConflictReadModelStore();
        await store.UpsertTopicAsync(TopicWithArticles(6), CancellationToken.None);

        var topics = await store.GetTopicsAsync(CancellationToken.None);
        var topic = await store.GetTopicAsync("topic", CancellationToken.None);

        Assert.Equal(4, topics.Single().Articles.Count);
        Assert.Empty(topic!.Articles);
    }

    private static ConflictAggregationService CreateService(IConflictReadModelStore store)
        => new(
            store,
            new TopicScoreCalculator(),
            Options.Create(new ConflictTopicsCatalogOptions
            {
                Topics = [new ConflictTopicCatalogItem { Id = "topic", DisplayName = "Conflict Topic" }]
            }),
            NullLogger<ConflictAggregationService>.Instance);

    private static ArticleConflictUpdateEvent Update(ArticleConflictSnapshotDto snapshot)
        => new(Guid.NewGuid().ToString("n"), "topic", snapshot.PageId, Edit(snapshot.PageId), snapshot, DateTimeOffset.UtcNow);

    private static ClassifiedEditDto Edit(long revision)
        => new("ruwiki", "topic", 1, "A", revision, revision - 1, "User", DateTimeOffset.UtcNow, "", [], EditActionType.OrdinaryEdit, .5, [], [], [], []);

    private static ArticleConflictSnapshotDto Snapshot(long pageId, string title, double score)
        => new(
            "topic",
            "ruwiki",
            pageId,
            title,
            .95,
            score,
            score >= 60 ? ArticleConflictStatus.EditWarCandidate : ArticleConflictStatus.Active,
            5,
            score >= 60 ? 3 : 1,
            4,
            [Edit(pageId)],
            [],
            [],
            [],
            [],
            [$"score {score}"],
            DateTimeOffset.UtcNow);

    private static TopicSnapshotDto TopicWithArticles(int count)
        => new(
            "topic",
            "Conflict Topic",
            50,
            TopicConflictStatus.Watching,
            count,
            count,
            0,
            count,
            Enumerable.Range(1, count)
                .Select(i => new ArticleListItemDto(
                    "topic",
                    "ruwiki",
                    i,
                    $"Article {i}",
                    .95,
                    50,
                    1,
                    0,
                    1,
                    ArticleConflictStatus.Active,
                    DateTimeOffset.UtcNow))
                .ToList(),
            [],
            DateTimeOffset.UtcNow);
}
