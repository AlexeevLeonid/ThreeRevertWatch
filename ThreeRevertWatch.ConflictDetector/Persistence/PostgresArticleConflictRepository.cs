using System.Text.Json;
using Npgsql;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.Infrastructure.Persistence;

namespace ThreeRevertWatch.ConflictDetector.Persistence;

public sealed class PostgresArticleConflictRepository : IArticleConflictRepository
{
    private readonly IPostgresConnectionFactory _connectionFactory;

    public PostgresArticleConflictRepository(IPostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveAsync(
        ClassifiedEditDto edit,
        ArticleConflictSnapshotDto snapshot,
        IReadOnlyList<RevertEdgeDto> newEdges,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        await InsertEditAsync(connection, edit, cancellationToken);
        foreach (var edge in newEdges)
        {
            await InsertEdgeAsync(connection, edit, edge, cancellationToken);
        }

        await UpsertSnapshotAsync(connection, snapshot, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static async Task InsertEditAsync(NpgsqlConnection connection, ClassifiedEditDto edit, CancellationToken ct)
    {
        const string sql = """
INSERT INTO classified_edits (
    topic_id, wiki, page_id, title, revision_id, parent_revision_id, user_name,
    timestamp, comment, tags_json, action_type, confidence, reverted_users_json,
    reverted_revision_ids_json, fragment_ids_json, flags_json)
VALUES (
    @topic_id, @wiki, @page_id, @title, @revision_id, @parent_revision_id, @user_name,
    @timestamp, @comment, @tags_json::jsonb, @action_type, @confidence, @reverted_users_json::jsonb,
    @reverted_revision_ids_json::jsonb, @fragment_ids_json::jsonb, @flags_json::jsonb)
""";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", edit.TopicId);
        command.Parameters.AddWithValue("wiki", edit.Wiki);
        command.Parameters.AddWithValue("page_id", edit.PageId);
        command.Parameters.AddWithValue("title", edit.Title);
        command.Parameters.AddWithValue("revision_id", edit.RevisionId);
        command.Parameters.AddWithValue("parent_revision_id", (object?)edit.ParentRevisionId ?? DBNull.Value);
        command.Parameters.AddWithValue("user_name", edit.User);
        command.Parameters.AddWithValue("timestamp", edit.Timestamp);
        command.Parameters.AddWithValue("comment", edit.Comment);
        command.Parameters.AddWithValue("tags_json", JsonSerializer.Serialize(edit.Tags, KafkaJson.Options));
        command.Parameters.AddWithValue("action_type", edit.Action.ToString());
        command.Parameters.AddWithValue("confidence", edit.Confidence);
        command.Parameters.AddWithValue("reverted_users_json", JsonSerializer.Serialize(edit.RevertedUsers, KafkaJson.Options));
        command.Parameters.AddWithValue("reverted_revision_ids_json", JsonSerializer.Serialize(edit.RevertedRevisionIds, KafkaJson.Options));
        command.Parameters.AddWithValue("fragment_ids_json", JsonSerializer.Serialize(edit.FragmentIds, KafkaJson.Options));
        command.Parameters.AddWithValue("flags_json", JsonSerializer.Serialize(edit.Flags, KafkaJson.Options));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertEdgeAsync(
        NpgsqlConnection connection,
        ClassifiedEditDto edit,
        RevertEdgeDto edge,
        CancellationToken ct)
    {
        const string sql = """
INSERT INTO revert_edges (
    topic_id, wiki, page_id, from_user, to_user, from_revision_id, to_revision_id,
    revert_type, confidence, fragment_id, timestamp)
VALUES (
    @topic_id, @wiki, @page_id, @from_user, @to_user, @from_revision_id, @to_revision_id,
    @revert_type, @confidence, @fragment_id, @timestamp)
""";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("topic_id", edit.TopicId);
        command.Parameters.AddWithValue("wiki", edit.Wiki);
        command.Parameters.AddWithValue("page_id", edit.PageId);
        command.Parameters.AddWithValue("from_user", edge.FromUser);
        command.Parameters.AddWithValue("to_user", edge.ToUser);
        command.Parameters.AddWithValue("from_revision_id", edge.FromRevisionId);
        command.Parameters.AddWithValue("to_revision_id", edge.ToRevisionId);
        command.Parameters.AddWithValue("revert_type", edge.RevertType);
        command.Parameters.AddWithValue("confidence", edge.Confidence);
        command.Parameters.AddWithValue("fragment_id", (object?)edge.FragmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("timestamp", edge.Timestamp);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertSnapshotAsync(
        NpgsqlConnection connection,
        ArticleConflictSnapshotDto snapshot,
        CancellationToken ct)
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
        await using var command = new NpgsqlCommand(sql, connection);
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
        await command.ExecuteNonQueryAsync(ct);
    }
}

