using System.Collections.Concurrent;
using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.TopicMatcher.Persistence;

public sealed class InMemoryTopicArticleRepository : ITopicArticleRepository
{
    private readonly ConcurrentDictionary<string, TopicArticleMembershipDto> _items = new();

    public Task<TopicArticleMembershipDto?> GetAsync(
        string topicId,
        string wiki,
        long pageId,
        CancellationToken cancellationToken)
    {
        _items.TryGetValue(Key(topicId, wiki, pageId), out var membership);
        return Task.FromResult(membership);
    }

    public Task UpsertAsync(TopicArticleMembershipDto membership, CancellationToken cancellationToken)
    {
        _items.AddOrUpdate(Key(membership.TopicId, membership.Wiki, membership.PageId), membership, (_, existing) =>
            membership with
            {
                FirstSeenAt = existing.FirstSeenAt,
                LastSeenAt = membership.LastSeenAt
            });
        return Task.CompletedTask;
    }

    private static string Key(string topicId, string wiki, long pageId) => $"{topicId}:{wiki}:{pageId}";
}

