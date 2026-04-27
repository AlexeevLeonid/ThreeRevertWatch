using Microsoft.Extensions.Options;
using ThreeRevertWatch.Aggregator.Configuration;
using ThreeRevertWatch.Aggregator.ReadModel;
using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Aggregator.Services;

public sealed class ConflictAggregationService : IConflictAggregationService
{
    private readonly IConflictReadModelStore _store;
    private readonly TopicScoreCalculator _calculator;
    private readonly ConflictTopicsCatalogOptions _topics;
    private readonly ILogger<ConflictAggregationService> _logger;

    public ConflictAggregationService(
        IConflictReadModelStore store,
        TopicScoreCalculator calculator,
        IOptions<ConflictTopicsCatalogOptions> topics,
        ILogger<ConflictAggregationService> logger)
    {
        _store = store;
        _calculator = calculator;
        _topics = topics.Value;
        _logger = logger;
    }

    public async Task<TopicSnapshotDto> ApplyArticleUpdateAsync(
        ArticleConflictUpdateEvent update,
        CancellationToken cancellationToken)
    {
        await _store.UpsertArticleAsync(update.Snapshot, cancellationToken);
        var articles = await _store.GetArticleSnapshotsAsync(update.TopicId, cancellationToken);
        var sortedArticles = articles
            .OrderByDescending(a => a.ConflictScore)
            .ThenByDescending(a => a.UpdatedAt)
            .Select(ToListItem)
            .ToList();

        var score = _calculator.Calculate(articles);
        var topic = new TopicSnapshotDto(
            update.TopicId,
            DisplayName(update.TopicId),
            Math.Round(score, 2),
            TopicScoreCalculator.StatusFor(score),
            articles.Count(a => a.ConflictScore >= 30),
            articles.Sum(a => a.RecentEditCount),
            articles.Sum(a => a.RecentRevertCount),
            articles.Sum(a => a.RecentParticipantCount),
            sortedArticles,
            BuildEvidence(articles),
            DateTimeOffset.UtcNow);

        await _store.UpsertTopicAsync(topic, cancellationToken);

        _logger.LogInformation(
            "Metric={Metric} TopicId={TopicId} ConflictScore={ConflictScore} Status={Status} ActiveArticleCount={ActiveArticleCount}",
            "aggregator_topic_snapshot_updated",
            topic.TopicId,
            topic.ConflictScore,
            topic.Status,
            topic.ActiveArticleCount);

        return topic;
    }

    private string DisplayName(string topicId)
        => _topics.Topics.FirstOrDefault(t => string.Equals(t.Id, topicId, StringComparison.OrdinalIgnoreCase))?.DisplayName
           ?? topicId;

    private static ArticleListItemDto ToListItem(ArticleConflictSnapshotDto article)
        => new(
            article.TopicId,
            article.Wiki,
            article.PageId,
            article.Title,
            article.TopicRelevanceScore,
            article.ConflictScore,
            article.RecentEditCount,
            article.RecentRevertCount,
            article.RecentParticipantCount,
            article.Status,
            article.RecentEdits.FirstOrDefault()?.Timestamp ?? article.UpdatedAt);

    private static IReadOnlyList<string> BuildEvidence(IReadOnlyList<ArticleConflictSnapshotDto> articles)
    {
        var evidence = new List<string>();
        var hot = articles.Where(a => a.ConflictScore >= 60).OrderByDescending(a => a.ConflictScore).Take(3).ToList();
        if (hot.Count > 0)
        {
            evidence.Add($"hot articles: {string.Join(", ", hot.Select(a => $"{a.Title} ({a.ConflictScore:0})"))}");
        }

        var reverts = articles.Sum(a => a.RecentRevertCount);
        if (reverts > 0)
        {
            evidence.Add($"recent reverts across topic: {reverts}");
        }

        var spread = articles.Count(a => a.ConflictScore >= 30);
        if (spread > 1)
        {
            evidence.Add($"conflict spread across {spread} articles");
        }

        return evidence;
    }
}

