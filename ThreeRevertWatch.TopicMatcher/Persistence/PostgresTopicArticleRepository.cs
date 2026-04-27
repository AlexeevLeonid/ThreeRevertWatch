using System.Text.Json;
using Npgsql;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.Infrastructure.Persistence;

namespace ThreeRevertWatch.TopicMatcher.Persistence;

public sealed class PostgresTopicArticleRepository : ITopicArticleRepository
{
    private readonly IPostgresConnectionFactory _connectionFactory;

    public PostgresTopicArticleRepository(IPostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<TopicArticleMembershipDto?> GetAsync(
        string topicId,
        string wiki,
        long pageId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT topic_id, wiki, page_id, title, membership_status, relevance_score, reasons_json, first_seen_at, last_seen_at
FROM topic_articles
WHERE topic_id = @topic_id AND wiki = @wiki AND page_id = @page_id
""";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", topicId);
        command.Parameters.AddWithValue("wiki", wiki);
        command.Parameters.AddWithValue("page_id", pageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TopicArticleMembershipDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetString(3),
            Enum.Parse<TopicMembershipStatus>(reader.GetString(4)),
            reader.GetDouble(5),
            JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(6), KafkaJson.Options) ?? [],
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8));
    }

    public async Task UpsertAsync(TopicArticleMembershipDto membership, CancellationToken cancellationToken)
    {
        const string sql = """
INSERT INTO topic_articles (
    topic_id, wiki, page_id, title, membership_status, relevance_score,
    reasons_json, first_seen_at, last_seen_at)
VALUES (
    @topic_id, @wiki, @page_id, @title, @membership_status, @relevance_score,
    @reasons_json::jsonb, @first_seen_at, @last_seen_at)
ON CONFLICT (topic_id, wiki, page_id) DO UPDATE SET
    title = EXCLUDED.title,
    membership_status = EXCLUDED.membership_status,
    relevance_score = GREATEST(topic_articles.relevance_score, EXCLUDED.relevance_score),
    reasons_json = EXCLUDED.reasons_json,
    last_seen_at = EXCLUDED.last_seen_at
""";
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", membership.TopicId);
        command.Parameters.AddWithValue("wiki", membership.Wiki);
        command.Parameters.AddWithValue("page_id", membership.PageId);
        command.Parameters.AddWithValue("title", membership.Title);
        command.Parameters.AddWithValue("membership_status", membership.Status.ToString());
        command.Parameters.AddWithValue("relevance_score", membership.RelevanceScore);
        command.Parameters.AddWithValue("reasons_json", JsonSerializer.Serialize(membership.Reasons, KafkaJson.Options));
        command.Parameters.AddWithValue("first_seen_at", membership.FirstSeenAt);
        command.Parameters.AddWithValue("last_seen_at", membership.LastSeenAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

