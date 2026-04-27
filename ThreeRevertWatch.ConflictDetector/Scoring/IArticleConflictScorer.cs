using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.ConflictDetector.Scoring;

public interface IArticleConflictScorer
{
    ArticleConflictSnapshotDto BuildSnapshot(ArticleRuntimeState state, ClassifiedEditDto latestEdit);
}

