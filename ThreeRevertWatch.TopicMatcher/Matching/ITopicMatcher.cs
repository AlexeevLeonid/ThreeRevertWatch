using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.TopicMatcher.Matching;

public interface ITopicMatcher
{
    Task<IReadOnlyList<TopicMatch>> MatchAsync(RawEditEvent edit, CancellationToken cancellationToken);
}

public sealed record TopicMatch(
    string TopicId,
    long PageId,
    string Title,
    double Confidence,
    IReadOnlyList<string> Reasons,
    TopicMembershipStatus Status);

