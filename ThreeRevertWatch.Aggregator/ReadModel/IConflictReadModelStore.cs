using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Aggregator.ReadModel;

public interface IConflictReadModelStore
{
    Task UpsertArticleAsync(ArticleConflictSnapshotDto snapshot, CancellationToken cancellationToken);

    Task UpsertTopicAsync(TopicSnapshotDto snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<TopicSnapshotDto>> GetTopicsAsync(CancellationToken cancellationToken);

    Task<TopicSnapshotDto?> GetTopicAsync(string topicId, CancellationToken cancellationToken);

    Task<TopicActivityDto> GetTopicActivityAsync(
        string topicId,
        int hours,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArticleConflictSnapshotDto>> GetArticleSnapshotsAsync(
        string topicId,
        CancellationToken cancellationToken);

    Task<ArticleConflictSnapshotDto?> GetArticleSnapshotAsync(
        string topicId,
        long pageId,
        CancellationToken cancellationToken);
}
