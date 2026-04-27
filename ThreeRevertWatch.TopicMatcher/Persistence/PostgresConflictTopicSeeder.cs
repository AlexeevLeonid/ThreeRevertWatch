using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.Infrastructure.Persistence;
using ThreeRevertWatch.TopicMatcher.Configuration;

namespace ThreeRevertWatch.TopicMatcher.Persistence;

public sealed class PostgresConflictTopicSeeder : IConflictTopicSeeder
{
    private readonly IPostgresConnectionFactory _connectionFactory;
    private readonly ConflictTopicsOptions _options;

    public PostgresConflictTopicSeeder(
        IPostgresConnectionFactory connectionFactory,
        IOptions<ConflictTopicsOptions> options)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        const string sql = """
INSERT INTO conflict_topics (id, display_name, wiki, is_active, config_json, created_at, updated_at)
VALUES (@id, @display_name, @wiki, @is_active, @config_json::jsonb, @created_at, @updated_at)
ON CONFLICT (id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    wiki = EXCLUDED.wiki,
    is_active = EXCLUDED.is_active,
    config_json = EXCLUDED.config_json,
    updated_at = EXCLUDED.updated_at
""";

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        foreach (var topic in _options.Topics)
        {
            var now = DateTimeOffset.UtcNow;
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", topic.Id);
            command.Parameters.AddWithValue("display_name", topic.DisplayName);
            command.Parameters.AddWithValue("wiki", topic.Wiki);
            command.Parameters.AddWithValue("is_active", topic.IsActive);
            command.Parameters.AddWithValue("config_json", JsonSerializer.Serialize(topic, KafkaJson.Options));
            command.Parameters.AddWithValue("created_at", now);
            command.Parameters.AddWithValue("updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

