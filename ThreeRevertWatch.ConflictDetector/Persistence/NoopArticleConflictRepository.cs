using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.ConflictDetector.Persistence;

public sealed class NoopArticleConflictRepository : IArticleConflictRepository
{
    public Task SaveAsync(
        ClassifiedEditDto edit,
        ArticleConflictSnapshotDto snapshot,
        IReadOnlyList<RevertEdgeDto> newEdges,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}

