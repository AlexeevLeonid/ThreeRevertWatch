using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.ConflictDetector.Persistence;

public interface IArticleConflictRepository
{
    Task SaveAsync(
        ClassifiedEditDto edit,
        ArticleConflictSnapshotDto snapshot,
        IReadOnlyList<RevertEdgeDto> newEdges,
        CancellationToken cancellationToken);
}

