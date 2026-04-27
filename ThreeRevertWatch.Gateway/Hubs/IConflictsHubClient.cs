using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Gateway.Hubs;

public interface IConflictsHubClient
{
    Task TopicSnapshotUpdated(TopicSnapshotDto snapshot);

    Task ArticleConflictUpdated(ArticleConflictUpdateEvent update);

    Task ConflictAlert(ConflictAlertDto alert);
}

