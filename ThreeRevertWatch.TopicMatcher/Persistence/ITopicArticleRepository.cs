using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.TopicMatcher.Persistence;

public interface ITopicArticleRepository
{
    Task<TopicArticleMembershipDto?> GetAsync(
        string topicId,
        string wiki,
        long pageId,
        CancellationToken cancellationToken);

    Task UpsertAsync(TopicArticleMembershipDto membership, CancellationToken cancellationToken);
}

