using System.Collections.Concurrent;
using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Aggregator.ReadModel;

public sealed class InMemoryConflictReadModelStore : IConflictReadModelStore
{
    private readonly ConcurrentDictionary<string, TopicSnapshotDto> _topics = new();
    private readonly ConcurrentDictionary<string, ArticleConflictSnapshotDto> _articles = new();

    public Task UpsertArticleAsync(ArticleConflictSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        _articles[ArticleKey(snapshot.TopicId, snapshot.PageId)] = snapshot;
        return Task.CompletedTask;
    }

    public Task UpsertTopicAsync(TopicSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        _topics[snapshot.TopicId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TopicSnapshotDto>> GetTopicsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TopicSnapshotDto>>(
            _topics.Values.OrderByDescending(t => t.ConflictScore).ThenBy(t => t.DisplayName).ToList());

    public Task<TopicSnapshotDto?> GetTopicAsync(string topicId, CancellationToken cancellationToken)
    {
        _topics.TryGetValue(topicId, out var topic);
        return Task.FromResult(topic);
    }

    public Task<IReadOnlyList<ArticleConflictSnapshotDto>> GetArticleSnapshotsAsync(
        string topicId,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ArticleConflictSnapshotDto>>(
            _articles.Values
                .Where(a => string.Equals(a.TopicId, topicId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.ConflictScore)
                .ToList());

    public Task<ArticleConflictSnapshotDto?> GetArticleSnapshotAsync(
        string topicId,
        long pageId,
        CancellationToken cancellationToken)
    {
        _articles.TryGetValue(ArticleKey(topicId, pageId), out var snapshot);
        return Task.FromResult(snapshot);
    }

    private static string ArticleKey(string topicId, long pageId) => $"{topicId}:{pageId}";
}

