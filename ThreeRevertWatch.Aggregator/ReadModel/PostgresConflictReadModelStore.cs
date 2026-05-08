using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.Infrastructure.Persistence;

namespace ThreeRevertWatch.Aggregator.ReadModel;

public sealed class PostgresConflictReadModelStore : IConflictReadModelStore
{
    private readonly IPostgresConnectionFactory _connectionFactory;
    private readonly IDistributedCache? _cache;

    public PostgresConflictReadModelStore(IPostgresConnectionFactory connectionFactory, IServiceProvider serviceProvider)
    {
        _connectionFactory = connectionFactory;
        _cache = serviceProvider.GetService<IDistributedCache>();
    }

    public async Task UpsertArticleAsync(ArticleConflictSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        const string sql = """
INSERT INTO article_conflict_snapshots (
    topic_id, wiki, page_id, title, relevance_score, conflict_score, status,
    recent_edit_count, recent_revert_count, recent_participant_count, snapshot_json, updated_at)
VALUES (
    @topic_id, @wiki, @page_id, @title, @relevance_score, @conflict_score, @status,
    @recent_edit_count, @recent_revert_count, @recent_participant_count, @snapshot_json::jsonb, @updated_at)
ON CONFLICT (topic_id, wiki, page_id) DO UPDATE SET
    title = EXCLUDED.title,
    relevance_score = EXCLUDED.relevance_score,
    conflict_score = EXCLUDED.conflict_score,
    status = EXCLUDED.status,
    recent_edit_count = EXCLUDED.recent_edit_count,
    recent_revert_count = EXCLUDED.recent_revert_count,
    recent_participant_count = EXCLUDED.recent_participant_count,
    snapshot_json = EXCLUDED.snapshot_json,
    updated_at = EXCLUDED.updated_at
""";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddArticleParameters(command, snapshot);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await CacheSetAsync(ArticleCacheKey(snapshot.TopicId, snapshot.PageId), snapshot, cancellationToken);
    }

    public async Task UpsertTopicAsync(TopicSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        const string sql = """
INSERT INTO topic_snapshots (
    topic_id, conflict_score, status, active_article_count, recent_edit_count,
    recent_revert_count, recent_participant_count, snapshot_json, updated_at)
VALUES (
    @topic_id, @conflict_score, @status, @active_article_count, @recent_edit_count,
    @recent_revert_count, @recent_participant_count, @snapshot_json::jsonb, @updated_at)
ON CONFLICT (topic_id) DO UPDATE SET
    conflict_score = EXCLUDED.conflict_score,
    status = EXCLUDED.status,
    active_article_count = EXCLUDED.active_article_count,
    recent_edit_count = EXCLUDED.recent_edit_count,
    recent_revert_count = EXCLUDED.recent_revert_count,
    recent_participant_count = EXCLUDED.recent_participant_count,
    snapshot_json = EXCLUDED.snapshot_json,
    updated_at = EXCLUDED.updated_at
""";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", snapshot.TopicId);
        command.Parameters.AddWithValue("conflict_score", snapshot.ConflictScore);
        command.Parameters.AddWithValue("status", snapshot.Status.ToString());
        command.Parameters.AddWithValue("active_article_count", snapshot.ActiveArticleCount);
        command.Parameters.AddWithValue("recent_edit_count", snapshot.RecentEditCount);
        command.Parameters.AddWithValue("recent_revert_count", snapshot.RecentRevertCount);
        command.Parameters.AddWithValue("recent_participant_count", snapshot.RecentParticipantCount);
        command.Parameters.AddWithValue("snapshot_json", JsonSerializer.Serialize(snapshot, KafkaJson.Options));
        command.Parameters.AddWithValue("updated_at", snapshot.UpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await CacheSetAsync(TopicCacheKey(snapshot.TopicId), CompactTopic(snapshot, 0), cancellationToken);
    }

    public async Task<IReadOnlyList<TopicSnapshotDto>> GetTopicsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
SELECT jsonb_set(
    snapshot_json - 'articles',
    '{articles}',
    COALESCE((
        SELECT jsonb_agg(article)
        FROM (
            SELECT article
            FROM jsonb_array_elements(snapshot_json->'articles') WITH ORDINALITY AS articles(article, ordinal)
            WHERE ordinal <= 4
        ) top_articles
    ), '[]'::jsonb)
)::text
FROM topic_snapshots
ORDER BY conflict_score DESC, updated_at DESC
""";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var topics = new List<TopicSnapshotDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var topic = JsonSerializer.Deserialize<TopicSnapshotDto>(reader.GetString(0), KafkaJson.Options);
            if (topic is not null)
            {
                topics.Add(topic);
            }
        }

        return topics;
    }

    public async Task<TopicSnapshotDto?> GetTopicAsync(string topicId, CancellationToken cancellationToken)
    {
        var cached = await CacheGetAsync<TopicSnapshotDto>(TopicCacheKey(topicId), cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        const string sql = """
SELECT jsonb_set(snapshot_json - 'articles', '{articles}', '[]'::jsonb)::text
FROM topic_snapshots
WHERE topic_id = @topic_id
""";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", topicId);
        var result = await command.ExecuteScalarAsync(cancellationToken) as string;
        return result is null ? null : JsonSerializer.Deserialize<TopicSnapshotDto>(result, KafkaJson.Options);
    }

    public async Task<TopicActivityDto> GetTopicActivityAsync(
        string topicId,
        int hours,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT
    date_trunc('hour', "timestamp") AS hour_start,
    count(*)::integer AS edit_count,
    count(*) FILTER (
        WHERE action_type IN (
            'ExactRevert',
            'PartialRevert',
            'Restoration',
            'CounterRevert',
            'SelfRevert',
            'VandalismCleanup',
            'SpamCleanup',
            'CopyvioCleanup'
        )
    )::integer AS revert_count,
    count(DISTINCT user_name)::integer AS participant_count
FROM classified_edits
WHERE topic_id = @topic_id
  AND "timestamp" >= @from
  AND "timestamp" < @to
GROUP BY hour_start
ORDER BY hour_start
""";

        var (from, to, buckets) = CreateEmptyBuckets(hours);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", topicId);
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("to", to);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var hourStart = FloorHour(reader.GetFieldValue<DateTimeOffset>(0));
            if (!buckets.ContainsKey(hourStart))
            {
                continue;
            }

            buckets[hourStart] = new TopicHourlyActivityDto(
                hourStart,
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3));
        }

        return new TopicActivityDto(topicId, from, to, buckets.Values.ToList());
    }

    public async Task<IReadOnlyList<ArticleConflictSnapshotDto>> GetArticleSnapshotsAsync(
        string topicId,
        CancellationToken cancellationToken,
        int? limit = null)
    {
        const string sql = """
SELECT snapshot_json
FROM article_conflict_snapshots
WHERE topic_id = @topic_id
ORDER BY conflict_score DESC, updated_at DESC
LIMIT @limit
""";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", topicId);
        command.Parameters.AddWithValue("limit", limit ?? int.MaxValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var articles = new List<ArticleConflictSnapshotDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var article = JsonSerializer.Deserialize<ArticleConflictSnapshotDto>(reader.GetString(0), KafkaJson.Options);
            if (article is not null)
            {
                articles.Add(article);
            }
        }

        return articles;
    }

    public async Task<ArticleConflictSnapshotDto?> GetArticleSnapshotAsync(
        string topicId,
        long pageId,
        CancellationToken cancellationToken)
    {
        var cached = await CacheGetAsync<ArticleConflictSnapshotDto>(ArticleCacheKey(topicId, pageId), cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        const string sql = "SELECT snapshot_json FROM article_conflict_snapshots WHERE topic_id = @topic_id AND page_id = @page_id";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", topicId);
        command.Parameters.AddWithValue("page_id", pageId);
        var result = await command.ExecuteScalarAsync(cancellationToken) as string;
        return result is null ? null : JsonSerializer.Deserialize<ArticleConflictSnapshotDto>(result, KafkaJson.Options);
    }

    private static void AddArticleParameters(NpgsqlCommand command, ArticleConflictSnapshotDto snapshot)
    {
        command.Parameters.AddWithValue("topic_id", snapshot.TopicId);
        command.Parameters.AddWithValue("wiki", snapshot.Wiki);
        command.Parameters.AddWithValue("page_id", snapshot.PageId);
        command.Parameters.AddWithValue("title", snapshot.Title);
        command.Parameters.AddWithValue("relevance_score", snapshot.TopicRelevanceScore);
        command.Parameters.AddWithValue("conflict_score", snapshot.ConflictScore);
        command.Parameters.AddWithValue("status", snapshot.Status.ToString());
        command.Parameters.AddWithValue("recent_edit_count", snapshot.RecentEditCount);
        command.Parameters.AddWithValue("recent_revert_count", snapshot.RecentRevertCount);
        command.Parameters.AddWithValue("recent_participant_count", snapshot.RecentParticipantCount);
        command.Parameters.AddWithValue("snapshot_json", JsonSerializer.Serialize(snapshot, KafkaJson.Options));
        command.Parameters.AddWithValue("updated_at", snapshot.UpdatedAt);
    }

    private async Task CacheSetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        if (_cache is null)
        {
            return;
        }

        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(value, KafkaJson.Options),
            new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(15) },
            cancellationToken);
    }

    private async Task<T?> CacheGetAsync<T>(string key, CancellationToken cancellationToken)
    {
        if (_cache is null)
        {
            return default;
        }

        var json = await _cache.GetStringAsync(key, cancellationToken);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, KafkaJson.Options);
    }

    private static string TopicCacheKey(string topicId) => $"conflict-topic:{topicId}";
    private static string ArticleCacheKey(string topicId, long pageId) => $"conflict-article:{topicId}:{pageId}";

    private static TopicSnapshotDto CompactTopic(TopicSnapshotDto topic, int articleLimit)
        => topic with { Articles = topic.Articles.Take(articleLimit).ToList() };

    private static (DateTimeOffset From, DateTimeOffset To, SortedDictionary<DateTimeOffset, TopicHourlyActivityDto> Buckets)
        CreateEmptyBuckets(int hours)
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
}
