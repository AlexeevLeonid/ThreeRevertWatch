using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.ConflictDetector.Scoring;

public interface IParticipantClusterInferer
{
    IReadOnlyList<ParticipantClusterDto> Infer(ArticleRuntimeState state);
}

