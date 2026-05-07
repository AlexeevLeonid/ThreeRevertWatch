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

    public Task<TopicActivityDto> GetTopicActivityAsync(
        string topicId,
        int hours,
        CancellationToken cancellationToken)
    {
        var (from, to, buckets) = CreateEmptyBuckets(topicId, hours);
        var edits = _articles.Values
            .Where(a => string.Equals(a.TopicId, topicId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(a => a.RecentEdits)
            .Where(e => e.Timestamp >= from && e.Timestamp < to);

        foreach (var group in edits.GroupBy(e => FloorHour(e.Timestamp)))
        {
            if (!buckets.TryGetValue(group.Key, out var current))
            {
                continue;
            }

            buckets[group.Key] = current with
            {
                EditCount = group.Count(),
                RevertCount = group.Count(e => IsRevertAction(e.Action)),
                ParticipantCount = group.Select(e => e.User).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            };
        }

        return Task.FromResult(new TopicActivityDto(topicId, from, to, buckets.Values.ToList()));
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

    private static (DateTimeOffset From, DateTimeOffset To, SortedDictionary<DateTimeOffset, TopicHourlyActivityDto> Buckets)
        CreateEmptyBuckets(string topicId, int hours)
    {
        var clampedHours = Math.Clamp(hours, 1, 168);
        var now = DateTimeOffset.UtcNow;
        var currentHour = FloorHour(now);
        var from = currentHour.AddHours(-(clampedHours - 1));
        var to = now;
        var buckets = new SortedDictionary<DateTimeOffset, TopicHourlyActivityDto>();
        for (var hour = from; hour <= currentHour; hour = hour.AddHours(1))
        {
            buckets[hour] = new TopicHourlyActivityDto(hour, 0, 0, 0);
        }

        return (from, to, buckets);
    }

    private static DateTimeOffset FloorHour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    private static bool IsRevertAction(EditActionType action)
        => action is EditActionType.ExactRevert
            or EditActionType.PartialRevert
            or EditActionType.Restoration
            or EditActionType.CounterRevert
            or EditActionType.SelfRevert
            or EditActionType.VandalismCleanup
            or EditActionType.SpamCleanup
            or EditActionType.CopyvioCleanup;
}
