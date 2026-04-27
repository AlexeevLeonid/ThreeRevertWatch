using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.ConflictDetector.Classification;

public interface IEditClassifier
{
    Task<ClassifiedEditDto> ClassifyAsync(
        TopicMatchedEditEvent matchedEdit,
        RevisionDetails revision,
        ArticleRuntimeState state,
        CancellationToken cancellationToken);
}

